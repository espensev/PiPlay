namespace PiPlay.Services;

/// <summary>
/// Pure decision logic for whole-window opacity on the Popout Player. Kept
/// free of WPF/Win32 types so the idle/constant interplay, the floors, and the alpha mapping can
/// be unit tested without a window. Two persisted levels exist:
/// <c>player.constantWindowOpacity</c> (the "Active" look) and <c>player.idleWindowOpacity</c>
/// (the level the window eases to when idle). Idleness comes from the SAME idle source as the
/// controls fade (<see cref="PlayerWindow"/>'s idle timer fed by <see cref="FadePolicy"/>
/// vocabulary) — there is exactly one definition of "idle" per window. Opacity changes animate
/// over <see cref="FadeDurationMs"/>, the controls-fade duration, so the window has one fade
/// rhythm. Click-through is a hard non-goal at every opacity (ADR-0006):
/// WS_EX_TRANSPARENT is never set, which is asserted at the applier seam, not here.
/// </summary>
public static class WindowOpacityPolicy
{
    /// <summary>Default for both levels: fully opaque.</summary>
    public const double Default = 1.0;

    /// <summary>
    /// Lowest value honored from a hand-edited settings.json. The UI slider stops at 0.45, but
    /// file values down to 0.10 are preserved. Anything lower resets to <see cref="Default"/>.
    /// </summary>
    public const double FileFloor = 0.10;

    /// <summary>
    /// Lowest value the Settings UI sliders offer. Hand-edited values in
    /// [<see cref="FileFloor"/>, <see cref="UiFloor"/>)
    /// are honored but not reachable from the UI.
    /// </summary>
    public const double UiFloor = 0.45;

    /// <summary>Upper bound for both levels.</summary>
    public const double Max = 1.0;

    /// <summary>
    /// Opacity animation duration, in milliseconds. Deliberately the controls-fade duration
    /// (<see cref="FadePolicy.FadeDurationMs"/>) so the strip fade and the window fade share one
    /// rhythm.
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
    /// Repair an OPTIONAL opacity override (the nullable theme behavior values): an invalid
    /// value becomes null — "follow the preset default" — rather than an explicit
    /// <see cref="Default"/> override. One rule shared by settings-load repair and the
    /// Settings-apply writer so the two paths cannot diverge.
    /// </summary>
    public static double? NormalizeOptional(double? value)
    {
        if (value is null) return null;
        var raw = value.Value;
        if (double.IsNaN(raw) || raw < FileFloor || raw > Max) return null;
        return raw;
    }

    /// <summary>
    /// The opacity the window should have right now. Idle never makes the window MORE opaque than
    /// the active level, so an idle level above the constant level is clamped to the constant level
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
