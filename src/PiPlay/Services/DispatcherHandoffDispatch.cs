using System.Windows.Threading;

namespace PiPlay.Services;

/// <summary>A single-instance hand-off posted to the WPF dispatcher.</summary>
internal sealed class DispatcherHandoffDispatch : IHandoffDispatch
{
    private readonly DispatcherOperation _operation;

    private DispatcherHandoffDispatch(DispatcherOperation operation) => _operation = operation;

    /// <summary>Queue at <see cref="DispatcherPriority.Send"/>, matching the synchronous invoke it replaces.</summary>
    public static IHandoffDispatch Post(Dispatcher dispatcher, Action callback)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(callback);
        return new DispatcherHandoffDispatch(dispatcher.InvokeAsync(callback, DispatcherPriority.Send));
    }

    public Task Completion => _operation.Task;

    // DispatcherOperation.Abort removes the operation only while it is still queued, under the
    // same lock the dispatcher takes to dequeue it, so a true result means it can never run.
    public bool TryAbort() => _operation.Abort();
}
