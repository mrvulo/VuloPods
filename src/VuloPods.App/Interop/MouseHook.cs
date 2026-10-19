using System.Runtime.InteropServices;

namespace VuloPods.App.Interop;

/// <summary>Globaler Low-Level-Maus-Hook (WH_MOUSE_LL); meldet Klicks aller drei Haupttasten mit Bildschirmkoordinaten.</summary>
public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x201;
    private const int WM_RBUTTONDOWN = 0x204;
    private const int WM_MBUTTONDOWN = 0x207;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int X;
        public int Y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    // Delegate als Feld halten, damit der GC ihn nicht einsammelt, solange der Hook installiert ist.
    private readonly HookProc _proc;
    private IntPtr _handle;

    public event Action<int, int>? ButtonDown;

    public MouseHook()
    {
        _proc = Callback;
        _handle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (int)wParam;
            if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                try
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    ButtonDown?.Invoke(data.X, data.Y);
                }
                catch
                {
                    // Ein Fehler im Handler darf die Maus nicht blockieren.
                }
            }
        }
        return CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }
}
