// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Display;
using UrDeck.Engine.Settings;

namespace UrDeck.Host;

/// <summary>
/// Everything the settings window asks of the host, and nothing else: it never sees the deck window. The seam is what
/// lets the window move to a process of its own, with an implementation that reads and writes the configuration file.
/// All members are used on the UI thread.
/// </summary>
internal interface ISettingsHost
{
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

    /// <summary>The displays, the arrangement or the placement changed. May arrive in bursts and when nothing visible changed.</summary>
    event Action? Changed;
}
