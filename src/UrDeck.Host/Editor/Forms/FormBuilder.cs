// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Config;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Data;

namespace UrDeck.Host.Editor.Forms;

/// <summary>
/// A generated form: its controls, and the refresh that reads their values again from the document. The form keeps no copy of
/// the values; every control asks the target. Dispose it when the pane is replaced.
/// </summary>
internal sealed class FormView : IDisposable
{
    private readonly List<Action> _refreshers = [];
    private readonly List<FormView> _children = [];
    private readonly List<IDisposable> _owned = [];
    private readonly ConfigDocument? _document;
    private bool _refreshing;
    private bool _disposed;

    /// <param name="document">The document whose changes refresh the form; null for a child, which its parent refreshes.</param>
    public FormView(ConfigDocument? document = null)
    {
        _document = document;
        if (document != null)
            document.Changed += OnChanged;
    }

    /// <summary>The controls.</summary>
    public StackPanel Root { get; } = new() { Spacing = 12 };

    /// <summary>Runs when the form wants its host to react to a refresh (the pane's key may change).</summary>
    public event Action? Refreshed;

    public void Add(Action refresh) => _refreshers.Add(refresh);

    public void Own(IDisposable disposable) => _owned.Add(disposable);

    public void AddChild(FormView child) => _children.Add(child);

    public void RemoveChild(FormView child)
    {
        if (_children.Remove(child))
            child.Dispose();
    }

    /// <summary>Reads every control's value again.</summary>
    public void RefreshAll()
    {
        if (_refreshing || _disposed)
            return;
        _refreshing = true;
        try
        {
            foreach (var refresh in _refreshers.ToArray())
                refresh();
            foreach (var child in _children.ToArray())
                child.RefreshAll();
        }
        finally
        {
            _refreshing = false;
        }

        Refreshed?.Invoke();
    }

    private void OnChanged(ConfigChange change) => RefreshAll();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_document != null)
            _document.Changed -= OnChanged;
        foreach (var child in _children)
            child.Dispose();
        _children.Clear();
        foreach (var owned in _owned)
            owned.Dispose();
        _owned.Clear();
        _refreshers.Clear();
    }
}

/// <summary>
/// What a control knows about its setting and the object it edits (editor-forms D1). One per row; controls ask it for the
/// current value and write through it, so a value reaches the file exactly as the widget reads it.
/// </summary>
internal sealed class RowScope(FormBuilder builder, FormView view, FormTarget target, SettingSchema schema, SettingDescriptor setting, string? role)
{
    private bool _updating;

    public FormBuilder Builder => builder;

    public FormContext Context => builder.Context;

    public FormView View => view;

    public FormTarget Target => target;

    public SettingSchema Schema => schema;

    public SettingDescriptor Setting => setting;

    public string? Role => role;

    /// <summary>Set by a control that hides its row (a unit with one choice).</summary>
    public Func<bool>? ExtraVisible { get; set; }

    /// <summary>Set by a control that is disabled for a reason of its own (an action without a reading).</summary>
    public Func<bool>? ExtraEnabled { get; set; }

    /// <summary>Shows or clears the line under the control (an invalid number says its range).</summary>
    public Action<string?> Error { get; set; } = _ => { };

    /// <summary>True while the control is being filled from the document, so its change events write nothing.</summary>
    public bool Updating => _updating;

    /// <summary>The stored value as the setting's kind types it; null when absent.</summary>
    public object? Current() => SettingValues.Read(target.Get(setting.Key), setting);

    /// <summary>The text of another setting of the same object (a reading's id); empty when absent.</summary>
    public string TextOf(string? key)
    {
        if (key == null || schema.Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)) is not { } other)
            return "";
        return SettingValues.Read(target.Get(other.Key), other) as string ?? "";
    }

    /// <summary>Fills the control without writing: <paramref name="fill"/> runs with the change events muted.</summary>
    public void Fill(Action fill)
    {
        bool before = _updating;
        _updating = true;
        try
        {
            fill();
        }
        finally
        {
            _updating = before;
        }
    }

    /// <summary>Writes the value as one step; <paramref name="merge"/> makes typing into the field one undo step.</summary>
    public void Write(object? value, bool merge = false)
    {
        if (_updating)
            return;
        target.Set($"Change {setting.Label}", setting.Key, SettingValues.Write(value, setting), merge);
    }

    /// <summary>The descriptor of the reading chosen in the setting named, if the provider has described it.</summary>
    public ReadingDescriptor? ReadingOf(string? settingKey)
    {
        string id = TextOf(settingKey);
        return id.Length == 0 ? null : Context.Readings.Describe(id);
    }

    /// <summary>Whether the keyboard focus is inside the control (it is not overwritten while the user types).</summary>
    public static bool HasFocus(FrameworkElement element)
    {
        if (element.XamlRoot == null)
            return false;
        var focused = FocusManager.GetFocusedElement(element.XamlRoot) as DependencyObject;
        while (focused != null)
        {
            if (ReferenceEquals(focused, element))
                return true;
            focused = VisualTreeHelper.GetParent(focused);
        }

        return false;
    }

    /// <summary>The unit a number is shown in: the item's unit setting, else the reading's, else the region's.</summary>
    public DisplayUnit DisplayUnitFor(string? readingSetting)
    {
        var reading = ReadingOf(readingSetting);
        string? unitKey = schema.Settings.FirstOrDefault(s => s.Kind == SettingKinds.Unit && string.Equals(s.OwnerReading, readingSetting, StringComparison.OrdinalIgnoreCase))?.Key;
        return FormRules.ResolveUnit(unitKey == null ? null : TextOf(unitKey), reading, Context.Readings.RegionUsesFahrenheit);
    }
}

