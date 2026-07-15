using System.IO;
using System.Text.Json;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlayerSurfaceScriptTests
{
    [Fact]
    public void Surface_drag_bridge_stays_top_level_to_avoid_child_frame_runtime_crashes_Q6()
    {
        var source = File.ReadAllText(Path.Combine(
            XamlTestFiles.SrcDir, "Services", "PlayerSurfaceDragBridge.cs"));

        Assert.Contains("_core.WebMessageReceived += Core_WebMessageReceived", source);
        Assert.DoesNotContain("FrameCreated", source);
        Assert.DoesNotContain("CoreWebView2Frame", source);
    }

    [Fact]
    public void Passive_surface_drag_script_preserves_clicks_until_the_drag_threshold_Q7_Q8()
    {
        const string nonce = "nonce-with-\"quote";
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript(nonce, 4.25, 7.5);

        Assert.Contains($"const nonce = {JsonSerializer.Serialize(nonce)};", script);
        Assert.Contains("const thresholdX = 4.25;", script);
        Assert.Contains("const thresholdY = 7.5;", script);
        Assert.Contains("event.button !== 0", script);
        Assert.Contains("event.pointerType !== \"mouse\" && event.pointerType !== \"pen\"", script);
        Assert.Contains("if (dx < thresholdX && dy < thresholdY) return;", script);
        Assert.Contains("releasePointerCapture", script);

        var thresholdGate = script.IndexOf("if (dx < thresholdX && dy < thresholdY) return;", StringComparison.Ordinal);
        var releaseCapture = script.IndexOf("releasePointerCapture", thresholdGate, StringComparison.Ordinal);
        var preventDefault = script.IndexOf("event.preventDefault();", thresholdGate, StringComparison.Ordinal);
        var postMessage = script.IndexOf("host.postMessage", preventDefault, StringComparison.Ordinal);
        Assert.True(thresholdGate >= 0 && releaseCapture > thresholdGate &&
                    preventDefault > releaseCapture && postMessage > preventDefault,
            "The script must not suppress an ordinary click before movement crosses the drag threshold.");
    }

    [Fact]
    public void Passive_surface_drag_script_runs_only_in_the_top_document_and_requires_real_pointer_input_Q6_Q8()
    {
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript("nonce", 4, 4);

        Assert.Contains("if (window.top !== window || window.__piplaySurfaceDragInstalled) return;", script);
        Assert.Contains("if (!event.isTrusted || !event.isPrimary || event.button !== 0) return;", script);
        Assert.Contains("if (!event.isTrusted || !armed || armed.id !== event.pointerId || armed.posted) return;", script);

        var topFrameGate = script.IndexOf("window.top !== window", StringComparison.Ordinal);
        var installedMarker = script.IndexOf("window.__piplaySurfaceDragInstalled = true", StringComparison.Ordinal);
        Assert.True(topFrameGate >= 0 && installedMarker > topFrameGate,
            "Child frames must exit before the drag bridge is marked as installed.");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("button")]
    [InlineData("input")]
    [InlineData("[role='slider']")]
    [InlineData("[data-piplay-no-drag]")]
    [InlineData(".piplay-focused-overlay")]
    [InlineData(".ytp-progress-bar-container")]
    [InlineData(".ytp-volume-area")]
    [InlineData(".ytp-subtitles-button")]
    [InlineData(".ytp-settings-button")]
    [InlineData(".ytp-fullscreen-button")]
    [InlineData(".ytp-settings-menu")]
    [InlineData(".ytp-menuitem")]
    [InlineData(".ytp-ce-element")]
    [InlineData(".ytp-endscreen-content")]
    [InlineData(".ytp-caption-window-container")]
    [InlineData(".ytp-ad-overlay-container")]
    public void Passive_surface_drag_script_excludes_interactive_youtube_and_piplay_surfaces(string selector)
    {
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript("nonce", 4, 4);

        Assert.Contains($"\"{selector}\"", script);
    }

    [Theory]
    [InlineData(".ytp-chrome-bottom")]
    [InlineData(".ytp-chrome-top")]
    public void Passive_surface_drag_script_keeps_blank_youtube_overlay_containers_draggable(string selector)
    {
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript("nonce", 4, 4);

        Assert.DoesNotContain($"\"{selector}\"", script);
    }

    [Fact]
    public void Passive_surface_drag_script_posts_only_the_closed_nonce_bearing_request()
    {
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript("nonce", 4, 4);
        var postStart = script.IndexOf("host.postMessage", StringComparison.Ordinal);
        var postEnd = script.IndexOf("}));", postStart, StringComparison.Ordinal);

        Assert.True(postStart >= 0 && postEnd > postStart, "Expected the WebView2 drag request.");
        var payload = script[postStart..postEnd];
        Assert.Contains("channel: \"piplay.window\"", payload);
        Assert.Contains("v: 1", payload);
        Assert.Contains("type: \"dragStart\"", payload);
        Assert.Contains("nonce: nonce", payload);
        Assert.Contains("documentToken: documentToken", payload);
        Assert.DoesNotContain("clientX", payload);
        Assert.DoesNotContain("clientY", payload);
        Assert.DoesNotContain("action:", payload);
    }

    [Fact]
    public void Passive_surface_drag_starts_revoked_and_requires_host_document_authorization()
    {
        const string documentToken = "document-token-with-\"quote";
        var script = YouTubeDomBridge.BuildPassiveSurfaceDragScript("nonce", 4, 4);
        var authorize = YouTubeDomBridge.BuildPassiveSurfaceDocumentTokenScript(documentToken);

        Assert.Contains("let documentToken = null;", script);
        Assert.Contains("if (!documentToken) return;", script);
        Assert.Contains("authorizeDocument(nextToken)", script);
        Assert.Contains($"const documentToken = {JsonSerializer.Serialize(documentToken)};", authorize);
        Assert.Contains("surface.authorizeDocument(documentToken)", authorize);
        Assert.Contains("return true;", authorize);
    }

    [Fact]
    public void Focused_surface_keeps_the_real_watch_player_full_viewport_and_never_crops_Q3_Q5()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains("location.pathname === \"/watch\"", script);
        Assert.Contains("position: fixed !important; inset: 0 !important;", script);
        Assert.Contains("width: 100vw !important; height: 100vh !important;", script);
        Assert.Contains("object-fit: contain !important", script);
        Assert.DoesNotContain("object-fit: cover", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/embed/", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("piplay.local", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("yt-navigate-finish", script);
        Assert.Contains("MutationObserver", script);
    }

    [Theory]
    [InlineData("mute", "Mute")]
    [InlineData("captions", "Captions")]
    [InlineData("settings", "PiPlay settings")]
    [InlineData("pinToggle", "Pin popout on top")]
    [InlineData("fullscreenToggle", "Expand or restore popout")]
    [InlineData("close", "Close popout and return video")]
    [InlineData("playPause", "Play")]
    [InlineData("next", "Next video")]
    [InlineData("seek", "Video progress")]
    public void Focused_surface_exposes_named_media_and_window_controls(string action, string accessibleName)
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        if (action == "seek")
            Assert.Contains("data-action=\"seek\"", script);
        else
            Assert.Contains($"button(\"{action}\", \"{accessibleName}\"", script);
    }

    [Fact]
    public void Focused_overlay_is_pointer_transparent_except_for_its_controls_and_seek_rail()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains(".piplay-focused-overlay {", script);
        Assert.Contains("pointer-events: none", script);
        Assert.Contains(".piplay-focused-button {", script);
        Assert.Contains(".piplay-focused-overlay.is-visible .piplay-focused-button", script);
        Assert.Contains(".piplay-focused-overlay.is-visible .piplay-focused-progress { pointer-events: auto; }", script);
        Assert.Contains("bottom: 64px", script); // custom rail stays above native YouTube chrome
        Assert.Contains("data-piplay-no-drag=\"true\"", script);
        Assert.Contains(".ytp-subtitles-button", script);
        Assert.Contains(".ytp-next-button", script);
        Assert.Contains("native.click();", script);
    }

    [Fact]
    public void Focused_surface_posts_only_allowlisted_native_window_actions()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: false, fadeDelayMs: 2500);

        Assert.Contains("[\"close\", \"pinToggle\", \"fullscreenToggle\", \"settings\"].includes(action)", script);
        Assert.Contains("channel: \"piplay.focused\", v: 1, type: \"request\"", script);
        Assert.Contains("nonce: nonce, documentToken: documentToken, action: action", script);
        Assert.DoesNotContain("postWindowAction(\"playPause\")", script);
        Assert.DoesNotContain("postWindowAction(\"mute\")", script);
        Assert.DoesNotContain("postWindowAction(\"captions\")", script);
        Assert.DoesNotContain("postWindowAction(\"next\")", script);
    }

    [Fact]
    public void Focused_surface_requires_real_user_events_before_media_or_native_actions_Q5_Q8()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: false, fadeDelayMs: 2500);

        Assert.Contains("if (!event.isTrusted) return;", script);
        Assert.Contains("function postWindowAction(action, trustedEvent)", script);
        Assert.Contains("if (!trustedEvent || ![\"close\", \"pinToggle\", \"fullscreenToggle\", \"settings\"].includes(action)) return;", script);
        Assert.Contains("postWindowAction(action, event.isTrusted);", script);

        var handlerStart = script.IndexOf("function handleAction(event)", StringComparison.Ordinal);
        var trustedGate = script.IndexOf("if (!event.isTrusted) return;", handlerStart, StringComparison.Ordinal);
        var nativePost = script.IndexOf("postWindowAction(action, event.isTrusted);", handlerStart, StringComparison.Ordinal);
        Assert.True(handlerStart >= 0 && trustedGate > handlerStart && nativePost > trustedGate,
            "A Focused native action must remain behind the trusted user-event gate.");
    }

    [Fact]
    public void Focused_surface_never_seeks_or_advances_while_youtube_reports_an_active_ad_Q5()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains("function isAdActive()", script);
        Assert.Contains("player.classList.contains(\"ad-showing\")", script);
        Assert.Contains("player.classList.contains(\"ad-interrupting\")", script);
        Assert.Contains("toggleClass(root, \"is-ad\", adActive);", script);
        Assert.Contains("setDisabled(controls.next, adActive ||", script);
        Assert.Contains("if (isAdActive()) return;", script);
        Assert.Contains(".piplay-focused-overlay.is-ad .piplay-focused-bottom { display: none; }", script);
        Assert.Contains(".piplay-focused-overlay.is-ad .piplay-focused-center { display: none; }", script);
        Assert.Contains(".piplay-focused-overlay.is-ad::after { opacity: 0; }", script);
    }

    [Fact]
    public void Focused_surface_caches_controls_and_uses_media_events_with_only_an_active_fallback_tick()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains("function cacheControls()", script);
        Assert.Contains("function bindMedia(nextMedia)", script);
        Assert.Contains("boundMedia.addEventListener(\"timeupdate\", updateProgress);", script);
        Assert.Contains("function stopUpdateTimer()", script);
        Assert.Contains("window.setInterval(updateControls, 1000)", script);
        Assert.Contains("stopUpdateTimer();", script);
        Assert.Contains("if (surfaceActive !== true || !root || root.hidden || !event.isTrusted) return;", script);
        Assert.Contains("if (now - lastPointerRevealAt < 125) return;", script);
        Assert.Contains("if (surfaceActive === true)", script);
        Assert.DoesNotContain("window.setInterval(updateControls, 250)", script);
        Assert.DoesNotContain("play.innerHTML = media.paused ? icons.play : icons.pause;", script);

        var deactivateStart = script.IndexOf("function deactivateSurface()", StringComparison.Ordinal);
        var installStart = script.IndexOf("function install()", deactivateStart, StringComparison.Ordinal);
        Assert.True(deactivateStart >= 0 && installStart > deactivateStart, "Expected an explicit inactive teardown path.");
        var deactivation = script[deactivateStart..installStart];
        Assert.Contains("stopUpdateTimer();", deactivation);
        Assert.Contains("bindMedia(null);", deactivation);
        Assert.Contains("bindPlayer(null);", deactivation);
    }

    [Fact]
    public void Focused_surface_reports_active_state_so_native_recovery_chrome_is_never_hidden_early()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains("function postSurfaceState(active)", script);
        Assert.Contains("type: \"state\", nonce: nonce, documentToken: documentToken, active: active", script);
        Assert.Contains("postSurfaceState(false);", script);
        Assert.Contains("postSurfaceState(true);", script);
    }

    [Fact]
    public void Focused_surface_starts_revoked_and_reports_state_only_after_host_authorization()
    {
        const string documentToken = "focused-document-token";
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);
        var authorize = YouTubeDomBridge.BuildPlayerFirstDocumentTokenScript(documentToken);
        var reportState = YouTubeDomBridge.BuildPlayerFirstStateRequestScript();

        Assert.Contains("let documentToken = null;", script);
        Assert.Contains("if (!documentToken) return;", script);
        Assert.Contains("authorizeDocument(nextToken)", script);
        Assert.Contains("reportState()", script);
        Assert.Contains($"const documentToken = {JsonSerializer.Serialize(documentToken)};", authorize);
        Assert.Contains("surface.authorizeDocument(documentToken)", authorize);
        Assert.Contains("surface.reportState()", reportState);
    }

    [Fact]
    public void Focused_fade_holds_for_hover_focus_and_rearms_when_playback_starts()
    {
        var script = YouTubeDomBridge.BuildPlayerFirstSurfaceScript(
            "nonce", "#2BAED0", fadeEnabled: true, fadeDelayMs: 2500);

        Assert.Contains("function hasInteractiveAttention()", script);
        Assert.Contains(".piplay-focused-button:hover,.piplay-focused-progress:hover", script);
        Assert.Contains("root.contains(document.activeElement)", script);
        Assert.Contains("hideControlsWhenIdle, 250", script);
        Assert.Contains("pausedChangedToPlaying", script);
        Assert.Contains("else if (pausedChangedToPlaying) revealControls();", script);
    }
}
