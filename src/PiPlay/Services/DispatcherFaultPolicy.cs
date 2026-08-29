using System.Runtime.InteropServices;

namespace PiPlay.Services;

/// <summary>What one dispatcher-level fault earns: survival, and a dialog or silence.</summary>
/// <param name="Handled">
/// True to swallow the exception and keep the app alive; false to leave it to terminate the
/// process through the existing unhandled-exception path.
/// </param>
/// <param name="ShowDialog">True when this fault is the one allowed to occupy the dialog slot.</param>
/// <param name="Signature">Bounded single-line fault identity, for the log.</param>
internal readonly record struct DispatcherFaultDecision(bool Handled, bool ShowDialog, string Signature);

/// <summary>
/// Fault policy for <c>Application.DispatcherUnhandledException</c> (spec 15.4, Q-6). Two
/// independent questions, answered separately:
///
/// - Survival: everything is handled except a fault saying the process cannot continue
///   (out-of-memory, stack overflow, access violation, SEH), which is logged and left to
///   terminate the process. A fault raised once shutdown has started is handled as well, because
///   letting it escape Dispatcher.Run can skip the OnExit cleanup and turn an ordinary exit into a
///   WER crash; it is logged and never gets a dialog.
/// - Dialog budget: at most one dialog at a time, and at most one per fault signature per
///   <see cref="RepeatDialogWindow"/>. A fault that throws on every render or timer tick would
///   otherwise stack a new modal on each pass - the dispatcher keeps pumping while a
///   <c>MessageBox</c> is up, so repeats re-enter the handler on the same thread.
///
/// Dispatcher-affine: every call arrives on the UI thread, including the re-entrant ones raised
/// behind an open dialog, so no locking is needed.
/// </summary>
internal sealed class DispatcherFaultPolicy
{
    /// <summary>Quiet period after a dialog is dismissed during which the same fault stays silent.</summary>
    internal static readonly TimeSpan RepeatDialogWindow = TimeSpan.FromSeconds(10);

    private const int MaxSignatureLength = 200;
    private const int MaxInnerExceptionDepth = 8;

    private readonly Func<TimeSpan> _monotonicClock;
    private bool _dialogOpen;
    private string? _lastShownSignature;
    private TimeSpan _lastShownAt;

    /// <summary>Uptime, not wall clock: a DST or NTP jump must not widen or void the quiet period.</summary>
    public DispatcherFaultPolicy()
        : this(() => TimeSpan.FromMilliseconds(Environment.TickCount64))
    {
    }

    internal DispatcherFaultPolicy(Func<TimeSpan> monotonicClock)
    {
        ArgumentNullException.ThrowIfNull(monotonicClock);
        _monotonicClock = monotonicClock;
    }

    public DispatcherFaultDecision Evaluate(Exception exception, bool isShuttingDown)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var signature = BuildSignature(exception);
        var now = _monotonicClock();

        // A shutting-down fault is swallowed, not rethrown: letting it escape Dispatcher.Run can
        // skip the OnExit cleanup and raise a WER crash for a process that was already leaving.
        var handled = isShuttingDown || !IsFatal(exception);
        var showDialog = !isShuttingDown && !_dialogOpen && !IsSuppressedRepeat(signature, now);

        if (showDialog)
        {
            // Only a fault that actually gets a dialog claims the slot. A coalesced one must not
            // overwrite the signature being suppressed, or the storm resumes on dismissal.
            _dialogOpen = true;
            _lastShownSignature = signature;
            _lastShownAt = now;
        }

        return new DispatcherFaultDecision(handled, showDialog, signature);
    }

    /// <summary>Release the dialog slot. Idempotent; the quiet period runs from dismissal.</summary>
    public void DialogClosed()
    {
        if (!_dialogOpen) return;
        _dialogOpen = false;
        _lastShownAt = _monotonicClock();
    }

    private bool IsSuppressedRepeat(string signature, TimeSpan now) =>
        _lastShownSignature == signature && now - _lastShownAt < RepeatDialogWindow;

    /// <summary>
    /// Walk the inner-exception chain: a fatal failure often arrives wrapped (reflection and
    /// binding paths raise <c>TargetInvocationException</c>, and an aggregate exposes its first
    /// inner the same way). <c>COMException</c> is a sibling of <c>SEHException</c>, not a
    /// subclass, so routine WebView2 interop failures stay recoverable.
    /// </summary>
    private static bool IsFatal(Exception exception)
    {
        var current = exception;
        for (var depth = 0; current is not null && depth < MaxInnerExceptionDepth; depth++)
        {
            // StackOverflowException and AccessViolationException cannot reach a managed handler on
            // modern .NET; they are classified here so the policy is complete, not because the
            // runtime is expected to deliver them.
            if (current is OutOfMemoryException or StackOverflowException
                or AccessViolationException or SEHException)
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    /// <summary>
    /// Identity is exception type plus throw site. The throw site is the stable half: messages
    /// routinely embed a path, handle, or count that differs between two occurrences of the same
    /// fault, which would defeat the very repeat suppression this exists for. The message is only
    /// the fallback for an exception that was never thrown and so has no target site.
    /// </summary>
    private static string BuildSignature(Exception exception)
    {
        var type = exception.GetType();
        var site = exception.TargetSite;
        var detail = site is null
            ? exception.Message
            : $"{site.DeclaringType?.FullName ?? "?"}.{site.Name}";

        // One bounded line: this reaches the log, where an unbounded multi-line message would
        // break the entry format and the queue's size accounting.
        var signature = $"{type.FullName ?? type.Name}@{detail}".ReplaceLineEndings(" ");
        return signature.Length <= MaxSignatureLength
            ? signature
            : signature[..MaxSignatureLength];
    }
}
