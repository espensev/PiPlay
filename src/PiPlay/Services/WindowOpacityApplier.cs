using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PiPlay.Services;

/// <summary>
/// Applies whole-window opacity to a borderless PiPlay window as layered-window alpha
/// (WS_EX_LAYERED + SetLayeredWindowAttributes(LWA_ALPHA)) on the top-level HWND, plus the DWM
/// rounded-corner preference for the floating look (spec 7.3, Phase 4; verified live by the
/// Stage 0 spikes, see docs/superpowers/worklog/2026-06-10-popout-overlay-opacity-spikes.md).
///
/// Two native facts from the spikes shape this class:
/// 1. WPF's HwndTarget strips WS_EX_LAYERED out of any exstyle change while the window doesn't
///    use per-pixel opacity, so the bit only survives behind a comctl32 subclass that forces it
///    back into STYLESTRUCT.styleNew inside WM_STYLECHANGING and does NOT chain for that message
///    (chaining would let HwndTarget edit it out again).
/// 2. WPF rewrites its cached exstyle wholesale during move/size/topmost operations, so a one-shot
///    SetWindowLongPtr is not durable — the forcing stays on for as long as opacity is engaged.
///
/// WS_EX_TRANSPARENT is never set (spec 7.5 / ADR-0006 / Q-8): the subclass only ever ORs the
/// layered bit, and the recorded per-window state lets tests assert no write carried the
/// transparent bit. Alpha animates over <see cref="WindowOpacityPolicy.FadeDurationMs"/> in
/// DispatcherTimer steps — native layered alpha is invisible to WPF's animation system. When the
/// target returns to fully opaque the applier disengages (clears the bit, stops forcing), so a
/// window with the feature off is byte-identical to the pre-Phase-4 window.
/// All members are UI-thread-only, like <see cref="BorderlessWindowHelper"/>.
/// </summary>
public static class WindowOpacityApplier
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const uint LWA_ALPHA = 0x2;
    private const int WM_STYLECHANGING = 0x007C;
    private const int WM_NCDESTROY = 0x0082;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_ROUND = 2;
    private const int AnimationStepMs = 15;
    private static readonly UIntPtr GuardSubclassId = new(0x4F504143); // "OPAC"
    private static readonly Dictionary<IntPtr, GuardState> States = new();

    private sealed class GuardState
    {
        public required SubclassProc Proc { get; init; }   // comctl32 holds an unmanaged pointer to it
        public byte CurrentAlpha = 255;
        public byte TargetAlpha = 255;
        public bool ForceLayeredBit;
        public bool RoundedCorners;
        public long LastExStyleWritten;
        public DispatcherTimer? Animator;
    }

    /// <summary>
    /// Drive the window to the given opacity level. Fully-opaque targets on windows that were
    /// never engaged are a strict no-op (the default look stays byte-identical); a target of 1.0
    /// on an engaged window animates up and then disengages cleanly.
    /// </summary>
    public static void Apply(IntPtr hwnd, double opacity, bool animate)
    {
        if (hwnd == IntPtr.Zero) return;
        var target = WindowOpacityPolicy.ToAlphaByte(opacity);
        States.TryGetValue(hwnd, out var state);
        if (target == 255 && (state is null || (!state.ForceLayeredBit && state.CurrentAlpha == 255))) return;

        state ??= Install(hwnd);
        if (state is null) return;
        state.TargetAlpha = target;

        if (!state.ForceLayeredBit)
        {
            // Engage: force the bit on every future exstyle write (spike finding 2), then land it
            // now. Alpha starts at the window's current 255 so a fade-down has no visual pop.
            state.ForceLayeredBit = true;
            var ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_LAYERED) == 0)
            {
                state.LastExStyleWritten = ex | WS_EX_LAYERED;
                SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(state.LastExStyleWritten));
            }
            if (!SetLayeredWindowAttributes(hwnd, 0, state.CurrentAlpha, LWA_ALPHA))
            {
                // The spike's run-1 failure signature (ERROR_INVALID_PARAMETER when the layered
                // bit didn't land) — log it so a field report of "opacity does nothing" is
                // diagnosable; this mechanism is live-verified on Windows 11 only.
                Log.Warn($"Window opacity engage failed: SetLayeredWindowAttributes error " +
                         $"{Marshal.GetLastWin32Error()} (exstyle=0x{GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64():X}).");
            }
        }

        if (!animate)
        {
            state.Animator?.Stop();
            SetAlpha(hwnd, state, target);
            DisengageIfOpaque(hwnd, state);
            return;
        }
        StartAnimation(hwnd, state);
    }

    /// <summary>
    /// DWM rounded corners for the floating look (spike S-3). Never touches a window it hasn't
    /// rounded before when asked for square corners, so default-look windows stay pristine.
    /// Silently a no-op on Windows 10 (DWM rejects the attribute).
    /// </summary>
    public static void SetRoundedCorners(IntPtr hwnd, bool rounded)
    {
        if (hwnd == IntPtr.Zero) return;
        States.TryGetValue(hwnd, out var state);
        if (!rounded && (state is null || !state.RoundedCorners)) return;
        state ??= Install(hwnd);
        if (state is null || state.RoundedCorners == rounded) return;
        state.RoundedCorners = rounded;
        var pref = rounded ? DWMWCP_ROUND : DWMWCP_DEFAULT;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    /// <summary>Cursor position probe for the hover-restore poll (WPF gets no mouse events over
    /// the WebView2 child HWND — Stage 0 spike finding).</summary>
    internal static bool TryGetCursorPos(out int x, out int y)
    {
        if (GetCursorPos(out var p)) { x = p.X; y = p.Y; return true; }
        x = 0; y = 0;
        return false;
    }

    /// <summary>Whether the window visibly under the screen point belongs to <paramref name="hwnd"/>
    /// (itself or a child, e.g. the WebView2). False when another window covers it, so the activity
    /// probe doesn't count movement over an occluding app as in-window activity.</summary>
    internal static bool IsPointOverWindow(IntPtr hwnd, int x, int y)
    {
        var under = WindowFromPoint(new POINT { X = x, Y = y });
        return under != IntPtr.Zero && GetAncestor(under, GA_ROOT) == hwnd;
    }

    // Test seams (WPF lane): assert policy output and style hygiene without reading live HWND alpha.
    internal static bool IsEngagedForTests(IntPtr hwnd) => States.TryGetValue(hwnd, out var s) && s.ForceLayeredBit;
    internal static byte? TargetAlphaForTests(IntPtr hwnd) => States.TryGetValue(hwnd, out var s) ? s.TargetAlpha : null;
    internal static byte? CurrentAlphaForTests(IntPtr hwnd) => States.TryGetValue(hwnd, out var s) ? s.CurrentAlpha : null;
    internal static bool IsRoundedForTests(IntPtr hwnd) => States.TryGetValue(hwnd, out var s) && s.RoundedCorners;
    internal static bool LastExStyleWriteCarriedTransparentBitForTests(IntPtr hwnd) =>
        States.TryGetValue(hwnd, out var s) && (s.LastExStyleWritten & WS_EX_TRANSPARENT) != 0;

    private static GuardState? Install(IntPtr hwnd)
    {
        if (States.TryGetValue(hwnd, out var existing)) return existing;
        SubclassProc proc = GuardProc;
        if (!SetWindowSubclass(hwnd, proc, GuardSubclassId, UIntPtr.Zero))
        {
            Log.Warn("Window opacity unavailable: SetWindowSubclass failed for the layered-bit guard.");
            return null;
        }
        var state = new GuardState { Proc = proc };
        States[hwnd] = state;
        return state;
    }

    private static IntPtr GuardProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData)
    {
        if (States.TryGetValue(hwnd, out var state))
        {
            if (msg == WM_STYLECHANGING && wParam.ToInt64() == GWL_EXSTYLE && state.ForceLayeredBit)
            {
                // STYLESTRUCT { DWORD styleOld; DWORD styleNew; } — force the layered bit into
                // styleNew and do not chain (spike findings 1+2). Only the layered bit is ever
                // added; WS_EX_TRANSPARENT is never touched.
                var styleNew = Marshal.ReadInt32(lParam, 4);
                Marshal.WriteInt32(lParam, 4, styleNew | unchecked((int)WS_EX_LAYERED));
                return IntPtr.Zero;
            }

            if (msg == WM_NCDESTROY)
            {
                state.Animator?.Stop();
                RemoveWindowSubclass(hwnd, state.Proc, GuardSubclassId);
                States.Remove(hwnd);
            }
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private static void StartAnimation(IntPtr hwnd, GuardState state)
    {
        state.Animator?.Stop();
        var from = (double)state.CurrentAlpha;
        var to = (double)state.TargetAlpha;
        if (from == to) { DisengageIfOpaque(hwnd, state); return; }

        var steps = Math.Max(1, WindowOpacityPolicy.FadeDurationMs / AnimationStepMs);
        var step = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(AnimationStepMs) };
        timer.Tick += (_, _) =>
        {
            step++;
            var alpha = (byte)Math.Round(from + (to - from) * Math.Min(1.0, (double)step / steps));
            SetAlpha(hwnd, state, alpha);
            if (step < steps) return;
            timer.Stop();
            DisengageIfOpaque(hwnd, state);
        };
        state.Animator = timer;
        timer.Start();
    }

    private static void SetAlpha(IntPtr hwnd, GuardState state, byte alpha)
    {
        SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
        state.CurrentAlpha = alpha;
    }

    private static void DisengageIfOpaque(IntPtr hwnd, GuardState state)
    {
        if (state.TargetAlpha != 255 || state.CurrentAlpha != 255 || !state.ForceLayeredBit) return;
        // Back to the pristine non-layered window. Forcing goes off first so our own write (and
        // WPF's next cache rewrite) can land without the bit. The subclass stays installed for
        // cheap re-engagement; WM_NCDESTROY removes it.
        state.ForceLayeredBit = false;
        var ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        if ((ex & WS_EX_LAYERED) != 0)
        {
            state.LastExStyleWritten = ex & ~WS_EX_LAYERED;
            SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(state.LastExStyleWritten));
        }
    }

    private delegate IntPtr SubclassProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr refData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, UIntPtr subclassId, UIntPtr refData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
