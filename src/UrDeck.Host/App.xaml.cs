// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Startup;

namespace UrDeck.Host;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed when the main window closes, the shutdown hook.")]
public partial class App : Application
{
    private HostContext? _host;
    private MainWindow? _window;
    private TrayIcon? _tray;
    private DispatcherQueueTimer? _memTimer;
    private readonly RunKeyAutostart _autostart = new();

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => UrDeckLog.Error("Unhandled UI exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Opt-in diagnostics: URDECK_MEMLOG=1 logs process/GC memory 10 s after startup.
        if (Environment.GetEnvironmentVariable("URDECK_MEMLOG") == "1")
        {
            _memTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _memTimer.Interval = TimeSpan.FromSeconds(10);
            _memTimer.IsRepeating = false;
            _memTimer.Tick += (_, _) =>
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                UrDeckLog.Info($"MEM ws={proc.WorkingSet64 / 1048576.0:0.#}MB private={proc.PrivateMemorySize64 / 1048576.0:0.#}MB " +
                              $"gcHeap={GC.GetTotalMemory(false) / 1048576.0:0.#}MB gcCommitted={GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0:0.#}MB");
            };
            _memTimer.Start();
        }

        _host = new HostContext(saveConfiguration: true);
        _window = new MainWindow(_host.Config, _host.Plugins, _host.Themes, _host.SaveConfiguration);
        _window.Closed += (_, _) =>
        {
            _tray?.Dispose();
            _tray = null;
            _host.Dispose();
            _host = null;
        };
        // The tray icon is the way to close the app: the window never takes focus and has no taskbar button.
        var window = _window;
        _tray = new TrayIcon(
            [
                new TrayMenuEntry("Start with Windows", ToggleAutostart, () => _autostart.State == AutostartState.On),
                new TrayMenuEntry("Quit", window.Close),
            ],
            Path.Combine(AppContext.BaseDirectory, "urdeck.ico"));

        // --quit sets an event; closing the window on the UI thread is what the tray's Quit does.
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        Program.Instance?.ListenForQuit(() =>
        {
            UrDeckLog.Info("--quit received; closing");
            dispatcher.TryEnqueue(window.Close);
        });

        // The window is not shown here: placement shows it once its display is connected, and hides it while it is not.
        window.PlacementChanged += (state, display) => _tray?.SetTooltip(
            state == PlacementState.Absent ? $"UrDeck: waiting for {display ?? "a display"}" : "UrDeck");
        window.Start();
    }

    private void ToggleAutostart()
    {
        var before = _autostart.State;
        string? replaced = before == AutostartState.AnotherCopy ? _autostart.RegisteredExecutable : null;
        if (before == AutostartState.On)
            _autostart.SwitchOff();
        else
            _autostart.SwitchOn();

        UrDeckLog.Info($"Start with Windows: {before} -> {_autostart.State}" + (replaced is null ? "" : $" (replaced {replaced})"));
    }
}
