// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Deck;

/// <summary>The configuration a deck session shows, and when it changed (<see cref="ConfigStore"/> is one).</summary>
public interface IDeckConfigSource
{
    UrDeckConfig Config { get; }

    /// <summary>Raised on any thread after the configuration was reloaded.</summary>
    event Action<UrDeckConfig>? ConfigChanged;
}

/// <summary>Where a deck session's widgets come from (<see cref="WidgetPluginLoader"/> is one).</summary>
public interface IDeckWidgets
{
    WidgetDescriptor? GetDescriptor(string typeId);

    IReadOnlyList<string> GetRegisteredWidgetTypes();

    /// <summary>A configured widget and its own host services (null when it gets none); null when the type is not registered.</summary>
    IWidget? CreateWidget(WidgetConfig config, out WidgetServices? services);

    /// <summary>Raised on any thread after the plugins were reloaded: every widget made before belongs to an old plugin.</summary>
    event Action? PluginsChanged;
}

/// <summary>What a deck session needs of the readings its widgets declared (<see cref="Data.ReadingHub"/> is one).</summary>
public interface IDeckReadings
{
    /// <summary>Counts a user of each reading (its provider runs while it has users).</summary>
    void Subscribe(IEnumerable<string> ids);

    void Unsubscribe(IEnumerable<string> ids);

    /// <summary>Raised on a sampling thread with the ids whose values changed.</summary>
    event Action<IReadOnlyCollection<string>>? ReadingsChanged;
}

/// <summary>Everything a <see cref="DeckSession"/> takes from outside; nothing it uses is process-wide.</summary>
/// <param name="Config">The configuration and its reloads.</param>
/// <param name="LoadTheme">Loads the theme of a name (the configuration's <c>theme</c>), falling back as the theme store does.</param>
/// <param name="Widgets">The widget types and how to make one.</param>
/// <param name="Readings">The readings the widgets subscribe to.</param>
/// <param name="Scheduler">The deck's thread.</param>
/// <param name="Frames">The frames animating widgets are repainted at.</param>
/// <param name="WindowsAnimations">Windows' "animation effects" setting at the start.</param>
public sealed record DeckSessionInputs(
    IDeckConfigSource Config,
    Func<string?, LoadedTheme> LoadTheme,
    IDeckWidgets Widgets,
    IDeckReadings Readings,
    IDeckScheduler Scheduler,
    IFrameSource Frames,
    bool WindowsAnimations = true);
