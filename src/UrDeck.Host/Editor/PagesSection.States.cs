// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Plugin;

namespace UrDeck.Host.Editor;

/// <summary>
/// The editor's calm answers when the file and the installed plugins disagree (editor-states): placeholders for widgets
/// that did not load or are not installed, the badge and the pane's offer for a size the type does not declare, the bars
/// above the preview for overlapping or off-grid widgets, and the tip on an empty page. None of it edits an object it does
/// not understand: the only edit that touches an unknown widget is removing it whole.
/// </summary>
internal sealed partial class PagesSection
{
    private const string ClockType = "urdeck.widgets.clock";

    private readonly StackPanel _placementBars = new() { Spacing = 4 };
    private readonly HashSet<string> _tipShown = new(StringComparer.Ordinal);
    private TeachingTip? _emptyTip;
    private string? _emptyTipPage;
    private string? _placementKey;

    // ---------------- placeholders ----------------

    /// <summary>The widgets drawn by nobody because their type is not registered, with where the layout put them.</summary>
    private List<(WidgetRef Ref, Sdk.WidgetConfig Config, WidgetUnavailable State, System.Drawing.Rectangle Rect)> UnavailableWidgets()
    {
        var result = new List<(WidgetRef, Sdk.WidgetConfig, WidgetUnavailable, System.Drawing.Rectangle)>();
        if (_pageId == null)
            return result;
        int pageIndex = PageEditing.PageIndex(Config, _pageId);
        if (CurrentPage is { } page && pageIndex >= 0)
        {
            var widgets = Config.Pages[pageIndex].Widgets;
            for (int i = 0; i < page.Layout.Count && i < widgets.Count; i++)
            {
                if (page.Layout[i].Placed && _host.ClassifyWidget(widgets[i].WidgetTypeId) is { } state)
                    result.Add((new WidgetRef(_pageId, i), widgets[i], state, new System.Drawing.Rectangle(page.Layout[i].Position, page.Layout[i].Size)));
            }
        }

        if (_preview.Session.Pager.Dock is { } dock)
        {
            for (int i = 0; i < dock.Layout.Count && i < Config.Dock.Count; i++)
            {
                // A slot is left empty for a type that is not registered (Placed is false), or for a hidden one: only the first is a placeholder.
                if (!dock.Layout[i].Placed && Config.Dock[i].IsVisible && _host.ClassifyWidget(Config.Dock[i].WidgetTypeId) is { } state)
                    result.Add((WidgetRef.Dock(i), Config.Dock[i], state, new System.Drawing.Rectangle(dock.Layout[i].Position, dock.Layout[i].Size)));
            }
        }

        return result;
    }

