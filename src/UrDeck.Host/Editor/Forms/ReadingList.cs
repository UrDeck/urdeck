// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using UrDeck.Engine.Editor;
using UrDeck.Sdk.Settings;
using Windows.ApplicationModel.DataTransfer;
using VirtualKey = Windows.System.VirtualKey;

namespace UrDeck.Host.Editor.Forms;

/// <summary>What a reading list offers.</summary>
/// <param name="Restrict">What the setting accepts: every reading, readings with a range, or readings with an action.</param>
/// <param name="OnlyProvider">One provider's readings only; null for all.</param>
/// <param name="Current">The id to select and scroll to when the list opens.</param>
/// <param name="Chooses">Whether a reading can be chosen (the picker) or only looked at and copied (the Readings section).</param>
internal sealed record ReadingListOptions(ReadingFilter Restrict, string? OnlyProvider, string? Current, bool Chooses);

/// <summary>A line of the list: a heading or a reading.</summary>
internal abstract class ReadingRow;

internal sealed class HeaderRow(string text) : ReadingRow
{
    public string Text { get; } = text;
}

internal sealed class EntryRow(ReadingHit hit, bool connected) : ReadingRow
{
    public ReadingHit Hit { get; } = hit;

    public bool Connected { get; } = connected;
}

/// <summary>
/// The reading list of the picker and of the Readings section (reading-picker D3, D4, D5, D10): a search box, a tab for each
/// provider, area and kind filters when the provider supplies them, a count, and a grouped, virtualised list whose rows show the
/// label and id with the matched text in bold, an "action" tag and the live value of the rows on screen. A provider that is not
/// connected says so, with a way to set it up. Search and filters run in memory (<see cref="ReadingIndex"/>).
/// </summary>
internal sealed class ReadingList : IDisposable
{
    private const string AllTab = "";

    private static readonly DataTemplate EntryTemplate = (DataTemplate)XamlReader.Load("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Grid ColumnSpacing="12" Padding="4,3">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <StackPanel Grid.Column="0">
              <TextBlock TextTrimming="CharacterEllipsis"/>
              <TextBlock FontFamily="Consolas" FontSize="11" Opacity="0.7" TextTrimming="CharacterEllipsis"/>
            </StackPanel>
            <Border Grid.Column="1" Padding="6,1" CornerRadius="4" VerticalAlignment="Center" Visibility="Collapsed"
                    Background="{ThemeResource ControlFillColorSecondaryBrush}">
              <TextBlock FontSize="11"/>
            </Border>
            <TextBlock Grid.Column="2" VerticalAlignment="Center" MinWidth="72" TextAlignment="Right" TextTrimming="CharacterEllipsis"/>
          </Grid>
        </DataTemplate>
        """);

    private static readonly DataTemplate HeaderTemplate = (DataTemplate)XamlReader.Load("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <TextBlock FontWeight="SemiBold" Margin="0,10,0,2" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
        </DataTemplate>
        """);

    private readonly ReadingCatalog _catalog;
    private readonly IEditorReadings _readings;
    private readonly ReadingListOptions _options;
    private readonly Action<string>? _setUp;
    private readonly Action<ReadingEntry>? _activated;
    private readonly DispatcherQueue _queue;
    private readonly DispatcherQueueTimer _leaseTimer;
    private readonly Grid _root = new() { RowSpacing = 8 };
    private readonly TextBox _search = new() { PlaceholderText = "Search readings: name, id or room" };
    private readonly Button _refresh = new() { Content = new SymbolIcon(Symbol.Refresh) };
    private readonly SelectorBar _tabs = new();
    private readonly DropDownButton _areaButton = new() { Content = "Area" };
    private readonly DropDownButton _kindButton = new() { Content = "Kind" };
    private readonly StackPanel _filters = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly StackPanel _bars = new() { Spacing = 6 };
    private readonly TextBlock _count = new() { Opacity = 0.75 };
    private readonly ProgressRing _progress = new() { IsActive = false, Width = 16, Height = 16, Visibility = Visibility.Collapsed };
    private readonly ListView _list;
    private readonly Dictionary<string, TextBlock> _valueBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _visible = new(StringComparer.OrdinalIgnoreCase);
    private string? _tabsKey;
    private string _provider = AllTab;
    private string? _area;
    private string? _category;
    private string? _watchedKey;
    private IDisposable? _watch;
    private bool _active = true;
    private bool _currentShown;
    private bool _disposed;
    private bool _building;

