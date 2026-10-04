// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>The engine objects both the live window and <c>--snapshot</c> need: config, themes, plugins and the target monitor.</summary>
internal sealed class HostContext : IDisposable
{
    public ConfigStore Config { get; }
    public ThemeStore Themes { get; }
    public WidgetPluginLoader Plugins { get; }
    public MonitorInfo Target { get; }

    // Startup sequence per host-shell spec: monitor -> config -> plugins.
    public HostContext()
    {
        string appDir = AppContext.BaseDirectory;
        UrDeckLog.Info($"UrDeck starting from {appDir}");

        var monitors = MonitorPlacement.GetMonitors();
        foreach (var m in monitors)
            UrDeckLog.Info($"Monitor: {m}");

        Config = new ConfigStore(Path.Combine(appDir, "urdeck-config.json"));

        // User themes live in a themes folder next to the executable; the built-in ones are embedded in the engine.
        Themes = new ThemeStore(Path.Combine(appDir, "themes"));

        Plugins = new WidgetPluginLoader();
        Plugins.ScanAndLoadPlugins(Path.Combine(appDir, "plugins"));

        Target = MonitorPlacement.Select(Config.Config, monitors);
        UrDeckLog.Info($"Target monitor: {Target}");
    }

    public void Dispose()
    {
        try
        { Config.Save(); }
        catch (Exception ex) { UrDeckLog.Warn($"Could not save config on exit: {ex.Message}"); }
        Config.Dispose();
        Plugins.Dispose();
    }
}
