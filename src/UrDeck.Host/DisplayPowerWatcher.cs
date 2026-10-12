// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Tells when Windows switches the display off and on (<c>GUID_CONSOLE_DISPLAY_STATE</c>). A monitor that sleeps does not
/// always disconnect, so placement may not hide the deck; this is how a moving background learns to stop its frames.
/// It reads <c>WM_POWERBROADCAST</c> in a subclass of the deck's window of its own, so it works whether or not
/// <see cref="WindowFocus"/> is installed (it is not in the activatable development mode). Sleep needs nothing here: no
/// frames are produced while the PC is suspended.
/// </summary>
internal sealed class DisplayPowerWatcher : IDisposable
{
    private const uint WmPowerBroadcast = 0x0218;
    private const nint PbtPowerSettingChange = 0x8013;
    private const uint DeviceNotifyWindowHandle = 0;
    private const nuint SubclassId = 2; // WindowFocus uses 1

    /// <summary>Where the setting's data starts in POWERBROADCAST_SETTING: a GUID (16 bytes) and a length (4).</summary>
    private const int DataOffset = 20;

    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private readonly nint _hwnd;
    private readonly SubclassProc _proc;
    private nint _registration;
    private bool _subclassed;

    private DisplayPowerWatcher(nint hwnd)
    {
        _hwnd = hwnd;
        _proc = OnMessage;
    }

    /// <summary>Raised on the window's thread with true when the display is on (or dimmed) and false when it is off.</summary>
    public event Action<bool>? DisplayStateChanged;

    /// <summary>Starts watching <paramref name="hwnd"/>; null (after a warning) when Windows refuses. Keep the result alive with the window.</summary>
    public static DisplayPowerWatcher? Start(nint hwnd)
    {
        var watcher = new DisplayPowerWatcher(hwnd);
        Guid setting = ConsoleDisplayState;
        watcher._registration = RegisterPowerSettingNotification(hwnd, ref setting, DeviceNotifyWindowHandle);
        if (watcher._registration == 0)
        {
            UrDeckLog.Warn("Could not register for display power changes; a moving background will not stop when the display is off");
            return null;
        }

        watcher._subclassed = SetWindowSubclass(hwnd, watcher._proc, SubclassId, 0);
        if (!watcher._subclassed)
        {
            UrDeckLog.Warn("Could not subclass the window for display power changes; a moving background will not stop when the display is off");
            watcher.Dispose();
            return null;
        }

        return watcher;
    }

    public void Dispose()
    {
        if (_subclassed)
        {
            RemoveWindowSubclass(_hwnd, _proc, SubclassId);
            _subclassed = false;
        }

        if (_registration != 0)
        {
            UnregisterPowerSettingNotification(_registration);
            _registration = 0;
        }
    }

    private nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == WmPowerBroadcast && wParam == PbtPowerSettingChange && lParam != 0)
        {
            var setting = Marshal.PtrToStructure<PowerBroadcastSetting>(lParam);
            if (setting.PowerSetting == ConsoleDisplayState && setting.DataLength >= sizeof(uint))
            {
                // 0 off, 1 on, 2 dimmed: only off stops the frames.
                uint state = (uint)Marshal.ReadInt32(lParam, DataOffset);
                bool on = state != 0;
                UrDeckLog.Info(on ? "Windows: display on" : "Windows: display off");
                DisplayStateChanged?.Invoke(on);
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public uint DataLength;
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint RegisterPowerSettingNotification(nint recipient, ref Guid powerSetting, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(nint handle);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
}
