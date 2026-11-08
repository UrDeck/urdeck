// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using VirtualKey = Windows.System.VirtualKey;

namespace UrDeck.Host.Editor;

/// <summary>The pages as a list beside the stage (D5): a picture of each page's layout, drag to reorder, double-click to rename.</summary>
internal sealed partial class PagesSection
{
    private const double ThumbWidth = 32;

    // The choice of tabs or list is kept for the session (the window is made anew each time it opens).
    private static bool ListMode;

    private readonly Grid _stageHost = new();
    private readonly ScrollViewer _tabsScroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalScrollMode = ScrollMode.Auto,
        VerticalScrollMode = ScrollMode.Disabled,
    };

    private readonly ToggleButton _listToggle = new() { Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 0) };
    private readonly ListView _pageList = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        CanReorderItems = true,
        AllowDrop = true,
        Width = 230,
        Padding = new Thickness(0, 0, 8, 0),
        Visibility = Visibility.Collapsed,
    };

    private bool _listBuilding;

    private void BuildStageHost()
    {
        _stageHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _stageHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AutomationProperties.SetName(_pageList, "Pages");
        _pageList.SelectionChanged += OnPageListSelection;
        _pageList.DragItemsCompleted += OnPageListReordered;
        Grid.SetColumn(_stage, 1);
        _stageHost.Children.Add(_pageList);
        _stageHost.Children.Add(_stage);
    }

    private void ApplyListMode()
    {
        _pageList.Visibility = ListMode ? Visibility.Visible : Visibility.Collapsed;
        _tabsScroll.Visibility = ListMode ? Visibility.Collapsed : Visibility.Visible;
        _listToggle.IsChecked = ListMode;
        if (ListMode)
            BuildPageList();
    }

    private void BuildPageList()
    {
        if (!ListMode)
            return;
        _listBuilding = true;
        try
        {
            var config = Config;
            int start = StartPageResolver.Resolve(config);
            _pageList.CanReorderItems = Editable;
            _pageList.Items.Clear();
            ListViewItem? current = null;
            for (int i = 0; i < config.Pages.Count; i++)
            {
                var page = config.Pages[i];
                if (page.Id is not { } id)
                    continue;
                var item = new ListViewItem { Content = PageRow(i, page, i == start), Tag = id, Padding = new Thickness(8, 6, 4, 6) };
                AutomationProperties.SetName(item, i == start ? $"{page.Name}, start page" : page.Name);
                _pageList.Items.Add(item);
                if (id == _pageId)
                    current = item;
            }

            _pageList.SelectedItem = current;
        }
        finally
        {
            _listBuilding = false;
        }
    }

    private Grid PageRow(int index, PageConfig page, bool isStart)
    {
        string id = page.Id!;
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new FontIcon { Glyph = "", FontSize = 12, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center });
        var thumb = Thumbnail(page);
        Grid.SetColumn(thumb, 1);
        row.Children.Add(thumb);

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = isStart ? $"{page.Name}  ⌂" : page.Name, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = id == _pageId ? FontWeights.SemiBold : FontWeights.Normal };
        name.DoubleTapped += (_, e) =>
        {
            if (!Editable)
                return;
            e.Handled = true;
            BeginRename(text, name, id, page.Name);
        };
        text.Children.Add(name);
        int count = page.Widgets.Count;
        text.Children.Add(Sections.Caption(count == 1 ? "1 widget" : $"{count} widgets"));
        Grid.SetColumn(text, 2);
        row.Children.Add(text);

        var more = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Padding = new Thickness(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = Editable,
        };
        AutomationProperties.SetName(more, $"Page menu for {page.Name}");
        if (Editable)
            more.Flyout = PageMenu(index, page, Config.Pages.Count);
        Grid.SetColumn(more, 3);
        row.Children.Add(more);
        return row;
    }

    // The name becomes a text box in place: Enter or leaving it renames (one undo step), Escape leaves the name as it was.
    private void BeginRename(StackPanel host, TextBlock name, string id, string current)
    {
        var box = new TextBox { Text = current, MaxLength = 60 };
        bool done = false;
        void Finish(bool apply)
        {
            if (done)
                return;
            done = true;
            if (apply && box.Text.Trim() is { Length: > 0 } text)
                _editing.RenamePage(id, text);
            else
                BuildPageList();
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                Finish(true);
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                Finish(false);
            }
        };
        box.LostFocus += (_, _) => Finish(true);
        host.Children[0] = box;
        box.Focus(FocusState.Programmatic);
        box.SelectAll();
    }

    /// <summary>
    /// A small picture of the page's layout drawn from the configuration, not rendered: each widget a rounded block on the grid,
    /// the page as tall as the deck's surface.
    /// </summary>
    private Border Thumbnail(PageConfig page)
    {
        var surface = _host.DeckSurface;
        var grid = CellGrid;
        double cell = ThumbWidth / grid.Columns;
        double aspect = surface.IsUsable ? surface.Size.Height / (double)surface.Size.Width : 3.5;
        var canvas = new Canvas { Width = ThumbWidth, Height = Math.Round(ThumbWidth * aspect) };
        double rowHeight = canvas.Height / Math.Max(1, grid.Rows);
        var fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        foreach (var widget in page.Widgets)
        {
            var (col, row, w, h) = PageEditing.Area(widget);
            var block = new Rectangle
            {
                Width = Math.Max(2, w * cell - 1.5),
                Height = Math.Max(2, h * rowHeight - 1.5),
                RadiusX = 2,
                RadiusY = 2,
                Fill = fill,
                Opacity = 0.8,
            };
            Canvas.SetLeft(block, col * cell + 0.75);
            Canvas.SetTop(block, row * rowHeight + 0.75);
            canvas.Children.Add(block);
        }

        return new Border
        {
            Child = canvas,
            Width = ThumbWidth,
            Height = canvas.Height,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 18, 18, 20)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, 2),
        };
    }

    private void OnPageListSelection(object sender, SelectionChangedEventArgs e)
    {
        if (_listBuilding || _pageList.SelectedItem is not ListViewItem { Tag: string id })
            return;
        SelectPage(id);
    }

    private void OnPageListReordered(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (_listBuilding || !Editable)
            return;
        var config = Config;
        var order = _pageList.Items.OfType<ListViewItem>().Select(i => (string)i.Tag).ToList();
        for (int to = 0; to < order.Count; to++)
        {
            int from = PageEditing.PageIndex(config, order[to]);
            if (from == to || from < 0)
                continue;
            // The first page out of place is the one that was dragged; one step of MovePage gets it where the list shows it.
            _editing.MovePage(order[to], to - from);
            return;
        }
    }
}
