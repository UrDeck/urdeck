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
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>
/// The theme's page pattern over the background and under the pages: compositor sprites in whole physical pixels, all painted
/// from one <see cref="PatternTile"/> block decoded once. Made when the theme or the window's size changes and never repainted;
/// takes no input and does not slide with the pages. It sits in the window's root grid between the background and the pages.
/// </summary>
internal sealed class PatternLayer : Canvas, IDisposable
{
    private readonly List<IDisposable> _owned = [];

    private PatternTile? _tile;
    private LoadedTheme? _theme;
    private System.Drawing.Size _windowPx;

    public PatternLayer()
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Shows the pattern of <paramref name="theme"/> for a window of <paramref name="screen"/> DIPs at <paramref name="scale"/>
    /// physical pixels per DIP. The same theme and window size does nothing; a pattern whose pixels are the same as the one
    /// shown keeps its sprites. A theme without a pattern removes it.
    /// </summary>
    public void Apply(LoadedTheme theme, System.Drawing.Size screen, double scale)
    {
        var windowPx = new System.Drawing.Size((int)Math.Round(screen.Width * scale), (int)Math.Round(screen.Height * scale));
        if (ReferenceEquals(theme, _theme) && windowPx == _windowPx)
            return;
        _theme = theme;
        _windowPx = windowPx;

        long started = Stopwatch.GetTimestamp();
        var tile = PatternTile.Create(theme, windowPx.Width / 4f, windowPx);
        if (tile != null && _tile != null && tile.Key == _tile.Key)
        {
            // The same pixels: what is shown stays, and so does its decoded surface.
            tile.Dispose();
            return;
        }

        bool hadPattern = _tile != null;
        Clear();
        if (tile == null)
        {
            if (hadPattern)
                UrDeckLog.Info("Pattern: none");
            return;
        }

        _tile = tile;
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        var container = Own(compositor.CreateContainerVisual());
        container.Clip = Own(compositor.CreateRectangleClip(0, 0, (float)(windowPx.Width / scale), (float)(windowPx.Height / scale)));

        var surface = LoadedImageSurface.StartLoadFromStream(new MemoryStream(tile.Png).AsRandomAccessStream());
        surface.LoadCompleted += (_, e) =>
        {
            if (e.Status != LoadedImageSourceLoadStatus.Success)
                UrDeckLog.Warn($"Pattern: the {tile.PixelWidth}x{tile.PixelHeight} block did not load ({e.Status}); the deck goes on without it");
        };
        tile.HostSurface = surface;
        var brush = Own(compositor.CreateSurfaceBrush(surface));
        brush.Stretch = CompositionStretch.Fill;
        brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;

        // Each tile starts on a whole physical pixel: blockPx / scale DIPs, at k times that.
        var (columns, rows) = tile.TileCount(windowPx);
        var size = new Vector2((float)(tile.PixelWidth / scale), (float)(tile.PixelHeight / scale));
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var sprite = Own(compositor.CreateSpriteVisual());
                sprite.Brush = brush;
                sprite.Size = size;
                sprite.Offset = new Vector3((float)(column * tile.PixelWidth / scale), (float)(row * tile.PixelHeight / scale), 0);
                container.Children.InsertAtTop(sprite);
            }
        }

        ElementCompositionPreview.SetElementChildVisual(this, container);
        UrDeckLog.Info(string.Create(CultureInfo.InvariantCulture,
            $"Pattern: {tile.Kind}, repeat {tile.RepeatX}x{tile.RepeatY} px, block {tile.PixelWidth}x{tile.PixelHeight} px, {columns}x{rows} tiles, made in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms"));
    }

    /// <summary>Drops the pattern with the deck's page; the next <see cref="Apply"/> makes it again.</summary>
    public void Release()
    {
        Clear();
        _theme = null;
        _windowPx = default;
    }

    public void Dispose() => Release();

    private void Clear()
    {
        ElementCompositionPreview.SetElementChildVisual(this, null);
        foreach (var item in _owned)
            item.Dispose();
        _owned.Clear();
        _tile?.Dispose();
        _tile = null;
    }

    private T Own<T>(T item)
        where T : IDisposable
    {
        _owned.Add(item);
        return item;
    }
}
