// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace UrDeck.Host.Editor;

/// <summary>What the user chose on the first run's welcome.</summary>
internal enum WelcomeChoice
{
    /// <summary>The sample page stays (also what dismissing the dialog means).</summary>
    Sample,

    /// <summary>One empty page and an empty dock.</summary>
    Empty,
}

/// <summary>
/// The first run's welcome (editor-states, "Welcome On First Run"): the deck already shows the sample page on its display;
/// the user keeps it or starts with an empty page. Escape keeps the sample.
/// </summary>
internal static class WelcomeDialog
{
    public static async Task<WelcomeChoice> ShowAsync(XamlRoot root, ISettingsHost host, DispatcherQueue queue)
    {
        var choice = WelcomeChoice.Sample;
        ContentDialog? dialog = null;
        using var pictures = new ThemeThumbnails(host, queue);

        var body = new StackPanel { Spacing = 16, MaxWidth = 620 };
        body.Children.Add(new TextBlock { Text = Greeting(host.DeckState), TextWrapping = TextWrapping.Wrap });

        var sampleImage = new Image { Stretch = Stretch.Uniform, Height = 220 };
        var samplePicture = new Border { Child = sampleImage, Height = 220, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 18, 18, 20)) };
        string theme = host.Document.Typed.Theme;
        void Show(string file) => sampleImage.Source = new BitmapImage(new Uri(file));
        if (pictures.Request(theme, host.DeckPageId) is { } cached)
            Show(cached);
        pictures.Ready += (name, file) =>
        {
            if (name == theme)
                Show(file);
        };

        var emptyPicture = new Border
        {
            Height = 220,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.5),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 18, 18, 20)),
            Child = new TextBlock { Text = "+", FontSize = 48, Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var sample = Option("Start with the sample page", samplePicture, WelcomeChoice.Sample);
        var empty = Option("Start with an empty page", emptyPicture, WelcomeChoice.Empty);
        Grid.SetColumn(empty, 1);
        row.Children.Add(sample);
        row.Children.Add(empty);
        body.Children.Add(row);

        var file = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        file.Children.Add(new TextBlock { Text = "Prefer the file?", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
        var link = new HyperlinkButton { Content = Path.GetFileName(host.ConfigPath), Padding = new Thickness(2) };
        link.Click += (_, _) => GeneralSection.Start("explorer.exe", $"/select,\"{host.ConfigPath}\"");
        file.Children.Add(link);
        body.Children.Add(file);

        dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Welcome to UrDeck",
            Content = body,
            CloseButtonText = "Keep the sample page",
            DefaultButton = ContentDialogButton.None,
        };
        await dialog.ShowAsync();
        return choice;

        Button Option(string text, UIElement picture, WelcomeChoice value)
        {
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(picture);
            content.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var button = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10),
            };
            AutomationProperties.SetName(button, text);
            button.Click += (_, _) =>
            {
                choice = value;
                dialog?.Hide();
            };
            return button;
        }
    }

    internal static string Greeting(DeckState state)
    {
        const string pick = "Pick a start; you can change everything afterwards.";
        if (state.Shown && state.DisplayName != null)
            return $"Your deck is showing on {state.DisplayName}. {pick}";
        return state.DisplayName != null
            ? $"Your deck will appear on {state.DisplayName} when it is connected. {pick}"
            : $"Your deck will appear on its display when it is connected. {pick}";
    }
}
