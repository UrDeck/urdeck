// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Startup;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>The engine objects both the live window and <c>--snapshot</c> need: config, themes, plugins and the displays.</summary>
internal sealed class HostContext : IDisposable
{
    private readonly bool _saveConfiguration;

    public ConfigStore Config { get; }
    public ThemeStore Themes { get; }
    public ShaderStore Shaders { get; }

    /// <summary>Where the user's files live; a background picture's relative path starts at its root.</summary>
    public DataFolder Data { get; }

    public WidgetPluginLoader Plugins { get; }

    /// <summary>The displays connected at startup.</summary>
    public IReadOnlyList<DisplayInfo> Displays { get; }

    /// <summary>
    /// The deck's display among <see cref="Displays"/>, or null when the remembered one is not connected. Selecting
    /// here remembers nothing: the live window's placement does that, and <c>--snapshot</c> must not.
    /// </summary>
    public DisplayInfo? Target { get; }

    // Startup sequence per host-shell spec: monitor -> config -> plugins.
    /// <param name="data">Where the user's files live.</param>
    /// <param name="saveConfiguration">False for <c>--snapshot</c> and <c>--demo</c>, which never write the configuration.</param>
    /// <param name="demo">The clock of the samples in demo mode (<c>--demo</c>); null runs the providers.</param>
    public HostContext(DataFolder data, bool saveConfiguration, SampleClock? demo = null)
    {
        _saveConfiguration = saveConfiguration;
        Data = data;
        UrDeckLog.Info($"UrDeck starting from {data.ApplicationFolder}");

        Displays = MonitorPlacement.GetDisplays();
        foreach (var d in Displays)
            UrDeckLog.Info($"Monitor: {d}");

        // The deck creates a missing configuration from the sample; --snapshot and --demo read the sample in memory and write nothing.
        Config = new ConfigStore(data.ConfigPath, data.SamplePath, createIfMissing: saveConfiguration);

        // User themes live in the data folder; the built-in ones are embedded in the engine.
        Themes = new ThemeStore(data.ThemesFolder);

        // User background shaders live in the data folder; the built-in ones are embedded.
        Shaders = new ShaderStore(data.BackgroundsFolder);

        // First-party plugins come with the application, the user's from the data folder. A context that writes nothing
        // does not create the user folder, and so not the data folder either.
        Plugins = new WidgetPluginLoader(iconCacheFolder: data.IconCacheFolder, demo: demo);
        Plugins.ScanAndLoadPlugins(saveConfiguration ? data.PluginFolders : data.PluginFolders.Where(Directory.Exists).ToList());

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
