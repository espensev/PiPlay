using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PiPlay.Services;

/// <summary>
/// Lightweight, thread-safe local file logging (spec 18). Logs to
/// %LOCALAPPDATA%/PiPlay/logs/piplay.log with simple size-based rotation.
/// Never throws. Never logs cookies, auth headers, or credential-bearing URLs -
/// use <see cref="RedactUrl"/> for anything URL-shaped.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private const long MaxBytes = 1_000_000;
    private static string? _path;

    // Compiled once (hot path): strips a query string if a raw URL slips through.
    private static readonly Regex QueryStrip = new(@"\?.*$", RegexOptions.Compiled);

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDir);
            _path = AppPaths.LogFile;
        }
        catch
        {
            _path = null; // logging disabled rather than crashing the app
        }
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
        var path = _path;
        if (path is null) return;
        try
        {
            lock (Gate)
            {
                RotateIfNeeded(path);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never throw (Q-6).
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length > MaxBytes)
            {
                var backup = path + ".1";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
            }
        }
        catch
        {
            // ignore rotation failures
        }
    }
}
