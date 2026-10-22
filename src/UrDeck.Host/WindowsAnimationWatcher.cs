// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Windows' "Animation effects" setting (Settings, Accessibility, Visual effects): read at startup and again whenever the
/// deck's window gets <c>WM_SETTINGCHANGE</c>, in a subclass of its own (id 3; <see cref="WindowFocus"/> is 1 and
/// <see cref="DisplayPowerWatcher"/> 2). Any <c>WM_SETTINGCHANGE</c> re-reads the value, not only one with a matching
/// parameter: the read is cheap and the parameter is not reliable for every sender.
/// </summary>
internal sealed class WindowsAnimationWatcher : IDisposable
{
    private const uint WmSettingChange = 0x001A;
    private const uint SpiGetClientAreaAnimation = 0x1042;
    private const nuint SubclassId = 3;

    private readonly nint _hwnd;
    private readonly SubclassProc _proc;
    private bool _subclassed;

    private WindowsAnimationWatcher(nint hwnd)
    {
        _hwnd = hwnd;
        _proc = OnMessage;
        Enabled = Read();
    }

    /// <summary>Whether Windows' animation effects are on.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Raised on the window's thread with the new value when it changed.</summary>
    public event Action<bool>? Changed;

    /// <summary>The setting now; true (animations on) when Windows does not say.</summary>
    public static bool Read() => SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int enabled, 0) ? enabled != 0 : true;

    /// <summary>Starts watching <paramref name="hwnd"/>; null (after a warning) when the window cannot be subclassed.</summary>
    public static WindowsAnimationWatcher? Start(nint hwnd)
    {
        var watcher = new WindowsAnimationWatcher(hwnd);
        watcher._subclassed = SetWindowSubclass(hwnd, watcher._proc, SubclassId, 0);
        if (!watcher._subclassed)
        {
            UrDeckLog.Warn("Could not subclass the window for Windows' animation setting; a change of it applies at the next start");
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
    }

    private nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == WmSettingChange)
        {
            bool now = Read();
            if (now != Enabled)
            {
                Enabled = now;
                UrDeckLog.Info(now ? "Windows: animation effects on" : "Windows: animation effects off");
                Changed?.Invoke(now);
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
}
