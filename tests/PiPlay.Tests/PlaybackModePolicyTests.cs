using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class PlaybackModePolicyTests
{
    // --- NormalizeProfileMode: durable vocabulary + legacy alias + safe fallback ---

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("normal", "normal")]
    [InlineData("NORMAL", "normal")]
    [InlineData("  Compact  ", "compact")]
    [InlineData("compact", "compact")]
    [InlineData("embed", "compact")]      // legacy/internal alias -> durable "compact"
    [InlineData("EMBED", "compact")]
    [InlineData("fullscreen", null)]      // unknown value -> fall back to global
    [InlineData("pip", null)]
    public void NormalizeProfileMode_maps_to_durable_values(string? input, string? expected)
    {
        Assert.Equal(expected, PlaybackModePolicy.NormalizeProfileMode(input));
    }

    // --- ResolveEffectiveMode: profile override wins; unset/unknown falls back to global ---

    [Theory]
    // profileMode, globalCompact, expected
    [InlineData(null, false, PlaybackMode.Normal)]    // default install: normal
    [InlineData(null, true, PlaybackMode.Compact)]    // global compact on, no override
    [InlineData("normal", true, PlaybackMode.Normal)] // profile forces normal despite global on
    [InlineData("compact", false, PlaybackMode.Compact)] // profile forces compact despite global off
    [InlineData("embed", false, PlaybackMode.Compact)]   // legacy alias resolves to compact
    [InlineData("garbage", true, PlaybackMode.Compact)]  // unknown override falls back to global
    [InlineData("garbage", false, PlaybackMode.Normal)]
    public void ResolveEffectiveMode_follows_profile_then_global(
        string? profileMode, bool globalCompact, PlaybackMode expected)
    {
        Assert.Equal(expected, PlaybackModePolicy.ResolveEffectiveMode(profileMode, globalCompact));
    }

    // --- Mode-specific minimums: compact is larger than normal (spec 10.2 / 16.1) ---

    [Fact]
    public void Normal_mode_uses_the_320x180_minimum()
    {
        Assert.Equal(320, PlaybackModePolicy.MinWidthFor(PlaybackMode.Normal));
        Assert.Equal(180, PlaybackModePolicy.MinHeightFor(PlaybackMode.Normal));
    }

    [Fact]
    public void Compact_mode_uses_a_separate_larger_480x270_minimum()
    {
        Assert.Equal(480, PlaybackModePolicy.MinWidthFor(PlaybackMode.Compact));
        Assert.Equal(270, PlaybackModePolicy.MinHeightFor(PlaybackMode.Compact));

        // The compact floor must be strictly larger so the embedded controls stay usable.
        Assert.True(PlaybackModePolicy.CompactMinWidth > PlaybackModePolicy.NormalMinWidth);
        Assert.True(PlaybackModePolicy.CompactMinHeight > PlaybackModePolicy.NormalMinHeight);
    }

    // --- DOM-sync-timer selection: exactly one timestamp source per mode (spec 10.3) ---

    [Theory]
    [InlineData(PlaybackMode.Normal, true)]    // normal polls the YouTube page DOM
    [InlineData(PlaybackMode.Compact, false)]  // compact reads it from the shell bridge instead
    public void Only_normal_mode_uses_the_dom_sync_timer(PlaybackMode mode, bool expected)
    {
        Assert.Equal(expected, PlaybackModePolicy.UsesDomSyncTimer(mode));
    }

    // --- BuildPopoutUrl: the mode -> URL join (compact = local shell, normal = watch page) ---

    [Fact]
    public void BuildPopoutUrl_uses_the_shell_for_compact_and_watch_for_normal()
    {
        var target = new YouTubeTarget { VideoId = "dQw4w9WgXcQ" };
        const string shellBase = "https://piplay.local/player.html";

        var compact = PlaybackModePolicy.BuildPopoutUrl(PlaybackMode.Compact, target, 30, shellBase);
        var normal = PlaybackModePolicy.BuildPopoutUrl(PlaybackMode.Normal, target, 30, shellBase);

        Assert.StartsWith(shellBase, compact);
        Assert.Contains("v=dQw4w9WgXcQ", compact);
        Assert.Contains("/watch?v=dQw4w9WgXcQ", normal);
        Assert.NotEqual(compact, normal);            // an inverted/collapsed ternary would fail this
        Assert.DoesNotContain("piplay.local", normal);
    }

    // --- ResolveProfileOverride: apply a profile's mode only when the popout target matches it ---

    [Theory]
    // profileMode, profileVideoId, targetVideoId, expected
    [InlineData("compact", "vid00000001", "vid00000001", "compact")] // match -> override applies
    [InlineData("normal", "vid00000001", "vid00000001", "normal")]
    [InlineData("compact", "vid00000001", "vid00000002", null)]      // mismatch -> global default
    [InlineData("compact", null, "vid00000001", null)]               // playlist-only/unparseable profile url
    [InlineData("compact", "vid00000001", null, null)]               // no concrete target
    [InlineData("compact", "vid00000001", "", null)]                 // empty target
    [InlineData(null, "vid00000001", "vid00000001", null)]           // profile carries no mode
    public void ResolveProfileOverride_applies_only_on_a_matching_video(
        string? profileMode, string? profileVideoId, string? targetVideoId, string? expected)
    {
        Assert.Equal(expected,
            PlaybackModePolicy.ResolveProfileOverride(profileMode, profileVideoId, targetVideoId));
    }

    // --- ResolveEffectivePopoutMode: the kill-switch for compact player ---

    [Fact]
    public void Embed_compact_player_is_disabled_so_new_popouts_resolve_to_normal()
    {
        Assert.False(PlaybackModePolicy.CompactPlayerEnabled);
        // The kill switch overrides BOTH a global compact default and a per-profile compact override.
        Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode(null, globalCompact: true));
        Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode("compact", globalCompact: true));
        Assert.Equal(PlaybackMode.Normal, PlaybackModePolicy.ResolveEffectivePopoutMode("compact", globalCompact: false));
    }
}
