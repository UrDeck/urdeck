// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Icons;
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
    /// <summary>The <c>icon</c> setting's prefix for a glyph of the theme's icon set.</summary>
    private const string ThemePrefix = "theme:";

    private LaunchTarget _target;
    private bool? _folder;

    protected override void OnConfigured()
    {
        _target = LaunchTarget.Parse(Config.Target);
        _folder = null;
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

        // A theme glyph is asked of the theme, never of the icon service. Otherwise the service answers; when it has given up
        // the target's kind picks a glyph, and while it is still loading the placeholder stays, so a card never shows a glyph
        // and then the real icon.
        string? glyph = ThemeGlyph(Config.Icon);
        SKImage? icon = null;
        if (glyph == null)
        {
            string? source = string.IsNullOrWhiteSpace(Config.Icon) ? Config.Target : Config.Icon;
            if (!string.IsNullOrWhiteSpace(source))
            {
                IconResult result = Icons.GetIcon(source, (int)Math.Ceiling(Math.Min(content.Width, content.Height)));
                icon = result.Image;
                if (icon == null && !result.IsLoading)
                    glyph = FallbackGlyph();
            }
        }

        // The placeholder doubles as the loading state, so the card is never empty while the icon is on its way.
        ImageTile.Draw(context.Canvas, context.Theme, content, icon, label, label ?? _target.DisplayName, glyph);
    }

    /// <summary>The key after <c>theme:</c>, or null when the setting is not a theme glyph. An empty key is a glyph nothing answers.</summary>
    private static string? ThemeGlyph(string? icon)
    {
        string? text = icon?.Trim();
        return text != null && text.StartsWith(ThemePrefix, StringComparison.OrdinalIgnoreCase) ? text[ThemePrefix.Length..].Trim() : null;
    }

    /// <summary>The glyph for a target whose icon cannot be had: a link, a folder, or a file; null for a card with no valid target.</summary>
    private string? FallbackGlyph() => _target.Kind switch
    {
        LaunchTargetKind.Web or LaunchTargetKind.Uri => "app/link",
        LaunchTargetKind.File => IsFolder() ? "app/folder" : "app/file",
        LaunchTargetKind.Shell => "app/file",
        _ => null,
    };

    /// <summary>A file target is a folder when it ends in a separator or names a directory; asked once, and only once the icon service has given up.</summary>
    private bool IsFolder() => _folder ??= _target.Text.EndsWith('\\') || _target.Text.EndsWith('/') || Directory.Exists(_target.Text);

    public bool CanTap(SKPoint point) => _target.IsValid;

    public void OnTap(SKPoint point)
    {
        if (_target.IsValid)
            Launcher.Launch(_target.Text, Config.Arguments);
    }
}
