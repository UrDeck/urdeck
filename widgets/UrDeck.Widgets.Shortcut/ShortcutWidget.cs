// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Input;
using UrDeck.Sdk.Launch;

namespace UrDeck.Widgets.Shortcut;

/// <summary>
/// A card that shows the icon of an application, a file, a folder or a web address and opens it on tap. The host
/// launches, finds the icon and shows the press; the widget only says what and draws the tile.
/// </summary>
[Widget("Shortcut", "Opens an application, a file, a folder or a web address on tap", Id = "urdeck.widgets.shortcut")]
[WidgetSize(1, 1)]
[RefreshOnData]
[Category("Launch")]
public class ShortcutWidget : Widget<ShortcutConfig>, ITapTarget
{
    private LaunchTarget _target;

    protected override void OnConfigured()
    {
        _target = LaunchTarget.Parse(Config.Target);
        if (!_target.IsValid && !string.IsNullOrWhiteSpace(Config.Target))
            Log($"Shortcut: '{Config.Target}' is not a valid target (a rooted path, a bare name, a shell: item or an address); the card does not react to taps.");
    }

    // The first paint is the host's, a new configuration builds a new widget, and the host repaints when the icon
    // arrives. Nothing else changes what the card shows; a tap does not.
    public override bool NeedsRender(DateTime now) => false;

    public override void Render(WidgetRenderContext context)
    {
        var content = context.ContentRect;
        string? label = string.IsNullOrEmpty(Config.Label) ? null : Config.Label;

        string? source = string.IsNullOrWhiteSpace(Config.Icon) ? Config.Target : Config.Icon;
        SKImage? icon = string.IsNullOrWhiteSpace(source)
            ? null
            : Icons.GetIcon(source, (int)Math.Ceiling(Math.Min(content.Width, content.Height))).Image;

        // The placeholder doubles as the loading state, so the card is never empty while the icon is on its way.
        ImageTile.Draw(context.Canvas, context.Theme, content, icon, label, label ?? _target.DisplayName);
    }

    public bool CanTap(SKPoint point) => _target.IsValid;

    public void OnTap(SKPoint point)
    {
        if (_target.IsValid)
            Launcher.Launch(_target.Text, Config.Arguments);
    }
}
