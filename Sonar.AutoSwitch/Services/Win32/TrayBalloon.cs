using System;
using System.IO;
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    const uint NIF_ICON = 2, NIF_TIP = 4, NIF_STATE = 8, NIF_INFO = 0x10;
    const uint NIS_HIDDEN = 1;
    const uint NIIF_INFO = 1;
    const uint NOTIFYICON_VERSION_4 = 4;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sonar.AutoSwitch", "debug.log");

    static void Log(string msg) =>
        File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} TrayBalloon: {msg}\n");

    public static void Show(string title, string text)
    {
        var hwnd = CreateWindowEx(0, "STATIC", null, 0x80000000u,
            0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        {
            Log($"CreateWindowEx failed err={Marshal.GetLastWin32Error()}");
            return;
        }

        var hIcon = ExtractIcon(IntPtr.Zero, Environment.ProcessPath ?? "", 0);
        Log($"hwnd={hwnd} hIcon={hIcon}");

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            // ponytail: NIS_HIDDEN keeps icon in overflow area; avoids duplicate in main tray strip.
            uFlags = NIF_ICON | NIF_TIP | NIF_STATE,
            dwState = NIS_HIDDEN,
            dwStateMask = NIS_HIDDEN,
            hIcon = hIcon,
            szTip = "Sonar Auto Switch",
        };
        var addOk = Shell_NotifyIcon(NIM_ADD, ref data);
        Log($"NIM_ADD ok={addOk} err={Marshal.GetLastWin32Error()}");

        // Windows 10+: opt into version 4 so balloons go to notification center.
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);

        data.uFlags = NIF_INFO;
        data.szInfo = text;
        data.szInfoTitle = title;
        data.dwInfoFlags = NIIF_INFO;
        var modOk = Shell_NotifyIcon(NIM_MODIFY, ref data);
        Log($"NIM_MODIFY ok={modOk} err={Marshal.GetLastWin32Error()}");

        _ = Task.Delay(5_000).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            var del = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1,
            };
            Shell_NotifyIcon(NIM_DELETE, ref del);
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
            DestroyWindow(hwnd);
        }));
    }
}
