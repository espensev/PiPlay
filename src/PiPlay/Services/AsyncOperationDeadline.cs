namespace PiPlay.Services;

/// <summary>
/// Bounds an asynchronous operation, cancels its adapter when the deadline or caller wins,
/// and observes any fault that arrives after the foreground wait has already ended.
/// </summary>
internal static class AsyncOperationDeadline
{
    public static Task<T> RunSingleFlightAsync<T>(
        SemaphoreSlim gate,
        Func<Task<T>> operationFactory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(operationFactory);

        return RunAsync(
            async operationToken =>
            {
                // Preserve the caller context while queued: the protected factory may own a
                // thread-affine adapter such as CoreWebView2 and must start back on its UI thread.
                await gate.WaitAsync(operationToken);
                try
                {
                    operationToken.ThrowIfCancellationRequested();
                    var operation = operationFactory()
                        ?? throw new InvalidOperationException("The bounded operation factory returned no task.");
                    return await operation;
                }
                finally
                {
                    gate.Release();
                }
            },
            timeout,
            cancellationToken);
    }

    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operationFactory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationFactory);
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), "The operation deadline must be finite and positive.");

        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var operation = operationFactory(operationCancellation.Token)
                ?? throw new InvalidOperationException("The bounded operation factory returned no task.");

            try
            {
                return await operation.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                CancelBestEffort(operationCancellation);
                _ = ObserveLateCompletionAndDisposeAsync(operation, operationCancellation);
                operationCancellation = null;
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancelBestEffort(operationCancellation);
                _ = ObserveLateCompletionAndDisposeAsync(operation, operationCancellation);
                operationCancellation = null;
                throw;
            }
        }
        finally
        {
            operationCancellation?.Dispose();
        }
    }

    private static void CancelBestEffort(CancellationTokenSource cancellation)
    {
        try { cancellation.Cancel(); }
        catch { /* an adapter's cancellation callback must not replace the deadline outcome */ }
    }

    private static async Task ObserveLateCompletionAndDisposeAsync(
        Task operation,
        CancellationTokenSource cancellation)
    {
        try { await operation.ConfigureAwait(false); }
        catch { /* the deadline/caller already owns the foreground outcome */ }
        finally { cancellation.Dispose(); }
    }
}
