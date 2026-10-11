// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>The engine objects both the live window and <c>--snapshot</c> need: config, themes, plugins and the displays.</summary>
internal sealed class HostContext : IDisposable
{
    private readonly bool _saveConfiguration;

    public ConfigStore Config { get; }
    public ThemeStore Themes { get; }
    public WidgetPluginLoader Plugins { get; }

    /// <summary>The displays connected at startup.</summary>
    public IReadOnlyList<DisplayInfo> Displays { get; }

    /// <summary>
    /// The deck's display among <see cref="Displays"/>, or null when the remembered one is not connected. Selecting
    /// here remembers nothing: the live window's placement does that, and <c>--snapshot</c> must not.
    /// </summary>
    public DisplayInfo? Target { get; }

    // Startup sequence per host-shell spec: monitor -> config -> plugins.
    /// <param name="saveConfiguration">False for <c>--snapshot</c>, which never writes the configuration.</param>
    public HostContext(bool saveConfiguration)
    {
        _saveConfiguration = saveConfiguration;
        string appDir = AppContext.BaseDirectory;
        UrDeckLog.Info($"UrDeck starting from {appDir}");

        Displays = MonitorPlacement.GetDisplays();
        foreach (var d in Displays)
            UrDeckLog.Info($"Monitor: {d}");

        Config = new ConfigStore(Path.Combine(appDir, "urdeck-config.json"));

        // User themes live in a themes folder next to the executable; the built-in ones are embedded in the engine.
        Themes = new ThemeStore(Path.Combine(appDir, "themes"));

        Plugins = new WidgetPluginLoader();
        Plugins.ScanAndLoadPlugins(Path.Combine(appDir, "plugins"));

        Target = DisplaySelector.Select(Config.Config, Displays).Display;
    }

    /// <summary>Writes the configuration: on exit, and when the remembered display was set or updated.</summary>
    public void SaveConfiguration()
    {
        if (!_saveConfiguration)
            return;
        try
        { Config.Save(); }
        catch (Exception ex) { UrDeckLog.Warn($"Could not save the configuration: {ex.Message}"); }
    }

    public void Dispose()
    {
        SaveConfiguration();
        Config.Dispose();
        Plugins.Dispose();
    }
}
