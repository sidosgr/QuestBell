using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace QuestBell;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    // Generic delegates (including Func<...>) cannot be marshalled as Win32 callbacks.
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect rect);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtrW(IntPtr h, int index);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out Rect value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int he, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);

    internal static bool Fullscreen(IntPtr h, bool preserveMinimized = false)
    {
        if (!IsWindow(h)) return false;
        if (IsIconic(h)) return preserveMinimized;
        if (!IsWindowVisible(h)) return false;
        var className = new System.Text.StringBuilder(256); GetClassName(h, className, 256);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return false;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == Environment.ProcessId) return false;
        // Captioned maximized windows are not full-screen video/game windows.
        if ((GetWindowLongPtrW(h, -16).ToInt64() & 0x00C00000L) != 0) return false;
        if (DwmGetWindowAttribute(h, 9, out Rect rect, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(h, out rect)) return false;
        var screen = Forms.Screen.FromHandle(h).Bounds;
        return Math.Abs(rect.Left - screen.Left) <= 3 && Math.Abs(rect.Top - screen.Top) <= 3 &&
               Math.Abs(rect.Right - screen.Right) <= 3 && Math.Abs(rect.Bottom - screen.Bottom) <= 3;
    }

    internal static HashSet<IntPtr> FullscreenWindows()
    {
        var result = new HashSet<IntPtr>();
        EnumWindowsProc callback = (h, _) => { if (Fullscreen(h)) result.Add(h); return true; };
        if (!EnumWindows(callback, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        GC.KeepAlive(callback);
        return result;
    }

    internal static void DarkTitle(Window window)
    {
        var h = new WindowInteropHelper(window).Handle;
        int value = 1; DwmSetWindowAttribute(h, 20, ref value, 4);
        int border = 0x004B3729, caption = 0x001B120D, text = 0x00F6F2EE;
        // Windows 11 frame colours; unsupported attributes are harmless on Windows 10.
        DwmSetWindowAttribute(h, 34, ref border, 4);
        DwmSetWindowAttribute(h, 35, ref caption, 4);
        DwmSetWindowAttribute(h, 36, ref text, 4);
    }
    internal static void FitToWorkArea(Window window)
    {
        var h = new WindowInteropHelper(window).Handle;
        var area = Forms.Screen.FromHandle(h).WorkingArea;
        double scale = Math.Max(96, GetDpiForWindow(h)) / 96.0;
        double width = Math.Max(320, area.Width / scale - 32), height = Math.Max(320, area.Height / scale - 32);
        window.MinWidth = Math.Min(window.MinWidth, width); window.MinHeight = Math.Min(window.MinHeight, height);
        window.Width = Math.Min(window.Width, width); window.Height = Math.Min(window.Height, height);
    }
    internal static void Place(Window window, Forms.Screen screen, bool behind)
    {
        var h = new WindowInteropHelper(window).Handle; var b = screen.Bounds;
        SetWindowPos(h, behind ? new IntPtr(1) : new IntPtr(-1), b.X, b.Y, b.Width, b.Height, 0x0010 | 0x0040);
    }
}
