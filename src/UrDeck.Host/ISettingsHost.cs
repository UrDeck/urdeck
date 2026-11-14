// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Display;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Settings;

namespace UrDeck.Host;

/// <summary>An installed widget type as the editor's palette lists it.</summary>
/// <param name="Descriptor">The type: id, name, description, sizes.</param>
/// <param name="PluginName">The assembly that provides it.</param>
/// <param name="PluginVersion">Its version.</param>
/// <param name="FirstParty">Whether it ships with UrDeck (no plugin line in the palette).</param>
internal sealed record WidgetTypeInfo(WidgetDescriptor Descriptor, string PluginName, string PluginVersion, bool FirstParty);

/// <summary>Where the deck is: shown on a display, or waiting for the remembered one.</summary>
/// <param name="Shown">The deck is on its display.</param>
/// <param name="DisplayName">The display it is on, or the one it waits for; null when none is known.</param>
internal sealed record DeckState(bool Shown, string? DisplayName);

/// <summary>The editor's preview: a deck session of its own over the preview's readings. Disposing it releases both.</summary>
internal sealed class PreviewDeck(DeckSession session, PreviewReadings readings, Action release) : IDisposable
{
    public DeckSession Session { get; } = session;

    public PreviewReadings Readings { get; } = readings;

    public void Dispose()
    {
        Session.Dispose();
        release();
        Readings.Dispose();
    }
}

/// <summary>
/// Everything the settings window (the editor) asks of the host, and nothing else: it never sees the deck window. The seam
/// is what lets the window move to a process of its own, with an implementation that reads and writes the configuration
/// file. All members are used on the UI thread.
/// </summary>
internal interface ISettingsHost
{
    /// <summary>The configuration as a document: what the editor shows, edits, undoes.</summary>
    ConfigDocument Document { get; }

    /// <summary>The configuration file's full path.</summary>
    string ConfigPath { get; }

    /// <summary>The log file's full path.</summary>
    string LogPath { get; }

    /// <summary>
    /// The surface the deck lays its page out for: the deck's display while it is shown, else the last one it was on, else
    /// 1100x3840 at scale 1. The preview is laid out for it, so text fits and wraps as on the deck.
    /// </summary>
    DeckSurface DeckSurface { get; }

    /// <summary>
    /// A deck session for the editor's preview, shown by <paramref name="presenter"/>: the deck's configuration, theme,
    /// background and widgets, on the page <paramref name="pageId"/>, with its own readings (live, or the samples), animated by
    /// <paramref name="frames"/>, without input. With <paramref name="config"/> the session shows that configuration (it never
    /// changes) instead of the deck's: a theme's picture of the user's page.
    /// </summary>
    PreviewDeck CreatePreview(IDeckPresenter presenter, IFrameSource frames, string? pageId, UrDeckConfig? config = null);

    /// <summary>The background of a plan for the preview, prepared for the surface; null for the theme's colour.</summary>
    Engine.Backgrounds.DeckBackground? BuildBackground(Engine.Backgrounds.BackgroundPlan plan, DeckSurface surface);

    /// <summary>The background's key, so the preview builds it only when it changed.</summary>
    string? BackgroundKey(Engine.Backgrounds.BackgroundPlan plan);

    /// <summary>The installed themes (built-in and the user's folders): the Theme and background section lists them.</summary>
    Engine.Themes.ThemeStore Themes { get; }

    /// <summary>The installed backgrounds.</summary>
    Engine.Backgrounds.ShaderStore Shaders { get; }

    /// <summary>The data folder: relative picture paths and the editor's cache resolve against it.</summary>
    string DataFolder { get; }

    /// <summary>Which background the deck shows now and, if the configured one could not be shown, why. Reads no pictures and compiles nothing.</summary>
    Engine.Backgrounds.BackgroundStatus BackgroundStatus();

    /// <summary>The page the deck shows now (its id); null without pages.</summary>
    string? DeckPageId { get; }

    /// <summary>Moves the deck to the page with the normal settle (the deck follows the editor); nothing is written.</summary>
    void ShowPageOnDeck(string pageId);

    /// <summary>The deck settled on another page (a swipe on the panel, or <see cref="ShowPageOnDeck"/>).</summary>
    event Action<string?>? DeckPageChanged;

    /// <summary>Where the deck is now.</summary>
    DeckState DeckState { get; }

    /// <summary>The host's readings: actions and suggestions come from its providers, as the deck's own widgets use them.</summary>
    Engine.Data.ReadingHub Readings { get; }

    /// <summary>The loaded providers, for the reading picker and the Readings section.</summary>
    IReadOnlyCollection<Engine.Data.ProviderDescriptor> ProviderDescriptors { get; }

    /// <summary>The descriptions of the readings the pages use, kept for when a provider cannot list its readings.</summary>
    Engine.Editor.ReadingDescriptionCache ReadingCache { get; }

    /// <summary>The installed widget types, by name.</summary>
    IReadOnlyList<WidgetTypeInfo> WidgetTypes { get; }

    /// <summary>An installed widget type; null when it is not installed.</summary>
    WidgetDescriptor? DescribeWidget(string typeId);

    /// <summary>
    /// Why a widget of this type cannot be shown: its plugin failed to load, was removed, or was never installed. Null when the
    /// type is installed.
    /// </summary>
    WidgetUnavailable? ClassifyWidget(string typeId);

    /// <summary>A reading's label (for the names of widgets in messages); null when it is unknown.</summary>
    string? ReadingLabel(string readingId);

    /// <summary>The providers whose readings are in use that cannot start because a required setting is missing.</summary>
    IReadOnlyList<string> ProvidersWaitingForSettings { get; }

    /// <summary>Whether this copy starts with Windows; null when it cannot tell.</summary>
    bool StartsWithWindows { get; }

    /// <summary>Turns starting with Windows on or off, as the tray entry does.</summary>
    void SetStartWithWindows(bool on);

    /// <summary>The displays now connected as picker rows, with the deck's display marked.</summary>
    DisplayList GetDisplayList();

    /// <summary>Moves the deck to <paramref name="display"/> and remembers the choice (see display-targeting, "Explicit Display Choice").</summary>
    void ChooseDisplay(DisplayInfo display);

    /// <summary>Hands the choice back to the <c>monitorName</c> and <c>monitor</c> selectors.</summary>
    void ChooseAutomatically();

    /// <summary>The providers that declare settings, with the current values (a secret's value never leaves the secret store).</summary>
    IReadOnlyList<ProviderForm> GetProviderForms();

    /// <summary>Saves a provider's form: non-secret values to the configuration, new secrets to the secret store; the provider restarts.</summary>
    EditorResult SaveProvider(string providerId, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> newSecrets);

    /// <summary>Deletes a provider's stored secret.</summary>
    EditorResult RemoveProviderSecret(string providerId, string key);

    /// <summary>Runs a provider's connection test with the values of the form, saving nothing.</summary>
    Task<EditorResult> TestProviderAsync(string providerId, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> newSecrets, CancellationToken cancellationToken);

    /// <summary>Sign-in of the providers that offer one (Connect, Cancel, Disconnect); its sessions end when the window closes.</summary>
    ProviderSignIn SignIn { get; }

    /// <summary>Opens a web address in the default browser through the launcher (a sign-in challenge's address); false when it is refused.</summary>
    bool OpenAddress(string address);

    /// <summary>The displays, the arrangement or the placement changed. May arrive in bursts and when nothing visible changed.</summary>
    event Action? Changed;
}
