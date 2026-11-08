// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SkiaSharp;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Themes;

namespace UrDeck.Host.Editor;

/// <summary>Colours and swatches of a theme, as the editor draws them before (or instead of) a picture.</summary>
internal static class ThemeVisuals
{
    /// <summary>A theme file's colour (<c>#RRGGBB</c> or <c>#AARRGGBB</c>); grey when it cannot be read.</summary>
    public static Windows.UI.Color Color(string? text)
    {
        var c = text != null && SKColor.TryParse(text, out var parsed) ? parsed : new SKColor(128, 128, 128);
        return ColorHelper.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
    }

    public static SolidColorBrush Brush(string? text) => new(Color(text));

    /// <summary>
    /// The theme's four colours as a small picture of a page: the background, a card in the card fill, an accent bar and a line of
    /// text in the text colour.
    /// </summary>
    public static Grid Swatch(ThemeSwatch swatch, double width, double height)
    {
        var grid = new Grid { Width = width, Height = height, Background = Brush(swatch.Background) };
        var card = new Border
        {
            Background = Brush(swatch.CardFill),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(width * 0.12, height * 0.16, width * 0.12, height * 0.16),
        };
        var inside = new StackPanel { Spacing = height * 0.07, Margin = new Thickness(width * 0.07, height * 0.12, width * 0.07, 0) };
        inside.Children.Add(new Rectangle { Height = height * 0.07, Width = width * 0.3, RadiusX = 2, RadiusY = 2, Fill = Brush(swatch.Accent), HorizontalAlignment = HorizontalAlignment.Left });
        inside.Children.Add(new Rectangle { Height = height * 0.05, Width = width * 0.5, RadiusX = 2, RadiusY = 2, Fill = Brush(swatch.Text), Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Left });
        inside.Children.Add(new Rectangle { Height = height * 0.05, Width = width * 0.35, RadiusX = 2, RadiusY = 2, Fill = Brush(swatch.Text), Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Left });
        card.Child = inside;
        grid.Children.Add(card);
        return grid;
    }

    /// <summary>A dot in the theme's accent, for a list of themes.</summary>
    public static FontIcon Dot(ThemeSwatch swatch) => new() { Glyph = "●", Foreground = Brush(swatch.Accent), FontSize = 14 };
}

/// <summary>
/// The Theme drop-down in the Pages header: the installed themes by name with their accent, the current one checked, and "All themes
/// and backgrounds…". Choosing one writes <c>theme</c> (one undo step); the preview and the deck follow the document.
/// </summary>
internal sealed class ThemeQuickSwitch
{
    private readonly ISettingsHost _host;
    private readonly BackgroundEditing _editing;
    private readonly Action _showAll;
    private readonly MenuFlyout _flyout = new();
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };

    public ThemeQuickSwitch(ISettingsHost host, BackgroundEditing editing, Action showAll)
    {
        _host = host;
        _editing = editing;
        _showAll = showAll;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new TextBlock { Text = "Theme", Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(_label);
        Button = new DropDownButton { Content = content, Flyout = _flyout };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(Button, "Theme");
        _flyout.Opening += OnOpening;
        Refresh();
    }

    public DropDownButton Button { get; }

    /// <summary>Shows the current theme's name and whether it can be changed.</summary>
    public void Refresh()
    {
        string name = _host.Document.Typed.Theme;
        var current = ThemeCatalog.List(_host.Themes).FirstOrDefault(t => t.Name == name);
        _label.Text = current?.DisplayName ?? name;
        Button.IsEnabled = !_host.Document.IsReadOnly;
    }

    public void Close() => _flyout.Opening -= OnOpening;

    private void OnOpening(object? sender, object e)
    {
        _flyout.Items.Clear();
        string name = _host.Document.Typed.Theme;
        foreach (var theme in ThemeCatalog.List(_host.Themes))
        {
            var item = new ToggleMenuFlyoutItem { Text = theme.DisplayName, IsChecked = theme.Name == name, Icon = ThemeVisuals.Dot(theme.Swatch) };
            string chosen = theme.Name;
            item.Click += (_, _) =>
            {
                _editing.SetTheme(chosen);
                Refresh();
            };
            _flyout.Items.Add(item);
        }

        _flyout.Items.Add(new MenuFlyoutSeparator());
        var all = new MenuFlyoutItem { Text = "All themes and backgrounds…" };
        all.Click += (_, _) => _showAll();
        _flyout.Items.Add(all);
    }
}
