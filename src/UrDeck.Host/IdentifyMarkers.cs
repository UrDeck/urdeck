// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;

namespace UrDeck.Host;

/// <summary>
/// The numbers Identify puts in a corner of every display. Each is a small layered pop-up window that never takes focus,
/// lets clicks through and stays out of the taskbar and the window switcher, drawn with GDI: nothing exists while no
/// marker shows, and showing one costs no XAML tree. Create and use it on the UI thread.
/// </summary>
internal sealed class IdentifyMarkers : IDisposable
{
    private const string ClassName = "UrDeckIdentifyMarker";
    private const int SideDip = 160;
    private const int MarginDip = 32;
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WmPaint = 0x000F;
    private const uint WmEraseBackground = 0x0014;
    private const uint LwaAlpha = 0x2;
    private const int SwShowNoActivate = 4;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int TransparentBackground = 1;
    private const int FwBold = 700;
    private const uint DtCenter = 0x1;
    private const uint DtVCenter = 0x4;
    private const uint DtSingleLine = 0x20;
    private const nint HwndTopmost = -1;

    // One class and one callback for the life of the process; the window procedure must not be collected.
    private static readonly WndProc Proc = OnMessage;
    private static bool Registered;

    private readonly List<nint> _windows = [];

    /// <summary>Shows each connected row's number on its display, replacing the markers that are showing.</summary>
    public void Show(IEnumerable<DisplayRow> rows)
    {
        Hide();
        if (!Register())
            return;

        foreach (var row in rows)
        {
            if (row.Display is not { } display || row.Number is not { } number)
                continue;

            int side = (int)Math.Round(SideDip * display.Scaling);
            int margin = (int)Math.Round(MarginDip * display.Scaling);
            nint hwnd = CreateWindowEx(
                WsExTopmost | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate,
                ClassName, number.ToString(System.Globalization.CultureInfo.InvariantCulture), WsPopup,
                display.Bounds.X + margin, display.Bounds.Y + margin, side, side, 0, 0, GetModuleHandle(null), 0);
            if (hwnd == 0)
            {
                UrDeckLog.Warn($"Identify: could not create the marker for display {number} (error {Marshal.GetLastWin32Error()})");
                continue;
            }

            SetLayeredWindowAttributes(hwnd, 0, 235, LwaAlpha);
            _ = SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, side + 1, side + 1, side / 5, side / 5), true);
            SetWindowPos(hwnd, HwndTopmost, display.Bounds.X + margin, display.Bounds.Y + margin, side, side, SwpNoActivate | SwpShowWindow);
            ShowWindow(hwnd, SwShowNoActivate);
            UpdateWindow(hwnd);
            _windows.Add(hwnd);
        }
    }

    /// <summary>Removes every marker that is showing.</summary>
    public void Hide()
    {
        foreach (nint hwnd in _windows)
            DestroyWindow(hwnd);
        _windows.Clear();
    }

    public void Dispose() => Hide();

    private static bool Register()
    {
        if (Registered)
            return true;

        var windowClass = new WindowClassEx
        {
            Size = (uint)Marshal.SizeOf<WindowClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(Proc),
            Instance = GetModuleHandle(null),
            ClassName = ClassName,
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            UrDeckLog.Warn($"Identify: could not register the marker window class (error {Marshal.GetLastWin32Error()})");
            return false;
        }

        Registered = true;
        return true;
    }

    private static nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmEraseBackground:
                return 1;
            case WmPaint:
                Paint(hwnd);
                return 0;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private static void Paint(nint hwnd)
    {
        nint hdc = BeginPaint(hwnd, out var paint);
        try
        {
            GetClientRect(hwnd, out var client);
            nint brush = CreateSolidBrush(0x00242020); // BGR: a dark card
            _ = FillRect(hdc, ref client, brush);
            DeleteObject(brush);

            char[] text = new char[16];
            int length = GetWindowText(hwnd, text, text.Length);
            nint font = CreateFont(-(client.Bottom * 3 / 5), 0, 0, 0, FwBold, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            nint previous = SelectObject(hdc, font);
            _ = SetBkMode(hdc, TransparentBackground);
            _ = SetTextColor(hdc, 0x00FFFFFF);
            _ = DrawText(hdc, new string(text, 0, Math.Max(length, 0)), -1, ref client, DtCenter | DtVCenter | DtSingleLine);
            SelectObject(hdc, previous);
            DeleteObject(font);
        }
        finally
        {
            EndPaint(hwnd, ref paint);
        }
    }

    private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public nint Hdc;
        public int Erase;
        public Rect Paint;
        public int Restore;
        public int IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Reserved;
    }

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

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, [Out] char[] text, int maxCount);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint hwnd, out PaintStruct paint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPaint(nint hwnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hdc, ref Rect rect, nint brush);

    [DllImport("user32.dll", EntryPoint = "DrawTextW", CharSet = CharSet.Unicode)]
    private static extern int DrawText(nint hdc, string text, int count, ref Rect rect, uint format);

    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("gdi32.dll", EntryPoint = "CreateFontW", CharSet = CharSet.Unicode)]
    private static extern nint CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(nint hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint hdc, uint color);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? name);
}
