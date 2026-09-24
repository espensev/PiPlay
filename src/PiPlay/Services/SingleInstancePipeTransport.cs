using System.IO;
using System.IO.Pipes;
using System.Text;

namespace PiPlay.Services;

/// <summary>
/// Named-pipe I/O for the acknowledged single-instance hand-off: one request line from the
/// sender, one reply line from the running instance. <see cref="SingleInstanceHandoffPolicy"/>
/// owns the wire format, timing, and apply-once decision.
/// </summary>
internal static class SingleInstancePipeTransport
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Serves one connection. Returns the decision sent back, or <see langword="null"/> for a
    /// malformed request, which is dropped without a reply.
    /// </summary>
    public static async Task<HandoffOutcome?> ServeOneAsync(
        string pipeName,
        Func<HandoffRequest, CancellationToken, Task<HandoffOutcome>> handleAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handleAsync);

        using var server = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(server, Utf8, detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024, leaveOpen: true);
        var line = await SingleInstancePipePolicy.ReadClientPayloadAsync(
            async token => await reader.ReadLineAsync(token).ConfigureAwait(false) ?? string.Empty,
            cancellationToken).ConfigureAwait(false);

        if (!SingleInstanceHandoffPolicy.TryParseRequest(line, out var request))
            return null;

        var outcome = await handleAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var writer = new StreamWriter(server, Utf8, bufferSize: 64, leaveOpen: true)
            {
                NewLine = "\n",
            };
            await writer.WriteLineAsync(SingleInstanceHandoffPolicy.FormatReply(outcome)).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The sender already gave up and closed its end. Its same-ID retry, or its
            // "did not respond" message, owns the outcome.
        }
        return outcome;
    }

    public static Task<HandoffOutcome> SendOnceAsync(
        string pipeName,
        HandoffRequest request,
        CancellationToken cancellationToken) =>
        SendOnceAsync(
            pipeName, request,
            SingleInstanceHandoffPolicy.ConnectTimeout,
            SingleInstanceHandoffPolicy.AckTimeout,
            cancellationToken);

    /// <summary>One attempt: connect, write the request, and wait for the reply line.</summary>
    internal static async Task<HandoffOutcome> SendOnceAsync(
        string pipeName,
        HandoffRequest request,
        TimeSpan connectTimeout,
        TimeSpan ackTimeout,
        CancellationToken cancellationToken)
    {
        using var client = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(connectTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return HandoffOutcome.Unreachable;
        }

        try
        {
            await using (var writer = new StreamWriter(client, Utf8, bufferSize: 1024, leaveOpen: true)
            {
                NewLine = "\n",
            })
            {
                await writer.WriteLineAsync(SingleInstanceHandoffPolicy.FormatRequest(request)).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // The wait starts only after the request is written, so the running instance's
            // dispatch bound (plus the margin) always ends inside it.
            using var reader = new StreamReader(client, Utf8, detectEncodingFromByteOrderMarks: false,
                bufferSize: 64, leaveOpen: true);
            var reply = await AsyncOperationDeadline.RunAsync(
                async token => await reader.ReadLineAsync(token).ConfigureAwait(false),
                ackTimeout,
                cancellationToken).ConfigureAwait(false);
            return SingleInstanceHandoffPolicy.ParseReply(reply);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            return HandoffOutcome.NoAcknowledgement;
        }
    }
}
