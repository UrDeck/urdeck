// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>One line of the tray menu: what the user reads and what choosing it does.</summary>
internal sealed record TrayMenuEntry(string Label, Action Run);

/// <summary>
/// The notification-area icon. It owns a hidden message-only window (the deck window never activates, and a popup
/// menu needs an owner that can be put in the foreground), shows a native popup menu built from an ordered list of
/// entries on right-click, and adds the icon again when Explorer restarts. It knows nothing about the application:
/// what each entry does is the entry's own action. Create and dispose it on the UI thread.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string ClassName = "UrDeckTrayWindow";
    private const string Tooltip = "UrDeck";
    private const uint IconId = 1;
    private const uint CallbackMessage = 0x8001; // WM_APP + 1
    private const uint WmNull = 0x0000;
    private const uint WmContextMenu = 0x007B;
    private const uint WmDestroy = 0x0002;
    private const uint NimAdd = 0;
    private const uint NimDelete = 2;
    private const uint NimSetVersion = 4;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint NifShowTip = 0x80; // version 4 only shows the standard tooltip when asked
    private const uint NotifyIconVersion4 = 4;
    private const uint MfString = 0x0;
    private const uint TpmReturnCmd = 0x0100;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmBottomAlign = 0x0020;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x10;
    private const int SmCxSmIcon = 49;
    private const int SmCySmIcon = 50;
    private const nint IdiApplication = 32512;
    private const nint HwndMessage = -3;
    private const uint FirstCommandId = 1000;

    private readonly IReadOnlyList<TrayMenuEntry> _entries;
    private readonly string _iconPath;
    private readonly WndProc _proc;
    private readonly uint _taskbarCreated;
    private readonly nint _instance;
    private nint _hwnd;
    private nint _icon;
    private bool _ownsIcon;
    private bool _added;

    public TrayIcon(IReadOnlyList<TrayMenuEntry> entries, string iconPath)
    {
        _entries = entries;
        _iconPath = iconPath;
        _proc = OnMessage;
        _instance = GetModuleHandle(null);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");

        var windowClass = new WindowClassEx
        {
            Size = (uint)Marshal.SizeOf<WindowClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            Instance = _instance,
            ClassName = ClassName,
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            UrDeckLog.Warn($"Tray icon: could not register the message window class (error {Marshal.GetLastWin32Error()})");
            return;
        }

        _hwnd = CreateWindowEx(0, ClassName, ClassName, 0, 0, 0, 0, 0, HwndMessage, 0, _instance, 0);
        if (_hwnd == 0)
        {
            UrDeckLog.Warn($"Tray icon: could not create the message window (error {Marshal.GetLastWin32Error()})");
            UnregisterClass(ClassName, _instance);
            return;
        }

        LoadIcon();
        if (AddIcon())
            UrDeckLog.Info($"Tray icon added ({_entries.Count} menu entr{(_entries.Count == 1 ? "y" : "ies")})");
    }

    public void Dispose()
    {
        if (_hwnd == 0)
            return;
        if (_added)
        {
            var data = NewData();
            Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }

        DestroyWindow(_hwnd);
        _hwnd = 0;
        UnregisterClass(ClassName, _instance);
        if (_ownsIcon && _icon != 0)
            DestroyIcon(_icon);
        _icon = 0;
    }

    private void LoadIcon()
    {
        int width = GetSystemMetrics(SmCxSmIcon);
        int height = GetSystemMetrics(SmCySmIcon);
        if (File.Exists(_iconPath))
            _icon = LoadImage(0, _iconPath, ImageIcon, width, height, LrLoadFromFile);
        if (_icon != 0)
        {
            _ownsIcon = true;
            return;
        }

        UrDeckLog.Warn($"Tray icon: could not load {_iconPath}; using the default application icon");
        _icon = LoadIconW(0, IdiApplication);
    }

    private bool AddIcon()
    {
        var data = NewData();
        data.Flags = NifMessage | NifIcon | NifTip | NifShowTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        data.Tip = Tooltip;
        _added = Shell_NotifyIcon(NimAdd, ref data);
        if (!_added)
        {
            UrDeckLog.Warn("Tray icon: Shell_NotifyIcon refused the icon (Explorer may not be ready; it is retried when the taskbar is re-created)");
            return false;
        }

        data.Version = NotifyIconVersion4;
        if (!Shell_NotifyIcon(NimSetVersion, ref data))
            UrDeckLog.Warn("Tray icon: could not select the version 4 callback; the menu may not open");
        return true;
    }

    private NotifyIconData NewData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Hwnd = _hwnd,
        Id = IconId,
    };

    private nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == _taskbarCreated && _taskbarCreated != 0)
        {
            _added = false;
            UrDeckLog.Info("Taskbar re-created; adding the tray icon again");
            AddIcon();
            return 0;
        }

        if (message == CallbackMessage)
        {
            // Version 4: the event is in the low word of lParam, the anchor point in wParam.
            if (((uint)lParam & 0xFFFF) == WmContextMenu)
                ShowMenu((short)((ulong)wParam & 0xFFFF), (short)(((ulong)wParam >> 16) & 0xFFFF));
            return 0;
        }

        return message == WmDestroy ? 0 : DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ShowMenu(int x, int y)
    {
        nint menu = CreatePopupMenu();
        if (menu == 0)
            return;

        try
        {
            for (int i = 0; i < _entries.Count; i++)
                AppendMenu(menu, MfString, (nuint)(FirstCommandId + i), _entries[i].Label);

            // Without the owner in the foreground the menu does not close on a click elsewhere; the WM_NULL after
            // it is the documented fix for the menu's first-click quirk.
            SetForegroundWindow(_hwnd);
            uint chosen = TrackPopupMenuEx(menu, TpmReturnCmd | TpmRightButton | TpmBottomAlign, x, y, _hwnd, 0);
            PostMessage(_hwnd, WmNull, 0, 0);

            int index = (int)chosen - (int)FirstCommandId;
            if (chosen != 0 && index >= 0 && index < _entries.Count)
            {
                UrDeckLog.Info($"Tray menu: {_entries[index].Label}");
                _entries[index].Run();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Hwnd;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid Item;
        public nint BalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string className, nint instance);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern nint LoadIconW(nint instance, nint name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? name);
}
