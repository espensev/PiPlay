namespace PiPlay.Services;

/// <summary>Result of one hand-off, as the running instance decides it and the sender observes it.</summary>
internal enum HandoffOutcome
{
    /// <summary>The UI thread ran the hand-off (now, or for an earlier attempt with the same request ID).</summary>
    Applied,

    /// <summary>The UI thread did not start the hand-off within the dispatch bound; it was withdrawn and will never run.</summary>
    NotApplied,

    /// <summary>The sender got no reply within its acknowledgement wait, or the pipe broke before one arrived.</summary>
    NoAcknowledgement,

    /// <summary>The sender could not connect to the running instance's pipe.</summary>
    Unreachable,
}

/// <summary>One second-launch hand-off. <see cref="Url"/> is empty for a plain activation.</summary>
internal readonly record struct HandoffRequest(string Id, string Url)
{
    public static HandoffRequest Create(string? url) =>
        new(Guid.NewGuid().ToString("N"), url ?? string.Empty);
}

/// <summary>A posted UI-thread hand-off that can still be withdrawn until the UI thread starts it.</summary>
internal interface IHandoffDispatch
{
    /// <summary>Completes when the callback has run, faults if it threw, and is cancelled if it was withdrawn.</summary>
    Task Completion { get; }

    /// <summary>True only when the callback had not started and now never will.</summary>
    bool TryAbort();
}

/// <summary>
/// Applies each hand-off request ID at most once, so a sender's same-ID retry acknowledges an
/// earlier apply instead of repeating it. Distinct launches carry distinct IDs and each apply.
/// </summary>
internal sealed class HandoffRequestLedger
{
    public const int Capacity = 32;

    private readonly object _gate = new();
    private readonly Queue<string> _order = new();
    private readonly HashSet<string> _applied = new(StringComparer.Ordinal);

    /// <summary>True the first time <paramref name="requestId"/> is seen; the caller then applies it.</summary>
    public bool TryBegin(string requestId)
    {
        lock (_gate)
        {
            if (!_applied.Add(requestId)) return false;

            _order.Enqueue(requestId);
            // A retry arrives within seconds of its first attempt; old IDs only cost memory.
            if (_order.Count > Capacity)
                _applied.Remove(_order.Dequeue());
            return true;
        }
    }
}

/// <summary>
/// Acknowledged single-instance hand-off (REQ-APP-01, ADR-0009). The running instance decides
/// within <see cref="DispatchTimeout"/> whether its UI thread applied a request, withdrawing a
/// dispatch that had not started, and replies before the sender's <see cref="AckTimeout"/> ends.
/// A withdrawn dispatch never runs later, so a sender that reports "did not respond" never sees
/// its link applied afterwards, and the request ledger keeps the one same-ID retry from applying
/// a link twice.
/// </summary>
internal static class SingleInstanceHandoffPolicy
{
    /// <summary>How long the running instance waits for its UI thread to start a hand-off.</summary>
    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromMilliseconds(2500);

    /// <summary>How long the sender waits for a reply after writing its request.</summary>
    public static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(3500);

    /// <summary>
    /// Minimum slack between the running instance's decision and the sender's give-up: it covers
    /// the request read, the reply write, and thread-pool scheduling between them.
    /// <c>DispatchTimeout + AckMargin</c> must stay below <see cref="AckTimeout"/>.
    /// </summary>
    public static readonly TimeSpan AckMargin = TimeSpan.FromMilliseconds(500);

    /// <summary>How long the sender waits for a free pipe instance on each attempt.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The first attempt plus one retry that reuses the same request ID.</summary>
    public const int MaxAttempts = 2;

    public const string AppliedReply = "applied";
    public const string NotAppliedReply = "not-applied";

    private const string RequestVersion = "v1";
    private const char FieldSeparator = '|';

    /// <summary>One line: version, request ID, and the escaped URL, so no field can carry a separator or newline.</summary>
    public static string FormatRequest(HandoffRequest request) =>
        string.Join(FieldSeparator, RequestVersion, request.Id, Uri.EscapeDataString(request.Url));