    private void AddStateCards(double f, GridSize grid)
    {
        foreach (var (_, _, state, rect) in UnavailableWidgets())
        {
            double width = rect.Width * f, height = rect.Height * f;
            var card = new Grid { Width = width, Height = height, IsHitTestVisible = false };
            card.Children.Add(new Rectangle
            {
                Stroke = new SolidColorBrush(ColorHelper.FromArgb(190, 255, 190, 80)),
                Fill = new SolidColorBrush(ColorHelper.FromArgb(110, 24, 24, 28)),
                StrokeDashArray = [4, 3],
                StrokeThickness = 1.5,
                RadiusX = 6,
                RadiusY = 6,
            });
            double font = Math.Clamp(Math.Min(width, height) * 0.13, 9, 15);
            var text = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
            text.Children.Add(new FontIcon { Glyph = "", FontSize = font * 1.6, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 190, 80)), HorizontalAlignment = HorizontalAlignment.Center });
            text.Children.Add(new TextBlock { Text = state.DisplayName, FontSize = font, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Foreground = new SolidColorBrush(Colors.White) });
            text.Children.Add(new TextBlock { Text = UnavailableLine(state), FontSize = font * 0.9, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Foreground = new SolidColorBrush(Colors.White) });
            card.Children.Add(text);
            Canvas.SetLeft(card, rect.X * f);
            Canvas.SetTop(card, rect.Y * f);
            _overlay.Children.Add(card);
        }
    }

    private static string UnavailableLine(WidgetUnavailable state) =>
        state.Kind == WidgetUnavailableKind.DidNotLoad ? "did not load" : "not installed";

    // ---------------- sizes the type does not declare, overlaps, off-grid ----------------

    private IReadOnlyList<PlacementIssue> PlacementOfPage()
    {
        int index = PageEditing.PageIndex(Config, _pageId);
        return index < 0 ? [] : _editing.PlacementIssues(Config.Pages[index].Widgets, CellGrid);
    }

    private PlacementIssue? UndeclaredSizeOf(WidgetRef widget) =>
        widget.InDock ? null : PlacementOfPage().FirstOrDefault(i => i.Kind == PlacementIssueKind.UndeclaredSize && i.Index == widget.Index);

    /// <summary>A key of what the pane shows about the selected widget's size, so the pane is built again when it changes.</summary>
    private string PlacementKey(WidgetRef widget) =>
        UndeclaredSizeOf(widget) is { } issue ? issue.Message + string.Join(",", issue.Suggestions.Select(s => $"{s.Label}@{s.At}")) : "";

    private void AddSizeBadges(double f, double cell)
    {
        if (_preview.Session.Layout is not { } layout)
            return;
        foreach (var issue in PlacementOfPage().Where(i => i.Kind == PlacementIssueKind.UndeclaredSize))
        {
            if (RectOf(layout, new WidgetRef(_pageId!, issue.Index)) is not { } rect)
                continue;
            double side = Math.Clamp(cell * 0.3, 16, 26);
            var badge = new Border
            {
                Width = side,
                Height = side,
                CornerRadius = new CornerRadius(side / 2),
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 245, 166, 35)),
                Child = new TextBlock { Text = "!", Foreground = new SolidColorBrush(Colors.Black), FontWeight = FontWeights.Bold, FontSize = side * 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(badge, (rect.Right * f) - side - 4);
            Canvas.SetTop(badge, (rect.Y * f) + 4);
            _overlay.Children.Add(badge);
        }
    }

    /// <summary>The bars above the preview for overlapping and off-grid widgets, each with its way out.</summary>
    private void UpdatePlacementBars()
    {
        var issues = PlacementOfPage().Where(i => i.Kind != PlacementIssueKind.UndeclaredSize).ToList();
        string key = _pageId + "|" + Editable + "|" + string.Join("|", issues.Select(i => i.Message));
        if (key == _placementKey)
            return;
        _placementKey = key;
        _placementBars.Children.Clear();
        foreach (var issue in issues.Take(3))
        {
            var bar = new InfoBar { Severity = InfoBarSeverity.Warning, IsOpen = true, IsClosable = false, Message = issue.Message };
            if (PageEditing.Widget(Config, new WidgetRef(_pageId!, issue.Index)) is { } widget)
            {
                var move = new Button { Content = $"Move {_editing.Describe(widget)} to the first free space", IsEnabled = Editable };
                var at = new WidgetRef(_pageId!, issue.Index);
                string message = issue.Message;
                move.Click += (_, _) =>
                {
                    if (!_editing.MoveToFirstFree(at, CellGrid).Applied)
                        bar.Message = message + " The page has no free space for it.";
                };
                bar.ActionButton = move;
            }

            _placementBars.Children.Add(bar);
        }
    }

    // ---------------- the empty page tip ----------------

    private void UpdateEmptyTip(int widgetCount, GridSize grid, double cell)
    {
        bool wanted = _pageId != null && Editable && _active && widgetCount == 0 && !_paletteOpen && _host.DescribeWidget(ClockType) != null;
        if (!wanted)
        {
            if (_emptyTip is { IsOpen: true })
                _emptyTip.IsOpen = false;
            return;
        }

        bool open = _emptyTip is { IsOpen: true } && _emptyTipPage == _pageId;
        if (!open && _tipShown.Contains(_pageId!))
            return;

        // The anchor is the first cell of the first row; the overlay is built again on every change, so the target follows it.
        var anchor = new Border { Width = Math.Max(1, cell), Height = Math.Max(1, cell), IsHitTestVisible = false };
        _overlay.Children.Add(anchor);
        if (_emptyTip == null)
        {
            _emptyTip = new TeachingTip { Title = "Add your first widget", ActionButtonContent = "Add a clock", CloseButtonContent = "Got it", PreferredPlacement = TeachingTipPlacementMode.Bottom };
            _emptyTip.ActionButtonClick += OnEmptyTipAction;
            _root.Children.Add(_emptyTip);
        }

        _emptyTip.Subtitle = $"Click any empty cell, or Add widget. Each widget takes a few cells; the page has room for about {grid.Rows} rows.";
        _emptyTip.Target = anchor;
        if (!open)
        {
            _tipShown.Add(_pageId!);
            _emptyTipPage = _pageId;
            // Opened once the anchor has a place in the tree.
            _queue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (_emptyTip != null && _emptyTipPage == _pageId && PageIsEmpty())
                    _emptyTip.IsOpen = true;
            });
        }
    }

    private bool PageIsEmpty() => PageEditing.PageIndex(Config, _pageId) is int i and >= 0 && Config.Pages[i].Widgets.Count == 0;

    private void OnEmptyTipAction(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        if (_pageId == null)
            return;
        var outcome = _editing.AddWidget(_pageId, ClockType, CellGrid, new Cell(0, 0));
        if (outcome.Applied)
        {
            _selection = WidgetSelection.Of(Config, outcome.Widget!);
            Refresh();
        }
    }

    private void CloseStates()
    {
        if (_emptyTip != null)
        {
            _emptyTip.IsOpen = false;
            _emptyTip.ActionButtonClick -= OnEmptyTipAction;
            _emptyTip.Target = null;
            _root.Children.Remove(_emptyTip);
            _emptyTip = null;
        }

        _placementBars.Children.Clear();
    }

    // ---------------- the pane ----------------

    /// <summary>The pane of a widget whose type is not registered: why, what is kept, and Remove (D4).</summary>
    private void BuildUnavailablePane(StackPanel panel, WidgetRef at, Sdk.WidgetConfig widget)
    {
        var state = _host.ClassifyWidget(widget.WidgetTypeId) ?? new WidgetUnavailable(WidgetUnavailableKind.NotInstalled, widget.WidgetTypeId, null, null, null);
        panel.Children.Add(new TextBlock { Text = state.DisplayName, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = widget.WidgetTypeId, FontFamily = new FontFamily("Consolas"), FontSize = 12, Opacity = 0.7 });

        if (state.Kind == WidgetUnavailableKind.DidNotLoad)
        {
            string plugin = state.PluginFile != null ? $"Its plugin, {state.PluginFile}," : "Its plugin";
            string why = string.IsNullOrWhiteSpace(state.Reason) ? "failed to start" : $"failed to start: {state.Reason.TrimEnd('.')}";
            var bar = new InfoBar
            {
                Severity = InfoBarSeverity.Error,
                IsOpen = true,
                IsClosable = false,
                Message = $"{state.DisplayName} did not load. {plugin} {why}. Its settings are kept and the deck leaves the space empty.",
            };
            var log = new Button { Content = "Show in the log" };
            log.Click += (_, _) => GeneralSection.Start(_host.LogPath, null);
            bar.ActionButton = log;
            panel.Children.Add(bar);
        }
        else
        {
            string provided = state.Kind == WidgetUnavailableKind.Removed && state.PluginFile != null ? $" It was provided by {state.PluginFile}, which is no longer in the plugin folders." : "";
            panel.Children.Add(new InfoBar
            {
                Severity = InfoBarSeverity.Warning,
                IsOpen = true,
                IsClosable = false,
                Message = $"{widget.WidgetTypeId} is not installed.{provided} It stays in the file untouched.",
            });
        }

        panel.Children.Add(Sections.Label("Settings in the file"));
        panel.Children.Add(new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            MaxHeight = 260,
            Text = WidgetJson(at),
        });
        panel.Children.Add(Sections.Caption("Read-only: UrDeck does not know this widget's settings, so it leaves them as they are."));
        AddRemoveButton(panel);
    }

    private string WidgetJson(WidgetRef widget)
    {
        JsonNode? node = widget.InDock
            ? (_host.Document.Root["dock"] as JsonArray)?.ElementAtOrDefault(widget.Index)
            : (_host.Document.Root["pages"] as JsonArray)?.ElementAtOrDefault(PageEditing.PageIndex(Config, widget.PageId)) is JsonObject page
                ? (page["widgets"] as JsonArray)?.ElementAtOrDefault(widget.Index)
                : null;
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "";
    }

    /// <summary>The "<c>3x1 is not a Stats size</c>" bar with the nearest declared sizes as buttons.</summary>
    private void AddUndeclaredSizeBar(StackPanel panel, WidgetRef at, PlacementIssue issue)
    {
        var bar = new InfoBar { Severity = InfoBarSeverity.Warning, IsOpen = true, IsClosable = false, Message = issue.Message };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var suggestion in issue.Suggestions)
        {
            var button = new Button
            {
                Content = suggestion.Moves ? $"Make it {suggestion.Label} in the first free space" : $"Make it {suggestion.Label}",
                IsEnabled = Editable,
            };
            var chosen = suggestion;
            button.Click += (_, _) =>
            {
                if (chosen.Moves)
                    ArrangeResult(_editing.MoveTo(at, chosen.At, chosen.Width, chosen.Height, CellGrid));
                else
                    Resize(at, chosen.Width, chosen.Height);
            };
            buttons.Children.Add(button);
        }

        if (buttons.Children.Count > 0)
            bar.Content = buttons;
        panel.Children.Add(bar);
    }

    private void AddRemoveButton(StackPanel panel)
    {
        var remove = new Button
        {
            Content = "Remove",
            IsEnabled = Editable,
            Margin = new Thickness(0, 16, 0, 0),
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        remove.Click += (_, _) => RemoveSelected();
        panel.Children.Add(remove);
    }
}
