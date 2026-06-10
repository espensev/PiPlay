using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PiPlay.Services;

/// <summary>
/// Native-window helpers for borderless (WindowStyle=None) PiPlay windows: work-area maximize
/// via WM_GETMINMAXINFO and larger resize hit zones via WM_NCHITTEST (Q-7 / REQ-WINDOW-02).
/// </summary>
public static class BorderlessWindowHelper
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCDESTROY = 0x0082;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private static readonly UIntPtr ResizeSubclassId = new(0x5049504C); // "PIPL"
    private static readonly Dictionary<IntPtr, ResizeSubclassState> ResizeSubclassStates = new();

    public static void EnableProperMaximize(Window window)
    {
        void Hook()
        {
            if (PresentationSource.FromVisual(window) is HwndSource src)
                src.AddHook(WndProc);
        }

        if (PresentationSource.FromVisual(window) is not null)
            Hook();
        else
            window.SourceInitialized += (_, _) => Hook();
    }

    public static void EnableExpandedResizeZones(Window window)
    {
        void Hook()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero && PresentationSource.FromVisual(window) is HwndSource src)
                hwnd = src.Handle;
            InstallResizeSubclass(hwnd, window);
        }

        if (PresentationSource.FromVisual(window) is not null)
            Hook();
        else
            window.SourceInitialized += (_, _) => Hook();
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var work = info.rcWork;
        var area = info.rcMonitor;
        mmi.ptMaxPosition.X = work.Left - area.Left;
        mmi.ptMaxPosition.Y = work.Top - area.Top;
        mmi.ptMaxSize.X = work.Right - work.Left;
        mmi.ptMaxSize.Y = work.Bottom - work.Top;
        Marshal.StructureToPtr(mmi, lParam, fDeleteOld: true);

        handled = true;
        return IntPtr.Zero;
    }

    private static void InstallResizeSubclass(IntPtr hwnd, Window window)
    {
        if (hwnd == IntPtr.Zero) return;
        if (ResizeSubclassStates.ContainsKey(hwnd)) return;

        SubclassProc proc = ResizeSubclassProc;
        var state = new ResizeSubclassState(window, proc);
        if (SetWindowSubclass(hwnd, proc, ResizeSubclassId, UIntPtr.Zero))
            ResizeSubclassStates[hwnd] = state;
    }

    internal static bool HasExpandedResizeSubclassForTests(IntPtr hwnd) =>
        ResizeSubclassStates.ContainsKey(hwnd);

    private static IntPtr ResizeSubclassProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr refData)
    {
        if (ResizeSubclassStates.TryGetValue(hwnd, out var state))
        {
            if (msg == WM_NCHITTEST)
            {
                var hit = HitTestResizeZone(state.Window, lParam);
                if (hit is not null) return new IntPtr(hit.Value);
            }

            if (msg == WM_NCDESTROY)
            {
                RemoveWindowSubclass(hwnd, state.Proc, ResizeSubclassId);
                ResizeSubclassStates.Remove(hwnd);
            }
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private static int? HitTestResizeZone(Window window, IntPtr lParam)
    {
        if (!IsResizable(window)) return null;

        var screen = new Point(GetSignedX(lParam), GetSignedY(lParam));
        var point = window.PointFromScreen(screen);
        var width = ActualOrConfigured(window.ActualWidth, window.Width);
        var height = ActualOrConfigured(window.ActualHeight, window.Height);

        return BorderlessResizeHitTestPolicy.HitTest(
            width,
            height,
            point.X,
            point.Y,
            isResizable: true,
            isNormalWindowState: window.WindowState == WindowState.Normal);
    }

    private static bool IsResizable(Window window) =>
        window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    private static double ActualOrConfigured(double actual, double configured) =>
        actual > 0 ? actual : double.IsNaN(configured) ? 0 : configured;

    private static int GetSignedX(IntPtr lParam) => unchecked((short)lParam.ToInt64());

    private static int GetSignedY(IntPtr lParam) => unchecked((short)(lParam.ToInt64() >> 16));

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private delegate IntPtr SubclassProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr refData);

    private sealed record ResizeSubclassState(Window Window, SubclassProc Proc);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hwnd,
        SubclassProc subclassProc,
        UIntPtr subclassId,
        UIntPtr refData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hwnd,
        SubclassProc subclassProc,
        UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
