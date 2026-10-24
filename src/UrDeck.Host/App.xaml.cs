// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UrDeck.Engine.Data;
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
    private SettingsWindow? _settings;
    private SettingsSoak? _soak;
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

        // --demo: the providers that carry a sample run as it, from now on; the configuration is never written.
        bool demo = Array.IndexOf(Environment.GetCommandLineArgs(), "--demo") >= 0;
        _host = new HostContext(
            Program.Data,
            saveConfiguration: !demo,
            demo: demo ? new SampleClock(() => DateTime.Now, DateTime.Now) : null);
        _window = new MainWindow(_host.Config, _host.Plugins, _host.Themes, _host.Shaders, _host.Data.Root, _host.SaveConfiguration);
        _window.Closed += (_, _) =>
        {
            // Quitting closes Settings too: it holds the host, which is disposed next.
            _settings?.Close();
            _tray?.Dispose();
            _tray = null;
            _host.Dispose();
            _host = null;
        };
        // AppContext.BaseDirectory below is the application folder: the icon ships with the executable.
        // The tray icon is the way to close the app: the window never takes focus and has no taskbar button.
        var window = _window;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        // Opening a window is queued, not done inside the tray's window procedure or the menu's message loop.
        void OpenSettingsLater() => dispatcher.TryEnqueue(OpenSettings);
        _tray = new TrayIcon(
            [
                new TrayMenuEntry("Settings", OpenSettingsLater),
                new TrayMenuEntry("Open data folder", OpenDataFolder),
                new TrayMenuEntry("Start with Windows", ToggleAutostart, () => _autostart.State == AutostartState.On),
                new TrayMenuEntry("Quit", window.Close),
            ],
            Path.Combine(AppContext.BaseDirectory, "urdeck.ico"));
        _tray.Selected += OpenSettingsLater;

        // --quit sets an event; closing the window on the UI thread is what the tray's Quit does.
        Program.Instance?.ListenForQuit(() =>
        {
            UrDeckLog.Info("--quit received; closing");
            dispatcher.TryEnqueue(window.Close);
        });

        // The window is not shown here: placement shows it once its display is connected, and hides it while it is not.
        window.PlacementChanged += (state, display) => _tray?.SetTooltip(
            state == PlacementState.Absent ? $"UrDeck: waiting for {display ?? "a display"}" : "UrDeck");
        window.Start();

        // Opt-in measurement: URDECK_SETTINGS_SOAK=1 opens and closes the settings window 20 times and logs memory;
        // =hold opens it once, five seconds after start, and leaves it open.
        string? soak = Environment.GetEnvironmentVariable("URDECK_SETTINGS_SOAK");
        if (soak is "1" or "hold")
        {
            _soak = new SettingsSoak(dispatcher, OpenSettings, () => _settings?.Close(), cycles: soak == "1" ? 20 : 0);
            _soak.Start();
        }
    }

    /// <summary>Opens the settings window, or brings the open one forward: there is at most one.</summary>
    private void OpenSettings()
    {
        if (_window is null)
            return;
        if (_settings is not null)
        {
            _settings.BringForward();
            return;
        }

        var settings = new SettingsWindow(_window, Path.Combine(AppContext.BaseDirectory, "urdeck.ico"));
        settings.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settings, settings))
                _settings = null;
        };
        _settings = settings;
        settings.Open();
    }

    /// <summary>Shows the data folder in File Explorer, creating it first if it is missing; a failure is logged.</summary>
    private void OpenDataFolder()
    {
        string root = Program.Data.Root;
        try
        {
            Directory.CreateDirectory(root);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{root}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Could not open the data folder {root}: {ex.Message}");
        }
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
