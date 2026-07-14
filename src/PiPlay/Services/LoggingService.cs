using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PiPlay.Services;

/// <summary>
/// Lightweight, thread-safe local file logging (spec 18). Logs to
/// %LOCALAPPDATA%/PiPlay/logs/piplay.log with simple size-based rotation.
/// Never throws. Never logs cookies, auth headers, or credential-bearing URLs -
/// use <see cref="RedactUrl"/> for anything URL-shaped.
///
/// Callers never touch the disk: <see cref="Info"/>/<see cref="Warn"/>/<see cref="Error"/> only
/// format a line and hand it to a bounded queue drained by one background writer thread, so a
/// burst of failures (e.g. a DOM read failing on every poll tick) can never turn into recurring
/// synchronous I/O on the UI thread. The queue drops rather than blocks when full, and reports how
/// many entries it dropped, so logging degrades visibly instead of stalling the app.
///
/// The writer coalesces everything already queued into a single append, and tracks the file length
/// in memory so the rotation check costs nothing per entry.
///
/// <see cref="Shutdown"/> drains the queue and must be called on the way out (App.OnExit) - without
/// it, entries still in flight at process exit are lost.
/// </summary>
public static class Log
{
    private const long MaxBytes = 1_000_000;
    private const int MaxQueuedEntries = 4096;
    private const int MaxBatchChars = 64 * 1024;
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);

    // Compiled once (hot path): strips a query string if a raw URL slips through.
    private static readonly Regex QueryStrip = new(@"\?.*$", RegexOptions.Compiled);

    private static readonly object WriterGate = new();      // serializes Init/Shutdown, not the log calls
    private static BlockingCollection<Entry>? _queue;
    private static Thread? _writer;
    private static string? _path;
    private static long _bytesOnDisk;                       // writer thread only; avoids a stat per entry
    private static int _dropped;                            // Interlocked

    /// <summary>A queued line, or (when <see cref="Barrier"/> is set) a flush sentinel.</summary>
    private sealed class Entry
    {
        public string? Text { get; init; }
        public ManualResetEventSlim? Barrier { get; init; }
    }

    public static void Init()
    {
        lock (WriterGate)
        {
            StopWriter();   // re-Init is a test-lane path (it repoints at a fresh data root)
            try
            {
                Directory.CreateDirectory(AppPaths.LogsDir);
                var path = AppPaths.LogFile;
                _bytesOnDisk = File.Exists(path) ? new FileInfo(path).Length : 0L;
                _path = path;
                _queue = new BlockingCollection<Entry>(MaxQueuedEntries);
                _writer = new Thread(DrainLoop) { IsBackground = true, Name = "PiPlay.Log" };
                _writer.Start();
            }
            catch
            {
                // Logging disabled rather than crashing the app.
                _path = null;
                _queue = null;
                _writer = null;
            }
        }
    }

    /// <summary>Drain and stop the writer. Safe to call twice; logging after it is a silent no-op.</summary>
    public static void Shutdown()
    {
        lock (WriterGate) StopWriter();
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", $"{message} :: {ex.GetType().Name}: {ex.Message}");

    /// <summary>Reduce a URL to scheme://host/path, dropping the query (which may carry tokens/timestamps).</summary>
    public static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "(none)";
        try
        {
            var u = new Uri(url);
            return $"{u.Scheme}://{u.Host}{u.AbsolutePath}";
        }
        catch
        {
            return QueryStrip.Replace(url, "?<redacted>");
        }
    }

    private static void Write(string level, string message)
    {
        var queue = _queue;
        if (queue is null) return;
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            if (!queue.TryAdd(new Entry { Text = line }))
                Interlocked.Increment(ref _dropped);   // full: drop the line rather than stall the caller
        }
        catch
        {
            // Racing Shutdown completes the queue mid-add. Logging must never throw (Q-6).
        }
    }

    private static void DrainLoop()
    {
        var queue = _queue;
        var path = _path;
        if (queue is null || path is null) return;

        var batch = new StringBuilder();
        try
        {
            foreach (var entry in queue.GetConsumingEnumerable())
            {
                Accumulate(entry, batch, path);
                // Fold everything already queued into one append: a burst costs one file write.
                while (batch.Length < MaxBatchChars && queue.TryTake(out var next))
                    Accumulate(next, batch, path);
                FlushBatch(path, batch);
            }
        }
        catch
        {
            // The writer thread must never take the app down with it.
        }
        FlushBatch(path, batch);
    }

    private static void Accumulate(Entry entry, StringBuilder batch, string path)
    {
        if (entry.Barrier is not null)
        {
            FlushBatch(path, batch);
            try { entry.Barrier.Set(); } catch { /* waiter gave up and disposed it */ }
            return;
        }
        batch.Append(entry.Text);
    }

    private static void FlushBatch(string path, StringBuilder batch)
    {
        var dropped = Interlocked.Exchange(ref _dropped, 0);
        if (dropped > 0)
        {
            // Surface the loss in the log itself; a silent gap would be worse than a slow one.
            batch.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [WARN] Log queue overflowed; " +
                         $"dropped {dropped} entr{(dropped == 1 ? "y" : "ies")}.{Environment.NewLine}");
        }
        if (batch.Length == 0) return;

        var text = batch.ToString();
        batch.Clear();
        try
        {
            RotateIfNeeded(path);
            File.AppendAllText(path, text, Encoding.UTF8);
            _bytesOnDisk += Encoding.UTF8.GetByteCount(text);
        }
        catch
        {
            // ignore write failures
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (_bytesOnDisk <= MaxBytes) return;
        try
        {
            var backup = path + ".1";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
        }
        catch
        {
            // ignore rotation failures
        }
        finally
        {
            // Either the file rolled or rotation is impossible here; either way, reset the counter so a
            // failing rotation is retried once per MaxBytes instead of on every entry.
            _bytesOnDisk = 0;
        }
    }

    private static void StopWriter()
    {
        var queue = _queue;
        var writer = _writer;
        _queue = null;
        _writer = null;
        _path = null;
        if (queue is null) return;

        try { queue.CompleteAdding(); } catch { /* already completed */ }
        try { writer?.Join(DrainTimeout); } catch { /* nothing to wait for */ }
        try { queue.Dispose(); } catch { /* best effort */ }
    }

    // Test seams: block until everything queued so far has hit the disk, and observe overflow.
    internal static bool FlushForTests(TimeSpan timeout)
    {
        var queue = _queue;
        if (queue is null) return true;
        // Not disposed: a timed-out wait would otherwise race the writer's Set().
        var barrier = new ManualResetEventSlim(false);
        try
        {
            if (!queue.TryAdd(new Entry { Barrier = barrier })) return false;
        }
        catch
        {
            return true;   // shut down mid-call; StopWriter already drained
        }
        return barrier.Wait(timeout);
    }

    internal static int DroppedCountForTests => Volatile.Read(ref _dropped);
}
