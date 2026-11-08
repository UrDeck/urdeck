// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Launch;
using UrDeck.Sdk.Settings;

namespace UrDeck.Host.Editor.Forms;

// The launch target and the icon (editor-forms D7, D8).
internal sealed partial class FormBuilder
{
    private static readonly string[] ProgramExtensions = [".exe", ".lnk", ".bat", ".cmd", ".com", ".msc"];

    private UIElement LaunchTargetControl(RowScope scope)
    {
        var allowed = scope.Setting.Launch ?? LaunchKinds.All;
        var box = new TextBox { PlaceholderText = "A path, a web address or an application", HorizontalAlignment = HorizontalAlignment.Stretch };
        var browse = new DropDownButton { Content = "Browse…" };
        var menu = new MenuFlyout();
        if (allowed.HasFlag(LaunchKinds.App))
            menu.Items.Add(MenuItem("App…", async () =>
            {
                if (await AppPicker.ChooseAsync(box.XamlRoot) is { } app)
                    Choose(app.Target, app.Name);
            }));
        if (allowed.HasFlag(LaunchKinds.File))
            menu.Items.Add(MenuItem("File…", async () => Choose(await PickFileAsync())));
        if (allowed.HasFlag(LaunchKinds.Folder))
            menu.Items.Add(MenuItem("Folder…", async () => Choose(await PickFolderAsync())));
        browse.Flyout = menu;
        browse.Visibility = menu.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(box);
        Grid.SetColumn(browse, 1);
        line.Children.Add(browse);
        var kind = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(line);
        panel.Children.Add(kind);

        void Describe()
        {
            var (text, warning) = Explain(box.Text, allowed);
            kind.Text = text;
            kind.Opacity = warning ? 1 : 0.7;
            kind.Foreground = warning
                ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
                : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            kind.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        void Choose(string? path, string? name = null)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var previous = LaunchTarget.Parse(scope.Current() as string);
            box.Text = path;
            // A card labelled with the old target's name (or the new card's default) is relabelled with the application chosen.
            var label = name == null ? null : scope.Schema.Settings.FirstOrDefault(s => s.Kind == SettingKinds.Text && s.Key == "label");
            string current = label == null ? "" : SettingValues.Read(scope.Target.Get(label.Key), label) as string ?? "";
            if (label != null && (current.Length == 0 || current == "UrDeck" || current == previous.DisplayName))
            {
                scope.Target.Apply("Change " + scope.Setting.Label, [new(scope.Setting.Key, JsonValue.Create(path)), new(label.Key, JsonValue.Create(name))], null);
                return;
            }

            scope.Write(path);
        }

        box.TextChanged += (_, _) =>
        {
            Describe();
            if (!scope.Updating)
                scope.Write(box.Text, merge: true);
        };
        scope.View.Add(() =>
        {
            if (!RowScope.HasFocus(box))
                scope.Fill(() => box.Text = scope.Current() as string ?? "");
            Describe();
        });
        return panel;
    }

    private static MenuFlyoutItem MenuItem(string text, Func<Task> click)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += async (_, _) => await click();
        return item;
    }

    private async Task<string?> PickFileAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(context.Owner));
        return (await picker.PickSingleFileAsync())?.Path;
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(context.Owner));
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    /// <summary>What a target is, in a sentence, and whether it deserves a warning (a path that does not exist).</summary>
    internal static (string Text, bool Warning) Explain(string? target, LaunchKinds allowed)
    {
        var parsed = LaunchTarget.Parse(target);
        if (string.IsNullOrWhiteSpace(target))
            return ("", false);
        switch (parsed.Kind)
        {
            case LaunchTargetKind.Web:
                return ("A web address. Opens in your default browser.", false);
            case LaunchTargetKind.Shell:
                return ("An application.", false);
            case LaunchTargetKind.Uri:
                return ("An address. Opens in the application registered for it.", false);
            case LaunchTargetKind.File:
                bool rooted = parsed.Text.Contains('\\') || parsed.Text.Contains('/');
                if (!rooted)
                    return ("A name Windows finds itself.", false);
                if (Directory.Exists(parsed.Text))
                    return ("A folder. Opens in File Explorer.", false);
                if (File.Exists(parsed.Text))
                    return (ProgramExtensions.Contains(Path.GetExtension(parsed.Text), StringComparer.OrdinalIgnoreCase) ? "An application." : "A file. Opens with its app.", false);
                return ("Not found.", true);
            default:
                return ("Not a target: use a full path, a web address or an application name.", true);
        }
    }

    // ---------------- icon ----------------

    private UIElement IconControl(RowScope scope)
    {
        var setting = scope.Setting;
        var shown = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var picture = new Image { Width = 24, Height = 24, Visibility = Visibility.Collapsed };
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        shown.Children.Add(picture);
        shown.Children.Add(name);
        var button = new DropDownButton { Content = shown, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        var flyout = new Flyout();
        button.Flyout = flyout;

        Dictionary<string, IconChoice>? glyphs = null;
        Dictionary<string, IconChoice> Glyphs() => glyphs ??= context.Icons.Glyphs().GroupBy(g => g.Key).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        void Pick(string? value)
        {
            flyout.Hide();
            scope.Write(value ?? "");
        }

        flyout.Opening += (_, _) =>
        {
            var content = new StackPanel { Spacing = 8, Width = 300 };
            var automatic = new Button { Content = "Automatic", HorizontalAlignment = HorizontalAlignment.Stretch };
            automatic.Click += (_, _) => Pick(null);
            content.Children.Add(automatic);
            var grid = new GridView { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None, MaxHeight = 260 };
            foreach (var choice in Glyphs().Values)
            {
                var cell = new Border
                {
                    Width = 44,
                    Height = 44,
                    CornerRadius = new CornerRadius(8),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 24, 24, 28)),
                    Child = new Image { Source = choice.Image, Width = 32, Height = 32 },
                    Tag = choice.Key,
                };
                ToolTipService.SetToolTip(cell, choice.Key);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cell, choice.Key);
                grid.Items.Add(cell);
            }

            grid.ItemClick += (_, args) =>
            {
                if (args.ClickedItem is FrameworkElement { Tag: string key })
                    Pick(key);
            };
            content.Children.Add(grid);
            if (setting.IconAllowsFile)
            {
                var file = new HyperlinkButton { Content = "From a file…" };
                file.Click += async (_, _) =>
                {
                    flyout.Hide();
                    var picker = new Windows.Storage.Pickers.FileOpenPicker();
                    foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".svg", ".gif", ".webp" })
                        picker.FileTypeFilter.Add(extension);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(context.Owner));
                    if ((await picker.PickSingleFileAsync())?.Path is { } path)
                        scope.Write(path);
                };
                content.Children.Add(file);
            }

            flyout.Content = content;
        };

        scope.View.Add(() =>
        {
            string stored = scope.Current() as string ?? "";
            if (stored.Length == 0)
            {
                name.Text = "Automatic";
                picture.Visibility = Visibility.Collapsed;
            }
            else if (!stored.Contains('\\') && !stored.Contains(':') && stored.Contains('/'))
            {
                name.Text = stored;
                picture.Source = null;
                picture.Visibility = Visibility.Collapsed;
                // The glyph is drawn only when the flyout was opened once; a name is enough to read.
                if (glyphs != null && glyphs.TryGetValue(stored, out var choice))
                {
                    picture.Source = choice.Image;
                    picture.Visibility = Visibility.Visible;
                }
            }
            else
            {
                name.Text = Path.GetFileName(stored);
                picture.Visibility = Visibility.Collapsed;
            }
        });
        return button;
    }
}
