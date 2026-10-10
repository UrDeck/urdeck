// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;

namespace UrDeck.Engine.Plugin;

public interface IPluginService
{
    /// <summary>Raised (on a thread-pool thread) after plugins were reloaded because the plugin directory changed.</summary>
    event Action? PluginsChanged;

    void ScanAndLoadPlugins(string pluginDirectory);
    void ReloadPlugins();
    IReadOnlyList<string> GetRegisteredWidgetTypes();
    WidgetDescriptor? GetDescriptor(string typeId);
    IWidget? CreateWidget(WidgetConfig config);
    IWidget? CreateWidget(WidgetConfig config, out WidgetServices? services);
}
