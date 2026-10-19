using System.Runtime.InteropServices;

namespace VuloPods.App.Interop;

public static class WindowStyles
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Macht das Fenster nicht aktivierbar und blendet es aus Alt-Tab aus.</summary>
    public static void MakeNoActivateToolWindow(IntPtr hwnd)
    {
        const long flags = WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        if (IntPtr.Size == 8)
        {
            var current = GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(current | flags));
        }
        else
        {
            var current = GetWindowLong32(hwnd, GWL_EXSTYLE);
            SetWindowLong32(hwnd, GWL_EXSTYLE, current | (int)flags);
        }
    }
}
