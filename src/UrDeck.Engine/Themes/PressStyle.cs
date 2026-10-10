// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

/// <summary>
/// The theme's press feedback: the scale and the opacity of a card while it is pressed. The host applies them to the
/// widget's layer; widgets never see them.
/// </summary>
public sealed record PressStyle(float Scale, float Opacity)
{
    /// <summary>False when a pressed card looks like a resting one, so there is nothing to animate.</summary>
    public bool IsVisible => Scale != 1f || Opacity != 1f;

    public static PressStyle Resolve(LoadedTheme loaded)
    {
        var press = loaded.Definition.Press!;
        return new PressStyle((float)press.Scale!.Value, (float)press.Opacity!.Value);
    }
}
