namespace PiPlay.Services;

/// <summary>
/// Owns the lifetime of the one destructive WebView profile clear. A foreground status timeout
/// must not release this slot: only terminal completion of the underlying task does.
/// </summary>
internal sealed class BrowserDataClearCoordinator
{
    private readonly object _sync = new();
    private Task? _activeTask;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _activeTask is { IsCompleted: false };
        }
    }

    /// <summary>
    /// Classify the `WhenAny` result without misreporting a timeout when the operation completed
    /// between timer selection and the awaiting continuation resuming.
    /// </summary>
    public static bool DidForegroundWaitExpire(Task operation, Task firstCompleted)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstCompleted);
        return !ReferenceEquals(firstCompleted, operation) && !operation.IsCompleted;
    }

    /// <summary>
    /// Start one operation, or return the currently active task without invoking
    /// <paramref name="operationFactory"/>. Completed operations never block a later start.
    /// </summary>
    public bool TryStart(Func<Task> operationFactory, out Task operation)
    {
        ArgumentNullException.ThrowIfNull(operationFactory);

        lock (_sync)
        {
            if (_activeTask is { IsCompleted: false } active)
            {
                operation = active;
                return false;
            }

            operation = operationFactory()
                ?? throw new InvalidOperationException("The browser-data clear factory returned no task.");
            _activeTask = operation;
            _ = ObserveAndReleaseAsync(operation);
            return true;
        }
    }

    private async Task ObserveAndReleaseAsync(Task operation)
    {
        // This observer owns a late fault even if the foreground WaitAsync has already timed out.
        // Awaiting the original task elsewhere still observes the same terminal state normally.
        try { await operation.ConfigureAwait(false); }
        catch { /* the foreground/late-status owner reports the concrete failure */ }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_activeTask, operation))
                    _activeTask = null;
            }
        }
    }
}