    public static bool TryParseRequest(string? line, out HandoffRequest request)
    {
        request = default;
        if (string.IsNullOrEmpty(line)) return false;

        var fields = line.Split(FieldSeparator);
        if (fields.Length != 3 || fields[0] != RequestVersion) return false;
        if (!Guid.TryParseExact(fields[1], "N", out _)) return false;

        request = new HandoffRequest(fields[1], Uri.UnescapeDataString(fields[2]));
        return true;
    }

    public static string FormatReply(HandoffOutcome outcome) => outcome switch
    {
        HandoffOutcome.Applied => AppliedReply,
        HandoffOutcome.NotApplied => NotAppliedReply,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Only the running instance's decisions are sent."),
    };

    public static HandoffOutcome ParseReply(string? line) => line switch
    {
        AppliedReply => HandoffOutcome.Applied,
        NotAppliedReply => HandoffOutcome.NotApplied,
        _ => HandoffOutcome.NoAcknowledgement,
    };

    public static Task<HandoffOutcome> DispatchAsync(
        HandoffRequest request,
        HandoffRequestLedger ledger,
        Action<string> apply,
        Func<Action, IHandoffDispatch> post,
        CancellationToken cancellationToken) =>
        DispatchAsync(request, ledger, apply, post, DispatchTimeout, cancellationToken);

    /// <summary>
    /// Posts <paramref name="apply"/> to the UI thread and decides the reply within
    /// <paramref name="dispatchTimeout"/>: a dispatch the UI thread has not started by then is
    /// withdrawn (<see cref="HandoffOutcome.NotApplied"/>); one it has started is acknowledged
    /// (<see cref="HandoffOutcome.Applied"/>) without waiting for it to finish.
    /// </summary>
    internal static async Task<HandoffOutcome> DispatchAsync(
        HandoffRequest request,
        HandoffRequestLedger ledger,
        Action<string> apply,
        Func<Action, IHandoffDispatch> post,
        TimeSpan dispatchTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(post);

        var dispatch = post(() =>
        {
            if (ledger.TryBegin(request.Id))
                apply(request.Url);
        });

        try
        {
            await dispatch.Completion.WaitAsync(dispatchTimeout, cancellationToken).ConfigureAwait(false);
            return HandoffOutcome.Applied;
        }
        catch (TimeoutException)
        {
            // Abort cannot stop a callback that has started, so only an unstarted one is withdrawn.
            return dispatch.TryAbort() || dispatch.Completion.IsCanceled
                ? HandoffOutcome.NotApplied
                : HandoffOutcome.Applied;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            dispatch.TryAbort();
            throw;
        }
        catch (Exception) when (dispatch.Completion.IsCanceled)
        {
            // Withdrawn by dispatcher shutdown before it ran.
            return HandoffOutcome.NotApplied;
        }
        catch (Exception)
        {
            // The callback started and threw; the app's fault handler owns the exception, and a
            // retry would repeat whatever part of the hand-off already ran.
            return HandoffOutcome.Applied;
        }
    }

    /// <summary>
    /// Sends <paramref name="request"/> up to <see cref="MaxAttempts"/> times with the same
    /// request ID and returns the first <see cref="HandoffOutcome.Applied"/> or the last failure.
    /// </summary>
    public static async Task<HandoffOutcome> SendAsync(
        HandoffRequest request,
        Func<HandoffRequest, CancellationToken, Task<HandoffOutcome>> attemptAsync,
        Action<int, HandoffOutcome> onAttemptFailed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attemptAsync);
        ArgumentNullException.ThrowIfNull(onAttemptFailed);

        var outcome = HandoffOutcome.Unreachable;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            outcome = await attemptAsync(request, cancellationToken).ConfigureAwait(false);
            if (outcome == HandoffOutcome.Applied) return outcome;
            onAttemptFailed(attempt, outcome);
        }
        return outcome;
    }
}
