using System.Runtime.InteropServices;
using System.Text;

namespace VuloPods.App.Interop;

public static class FullscreenDetector
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
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
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    /// <summary>True, wenn das Vordergrundfenster einen ganzen Monitor abdeckt und nicht die Shell (Desktop) ist. Bei Fehlern false.</summary>
    public static bool IsFullscreenForeground()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            if (!GetWindowRect(hwnd, out var window)) return false;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return false;
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;

            var name = new StringBuilder(256);
            if (GetClassName(hwnd, name, name.Capacity) == 0) return false;
            var className = name.ToString();
            if (className is "Progman" or "WorkerW") return false;

            var m = info.rcMonitor;
            return window.Left <= m.Left && window.Top <= m.Top
                && window.Right >= m.Right && window.Bottom >= m.Bottom;
        }
        catch
        {
            return false;
        }
    }
}
