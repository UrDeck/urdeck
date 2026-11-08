// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Settings;

namespace UrDeck.Host.Editor.Forms;

/// <summary>Keeps one reading counted as watched for as long as a form shows its value; follows the id as the setting changes.</summary>
internal sealed class ReadingWatcher(IEditorReadings readings, Action changed) : IDisposable
{
    private string? _id;
    private IDisposable? _subscription;

    public void Set(string? id)
    {
        id = string.IsNullOrEmpty(id) ? null : id;
        if (id == _id)
            return;
        _subscription?.Dispose();
        _id = id;
        _subscription = id == null ? null : readings.Watch(id, changed);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}

// The pickers that depend on readings and providers (editor-forms D2, D6): a reading, its action, its unit, and a place.
internal sealed partial class FormBuilder
{
    private static UIElement ReadingControl(RowScope scope)
    {
        var name = new TextBlock { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var id = new TextBlock { FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11, Opacity = 0.7, TextTrimming = TextTrimming.CharacterEllipsis };
        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var text = new StackPanel { Spacing = 0 };
        text.Children.Add(name);
        text.Children.Add(id);
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(text);
        Grid.SetColumn(value, 1);
        content.Children.Add(value);
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        var readings = scope.Context.Readings;
        void Show()
        {
            string current = scope.Current() as string ?? "";
            if (current.Length == 0)
            {
                name.Text = "Choose a reading…";
                id.Text = "";
                value.Text = "";
                id.Visibility = Visibility.Collapsed;
                return;
            }

            name.Text = readings.Describe(current)?.Label ?? current;
            id.Text = current;
            id.Visibility = Visibility.Visible;
            value.Text = readings.ValueText(current);
        }

        var watcher = new ReadingWatcher(readings, Show);
        scope.View.Own(watcher);
        button.Click += async (_, _) =>
        {
            string? chosen = await scope.Context.Chooser.ChooseAsync(scope.Setting.Reading, scope.Current() as string);
            if (chosen != null)
                scope.Write(chosen);
        };
        scope.View.Add(() =>
        {
            watcher.Set(scope.Current() as string);
            Show();
        });
        return button;
    }

    private static UIElement ActionControl(RowScope scope)
    {
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var paths = new List<string>();
        string builtFor = "\u0000";
        string OwnerId() => scope.TextOf(scope.Setting.OwnerReading);
        scope.ExtraEnabled = () => OwnerId().Length > 0;

        combo.SelectionChanged += (_, _) =>
        {
            if (!scope.Updating && combo.SelectedIndex >= 0 && combo.SelectedIndex < paths.Count)
                scope.Write(paths[combo.SelectedIndex]);
        };
        scope.View.Add(() => scope.Fill(() =>
        {
            string owner = OwnerId();
            string stored = scope.Current() as string ?? "";
            string key = owner + "|" + stored;
            if (key != builtFor || combo.Items.Count == 0)
            {
                builtFor = key;
                combo.Items.Clear();
                paths.Clear();
                var readings = scope.Context.Readings;
                var described = owner.Length == 0 ? null : readings.Describe(owner);
                combo.Items.Add(described?.Action != null ? "The reading's own action" : "None");
                paths.Add("");
                foreach (var action in owner.Length == 0 ? [] : readings.ActionsFor(owner))
                {
                    // The natural action is the first item already ("The reading's own action"); it is not listed twice.
                    if (action.Path.Equals(described?.Action, StringComparison.OrdinalIgnoreCase))
                        continue;
                    combo.Items.Add(action.Label);
                    paths.Add(action.Path);
                }

                if (stored.Length > 0 && !paths.Contains(stored, StringComparer.OrdinalIgnoreCase))
                {
                    combo.Items.Add(stored);
                    paths.Add(stored);
                }
            }

            int at = paths.FindIndex(p => p.Equals(stored, StringComparison.OrdinalIgnoreCase));
            combo.SelectedIndex = Math.Max(0, at);
        }));
        return combo;
    }

    private static UIElement UnitControl(RowScope scope)
    {
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var values = new List<string>();
        string builtFor = "\u0000";
        // One unit means there is nothing to choose: the row is hidden (a percentage has no Celsius).
        scope.ExtraVisible = () => scope.ReadingOf(scope.Setting.OwnerReading) is { } reading && FormRules.UnitChoices(reading.Kind).Count > 0;

        combo.SelectionChanged += (_, _) =>
        {
            if (!scope.Updating && combo.SelectedIndex >= 0 && combo.SelectedIndex < values.Count)
                scope.Write(values[combo.SelectedIndex]);
        };
        scope.View.Add(() => scope.Fill(() =>
        {
            var reading = scope.ReadingOf(scope.Setting.OwnerReading);
            string stored = scope.Current() as string ?? "";
            string key = (reading?.Kind.ToString() ?? "") + "|" + stored;
            if (key != builtFor)
            {
                builtFor = key;
                combo.Items.Clear();
                values.Clear();
                combo.Items.Add("Automatic (from your region)");
                values.Add("");
                foreach (var choice in reading == null ? [] : FormRules.UnitChoices(reading.Kind))
                {
                    combo.Items.Add(choice.Label);
                    values.Add(choice.Value);
                }

                if (stored.Length > 0 && !values.Contains(stored, StringComparer.OrdinalIgnoreCase))
                {
                    combo.Items.Add(stored);
                    values.Add(stored);
                }
            }

            combo.SelectedIndex = Math.Max(0, values.FindIndex(v => v.Equals(stored, StringComparison.OrdinalIgnoreCase)));
        }));
        return combo;
    }

    private static UIElement PlaceControl(RowScope scope)
    {
        var setting = scope.Setting;
        var spec = setting.Place!;
        var box = new AutoSuggestBox { PlaceholderText = "Search for a place", UpdateTextOnSelect = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var resolved = new TextBlock { Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(box);
        panel.Children.Add(resolved);

        CancellationTokenSource? query = null;
        scope.View.Own(new Disposer(() =>
        {
            query?.Cancel();
            query?.Dispose();
        }));

        box.TextChanged += async (sender, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || scope.Updating)
                return;
            query?.Cancel();
            query?.Dispose();
            var mine = query = new CancellationTokenSource();
            string typed = sender.Text.Trim();
            if (typed.Length < 2)
            {
                sender.ItemsSource = null;
                return;
            }

            try
            {
                await Task.Delay(300, mine.Token);
                var result = await scope.Context.Readings.SuggestAsync(spec.Source, "place", typed, mine.Token);
                if (mine.IsCancellationRequested)
                    return;
                sender.ItemsSource = result.Suggestions is { } found
                    ? found.Select(SuggestionItem).ToList()
                    : new List<UIElement> { new TextBlock { Text = result.Message ?? "The search failed.", Opacity = 0.7, IsHitTestVisible = false, TextWrapping = TextWrapping.Wrap } };
            }
            catch (OperationCanceledException)
            {
                // Typed on: the next query replaces this one.
            }
        };
        box.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is not FrameworkElement { Tag: Suggestion chosen })
                return;
            // The name and the coordinates change together, so one undo step restores the place that was.
            scope.Target.Apply($"Change {setting.Label}", chosen.Values.Select(v => new KeyValuePair<string, System.Text.Json.Nodes.JsonNode?>(v.Key, v.Value)).ToList(), mergeKey: null);
            scope.Fill(() => box.Text = chosen.Label);
            box.ItemsSource = null;
        };
        scope.View.Add(() =>
        {
            string name = scope.Current() as string ?? "";
            if (!RowScope.HasFocus(box))
                scope.Fill(() => box.Text = name);
            resolved.Text = Coordinates(scope, spec);
            resolved.Visibility = resolved.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        });
        return panel;
    }

    private static StackPanel SuggestionItem(Suggestion suggestion)
    {
        var item = new StackPanel { Tag = suggestion };
        item.Children.Add(new TextBlock { Text = suggestion.Label });
        if (!string.IsNullOrEmpty(suggestion.Detail))
            item.Children.Add(new TextBlock { Text = suggestion.Detail, FontSize = 12, Opacity = 0.7 });
        return item;
    }

    /// <summary>"45.52° N, 122.68° W" from the stored latitude and longitude; empty when either is missing.</summary>
    internal static string Coordinates(RowScope scope, PlaceSpec spec)
    {
        double? Number(string? key) =>
            key != null && scope.Target.Get(key) is JsonValue node && node.TryGetValue<double>(out double value) ? value : null;

        if (Number(spec.LatitudeSetting) is not { } lat || Number(spec.LongitudeSetting) is not { } lon)
            return "";
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Math.Abs(lat):0.00}° {(lat >= 0 ? "N" : "S")}, {Math.Abs(lon):0.00}° {(lon >= 0 ? "E" : "W")}");
    }
}

/// <summary>Runs an action when disposed.</summary>
internal sealed class Disposer(Action dispose) : IDisposable
{
    public void Dispose() => dispose();
}
