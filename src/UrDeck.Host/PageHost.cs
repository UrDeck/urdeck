// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>
/// The surfaces of one page, or of the dock (<see cref="PageSlots"/>): a canvas of <see cref="WidgetView"/>s, under them the
/// <see cref="UnderlayView"/> when the theme draws outside its cards, and the moving borders' sprites. Disposing it drops
/// every surface; the page's slots are the session's and are disposed by it right after.
/// </summary>
internal sealed class PageHost : Canvas, IDisposable
{
    private readonly List<WidgetView> _views = new();
    private readonly UnderlayView? _underlay;
    private readonly BorderMotion? _borders;

    public PageHost(PageSlots page)
    {
        var placed = new List<(WidgetLayoutItem, WidgetView, ResolvedCardStyle)>();
        foreach (var card in page.Cards)
        {
            var item = card.Item;
            var view = new WidgetView(card.Slot)
            {
                Width = item.Size.Width,
                Height = item.Size.Height,
            };
            SetLeft(view, item.Position.X);
            SetTop(view, item.Position.Y);
            Children.Add(view);
            if (card.AnimatedBorder)
            {
                _borders ??= new BorderMotion(page.BorderMotion!, page.Name);
                _borders.Attach(view, card.Style, item.Size, page.Scale);
            }

            _views.Add(view);
            placed.Add((item, view, card.Style));
        }

        // First, so it lies under every card; the dock's is cut to its band, so it never lies over the page.
        _underlay = page.HasUnderlay ? UnderlayView.Create(placed, page.Scale, page.UnderlayClip, page.Name) : null;
        if (_underlay != null)
            Children.Insert(0, _underlay);

        _borders?.Begin();
    }

    public void Dispose()
    {
        foreach (var view in _views)
            view.Dispose();
        _views.Clear();
        _borders?.Dispose();
        _underlay?.Dispose();
        Children.Clear();
    }
}
