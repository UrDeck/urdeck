// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Layout;
using Windows.Foundation;
using VirtualKey = Windows.System.VirtualKey;

namespace UrDeck.Host.Editor;

/// <summary>
/// Arranging by direct manipulation (D1-D4, D6): dragging a card to move or swap it, the resize handle and its stops, the
/// dock as a drop zone, and the keyboard moves. The drawing of a gesture lives in <c>_dragLayer</c>, above the overlay and
/// never cleared by a rebuild of it; the pointer stays captured by the overlay (or the handle) until the gesture ends.
/// The page underneath is not changed until release, so nothing relayouts per pointer move.
/// </summary>
internal sealed partial class PagesSection
{
    private const double DragThreshold = 4;
    private const double ScrollEdge = 48;
    private const double ScrollMaxStep = 12;
    private const double HandleSize = 12;

    private static readonly SolidColorBrush Green = new(ColorHelper.FromArgb(255, 52, 199, 89));
    private static readonly SolidColorBrush Orange = new(ColorHelper.FromArgb(255, 255, 159, 10));

    private readonly Canvas _dragLayer = new() { IsHitTestVisible = false };
    private DragState? _drag;
    private ResizeState? _resize;
    private DispatcherQueueTimer? _scrollTimer;
    private DispatcherQueueTimer? _hintTimer;
    private Border? _hint;
    private Point _lastScrollPoint;

    private sealed class DragState
    {
        public required WidgetRef Ref { get; init; }
        public required Point StartDip { get; init; }
        public required Rect CardDip { get; init; }
        public required Point CellOriginSu { get; init; }
        public required uint PointerId { get; init; }
        public bool Active { get; set; }
        public Canvas? Lift { get; set; }
        public Rectangle? Source { get; set; }
        public Rectangle? Outline { get; set; }
        public Border? Label { get; set; }
        public Rectangle? Marker { get; set; }
        public Action? Commit { get; set; }
        public bool Ok { get; set; }
    }

    private sealed class ResizeState
    {
        public required WidgetRef Ref { get; init; }
        public required IReadOnlyList<ResizeStop> Stops { get; init; }
        public required int Col { get; init; }
        public required int Row { get; init; }
        public required int CurrentW { get; init; }
        public required int CurrentH { get; init; }
        public int Nearest { get; set; } = -1;
    }

    // ---------------- the drag of a card ----------------

    /// <summary>Called by the press on the preview after it selected <paramref name="hit"/>: a move of more than 4 DIP starts the drag.</summary>
    private void BeginPotentialDrag(WidgetRef hit, PointerRoutedEventArgs e)
    {
        if (!Editable || _preview.Session.Layout is not { } layout || RectOf(layout, hit) is not { } rect)
            return;
        double f = _factor;
        double cellSu = layout.Surface.Size.Width / (double)GridLayoutManager.Columns;
        var start = e.GetCurrentPoint(_overlay).Position;
        var origin = hit.InDock || PageEditing.Widget(Config, hit) is not { } widget
            ? new Point(rect.X, rect.Y)
            : new Point(PageEditing.Area(widget).Col * cellSu, PageEditing.Area(widget).Row * cellSu);
        _drag = new DragState
        {
            Ref = hit,
            StartDip = start,
            CardDip = new Rect(rect.X * f, rect.Y * f, rect.Width * f, rect.Height * f),
            CellOriginSu = origin,
            PointerId = e.Pointer.PointerId,
        };
        _overlay.CapturePointer(e.Pointer);
        _lastScrollPoint = e.GetCurrentPoint(_scroll).Position;
    }

