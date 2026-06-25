using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace PiPlay.Services;

/// <summary>A snapshot of the YouTube &lt;video&gt; element. Duration is nullable (live/unknown).</summary>
public sealed record PlayerState(
    int CurrentTime,
    bool Paused,
    int? Duration,
    double? Volume,
    bool? Muted,
    double? PlaybackRate);

/// <summary>
/// The ONE place that talks to the YouTube page DOM (spec 12.5, Q-3). All JavaScript is
/// centralized here, uses the resilient selector from spec 26.3, and is best-effort:
/// every call swallows and logs failures so script errors can never crash the app.
/// </summary>
public static class YouTubeDomBridge
{
    // Resilient video selector (spec 26.3).
    private const string VideoSelector =
        "(document.querySelector('#movie_player video.html5-main-video')" +
        "||document.querySelector('video.html5-main-video')" +
        "||document.querySelector('video'))";

    private static readonly string ReadStateScript = $@"
(() => {{
  const v = {VideoSelector};
  if (!v) return null;
  return {{
    currentTime: Math.floor(v.currentTime || 0),
    paused: !!v.paused,
    duration: Number.isFinite(v.duration) ? Math.floor(v.duration) : null,
    volume: Number.isFinite(v.volume) ? v.volume : null,
    muted: typeof v.muted === 'boolean' ? v.muted : null,
    playbackRate: Number.isFinite(v.playbackRate) ? v.playbackRate : null
  }};
}})()";

    private const string CanonicalUrlScript = @"
(() => {
  const link = document.querySelector('link[rel=""canonical""]');
  if (link && link.href) return link.href;
  return location.href;
})()";

    /// <summary>Read current time / paused / duration, or null if no video or the read failed.</summary>
    public static async Task<PlayerState?> ReadPlayerStateAsync(CoreWebView2 webView)
    {
        var raw = await ExecuteAsync(webView, ReadStateScript);
        if (raw is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var currentTime = root.GetProperty("currentTime").GetInt32();
            var paused = root.GetProperty("paused").GetBoolean();
            int? duration = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetInt32()
                : null;
            double? volume = root.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble()
                : null;
            bool? muted = root.TryGetProperty("muted", out var m) && m.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? m.GetBoolean()
                : null;
            double? playbackRate = root.TryGetProperty("playbackRate", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetDouble()
                : null;
            return new PlayerState(currentTime, paused, duration, volume, muted, playbackRate);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to parse YouTube player state.", ex);
            return null;
        }
    }

    public static Task PauseAsync(CoreWebView2 webView) =>
        ExecuteVoidAsync(webView, $"(() => {{ const v = {VideoSelector}; if (v) v.pause(); }})()");

    public static Task PlayAsync(CoreWebView2 webView) =>
        ExecuteVoidAsync(webView,
            $"(() => {{ const v = {VideoSelector}; if (v) {{ const p = v.play(); if (p && p.catch) p.catch(() => {{}}); }} }})()");

    public static Task SeekAsync(CoreWebView2 webView, int seconds) =>
        ExecuteVoidAsync(webView,
            $"(() => {{ const v = {VideoSelector}; if (v) {{ try {{ v.currentTime = {seconds}; }} catch (e) {{}} }} }})()");

    public static Task SeekAndPauseAsync(CoreWebView2 webView, int seconds) =>
        ExecuteVoidAsync(webView,
            $"(() => {{ const v = {VideoSelector}; if (v) {{ try {{ v.currentTime = {seconds}; }} catch (e) {{}} v.pause(); }} }})()");

    public static Task SeekAndPlayAsync(CoreWebView2 webView, int seconds) =>
        ExecuteVoidAsync(webView,
            $"(() => {{ const v = {VideoSelector}; if (v) {{ try {{ v.currentTime = {seconds}; }} catch (e) {{}} const p = v.play(); if (p && p.catch) p.catch(() => {{}}); }} }})()");

    public static Task ApplyPlaybackSettingsAsync(
        CoreWebView2 webView, double? volume, bool? muted, double? playbackRate)
    {
        if (volume is null && muted is null && playbackRate is null) return Task.CompletedTask;

        static string Js(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        var volumeScript = volume is null
            ? string.Empty
            : $"const volume = {Js(Math.Clamp(volume.Value, 0.0, 1.0))}; if (Number.isFinite(volume)) v.volume = volume;";
        var mutedScript = muted is null
            ? string.Empty
            : $"v.muted = {(muted.Value ? "true" : "false")};";
        var rateScript = playbackRate is null
            ? string.Empty
            : $"const rate = {Js(playbackRate.Value)}; if (Number.isFinite(rate) && rate > 0) {{ try {{ v.playbackRate = rate; }} catch (e) {{}} }}";

        return ExecuteVoidAsync(webView, $@"
(() => {{
  const v = {VideoSelector};
  if (!v) return;
  {volumeScript}
  {mutedScript}
  {rateScript}
}})()");
    }

    /// <summary>Read the page's canonical URL (or location.href) for the currently playing item.</summary>
    public static async Task<string?> ReadCanonicalUrlAsync(CoreWebView2 webView)
    {
        var raw = await ExecuteAsync(webView, CanonicalUrlScript);
        if (raw is null) return null;
        try { return JsonSerializer.Deserialize<string>(raw); }
        catch { return null; }
    }

    private static async Task<string?> ExecuteAsync(CoreWebView2 webView, string script)
    {
        try
        {
            var result = await webView.ExecuteScriptAsync(script);
            // WebView2 returns the literal "null" for undefined / thrown / null results.
            if (string.IsNullOrEmpty(result) || result == "null") return null;
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("YouTube DOM script failed.", ex);
            return null;
        }
    }

    private static async Task ExecuteVoidAsync(CoreWebView2 webView, string script)
    {
        try
        {
            await webView.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            Log.Error("YouTube DOM command failed.", ex);
        }
    }
}
