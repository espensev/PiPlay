namespace PiPlay.Services;

/// <summary>
/// Pure decision logic for whole-window opacity on the Popout Player (spec 7.3, Phase 4). Kept
/// free of WPF/Win32 types so the idle/constant interplay, the floors, and the alpha mapping can
/// be unit tested without a window. Two persisted levels exist (spec design 2026-06-10):
/// <c>player.constantWindowOpacity</c> (the "Active" look) and <c>player.idleWindowOpacity</c>
/// (the level the window eases to when idle). Idleness comes from the SAME idle source as the
/// controls fade (<see cref="PlayerWindow"/>'s idle timer fed by <see cref="FadePolicy"/>
/// vocabulary) — there is exactly one definition of "idle" per window. Opacity changes animate
/// over <see cref="FadeDurationMs"/>, the controls-fade duration, so the window has one fade
/// rhythm. Click-through is a hard non-goal at every opacity (spec 7.5 / ADR-0006):
/// WS_EX_TRANSPARENT is never set, which is asserted at the applier seam, not here.
/// </summary>
public static class WindowOpacityPolicy
{
    /// <summary>Default for both levels: fully opaque (the feature is "off" at 1.0, spec 7.3).</summary>
    public const double Default = 1.0;

    /// <summary>
    /// Lowest value honored from a hand-edited settings.json (spec 7.3: "Minimum normal setting
    /// should not go below 45% unless the user explicitly unlocks it" — the manual file edit IS
    /// the explicit unlock, settled 2026-06-10). Anything below resets to <see cref="Default"/>.
    /// </summary>
    public const double FileFloor = 0.10;

    /// <summary>
    /// Lowest value the Settings UI sliders offer (QA checklist §3: the 45% floor without an
    /// explicit unlock). Hand-edited values in [<see cref="FileFloor"/>, <see cref="UiFloor"/>)
    /// are honored but not reachable from the UI.
    /// </summary>
    public const double UiFloor = 0.45;

    /// <summary>Upper bound for both levels.</summary>
    public const double Max = 1.0;

    /// <summary>
    /// Opacity animation duration, in milliseconds. Deliberately the controls-fade duration
    /// (<see cref="FadePolicy.FadeDurationMs"/>) so the strip fade and the window fade share one
    /// rhythm (design 2026-06-10 §5).
    /// </summary>
    public const int FadeDurationMs = FadePolicy.FadeDurationMs;

    /// <summary>
    /// Repair a persisted opacity level: values outside [<see cref="FileFloor"/>, <see cref="Max"/>]
    /// (including NaN) RESET to <see cref="Default"/> — the same reset-not-clamp rule the accent
    /// and fade-delay settings use, so a corrupted value yields the safe opaque look rather than
    /// the most extreme legal one.
    /// </summary>
    public static double Normalize(double value)
    {
        if (double.IsNaN(value) || value < FileFloor || value > Max) return Default;
        return value;
    }

    /// <summary>
    /// The opacity the window should have right now. Idle never makes the window MORE opaque than
    /// the active level: spec 7.3 reads idle fade as a step DOWN with "hover restores full
    /// opacity", so an idle level above the constant level is clamped to the constant level
    /// (e.g. constant hand-tuned to 0.8 with idle left at the 1.0 default must not brighten on
    /// idle). Both inputs are normalized first, so callers can pass raw persisted values.
    /// </summary>
    public static double Effective(bool isIdle, double constant, double idle)
    {
        var activeLevel = Normalize(constant);
        if (!isIdle) return activeLevel;
        return Math.Min(Normalize(idle), activeLevel);
    }

    /// <summary>
    /// Map an opacity level to the byte SetLayeredWindowAttributes(LWA_ALPHA) takes.
    /// Clamped defensively so no caller mistake can produce alpha 0 — a fully transparent layered
    /// window stops hit-testing, which would violate the no-click-through gate (Q-8).
    /// </summary>
    public static byte ToAlphaByte(double opacity)
    {
        var scaled = Math.Round(Normalize(opacity) * 255.0);
        return (byte)Math.Clamp(scaled, 1.0, 255.0);
    }
}