/// <summary>
/// Turns a list of setting descriptors and the object they describe into Fluent controls (editor-forms D1, D2, D3). The live form
/// writes to the configuration document; the staged form (providers) to a detached object the caller saves.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "The controls are chosen through one switch that returns the element; a concrete type per method would change nothing.")]
internal sealed partial class FormBuilder(FormContext context)
{
    private const int FoldAbove = 8;

    public FormContext Context => context;

    /// <summary>The form of the object at <paramref name="objectPath"/> (<c>/pages/0/widgets/3</c>); each change is a document transaction.</summary>
    public FormView Build(SettingSchema schema, string objectPath)
    {
        var view = new FormView(context.Document);
        Populate(view, view.Root, schema, new LiveTarget(context.Document, objectPath), role: null, secretRow: null);
        view.RefreshAll();
        return view;
    }

    /// <summary>
    /// The form of a detached object: edits go to <paramref name="target"/> and nowhere else. <paramref name="secretRow"/> makes the
    /// control of a secret (a provider keeps its secrets in the secret store, not in the object).
    /// </summary>
    public FormView BuildStaged(SettingSchema schema, StagedTarget target, Func<SettingDescriptor, UIElement>? secretRow)
    {
        var view = new FormView();
        target.Changed += view.RefreshAll;
        Populate(view, view.Root, schema, target, role: null, secretRow);
        view.RefreshAll();
        return view;
    }