    public ReadingList(ReadingCatalog catalog, IEditorReadings readings, ReadingListOptions options, DispatcherQueue queue, Action<string>? setUp, Action<ReadingEntry>? activated)
    {
        _catalog = catalog;
        _readings = readings;
        _options = options;
        _queue = queue;
        _setUp = setUp;
        _activated = activated;
        _leaseTimer = queue.CreateTimer();
        _leaseTimer.IsRepeating = false;
        _leaseTimer.Interval = TimeSpan.FromMilliseconds(100);
        _leaseTimer.Tick += OnLeaseTick;

        _list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemTemplateSelector = new RowSelector(),
            MinHeight = 240,
        };
        _list.ContainerContentChanging += OnContainer;
        _list.SelectionChanged += OnSelectionChanged;
        _list.DoubleTapped += OnDoubleTapped;
        _list.KeyDown += OnListKeyDown;
        _list.RightTapped += OnListRightTapped;
        _search.TextChanged += OnSearchChanged;
        _search.KeyDown += OnSearchKeyDown;
        _refresh.Click += OnRefresh;
        _tabs.SelectionChanged += OnTabChanged;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_refresh, "Read the readings again");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Search readings");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_list, "Readings");

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var top = new Grid { ColumnSpacing = 8 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(_search);
        Grid.SetColumn(_refresh, 1);
        top.Children.Add(_refresh);
        _root.Children.Add(top);
        Place(_tabs, 1);
        _filters.Children.Add(_areaButton);
        _filters.Children.Add(_kindButton);
        Place(_filters, 2);
        Place(_bars, 3);
        var countRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        countRow.Children.Add(_progress);
        countRow.Children.Add(_count);
        Place(countRow, 4);
        Place(_list, 5);

        _catalog.Changed += OnCatalogChanged;
        _catalog.Ensure();
        Rebuild();
    }

    public UIElement Root => _root;

    /// <summary>The reading selected, if it is one (a template included).</summary>
    public ReadingEntry? Selected => (_list.SelectedItem as EntryRow)?.Hit.Entry;

    /// <summary>Raised when the selection changes.</summary>
    public event Action? SelectionChanged;

    /// <summary>Puts the cursor in the search box.</summary>
    public void FocusSearch() => _search.Focus(FocusState.Programmatic);

    /// <summary>Whether the list is shown: a list that is not shown holds no subscriptions.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (active)
            ScheduleWatch();
        else
            DropWatch();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _catalog.Changed -= OnCatalogChanged;
        _leaseTimer.Stop();
        _leaseTimer.Tick -= OnLeaseTick;
        _list.ContainerContentChanging -= OnContainer;
        _list.SelectionChanged -= OnSelectionChanged;
        _list.DoubleTapped -= OnDoubleTapped;
        _list.KeyDown -= OnListKeyDown;
        _list.RightTapped -= OnListRightTapped;
        _search.TextChanged -= OnSearchChanged;
        _search.KeyDown -= OnSearchKeyDown;
        _refresh.Click -= OnRefresh;
        _tabs.SelectionChanged -= OnTabChanged;
        DropWatch();
        _list.ItemsSource = null;
        _valueBoxes.Clear();
        _visible.Clear();
    }

    private void Place(UIElement element, int row)
    {
        Grid.SetRow((FrameworkElement)element, row);
        _root.Children.Add(element);
    }

    // ---------------- data ----------------

    private void OnCatalogChanged() => Rebuild();

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        _currentShown = false;
        _catalog.Refresh();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Update();

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_building)
            return;
        _provider = (sender.SelectedItem?.Tag as string) ?? AllTab;
        _area = null;
        _category = null;
        Rebuild();
    }

    /// <summary>The providers shown as tabs, then the filters, then the list.</summary>
    private void Rebuild()
    {
        if (_disposed)
            return;
        var shown = _catalog.Providers
            .Where(p => (_options.OnlyProvider == null || string.Equals(p.ProviderId, _options.OnlyProvider, StringComparison.OrdinalIgnoreCase)) && (p.Entries.Count > 0 || !p.Connected))
            .ToList();
        bool withAll = _options.OnlyProvider == null && shown.Count != 1;
        string key = string.Join('|', shown.Select(p => p.ProviderId + (p.Connected ? "" : "!"))) + (withAll ? "+" : "");
        if (key != _tabsKey)
        {
            _tabsKey = key;
            _building = true;
            try
            {
                _tabs.Items.Clear();
                if (withAll)
                    _tabs.Items.Add(new SelectorBarItem { Text = "All", Tag = AllTab });
                foreach (var provider in shown)
                    _tabs.Items.Add(new SelectorBarItem { Text = provider.Connected ? provider.Name : provider.Name + " ●", Tag = provider.ProviderId });
                if (!withAll && !shown.Any(p => string.Equals(p.ProviderId, _provider, StringComparison.OrdinalIgnoreCase)))
                    _provider = shown.FirstOrDefault()?.ProviderId ?? AllTab;
                else if (withAll && _provider != AllTab && !shown.Any(p => string.Equals(p.ProviderId, _provider, StringComparison.OrdinalIgnoreCase)))
                    _provider = AllTab;
                _tabs.SelectedItem = _tabs.Items.FirstOrDefault(i => string.Equals(i.Tag as string, _provider, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _building = false;
            }
        }

        _tabs.Visibility = _tabs.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        BuildFilters();
        BuildBars(shown);
        Update();
    }

    private void BuildFilters()
    {
        string? provider = _provider == AllTab ? _options.OnlyProvider : _provider;
        var index = _catalog.Index;
        Fill(_areaButton, "Area", "All areas", index.Areas(provider), _area, value => _area = value);
        Fill(_kindButton, "Kind", "All kinds", index.Categories(provider), _category, value => _category = value);
        _filters.Visibility = _areaButton.Visibility == Visibility.Visible || _kindButton.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Fill(DropDownButton button, string name, string all, IReadOnlyList<string> values, string? selected, Action<string?> set)
    {
        if (selected != null && !values.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            selected = null;
            set(null);
        }

        button.Visibility = values.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        button.Content = selected == null ? name : $"{name}: {selected}";
        var flyout = new MenuFlyout();
        string group = name + "Filter";
        void Add(string label, string? value)
        {
            var item = new RadioMenuFlyoutItem { Text = label, GroupName = group, IsChecked = string.Equals(value, selected, StringComparison.OrdinalIgnoreCase) };
            item.Click += (_, _) =>
            {
                set(value);
                button.Content = value == null ? name : $"{name}: {value}";
                Update();
            };
            flyout.Items.Add(item);
        }

        Add(all, null);
        foreach (string value in values)
            Add(value, value);
        button.Flyout = flyout;
    }

    private void BuildBars(List<ProviderReadings> shown)
    {
        _bars.Children.Clear();
        foreach (var provider in shown.Where(p => !p.Connected && (_provider == AllTab || string.Equals(p.ProviderId, _provider, StringComparison.OrdinalIgnoreCase))))
        {
            var bar = new InfoBar
            {
                Severity = InfoBarSeverity.Warning,
                IsOpen = true,
                IsClosable = false,
                Title = $"{provider.Name} is not connected.",
                Message = provider.Message + (provider.FromCache ? " The readings your pages use are listed, without values." : ""),
            };
            if (_setUp != null)
            {
                var setUp = new Button { Content = "Set up" };
                string id = provider.ProviderId;
                setUp.Click += (_, _) => _setUp(id);
                bar.ActionButton = setUp;
            }

            _bars.Children.Add(bar);
        }
    }

    /// <summary>Searches and fills the list.</summary>
    private void Update()
    {
        if (_disposed)
            return;
        string? provider = _provider == AllTab ? _options.OnlyProvider : _provider;
        var query = new ReadingQuery(_search.Text.Trim(), provider, _area, _category, _options.Restrict);
        var result = _catalog.Index.Search(query);
        var disconnected = _catalog.Providers.Where(p => !p.Connected).Select(p => p.ProviderId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<ReadingRow>(result.Hits.Count + 16);
        foreach (var group in ReadingIndex.Group(result.Hits))
        {
            rows.Add(new HeaderRow(group.Header));
            rows.AddRange(group.Hits.Select(h => new EntryRow(h, !disconnected.Contains(h.Entry.ProviderId))));
        }

        _valueBoxes.Clear();
        _visible.Clear();
        _list.ItemsSource = rows;
        _count.Text = CountText(result, query, provider);
        _progress.IsActive = _catalog.IsLoading;
        _progress.Visibility = _catalog.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        ShowCurrent(rows);
        ScheduleWatch();
    }

    private string CountText(ReadingSearchResult result, ReadingQuery query, string? provider)
    {
        if (_catalog.IsLoading && result.Total == 0)
            return "Reading the readings…";
        string noun = string.Equals(provider, "homeassistant", StringComparison.OrdinalIgnoreCase) ? "entities" : "readings";
        bool narrowed = query.Text.Length > 0 || query.Area != null || query.Category != null;
        string text = narrowed
            ? string.Create(CultureInfo.CurrentCulture, $"{result.Hits.Count:N0} of {result.Total:N0} {noun} match.")
            : string.Create(CultureInfo.CurrentCulture, $"{result.Total:N0} {noun}.");
        text += _options.Restrict switch
        {
            ReadingFilter.HasAction => " Showing only readings that can be switched or set.",
            ReadingFilter.HasRange => " Showing only readings with a range.",
            _ => "",
        };
        return text;
    }

    private void ShowCurrent(List<ReadingRow> rows)
    {
        if (_currentShown || _options.Current is not { Length: > 0 } current || rows.Count == 0)
            return;
        var row = rows.OfType<EntryRow>().FirstOrDefault(r => string.Equals(r.Hit.Entry.Id, current, StringComparison.OrdinalIgnoreCase));
        if (row == null)
            return;
        _currentShown = true;
        _list.SelectedItem = row;
        _list.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
    }

    // ---------------- rows ----------------

    private void OnContainer(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is not FrameworkElement root)
            return;
        if (args.InRecycleQueue)
        {
            if (args.Item is EntryRow released)
            {
                _valueBoxes.Remove(released.Hit.Entry.Id);
                _visible.Remove(released.Hit.Entry.Id);
                ScheduleWatch();
            }

            return;
        }

        switch (args.Item)
        {
            case HeaderRow header when root is TextBlock title:
                title.Text = header.Text;
                args.ItemContainer.IsEnabled = false;
                break;
            case EntryRow row when root is Grid grid:
                args.ItemContainer.IsEnabled = true;
                Fill(grid, row);
                break;
        }
    }

    private void Fill(Grid grid, EntryRow row)
    {
        var entry = row.Hit.Entry;
        var text = (StackPanel)grid.Children[0];
        Highlight((TextBlock)text.Children[0], entry.Descriptor.Label, row.Hit.LabelMatches, FontWeights.SemiBold);
        Highlight((TextBlock)text.Children[1], entry.Id, row.Hit.IdMatches, FontWeights.SemiBold);

        var tag = (Border)grid.Children[1];
        string? tagText = entry.IsTemplate ? "template" : entry.HasAction ? "action" : null;
        tag.Visibility = tagText == null ? Visibility.Collapsed : Visibility.Visible;
        if (tagText != null)
            ((TextBlock)tag.Child).Text = tagText;

        var value = (TextBlock)grid.Children[2];
        if (entry.IsTemplate)
        {
            value.Text = "";
            return;
        }

        value.Text = row.Connected ? _readings.RowValue(entry.Id, entry.Descriptor) : "—";
        if (row.Connected)
        {
            _valueBoxes[entry.Id] = value;
            _visible.Add(entry.Id);
            ScheduleWatch();
        }
    }

    private static void Highlight(TextBlock block, string text, IReadOnlyList<MatchRange> ranges, Windows.UI.Text.FontWeight weight)
    {
        block.Inlines.Clear();
        int at = 0;
        foreach (var range in ranges)
        {
            if (range.Start < at || range.Start + range.Length > text.Length)
                continue;
            if (range.Start > at)
                block.Inlines.Add(new Run { Text = text[at..range.Start] });
            block.Inlines.Add(new Run { Text = text.Substring(range.Start, range.Length), FontWeight = weight });
            at = range.Start + range.Length;
        }

        if (at < text.Length)
            block.Inlines.Add(new Run { Text = text[at..] });
    }

    // ---------------- values ----------------

    private void ScheduleWatch()
    {
        if (_disposed || !_active)
            return;
        _leaseTimer.Stop();
        _leaseTimer.Start();
    }

    private void OnLeaseTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_disposed || !_active)
            return;
        string[] ids = [.. _visible.Order(StringComparer.OrdinalIgnoreCase)];
        string key = string.Join('\n', ids);
        if (key == _watchedKey)
            return;
        // The new lease first, so a reading that stays in view is never without a user.
        var next = ids.Length == 0 ? null : _readings.WatchMany(ids, OnValues);
        _watch?.Dispose();
        _watch = next;
        _watchedKey = key;
    }

    private void DropWatch()
    {
        _leaseTimer.Stop();
        _watch?.Dispose();
        _watch = null;
        _watchedKey = null;
    }

    private void OnValues(IReadOnlyCollection<string> ids)
    {
        if (_disposed)
            return;
        foreach (string id in ids)
        {
            if (_valueBoxes.TryGetValue(id, out var box) && _catalog.Index.Find(id) is { } entry)
                box.Text = _readings.RowValue(id, entry.Descriptor);
        }
    }

    // ---------------- input ----------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SelectionChanged?.Invoke();

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Activate(Selected);

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down)
        {
            e.Handled = true;
            if (_list.SelectedItem == null)
                _list.SelectedItem = _list.Items.OfType<EntryRow>().FirstOrDefault();
            if (_list.SelectedItem != null)
            {
                _list.ScrollIntoView(_list.SelectedItem);
                (_list.ContainerFromItem(_list.SelectedItem) as Control)?.Focus(FocusState.Keyboard);
            }
        }
        else if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Activate(Selected ?? (_list.Items.OfType<EntryRow>().FirstOrDefault()?.Hit.Entry));
        }
    }

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Activate(Selected);
        }
    }

    private void Activate(ReadingEntry? entry)
    {
        if (entry != null && _options.Chooses)
            _activated?.Invoke(entry);
    }

    private void OnListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_options.Chooses || (e.OriginalSource as FrameworkElement)?.DataContext is not EntryRow row)
            return;
        var menu = new MenuFlyout();
        var copy = new MenuFlyoutItem { Text = "Copy id" };
        copy.Click += (_, _) => Copy(row.Hit.Entry);
        menu.Items.Add(copy);
        menu.ShowAt(_list, e.GetPosition(_list));
        e.Handled = true;
    }

    private static void Copy(ReadingEntry entry)
    {
        var package = new DataPackage();
        package.SetText(entry.Id);
        Clipboard.SetContent(package);
    }

    private sealed class RowSelector : DataTemplateSelector
    {
        protected override DataTemplate SelectTemplateCore(object item) => item is HeaderRow ? HeaderTemplate : EntryTemplate;

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
    }
}
