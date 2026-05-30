using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PiPlay.Services;

/// <summary>
/// Makes a borderless (WindowStyle=None) window maximize to the monitor work area instead
/// of covering the taskbar, by handling WM_GETMINMAXINFO. Supports native window quality
/// (Q-7) for the custom-chrome Source Window.
/// </summary>
public static class BorderlessWindowHelper
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

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
}
