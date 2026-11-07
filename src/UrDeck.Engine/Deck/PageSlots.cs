// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Deck;

/// <summary>One placed card of a page: where it is (surface units), its slot, its style, and whether its border moves.</summary>
/// <param name="Index">The widget's index in the page's (or the dock's) list.</param>
/// <param name="Item">The card's and its cell's rectangles, in surface units (DIPs).</param>
/// <param name="Slot">The widget.</param>
/// <param name="Style">The card's style, with its border (a moving border's card back is painted without it).</param>
/// <param name="AnimatedBorder">True when the border is a moving sprite over the card (the full motion level and a moving theme border).</param>
public sealed record DeckCard(int Index, WidgetLayoutItem Item, WidgetSlot Slot, ResolvedCardStyle Style, bool AnimatedBorder);

/// <summary>What a page (or the dock) is built with; one per layout, shared by its pages.</summary>
/// <param name="Theme">The resolved theme the widgets draw with.</param>
/// <param name="CardStyle">The card styles of the theme at this size.</param>
/// <param name="Press">The theme's press values.</param>
/// <param name="Scale">Surface units to physical pixels.</param>
/// <param name="BorderMotion">The gate the moving borders obey; null below the full motion level (borders are drawn still).</param>
/// <param name="Motion">The motion level widgets are told.</param>
public sealed record PageBuild(
    Theme Theme,
    ResolvedCardStyle CardStyle,
    PressStyle Press,
    double Scale,
    MotionGate? BorderMotion,
    Func<UrDeck.Sdk.MotionLevel> Motion);

/// <summary>
/// The widgets of one page, or of the dock, without their surfaces: a <see cref="WidgetSlot"/> per placed widget, the
/// reading-to-slot map, the hit test, and what the presenter must draw around the cards (the underlay, the moving borders).
/// Disposing it releases every slot (readings unsubscribed, frame-source entries removed), so a page that is not shown
/// costs nothing. Deck thread only.
/// </summary>
public sealed class PageSlots : IDisposable
{
    private readonly List<DeckCard> _cards = [];
    private readonly Dictionary<string, List<WidgetSlot>> _slotsByReading = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, DeckCard> _cardsByIndex = [];
    private readonly IReadOnlyList<WidgetLayoutItem> _layout;

    /// <param name="page">The page (the dock is a page named "Dock").</param>
    /// <param name="layout">Where each widget of the page goes, by index.</param>
    /// <param name="cardIndex">Each widget's card number (the theme's per-card variations).</param>
    /// <param name="build">The theme, styles and motion of this layout.</param>
    /// <param name="widgets">Where the widgets come from.</param>
    /// <param name="readings">What their readings subscribe to.</param>
    /// <param name="scheduler">The deck's thread.</param>
    /// <param name="frames">The frames of animating widgets.</param>
    /// <param name="underlayClip">The dock's band, which its underlay is cut to; null for a page.</param>
    public PageSlots(
        PageConfig page,
        IReadOnlyList<WidgetLayoutItem> layout,
        IReadOnlyList<int> cardIndex,
        PageBuild build,
        IDeckWidgets widgets,
        IDeckReadings readings,
        IDeckScheduler scheduler,
        IFrameSource frames,
        System.Drawing.Rectangle? underlayClip = null)
    {
        Name = page.Name;
        Scale = build.Scale;
        BorderMotion = build.BorderMotion;
        UnderlayClip = underlayClip;
        _layout = layout;
        for (int i = 0; i < page.Widgets.Count; i++)
        {
            var config = page.Widgets[i];
            if (!config.IsVisible)
                continue;

            var item = layout[i];
            if (!item.Placed)
                continue;

            var style = build.CardStyle.ForCard(item);
            // A moving border is a sprite over the card, so its back is painted without the ring. Below the full motion
            // level there is no gate and the card is drawn as it is for a theme without borderAnimate.
            bool animated = build.BorderMotion != null && style.BorderAnimation != null;
            var descriptor = widgets.GetDescriptor(config.WidgetTypeId);
            if (descriptor == null)
            {
                UrDeckLog.Warn($"Widget type '{config.WidgetTypeId}' is not registered; leaving its cell empty. " +
                              $"Registered: [{string.Join(", ", widgets.GetRegisteredWidgetTypes())}]");
                continue;
            }

            try
            {
                var widget = widgets.CreateWidget(config, out var services)!;
                var slot = new WidgetSlot(widget, services, descriptor, build.Theme, new CardLook(animated ? style.WithoutBorder() : style, cardIndex[i]),
                    build.Press, scheduler, frames, readings, build.Motion);
                var card = new DeckCard(i, item, slot, style, animated);
                _cards.Add(card);
                _cardsByIndex[i] = card;
                foreach (string reading in slot.Subscriptions)
                {
                    if (!_slotsByReading.TryGetValue(reading, out var users))
                        _slotsByReading[reading] = users = [];
                    users.Add(slot);
                }

                UrDeckLog.Info($"Placed '{config.WidgetTypeId}' at grid ({config.Col},{config.Row}) {config.Width}x{config.Height} " +
                              $"-> {item.Position.X},{item.Position.Y} {item.Size.Width}x{item.Size.Height} DIPs");
            }
            catch (Exception ex)
            {
                UrDeckLog.Error($"Failed to create widget '{config.WidgetTypeId}'", ex);
            }
        }
    }

    /// <summary>The page's name (the dock's is "Dock"), for log lines.</summary>
    public string Name { get; }

    /// <summary>Surface units to physical pixels.</summary>
    public double Scale { get; }

    /// <summary>The gate the moving borders obey, when any card has one.</summary>
    public MotionGate? BorderMotion { get; }

    /// <summary>The rectangle the underlay is cut to (the dock's band, so it never lies over the page); null for a page.</summary>
    public System.Drawing.Rectangle? UnderlayClip { get; }

    /// <summary>The placed cards, in the page's order (the order they are drawn in).</summary>
    public IReadOnlyList<DeckCard> Cards => _cards;

    /// <summary>The layout of every widget of the page, placed or not.</summary>
    public IReadOnlyList<WidgetLayoutItem> Layout => _layout;

    /// <summary>How many widgets are shown.</summary>
    public int Count => _cards.Count;

    /// <summary>Whether a card's style draws outside its card (shadow, glow), so the page needs an underlay.</summary>
    public bool HasUnderlay => _cards.Any(c => c.Style.HasOutside);

    /// <summary>Whether a card has a moving border.</summary>
    public bool HasAnimatedBorders => _cards.Any(c => c.AnimatedBorder);

    /// <summary>The slots that declared <paramref name="reading"/>.</summary>
    public IReadOnlyList<WidgetSlot> SlotsFor(string reading) =>
        _slotsByReading.TryGetValue(reading, out var users) ? users : [];

    /// <summary>
    /// The card that contains the point (in the page's surface units), and the point relative to that card; null in a gap,
    /// on an empty cell and on a widget that could not be created.
    /// </summary>
    public DeckCard? Find(double x, double y, out System.Drawing.PointF local)
    {
        local = default;
        if (WidgetHitTest.Find(_layout, x, y) is not { } hit || !_cardsByIndex.TryGetValue(hit.Index, out var card))
            return null;
        local = new System.Drawing.PointF((float)hit.X, (float)hit.Y);
        return card;
    }

    public void Dispose()
    {
        foreach (var card in _cards)
            card.Slot.Dispose();
        _cards.Clear();
        _cardsByIndex.Clear();
        _slotsByReading.Clear();
    }
}
