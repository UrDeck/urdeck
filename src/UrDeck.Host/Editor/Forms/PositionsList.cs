// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Config;
using UrDeck.Engine.Settings;

namespace UrDeck.Host.Editor.Forms;

// A list of positions (editor-forms D9): one expander per position of the widget's current size.
internal sealed partial class FormBuilder
{
    private UIElement PositionsControl(RowScope scope, Func<SettingDescriptor, UIElement>? secretRow)
    {
        var setting = scope.Setting;
        var panel = new StackPanel { Spacing = 8 };
        if (scope.Target is not LiveTarget live || setting.Item == null || setting.Positions == null)
        {
            panel.Children.Add(Sections.Caption("This list is edited in the configuration file."));
            return panel;
        }

        string actualKey = setting.Key;
        string listPath() => live.Path + "/" + JsonPointer.Escape(actualKey);
        var roles = context.SizeKey == null ? null : setting.Positions.RolesAt(context.SizeKey);
        if (roles == null)
        {
            panel.Children.Add(Sections.Caption("This widget has no positions at this size."));
            return panel;
        }

        // The item forms and the headers of the positions currently built, rebuilt when the number of items changes.
        var children = new List<FormView>();
        var headers = new List<Action>();
        var watchers = new List<ReadingWatcher>();
        var expanders = new List<Expander>();
        int builtFor = -2;
        int openIndex = 0;
        var note = Sections.Caption("");

        int Count() => live.Get(actualKey) is JsonArray array ? array.Count : 0;

        void Rebuild(int count)
        {
            foreach (var child in children)
                scope.View.RemoveChild(child);
            foreach (var watcher in watchers)
                watcher.Dispose();
            children.Clear();
            headers.Clear();
            watchers.Clear();
            expanders.Clear();
            panel.Children.Clear();

            for (int i = 0; i < roles.Count; i++)
                panel.Children.Add(Position(i, count));
            panel.Children.Add(note);
        }

        FrameworkElement Position(int index, int count)
        {
            string role = roles[index];
            bool filled = index < count;
            var expander = new Expander
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = index == openIndex,
            };
            expanders.Add(expander);
            expander.Expanding += (_, _) =>
            {
                openIndex = index;
                foreach (var other in expanders.Where(e => !ReferenceEquals(e, expander)))
                    other.IsExpanded = false;
            };

            var badge = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources[string.Equals(role, roles[0], StringComparison.OrdinalIgnoreCase) ? "AccentFillColorDefaultBrush" : "ControlFillColorSecondaryBrush"],
                Child = new TextBlock
                {
                    Text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                },
            };
            string roleName = roles.Count == 1 ? "Reading" : char.ToUpperInvariant(role[0]) + role[1..] + " position";
            var summary = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
            var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            var titles = new StackPanel { Spacing = 0 };
            titles.Children.Add(new TextBlock { Text = roleName, FontWeight = FontWeights.SemiBold });
            titles.Children.Add(summary);
            var header = new Grid { ColumnSpacing = 10 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(badge);
            Grid.SetColumn(titles, 1);
            header.Children.Add(titles);
            Grid.SetColumn(value, 2);
            header.Children.Add(value);

            var content = new StackPanel { Spacing = 12 };
            if (filled)
            {
                var more = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(6), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(more, $"Position {index + 1}: more");
                var menu = new MenuFlyout();
                var up = new MenuFlyoutItem { Text = "Move up", IsEnabled = index > 0 };
                up.Click += (_, _) => MoveItem(index, index - 1);
                var down = new MenuFlyoutItem { Text = "Move down", IsEnabled = index < count - 1 };
                down.Click += (_, _) => MoveItem(index, index + 1);
                menu.Items.Add(up);
                menu.Items.Add(down);
                more.Flyout = menu;
                Grid.SetColumn(more, 3);
                header.Children.Add(more);

                var itemView = new FormView();
                Populate(itemView, itemView.Root, setting.Item, new LiveTarget(context.Document, $"{listPath()}/{index}"), role, secretRow);
                scope.View.AddChild(itemView);
                children.Add(itemView);
                content.Children.Add(itemView.Root);
                itemView.RefreshAll();
            }
            else
            {
                var fill = new Button { Content = "Fill this position" };
                fill.Click += (_, _) => Fill(index);
                content.Children.Add(fill);
            }

            ReadingWatcher watcher = null!;
            watcher = new ReadingWatcher(context.Readings, () => Summary());
            watchers.Add(watcher);

            void Summary()
            {
                if (!filled || live.Get(actualKey) is not JsonArray items || index >= items.Count || items[index] is not JsonObject item)
                {
                    summary.Text = "Empty";
                    value.Text = "";
                    return;
                }

                string readingId = ReadingIdOf(setting.Item, item);
                watcher.Set(readingId);
                string label = TextOfItem(item, "label");
                var described = readingId.Length == 0 ? null : context.Readings.Describe(readingId);
                summary.Text = label.Length > 0 ? label : described?.Label ?? (readingId.Length > 0 ? readingId : "Empty");
                value.Text = readingId.Length == 0 ? "" : context.Readings.ValueText(readingId);
            }

            headers.Add(Summary);
            Summary();
            expander.Header = header;
            expander.Content = content;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(expander, $"{roleName} {index + 1}");
            return expander;
        }

        void MoveItem(int from, int to) =>
            context.Document.Edit($"Move {setting.Label.ToLowerInvariant()} {(to < from ? "up" : "down")}", e => e.Move(listPath(), from, to));

        void Fill(int index) =>
            context.Document.Edit($"Fill position {index + 1}", e =>
            {
                string path = listPath();
                if (e.Get(path) is not JsonArray)
                    e.Set(path, new JsonArray());
                int have = e.Get(path) is JsonArray array ? array.Count : 0;
                for (int k = have; k <= index; k++)
                    e.Insert(path, k, new JsonObject());
            });

        scope.View.Add(() =>
        {
            int count = Count();
            if (count != builtFor)
            {
                builtFor = count;
                Rebuild(count);
            }

            foreach (var refresh in headers)
                refresh();
            int hidden = count - roles.Count;
            note.Text = hidden > 0 ? $"{hidden} more {(hidden == 1 ? "slot stays" : "slots stay")} in the file and {(hidden == 1 ? "comes" : "come")} back at a larger size." : "";
            note.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        });
        scope.View.Own(new Disposer(() =>
        {
            foreach (var watcher in watchers)
                watcher.Dispose();
        }));
        return panel;
    }

    /// <summary>The reading id of a list item: the text of its first reading setting.</summary>
    private static string ReadingIdOf(SettingSchema item, JsonObject values)
    {
        var reading = item.Settings.FirstOrDefault(s => s.Kind == SettingKinds.Reading);
        return reading == null ? "" : TextOfItem(values, reading.Key);
    }

    private static string TextOfItem(JsonObject item, string key)
    {
        var node = item.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        return node is JsonValue value && value.TryGetValue<string>(out string? text) ? text ?? "" : "";
    }
}
