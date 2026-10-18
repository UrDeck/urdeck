// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>
/// What a theme draws outside the cards of one page or of the dock (shadow, glow, the outside part of the corner marks): one
/// compositor sprite per card under the widget surfaces, stretching the style's <see cref="UnderlayPicture"/>, nine-grid or
/// exact size. Made once when the page is built, never repainted; takes no input. Each sprite's scale and opacity follow its
/// card's visual, so a pressed card's shadow shrinks and dims with it.
/// </summary>
internal sealed class UnderlayView : Canvas, IDisposable
{
    private readonly List<IDisposable> _owned = [];

    private UnderlayView()
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// The underlay of <paramref name="cards"/> (positions in DIPs, each with its own style), at <paramref name="scale"/>
    /// physical pixels per DIP, cut to <paramref name="clip"/> when one is given; null when no card's style draws anything outside.
    /// </summary>
    public static UnderlayView? Create(
        IReadOnlyList<(WidgetLayoutItem Item, WidgetView View, ResolvedCardStyle Style)> cards,
        double scale,
        System.Drawing.Rectangle? clip,
        string name)
    {
        if (cards.Count == 0 || !cards.Any(c => c.Style.HasOutside))
            return null;
        long started = Stopwatch.GetTimestamp();
        var view = new UnderlayView();
        var compositor = ElementCompositionPreview.GetElementVisual(view).Compositor;
        var container = view.Own(compositor.CreateContainerVisual());
        if (clip is { } c)
            container.Clip = view.Own(compositor.CreateRectangleClip(c.Left, c.Top, c.Right, c.Bottom));

        var brushes = new Dictionary<UnderlayPicture, CompositionBrush>();
        var made = new List<UnderlayPicture>();
        foreach (var (item, card, style) in cards)
        {
            var px = new System.Drawing.Size(
                Math.Max(1, (int)Math.Round(item.Size.Width * scale)),
                Math.Max(1, (int)Math.Round(item.Size.Height * scale)));
            if (style.Underlay(px) is not { } picture)
                continue;
            if (!brushes.TryGetValue(picture, out var brush))
            {
                // A picture without a surface is new: an earlier page would have decoded it.
                if (picture.HostSurface == null)
                    made.Add(picture);
                brushes[picture] = brush = view.MakeBrush(compositor, picture, scale);
            }

            float bleed = (float)(picture.Bleed / scale);
            var sprite = view.Own(compositor.CreateSpriteVisual());
            sprite.Brush = brush;
            sprite.Offset = new Vector3(item.Position.X - bleed, item.Position.Y - bleed, 0);
            sprite.Size = new Vector2(item.Size.Width + 2 * bleed, item.Size.Height + 2 * bleed);
            sprite.CenterPoint = new Vector3(bleed + item.Size.Width / 2f, bleed + item.Size.Height / 2f, 0);
            view.Follow(compositor, sprite, ElementCompositionPreview.GetElementVisual(card));
            container.Children.InsertAtTop(sprite);
        }

        ElementCompositionPreview.SetElementChildVisual(view, container);
        string Describe(UnderlayPicture p) => string.Create(CultureInfo.InvariantCulture,
            $"{p.PixelWidth}x{p.PixelHeight} {(p.IsNineGrid ? "nine-grid" : "exact")}{(p.Scale < 1 ? " half size" : "")} {(made.Contains(p) ? $"made in {p.BuildTime.TotalMilliseconds:F1} ms" : "kept")}");
        string pictures = string.Join(", ", brushes.Keys.Select(Describe));
        UrDeckLog.Info(string.Create(CultureInfo.InvariantCulture,
            $"Underlay ({name}): {cards.Count} cards, bleed {cards.Max(c => c.Style.Bleed)} px, pictures [{pictures}], {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms"));
        return view;
    }

    /// <summary>
    /// The picture's brush: the decoded surface which the picture keeps for every page, stretched whole round a card of its
    /// size, or as a nine-grid with a hollow centre round any card at least twice the corner zone.
    /// </summary>
    private CompositionBrush MakeBrush(Compositor compositor, UnderlayPicture picture, double scale)
    {
        if (picture.HostSurface is not LoadedImageSurface surface)
        {
            surface = LoadedImageSurface.StartLoadFromStream(new MemoryStream(picture.Png).AsRandomAccessStream());
            surface.LoadCompleted += (_, e) =>
            {
                if (e.Status != LoadedImageSourceLoadStatus.Success)
                    UrDeckLog.Warn($"Underlay: a {picture.PixelWidth}x{picture.PixelHeight} picture did not load ({e.Status}); its cards have no shadow");
            };
            picture.HostSurface = surface;
        }

        var source = Own(compositor.CreateSurfaceBrush(surface));
        source.Stretch = CompositionStretch.Fill;
        if (!picture.IsNineGrid)
            return source;

        var nine = Own(compositor.CreateNineGridBrush());
        nine.Source = source;
        nine.SetInsets(picture.Inset);
        // The insets are in the picture's pixels; the sprite is in DIPs.
        nine.SetInsetScales((float)(1 / (picture.Scale * scale)));
        nine.IsCenterHollow = true;
        return nine;
    }

    /// <summary>The sprite takes the card's scale and opacity: whatever the card's visual does, the sprite does.</summary>
    private void Follow(Compositor compositor, SpriteVisual sprite, Visual card)
    {
        var scaling = Own(compositor.CreateExpressionAnimation("card.Scale"));
        scaling.SetReferenceParameter("card", card);
        sprite.StartAnimation("Scale", scaling);
        var fading = Own(compositor.CreateExpressionAnimation("card.Opacity"));
        fading.SetReferenceParameter("card", card);
        sprite.StartAnimation("Opacity", fading);
    }

    private T Own<T>(T item)
        where T : IDisposable
    {
        _owned.Add(item);
        return item;
    }

    public void Dispose()
    {
        ElementCompositionPreview.SetElementChildVisual(this, null);
        foreach (var item in _owned)
            item.Dispose();
        _owned.Clear();
    }
}
