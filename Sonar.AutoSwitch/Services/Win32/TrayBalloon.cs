using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Sonar.AutoSwitch.Services.Win32;

static class TrayBalloon
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    static extern bool DestroyWindow(IntPtr hwnd);

    const uint NIM_ADD = 0, NIM_DELETE = 2;
    const uint NIF_TIP = 4, NIF_INFO = 0x10;
    const uint NIIF_INFO = 1;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    // Shows a tray balloon notification once; auto-cleans the temporary icon after 10 s.
    public static void Show(string title, string text)
    {
        var hwnd = CreateWindowEx(0, "STATIC", null, 0x80000000u,
            0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = NIF_TIP | NIF_INFO,
            szTip = "Sonar Auto Switch",
            szInfo = text,
            szInfoTitle = title,
            dwInfoFlags = NIIF_INFO,
        };
        Shell_NotifyIcon(NIM_ADD, ref data);

        _ = Task.Delay(10_000).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            var del = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1,
                szTip = "",
                szInfo = "",
                szInfoTitle = "",
            };
            Shell_NotifyIcon(NIM_DELETE, ref del);
            DestroyWindow(hwnd);
        }));
    }
}
