// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Settings;

namespace UrDeck.Host.Editor.Forms;

// The simple kinds (editor-forms D2): toggle, choice, text, number, range and the kind the editor does not know.
internal sealed partial class FormBuilder
{
    private static UIElement ToggleControl(RowScope scope)
    {
        var toggle = new ToggleSwitch { OnContent = "On", OffContent = "Off", MinWidth = 0 };
        toggle.Toggled += (_, _) => scope.Write(toggle.IsOn);
        scope.View.Add(() => scope.Fill(() => toggle.IsOn = scope.Current() as bool? ?? scope.Setting.Default as bool? ?? false));
        return toggle;
    }

    private static UIElement ChoiceControl(RowScope scope)
    {
        var options = scope.Setting.Options;
        // The option that stands for "absent", else the default, is selected while the key is missing.
        int Index(string? text)
        {
            text ??= "";
            int exact = IndexOf(options, text);
            if (exact >= 0 || text.Length > 0)
                return exact;
            return IndexOf(options, DefaultText(scope.Setting));
        }

        if (options.Count <= 3)
        {
            var radios = new RadioButtons { MaxColumns = Math.Max(1, options.Count) };
            foreach (var option in options)
                radios.Items.Add(option.Label);
            radios.SelectionChanged += (_, _) =>
            {
                if (!scope.Updating && radios.SelectedIndex >= 0)
                    scope.Write(options[radios.SelectedIndex].Value);
            };
            scope.View.Add(() => scope.Fill(() => radios.SelectedIndex = Index(scope.Current() as string)));
            return radios;
        }

        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var option in options)
            combo.Items.Add(option.Label);
        combo.SelectionChanged += (_, _) =>
        {
            if (!scope.Updating && combo.SelectedIndex >= 0 && combo.SelectedIndex < options.Count)
                scope.Write(options[combo.SelectedIndex].Value);
        };
        scope.View.Add(() => scope.Fill(() =>
        {
            // A value the declaration does not list (a newer file) shows as its own text, so it is not lost to a click.
            while (combo.Items.Count > options.Count)
                combo.Items.RemoveAt(combo.Items.Count - 1);
            string? stored = scope.Current() as string;
            int at = Index(stored);
            if (at < 0 && !string.IsNullOrEmpty(stored))
            {
                combo.Items.Add(stored);
                at = options.Count;
            }

            combo.SelectedIndex = at;
        }));
        return combo;
    }

    private static int IndexOf(IReadOnlyList<SettingOption> options, string value)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, value, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>The words an empty text box shows: the declared fallback, or what the widget takes from the chosen reading or the region.</summary>
    private static string PlaceholderOf(RowScope scope)
    {
        var setting = scope.Setting;
        string? readingKey = setting.Fallback.ReadingSetting;
        var reading = scope.ReadingOf(readingKey);
        return FormRules.Placeholder(setting, reading, scope.DisplayUnitFor(readingKey), scope.Context.Readings.RegionUsesFahrenheit);
    }

    private static UIElement TextControl(RowScope scope)
    {
        var box = new TextBox();
        box.TextChanged += (_, _) =>
        {
            if (!scope.Updating)
                scope.Write(box.Text, merge: true);
        };
        scope.View.Add(() =>
        {
            box.PlaceholderText = PlaceholderOf(scope);
            if (!RowScope.HasFocus(box))
                scope.Fill(() => box.Text = scope.Current() as string ?? "");
        });
        return box;
    }

    /// <summary>A text whose empty value differs from its absent one: a switch to show it, then the box (three states).</summary>
    private static UIElement OptionalTextControl(RowScope scope)
    {
        var setting = scope.Setting;
        var panel = new StackPanel { Spacing = 8 };
        string noun = setting.Label.ToLowerInvariant();
        var show = new ToggleSwitch { OnContent = $"Show a {noun}", OffContent = $"Show a {noun}", MinWidth = 0 };
        var box = new TextBox();
        panel.Children.Add(show);
        panel.Children.Add(box);

        show.Toggled += (_, _) =>
        {
            if (scope.Updating)
                return;
            // Off: an empty text, "none". On: the key goes (the widget's fallback), or the text typed so far.
            scope.Write(show.IsOn ? (box.Text.Length > 0 ? box.Text : null) : "");
            box.Visibility = show.IsOn ? Visibility.Visible : Visibility.Collapsed;
        };
        box.TextChanged += (_, _) =>
        {
            if (!scope.Updating && show.IsOn)
                scope.Write(box.Text.Length > 0 ? box.Text : null, merge: true);
        };
        scope.View.Add(() =>
        {
            box.PlaceholderText = PlaceholderOf(scope);
            string? stored = scope.Current() as string;
            scope.Fill(() =>
            {
                show.IsOn = stored != "";
                box.Visibility = show.IsOn ? Visibility.Visible : Visibility.Collapsed;
                if (!RowScope.HasFocus(box))
                    box.Text = stored ?? "";
            });
        });
        return panel;
    }

    private static UIElement NumberControl(RowScope scope)
    {
        var setting = scope.Setting;
        var spec = setting.Number!;
        var box = new NumberBox
        {
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            SmallChange = spec.Step > 0 ? spec.Step : 1,
            LargeChange = spec.Step > 0 ? spec.Step * 10 : 10,
            Value = double.NaN,
            MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var unit = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(box);
        Grid.SetColumn(unit, 1);
        line.Children.Add(unit);

        string? readingKey = spec.UnitFromReading;
        // Stepping from nothing starts at the default, else the reading's own value for this setting, else the lowest allowed value.
        double StepBase()
        {
            if (!double.IsNaN(box.Value))
                return box.Value;
            double? start = setting.Default is IConvertible c && setting.Default is not string ? Convert.ToDouble(c, CultureInfo.InvariantCulture) : null;
            if (start == null && readingKey != null && scope.ReadingOf(readingKey) is { } reading)
            {
                double? level = setting.Key switch { "min" => reading.Min, "max" => reading.Max, "warning" => reading.Warning, "critical" => reading.Critical, _ => null };
                if (level is { } canonical)
                    start = FormRules.ToForm(canonical, reading, scope.DisplayUnitFor(readingKey), spec.Step);
            }

            return start ?? spec.Min ?? 0;
        }

        void Step(int direction)
        {
            double step = spec.Step > 0 ? spec.Step : 1;
            double next = StepBase() + direction * step;
            box.Value = Math.Round(next, FormRules.Decimals(step));
        }

        // No spinner (it left no room to type); the arrow keys step, and from an empty box too.
        box.KeyDown += (_, args) =>
        {
            if (args.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down && double.IsNaN(box.Value))
            {
                Step(args.Key == Windows.System.VirtualKey.Up ? 1 : -1);
                args.Handled = true;
            }
        };
        box.ValueChanged += (_, args) =>
        {
            // The suffix follows a value; an empty box shows the fallback, which already carries the unit.
            unit.Visibility = unit.Text.Length == 0 || double.IsNaN(args.NewValue) ? Visibility.Collapsed : Visibility.Visible;
            if (scope.Updating)
                return;
            double typed = args.NewValue;
            if (double.IsNaN(typed))
            {
                scope.Error(null);
                scope.Write(null, merge: true);
                return;
            }

            // The limits are the declaration's (canonical); a number with a unit from a reading is converted first.
            double canonical = readingKey == null ? typed : FormRules.FromForm(typed, scope.ReadingOf(readingKey), scope.DisplayUnitFor(readingKey));
            if (readingKey == null && SettingValues.Invalid(canonical, setting) is { } problem)
            {
                scope.Error(problem);
                return;
            }

            scope.Error(null);
            scope.Write(canonical, merge: true);
        };
        scope.View.Add(() =>
        {
            var reading = scope.ReadingOf(readingKey);
            var display = scope.DisplayUnitFor(readingKey);
            box.PlaceholderText = PlaceholderOf(scope);
            unit.Text = readingKey != null ? FormRules.UnitSuffix(reading, display) : spec.Unit ?? "";
            unit.Visibility = unit.Text.Length == 0 || double.IsNaN(box.Value) ? Visibility.Collapsed : Visibility.Visible;
            if (RowScope.HasFocus(box))
                return;
            scope.Fill(() =>
            {
                double? stored = scope.Current() as double?;
                box.Value = stored is not { } value ? double.NaN : readingKey != null ? FormRules.ToForm(value, reading, display, spec.Step) : value;
                scope.Error(null);
            });
        });
        return line;
    }

    private static UIElement RangeControl(RowScope scope)
    {
        var setting = scope.Setting;
        var spec = setting.Number!;
        double min = spec.Min ?? 0, max = spec.Max ?? 100;
        var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = spec.Step > 0 ? spec.Step : 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 56, TextAlignment = TextAlignment.Right };
        var line = new Grid { ColumnSpacing = 12 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(slider);
        Grid.SetColumn(value, 1);
        line.Children.Add(value);

        void Show() => value.Text = string.IsNullOrEmpty(spec.Unit)
            ? slider.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : $"{slider.Value.ToString("0.####", CultureInfo.InvariantCulture)} {spec.Unit}";
        slider.ValueChanged += (_, _) =>
        {
            Show();
            scope.Write(slider.Value, merge: true);
        };
        scope.View.Add(() =>
        {
            if (!RowScope.HasFocus(slider))
                scope.Fill(() => slider.Value = scope.Current() as double? ?? setting.Default as double? ?? (setting.Default is IConvertible c ? Convert.ToDouble(c, CultureInfo.InvariantCulture) : min));
            Show();
        });
        return line;
    }

    /// <summary>A kind the editor does not know: edited as text, written as the string the plugin gets.</summary>
    private static UIElement UnknownControl(RowScope scope)
    {
        var box = new TextBox();
        box.TextChanged += (_, _) =>
        {
            if (!scope.Updating)
                scope.Target.Set($"Change {scope.Setting.Label}", scope.Setting.Key, box.Text.Length == 0 ? null : JsonValue.Create(box.Text), merge: true);
        };
        scope.View.Add(() =>
        {
            if (!RowScope.HasFocus(box))
                scope.Fill(() => box.Text = scope.Current() as string ?? "");
        });
        return box;
    }
}
