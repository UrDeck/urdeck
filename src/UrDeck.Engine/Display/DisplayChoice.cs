// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;

namespace UrDeck.Engine.Display;

/// <summary>
/// What the user's choice in the settings window does to the configuration's <c>display</c>. It changes the object and
/// says whether it did; writing the file and telling placement are the caller's.
/// </summary>
public static class DisplayChoice
{
    /// <summary>
    /// Remembers <paramref name="display"/> as chosen: no <c>selector</c>, so the selectors never replace it. Properties the
    /// host does not know are kept. False when that display was already chosen this way.
    /// </summary>
    public static bool Choose(UrDeckConfig config, DisplayInfo display)
    {
        var current = config.Display;
        if (current is { Selector: null } && string.Equals(current.Path, display.Path, StringComparison.OrdinalIgnoreCase))
            return false;

        config.Display = new RememberedDisplay
        {
            Name = display.Name,
            Model = display.Model,
            Path = display.Path,
            Selector = null,
            ExtensionData = current?.ExtensionData,
        };
        return true;
    }

    /// <summary>
    /// Forgets the remembered display, so that placement chooses again with the selectors and remembers the result. False
    /// when it was already chosen that way: nothing is remembered, or what is carries the selector it was chosen with.
    /// </summary>
    public static bool ChooseAutomatically(UrDeckConfig config)
    {
        if (config.Display is null or { Selector: not null })
            return false;

        config.Display = null;
        return true;
    }
}
