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
    public static Task<HandoffOutcome?> ServeOneAsync(
        string pipeName,
        Func<HandoffRequest, CancellationToken, Task<HandoffOutcome>> handleAsync,
        CancellationToken cancellationToken) =>
        ServeOneAsync(pipeName, handleAsync, SingleInstanceHandoffPolicy.ReplyTimeout, cancellationToken);

    internal static async Task<HandoffOutcome?> ServeOneAsync(
        string pipeName,
        Func<HandoffRequest, CancellationToken, Task<HandoffOutcome>> handleAsync,
        TimeSpan replyTimeout,
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
        var reply = Utf8.GetBytes(SingleInstanceHandoffPolicy.FormatReply(outcome) + "\n");
        try
        {
            await AsyncOperationDeadline.RunAsync(
                async token =>
                {
                    await server.WriteAsync(reply, token).ConfigureAwait(false);
                    // Close after the sender does: a pipe instance lives until its last handle
                    // closes, so closing first can leave the next server "all instances busy".
                    var drain = new byte[64];
                    while (await server.ReadAsync(drain, token).ConfigureAwait(false) > 0) { }
                    return true;
                },
                replyTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // The sender already gave up, or never read or closed. Its same-ID retry, or its
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
            SingleInstanceHandoffPolicy.RequestTimeout,
            SingleInstanceHandoffPolicy.AckTimeout,
            cancellationToken);

    /// <summary>One attempt: connect, write the request, and wait for the reply line.</summary>
    internal static async Task<HandoffOutcome> SendOnceAsync(
        string pipeName,
        HandoffRequest request,
        TimeSpan connectTimeout,
        TimeSpan requestTimeout,
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
            var line = Utf8.GetBytes(SingleInstanceHandoffPolicy.FormatRequest(request) + "\n");
            await AsyncOperationDeadline.RunAsync(
                async token =>
                {
                    await client.WriteAsync(line, token).ConfigureAwait(false);
                    return true;
                },
                requestTimeout,
                cancellationToken).ConfigureAwait(false);

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
