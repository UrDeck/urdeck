// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;

namespace UrDeck.Engine.Display;

/// <summary>How the deck's display was found among the connected ones.</summary>
public enum DisplayMatch
{
    /// <summary>The remembered display is not connected, or there is no display to choose from.</summary>
    Absent,

    /// <summary>Chosen with the selectors: nothing was remembered, or the selectors were edited since.</summary>
    Chosen,

    /// <summary>The remembered device path.</summary>
    Path,

    /// <summary>The only connected display with the remembered model; its path is new.</summary>
    Model,

    /// <summary>Several connected displays have the remembered model and none the path: the first by path order.</summary>
    AmbiguousModel,
}

/// <summary>The result of a selection.</summary>
/// <param name="Match">How the display was found.</param>
/// <param name="Display">The deck's display, or null when it is absent.</param>
/// <param name="Remember">What to write to the configuration as <c>display</c>, or null when nothing changes.</param>
/// <param name="Candidates">The displays sharing the model, for <see cref="DisplayMatch.AmbiguousModel"/>.</param>
public sealed record DisplaySelection(
    DisplayMatch Match, DisplayInfo? Display, RememberedDisplay? Remember, IReadOnlyList<DisplayInfo> Candidates);

/// <summary>
/// Which connected display the deck belongs to. The selectors (<c>monitorName</c>, <c>monitor</c>) make the choice once;
/// after that the remembered display is followed by its device path, then by its model.
/// </summary>
public static class DisplaySelector
{
    /// <summary>
    /// What the configuration's selectors currently say, as recorded in <see cref="RememberedDisplay.Selector"/>:
    /// <c>monitorName</c> in lower case, or <c>index:N</c> when only <c>monitor</c> applies.
    /// </summary>
    public static string CurrentSelector(UrDeckConfig config)
    {
        string? name = config.MonitorName?.Trim();
        return string.IsNullOrEmpty(name) ? $"index:{config.Monitor}" : name.ToLowerInvariant();
    }

    /// <summary>
    /// The selector rules: <c>monitorName</c> ("primary", "tallest", "widest", "largest", or a device name suffix like
    /// "DISPLAY1"), then the 1-based <c>monitor</c> index, then the primary display. Null only without any display.
    /// </summary>
    public static DisplayInfo? Choose(UrDeckConfig config, IReadOnlyList<DisplayInfo> displays)
    {
        if (displays.Count == 0)
            return null;

        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
        string? name = config.MonitorName?.Trim();

        DisplayInfo? chosen = name?.ToLowerInvariant() switch
        {
            null or "" => null,
            "primary" => primary,
            "tallest" => displays.Where(d => d.Bounds.Height > d.Bounds.Width).MaxBy(d => d.Bounds.Height),
            "widest" => displays.MaxBy(d => d.Bounds.Width),
            "largest" => displays.MaxBy(d => (long)d.Bounds.Width * d.Bounds.Height),
            _ => displays.FirstOrDefault(d => d.DeviceName.EndsWith(name!, StringComparison.OrdinalIgnoreCase)),
        };

        if (chosen == null && config.Monitor >= 1 && config.Monitor <= displays.Count)
            chosen = displays[config.Monitor - 1];

        return chosen ?? primary;
    }

    /// <summary>Finds the deck's display. Changes nothing: the caller stores <see cref="DisplaySelection.Remember"/>.</summary>
    public static DisplaySelection Select(UrDeckConfig config, IReadOnlyList<DisplayInfo> displays)
    {
        string selector = CurrentSelector(config);
        var remembered = config.Display;

        // An entry that identifies nothing is as good as none.
        bool chooseAgain = remembered == null
            || (string.IsNullOrEmpty(remembered.Path) && string.IsNullOrEmpty(remembered.Model))
            || (remembered.Selector != null && !Same(remembered.Selector.Trim(), selector));
        if (chooseAgain)
        {
            var chosen = Choose(config, displays);
            return chosen == null
                ? new DisplaySelection(DisplayMatch.Absent, null, null, [])
                : new DisplaySelection(DisplayMatch.Chosen, chosen, Remember(chosen, selector, null), []);
        }

        var byPath = displays.FirstOrDefault(d => Same(d.Path, remembered!.Path));
        if (byPath != null)
            return new DisplaySelection(DisplayMatch.Path, byPath, null, []);

        if (!string.IsNullOrEmpty(remembered!.Model))
        {
            var sameModel = displays.Where(d => Same(d.Model, remembered.Model))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
            if (sameModel.Count == 1)
                return new DisplaySelection(DisplayMatch.Model, sameModel[0], Remember(sameModel[0], remembered.Selector, remembered), []);
            if (sameModel.Count > 1)
                return new DisplaySelection(DisplayMatch.AmbiguousModel, sameModel[0], Remember(sameModel[0], remembered.Selector, remembered), sameModel);
        }

        return new DisplaySelection(DisplayMatch.Absent, null, null, []);
    }

    private static RememberedDisplay Remember(DisplayInfo display, string? selector, RememberedDisplay? previous) => new()
    {
        Name = display.Name,
        Model = display.Model,
        Path = display.Path,
        Selector = selector,
        ExtensionData = previous?.ExtensionData,
    };

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