    private void OnOverlayMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is not { } d || e.Pointer.PointerId != d.PointerId)
            return;
        _lastScrollPoint = e.GetCurrentPoint(_scroll).Position;
        if (!d.Active)
        {
            var p = e.GetCurrentPoint(_overlay).Position;
            if (Math.Abs(p.X - d.StartDip.X) < DragThreshold && Math.Abs(p.Y - d.StartDip.Y) < DragThreshold)
                return;
            StartDrag(d);
        }

        UpdateDrag();
    }

    private void OnOverlayReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is not { } d || e.Pointer.PointerId != d.PointerId)
            return;
        _drag = null;
        StopScrollTimer();
        _overlay.ReleasePointerCapture(e.Pointer);
        if (!d.Active)
            return;
        if (d.Ok)
        {
            ClearDragLayer();
            d.Commit?.Invoke();
        }
        else
        {
            AnimateBack(d);
        }
    }

    private void OnOverlayCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // The pointer left the window or the capture was taken: the gesture ends without a change.
        if (_drag != null)
            CancelDrag();
    }

    private void CancelDrag()
    {
        var d = _drag;
        _drag = null;
        _resize = null;
        StopScrollTimer();
        ClearDragLayer();
        if (d != null)
            _overlay.ReleasePointerCaptures();
    }

    private void StartDrag(DragState d)
    {
        d.Active = true;
        _focusTarget.Focus(FocusState.Programmatic);
        double f = _factor;
        var card = d.CardDip;
        d.Source = new Rectangle
        {
            Width = card.Width,
            Height = card.Height,
            Stroke = new SolidColorBrush(ColorHelper.FromArgb(200, 255, 255, 255)),
            StrokeDashArray = [4, 3],
            StrokeThickness = 1.5,
            RadiusX = 6,
            RadiusY = 6,
        };
        Canvas.SetLeft(d.Source, card.X);
        Canvas.SetTop(d.Source, card.Y);
        d.Outline = new Rectangle { StrokeThickness = 3, RadiusX = 8, RadiusY = 8, Visibility = Visibility.Collapsed };
        d.Marker = new Rectangle { Width = 4, Fill = Green, RadiusX = 2, RadiusY = 2, Visibility = Visibility.Collapsed };
        d.Label = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Visibility = Visibility.Collapsed,
            Child = new TextBlock { Foreground = new SolidColorBrush(Colors.Black), FontSize = 12 },
        };

        // The lifted card: the card's pixels from the preview's last frame, cropped; a tinted block until they are there.
        d.Lift = new Canvas
        {
            Width = card.Width,
            Height = card.Height,
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, card.Width, card.Height) },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform { ScaleX = 1.03, ScaleY = 1.03 },
            Opacity = 0.92,
        };
        d.Lift.Children.Add(new Rectangle
        {
            Width = card.Width,
            Height = card.Height,
            Fill = new SolidColorBrush(ColorHelper.FromArgb(150, 0, 120, 215)),
            RadiusX = 6,
            RadiusY = 6,
        });
        _dragLayer.Children.Add(d.Source);
        _dragLayer.Children.Add(d.Outline);
        _dragLayer.Children.Add(d.Marker);
        _dragLayer.Children.Add(d.Lift);
        _dragLayer.Children.Add(d.Label);
        _ = LiftImageAsync(d);

        _scrollTimer ??= _queue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(16);
        _scrollTimer.IsRepeating = true;
        _scrollTimer.Tick -= OnScrollTick;
        _scrollTimer.Tick += OnScrollTick;
        _scrollTimer.Start();
    }

    private async Task LiftImageAsync(DragState d)
    {
        try
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(_preview.Canvas);
            if (_drag != d || d.Lift == null)
                return;
            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Fill,
                Width = _preview.Canvas.ActualWidth,
                Height = _preview.Canvas.ActualHeight,
            };
            Canvas.SetLeft(image, -d.CardDip.X);
            Canvas.SetTop(image, -d.CardDip.Y);
            d.Lift.Children.Clear();
            d.Lift.Children.Add(image);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The tinted block stays.
        }
    }

    private void OnScrollTick(DispatcherQueueTimer sender, object args)
    {
        if (_drag is not { Active: true })
        {
            StopScrollTimer();
            return;
        }

        double height = _scroll.ViewportHeight;
        double y = _lastScrollPoint.Y;
        double step = 0;
        if (y < ScrollEdge)
            step = -ScrollMaxStep * Math.Min(1, (ScrollEdge - y) / ScrollEdge);
        else if (y > height - ScrollEdge)
            step = ScrollMaxStep * Math.Min(1, (y - (height - ScrollEdge)) / ScrollEdge);
        if (step != 0)
        {
            _scroll.ChangeView(null, Math.Clamp(_scroll.VerticalOffset + step, 0, _scroll.ScrollableHeight), null, disableAnimation: true);
            UpdateDrag();
        }
    }

    private void StopScrollTimer() => _scrollTimer?.Stop();

    private void UpdateDrag()
    {
        if (_drag is not { Active: true } d || d.Lift == null || _preview.Session.Layout is not { } layout || _pageId == null)
            return;
        var p = _scroll.TransformToVisual(_overlay).TransformPoint(_lastScrollPoint);
        double f = _factor;
        double px = p.X / f;
        double py = p.Y / f;
        double cellSu = layout.Surface.Size.Width / (double)GridLayoutManager.Columns;

        // The lifted card keeps the grab offset.
        Canvas.SetLeft(d.Lift, d.CardDip.X + (p.X - d.StartDip.X));
        Canvas.SetTop(d.Lift, d.CardDip.Y + (p.Y - d.StartDip.Y));

        var grid = GridSize.From(layout.Chrome);
        var band = layout.Chrome.Dock;
        bool inDock = band.Height > 0 && py >= band.Top && py < band.Bottom;
        var config = Config;
        bool ok = false;
        string text = "";
        Action? commit = null;
        (double X, double Y, double W, double H)? outline = null;
        int? markerIndex = null;

        if (d.Ref.InDock)
        {
            if (inDock)
            {
                int at = InsertionIndex(layout, px);
                int to = at > d.Ref.Index ? at - 1 : at;
                ok = true;
                markerIndex = at;
                if (to != d.Ref.Index)
                    commit = () => AfterMove(_editing.MoveInDock(d.Ref.Index, to));
            }
            else
            {
                int col = Math.Clamp((int)Math.Floor(px / cellSu), 0, grid.Columns - 1);
                int row = Math.Max(0, (int)Math.Floor(py / cellSu));
                var widgets = config.Pages[PageEditing.PageIndex(config, _pageId)].Widgets;
                ok = PageEditing.Fits(widgets, null, col, row, 1, 1, grid);
                text = ok ? "Release to move it to the page" : "No free space here";
                outline = (col * cellSu, row * cellSu, cellSu, cellSu);
                int slot = d.Ref.Index;
                string page = _pageId;
                commit = () => AfterMove(_editing.MoveToPage(slot, page, grid, new Cell(col, row)));
            }
        }
        else if (PageEditing.Widget(config, d.Ref) is { } widget)
        {
            if (inDock)
            {
                if (!_editing.SupportsDock(widget))
                    text = $"{_editing.TypeName(widget)} has no 1x1 size, so it cannot go in the dock";
                else if (config.Dock.Count >= DockLayout.MaxSlots)
                    text = "The dock is full";
                else
                {
                    int at = InsertionIndex(layout, px);
                    ok = true;
                    markerIndex = at;
                    text = "Release to move it to the dock";
                    commit = () => AfterMove(_editing.MoveToDock(d.Ref, at));
                }
            }
            else
            {
                int col = (int)Math.Round((px - (d.StartDip.X / f - d.CellOriginSu.X)) / cellSu);
                int row = (int)Math.Round((py - (d.StartDip.Y / f - d.CellOriginSu.Y)) / cellSu);
                var target = _editing.DropTarget(d.Ref, new Cell(col, row), grid);
                var (_, _, w, h) = PageEditing.Area(widget);
                ok = target.Kind != DropKind.Blocked;
                text = target.Message;
                outline = (target.Cell.Col * cellSu, target.Cell.Row * cellSu, w * cellSu, h * cellSu);
                commit = target.Kind switch
                {
                    DropKind.Free => () => AfterMove(_editing.MoveTo(d.Ref, target.Cell, w, h, grid)),
                    DropKind.Swap => () => AfterMove(_editing.Swap(d.Ref, target.Other!)),
                    _ => null,
                };
            }
        }

        d.Ok = ok;
        d.Commit = commit;
        var tone = ok ? Green : Orange;

        if (outline is { } o)
        {
            d.Outline!.Visibility = Visibility.Visible;
            d.Outline.Stroke = tone;
            d.Outline.Width = o.W * f;
            d.Outline.Height = o.H * f;
            Canvas.SetLeft(d.Outline, o.X * f);
            Canvas.SetTop(d.Outline, o.Y * f);
        }
        else
        {
            d.Outline!.Visibility = Visibility.Collapsed;
        }

        if (markerIndex is { } index && layout.Dock.Count > 0)
        {
            var first = layout.Dock[0].Item;
            double x = index < layout.Dock.Count ? layout.Dock[index].Item.CellPosition.X : layout.Dock[^1].Item.CellPosition.X + layout.Dock[^1].Item.CellSize.Width;
            d.Marker!.Visibility = Visibility.Visible;
            d.Marker.Height = first.CellSize.Height * f;
            Canvas.SetLeft(d.Marker, x * f - 2);
            Canvas.SetTop(d.Marker, first.CellPosition.Y * f);
        }
        else
        {
            d.Marker!.Visibility = Visibility.Collapsed;
        }

        if (text.Length > 0)
        {
            d.Label!.Visibility = Visibility.Visible;
            d.Label.Background = tone;
            ((TextBlock)d.Label.Child).Text = text;
            Canvas.SetLeft(d.Label, Math.Max(0, p.X + 12));
            Canvas.SetTop(d.Label, Math.Max(0, p.Y - 30));
        }
        else
        {
            d.Label!.Visibility = Visibility.Collapsed;
        }
    }

    private static int InsertionIndex(DeckLayout layout, double px) =>
        layout.Dock.Count(c => c.Item.Position.X + c.Item.Size.Width / 2.0 < px);

    private void AfterMove(EditOutcome outcome)
    {
        if (outcome.Applied && outcome.Widget != null)
        {
            _selection = WidgetSelection.Of(Config, outcome.Widget);
            _fit = null;
            Refresh();
        }
        else if (outcome.Message != null)
        {
            ShowHint(outcome.Message);
        }
    }

    private void AnimateBack(DragState d)
    {
        if (d.Lift == null)
        {
            ClearDragLayer();
            return;
        }

        d.Label!.Visibility = Visibility.Collapsed;
        d.Outline!.Visibility = Visibility.Collapsed;
        d.Marker!.Visibility = Visibility.Collapsed;
        var storyboard = new Storyboard();
        foreach (var (property, to) in new[] { ("(Canvas.Left)", d.CardDip.X), ("(Canvas.Top)", d.CardDip.Y) })
        {
            var animation = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(160), EnableDependentAnimation = true };
            Storyboard.SetTarget(animation, d.Lift);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        storyboard.Completed += (_, _) => ClearDragLayer();
        storyboard.Begin();
    }

    private void ClearDragLayer()
    {
        _dragLayer.Children.Clear();
        _hint = null;
    }

    // ---------------- hints and announcements ----------------

    /// <summary>Says <paramref name="text"/> to a screen reader and shows it for a moment beside the selection.</summary>
    private void Announce(string text)
    {
        if (FrameworkElementAutomationPeer.FromElement(_focusTarget) is { } peer)
            peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, text, "arrange");
        ShowHint(text);
    }

    private void ShowHint(string text)
    {
        if (_drag is { Active: true })
            return;
        var rect = _selection != null ? RectOf(_selection.Ref) : null;
        if (_hint != null)
            _dragLayer.Children.Remove(_hint);
        _hint = new Border
        {
            Background = Orange,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock { Text = text, Foreground = new SolidColorBrush(Colors.Black), FontSize = 12 },
        };
        Canvas.SetLeft(_hint, rect is { } r ? r.X * _factor : 8);
        Canvas.SetTop(_hint, rect is { } r2 ? Math.Max(0, r2.Y * _factor - 26) : 8);
        _dragLayer.Children.Add(_hint);
        _hintTimer ??= _queue.CreateTimer();
        _hintTimer.Interval = TimeSpan.FromSeconds(2.5);
        _hintTimer.IsRepeating = false;
        _hintTimer.Tick -= OnHintTick;
        _hintTimer.Tick += OnHintTick;
        _hintTimer.Start();
    }

    private void OnHintTick(DispatcherQueueTimer sender, object args)
    {
        if (_hint != null)
            _dragLayer.Children.Remove(_hint);
        _hint = null;
    }

    // ---------------- the resize handle ----------------

    /// <summary>The handle at the selected page widget's bottom-right corner (D3).</summary>
    private void AddResizeHandle(DeckLayout layout, WidgetSelection selection)
    {
        if (!Editable || selection.Ref.InDock || RectOf(layout, selection.Ref) is not { } rect)
            return;
        double f = _factor;
        var handle = new Rectangle
        {
            Width = HandleSize,
            Height = HandleSize,
            RadiusX = 3,
            RadiusY = 3,
            Fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1.5,
        };
        ToolTipService.SetToolTip(handle, "Drag to resize");
        AutomationProperties.SetName(handle, "Resize handle");
        Canvas.SetLeft(handle, rect.Right * f - HandleSize / 2 - 3);
        Canvas.SetTop(handle, rect.Bottom * f - HandleSize / 2 - 3);
        var reference = selection.Ref;
        handle.PointerPressed += (_, e) => BeginResize(handle, reference, e);
        handle.PointerMoved += (_, e) => UpdateResize(e);
        handle.PointerReleased += (_, e) => EndResize(handle, e);
        handle.PointerCaptureLost += (_, _) =>
        {
            if (_resize != null)
                CancelDrag();
        };
        _overlay.Children.Add(handle);
    }

    private void BeginResize(Rectangle handle, WidgetRef widget, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        if (PageEditing.Widget(Config, widget) is not { } config)
            return;
        var stops = _editing.ResizeStops(widget, CellGrid);
        if (stops.Count == 0)
            return;
        var (col, row, w, h) = PageEditing.Area(config);
        _resize = new ResizeState { Ref = widget, Stops = stops, Col = col, Row = row, CurrentW = w, CurrentH = h };
        handle.CapturePointer(e.Pointer);
        _focusTarget.Focus(FocusState.Programmatic);
        UpdateResize(e);
    }

    private void UpdateResize(PointerRoutedEventArgs e)
    {
        if (_resize is not { } r || _preview.Session.Layout is not { } layout)
            return;
        double f = _factor;
        double cellSu = layout.Surface.Size.Width / (double)GridLayoutManager.Columns;
        var p = e.GetCurrentPoint(_overlay).Position;
        double px = p.X / f;
        double py = p.Y / f;

        int nearest = -1;
        double best = double.MaxValue;
        for (int i = 0; i < r.Stops.Count; i++)
        {
            var (x, y, w, h) = StopRect(r, r.Stops[i], cellSu);
            double d = Math.Pow(px - (x + w), 2) + Math.Pow(py - (y + h), 2);
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }

        r.Nearest = nearest;
        _dragLayer.Children.Clear();
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        for (int i = 0; i < r.Stops.Count; i++)
        {
            var stop = r.Stops[i];
            var (x, y, w, h) = StopRect(r, stop, cellSu);
            bool snap = i == nearest;
            var brush = !stop.Fits ? Orange : snap ? accent : new SolidColorBrush(ColorHelper.FromArgb(200, 255, 255, 255));
            var box = new Rectangle
            {
                Width = w * f,
                Height = h * f,
                Stroke = brush,
                StrokeThickness = snap ? 3 : 1.5,
                StrokeDashArray = [4, 3],
                Fill = stop.Fits ? (snap ? new SolidColorBrush(ColorHelper.FromArgb(40, 0, 120, 215)) : null) : new SolidColorBrush(ColorHelper.FromArgb(50, 255, 159, 10)),
                RadiusX = 8,
                RadiusY = 8,
            };
            Canvas.SetLeft(box, x * f);
            Canvas.SetTop(box, y * f);
            _dragLayer.Children.Add(box);

            string detail = !stop.Fits ? (stop.PastBottom ? " · past the bottom" : $" · {stop.Blocker} is in the way") : snap ? " · snaps here" : "";
            var label = new Border
            {
                Background = brush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock
                {
                    Text = stop.Label + detail,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(!stop.Fits ? Colors.Black : Colors.White),
                    TextDecorations = stop.Fits ? Windows.UI.Text.TextDecorations.None : Windows.UI.Text.TextDecorations.Strikethrough,
                },
            };
            Canvas.SetLeft(label, x * f + 4);
            Canvas.SetTop(label, (y + h) * f - 22);
            _dragLayer.Children.Add(label);
        }
    }

    // A stop anchors at the widget's top-left; a column is moved left as far as Resize moves it.
    private static (double X, double Y, double W, double H) StopRect(ResizeState r, ResizeStop stop, double cellSu) =>
        (Math.Min(r.Col, GridLayoutManager.Columns - stop.Width) * cellSu, r.Row * cellSu, stop.Width * cellSu, stop.Height * cellSu);

    private void EndResize(Rectangle handle, PointerRoutedEventArgs e)
    {
        if (_resize is not { } r)
            return;
        _resize = null;
        handle.ReleasePointerCapture(e.Pointer);
        ClearDragLayer();
        if (r.Nearest >= 0 && r.Stops[r.Nearest] is { Fits: true } stop && (stop.Width != r.CurrentW || stop.Height != r.CurrentH))
            Resize(r.Ref, stop.Width, stop.Height);
    }

    // ---------------- the dock ----------------

    /// <summary>A "+" after the dock's last slot while the dock has a free one: it opens the palette with the types that have a 1x1 size.</summary>
    private void AddDockAffordance(DeckLayout layout, double f)
    {
        if (!Editable || layout.Dock.Count == 0 || layout.Dock.Count >= DockLayout.MaxSlots || layout.Chrome.Dock.Height <= 0)
            return;
        var last = layout.Dock[^1].Item;
        double side = last.CellSize.Width * f;
        var add = new Button
        {
            Content = "+",
            Width = Math.Max(20, side * 0.5),
            Height = Math.Max(20, side * 0.5),
            Padding = new Thickness(0),
            Opacity = 0.8,
            IsTabStop = false,
        };
        AutomationProperties.SetName(add, "Add a widget to the dock");
        ToolTipService.SetToolTip(add, "Add to the dock");
        Canvas.SetLeft(add, (last.CellPosition.X + last.CellSize.Width) * f + (side - add.Width) / 2);
        Canvas.SetTop(add, last.CellPosition.Y * f + (side - add.Height) / 2);
        add.Click += (_, _) => OpenPalette(null, dock: true);
        _overlay.Children.Add(add);
    }

    // ---------------- keyboard moves ----------------

    private static bool IsCtrlDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <summary>Ctrl+arrow: one cell in that direction when the widget fits there, else say what is in the way (D6).</summary>
    private void NudgeSelected(VirtualKey key)
    {
        if (!Editable || _selection is not { } selection || _pageId == null)
            return;
        var config = Config;
        if (selection.Ref.InDock)
        {
            int to = selection.Ref.Index + (key == VirtualKey.Left ? -1 : key == VirtualKey.Right ? 1 : 0);
            if (to == selection.Ref.Index)
                return;
            var outcome = _editing.MoveInDock(selection.Ref.Index, to);
            if (outcome.Applied)
            {
                AfterMove(outcome);
                Announce($"Moved to slot {to + 1} of {config.Dock.Count}");
            }
            else
            {
                Announce(to < 0 ? "It is the first slot" : "It is the last slot");
            }

            return;
        }

        if (PageEditing.Widget(config, selection.Ref) is not { } widget)
            return;
        var (col, row, w, h) = PageEditing.Area(widget);
        int nc = col + (key == VirtualKey.Left ? -1 : key == VirtualKey.Right ? 1 : 0);
        int nr = row + (key == VirtualKey.Up ? -1 : key == VirtualKey.Down ? 1 : 0);
        var grid = CellGrid;
        var widgets = config.Pages[PageEditing.PageIndex(config, _pageId)].Widgets;
        if (PageEditing.Fits(widgets, selection.Ref.Index, nc, nr, w, h, grid))
        {
            var outcome = _editing.MoveTo(selection.Ref, new Cell(nc, nr), w, h, grid);
            AfterMove(outcome);
            Announce($"Moved to column {nc + 1}, row {nr + 1}");
            return;
        }

        int blocker = nc < 0 || nr < 0 || nc + w > grid.Columns || nr + h > grid.Rows ? -1 : PageEditing.BlockerIndex(widgets, selection.Ref.Index, nc, nr, w, h);
        Announce(blocker >= 0 ? $"{_editing.Describe(widgets[blocker])} is in the way" : "That is the edge of the page");
    }
}