    /// <summary>Adds the controls of <paramref name="schema"/> to <paramref name="into"/>: groups under headings or folded, pairs on a row.</summary>
    internal void Populate(FormView view, Panel into, SettingSchema schema, FormTarget target, string? role, Func<SettingDescriptor, UIElement>? secretRow)
    {
        var settings = schema.Settings;
        bool fold = settings.Count(s => ShownNow(schema, target, s)) > FoldAbove;
        var groups = new List<string>();
        foreach (var setting in settings)
        {
            if (setting.Group != null && !groups.Contains(setting.Group))
                groups.Add(setting.Group);
        }

        AddRows(view, into, schema, target, role, secretRow, settings.Where(s => s.Group == null).ToList());
        for (int i = 0; i < groups.Count; i++)
        {
            string group = groups[i];
            var content = new StackPanel { Spacing = 12 };
            AddRows(view, content, schema, target, role, secretRow, settings.Where(s => s.Group == group).ToList());
            if (fold)
            {
                into.Children.Add(new Expander
                {
                    Header = group,
                    Content = content,
                    IsExpanded = i == 0,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                });
            }
            else
            {
                var block = new StackPanel { Spacing = 8 };
                block.Children.Add(new TextBlock { Text = group, FontSize = 12, FontWeight = FontWeights.SemiBold, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0) });
                block.Children.Add(content);
                into.Children.Add(block);
            }
        }
    }

    private void AddRows(FormView view, Panel into, SettingSchema schema, FormTarget target, string? role, Func<SettingDescriptor, UIElement>? secretRow, List<SettingDescriptor> settings)
    {
        for (int i = 0; i < settings.Count; i++)
        {
            var setting = settings[i];
            if (i + 1 < settings.Count && IsPair(setting, settings[i + 1]))
            {
                var pair = new Grid { ColumnSpacing = 12 };
                pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var left = CreateRow(view, schema, target, setting, role, secretRow);
                var right = CreateRow(view, schema, target, settings[i + 1], role, secretRow);
                Grid.SetColumn(right, 1);
                pair.Children.Add(left);
                pair.Children.Add(right);
                into.Children.Add(pair);
                i++;
                continue;
            }

            into.Children.Add(CreateRow(view, schema, target, setting, role, secretRow));
        }
    }

    /// <summary>Two numbers one after the other in a group (a minimum and a maximum, a warning and a critical level) share a row.</summary>
    private static bool IsPair(SettingDescriptor first, SettingDescriptor second) =>
        first.Kind == SettingKinds.Number && second.Kind == SettingKinds.Number && first.Group == second.Group
        && first.Number?.UnitFromReading != null && second.Number?.UnitFromReading != null;

    private FrameworkElement CreateRow(FormView view, SettingSchema schema, FormTarget target, SettingDescriptor setting, string? role, Func<SettingDescriptor, UIElement>? secretRow)
    {
        var scope = new RowScope(this, view, target, schema, setting, role);
        var row = new StackPanel { Spacing = 4 };
        var help = new TextBlock { Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        error.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        scope.Error = message =>
        {
            error.Text = message ?? "";
            error.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        };

        var control = CreateControl(scope, secretRow);
        string? extraLine = setting.Kind switch
        {
            SettingKinds.Toggle or SettingKinds.Choice or SettingKinds.Text or SettingKinds.Number or SettingKinds.Range or SettingKinds.Reading
                or SettingKinds.Action or SettingKinds.Unit or SettingKinds.Place or SettingKinds.LaunchTarget or SettingKinds.Icon
                or SettingKinds.Secret or SettingKinds.Positions => null,
            _ => "Edited as text: this plugin uses a newer kind of setting.",
        };

        if (setting.Kind == SettingKinds.Toggle)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock { Text = setting.Label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn((FrameworkElement)control, 1);
            line.Children.Add(control);
            row.Children.Add(line);
        }
        else
        {
            if (setting.Kind != SettingKinds.Positions)
                row.Children.Add(new TextBlock { Text = setting.Label + (setting.Required ? " *" : ""), TextWrapping = TextWrapping.Wrap });
            row.Children.Add(control);
        }

        row.Children.Add(help);
        row.Children.Add(error);
        if (extraLine != null)
            row.Children.Add(Sections.Caption(extraLine));

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, setting.Label);
        void Modify()
        {
            bool sizeOk = setting.Sizes == null || context.SizeKey == null || setting.Sizes.Contains(context.SizeKey, StringComparer.OrdinalIgnoreCase);
            bool roleOff = role != null && setting.DisabledInRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
            row.Visibility = ShownNow(schema, target, setting) && (scope.ExtraVisible?.Invoke() ?? true) ? Visibility.Visible : Visibility.Collapsed;
            if (control is Control enabling)
                enabling.IsEnabled = !target.ReadOnly && sizeOk && !roleOff && (scope.ExtraEnabled?.Invoke() ?? true);
            else
                SetEnabled(control, !target.ReadOnly && sizeOk && !roleOff && (scope.ExtraEnabled?.Invoke() ?? true));
            help.Text = !sizeOk ? $"Only at {string.Join(", ", setting.Sizes!)}." : roleOff ? setting.DisabledHelp ?? setting.Help : setting.Help;
            help.Visibility = help.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        view.Add(Modify);
        return row;
    }

    private static void SetEnabled(UIElement element, bool enabled)
    {
        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
                SetEnabled(child, enabled);
        }
        else if (element is Control control)
        {
            control.IsEnabled = enabled;
        }
    }

    private UIElement CreateControl(RowScope scope, Func<SettingDescriptor, UIElement>? secretRow) => scope.Setting.Kind switch
    {
        SettingKinds.Toggle => ToggleControl(scope),
        SettingKinds.Choice => ChoiceControl(scope),
        SettingKinds.Text => scope.Setting.AllowsNone ? OptionalTextControl(scope) : TextControl(scope),
        SettingKinds.Number => NumberControl(scope),
        SettingKinds.Range => RangeControl(scope),
        SettingKinds.Reading => ReadingControl(scope),
        SettingKinds.Action => ActionControl(scope),
        SettingKinds.Unit => UnitControl(scope),
        SettingKinds.Place => PlaceControl(scope),
        SettingKinds.LaunchTarget => LaunchTargetControl(scope),
        SettingKinds.Icon => IconControl(scope),
        SettingKinds.Positions => PositionsControl(scope, secretRow),
        SettingKinds.Secret => secretRow?.Invoke(scope.Setting) ?? Sections.Caption("Secrets are kept in the Providers section."),
        _ => UnknownControl(scope),
    };

    // ---------------- modifiers (D3) ----------------

    /// <summary>Whether the setting applies now: its "shown when" setting has one of its values (absent counts as its default).</summary>
    internal static bool ShownNow(SettingSchema schema, FormTarget target, SettingDescriptor setting)
    {
        if (setting.When is not { } when)
            return true;
        var other = schema.Settings.FirstOrDefault(s => string.Equals(s.Key, when.Key, StringComparison.OrdinalIgnoreCase));
        if (other == null)
            return true;
        string text = SettingValues.Read(target.Get(other.Key), other) switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            string s => s,
            _ => DefaultText(other),
        };
        return when.Values.Contains(text, StringComparer.OrdinalIgnoreCase);
    }

    internal static string DefaultText(SettingDescriptor setting) => setting.Default switch
    {
        null => "",
        bool b => b ? "true" : "false",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? "",
    };

    internal static JsonNode? Json(string? text) => text == null ? null : JsonValue.Create(text);
}
