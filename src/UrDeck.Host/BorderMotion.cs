// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;

namespace UrDeck.Host;

/// <summary>
/// The animated borders of one page (or the dock): one compositor sprite per card, each the border's ring cut from a moving
/// source by the compositor, so the card itself is never repainted for it. A <c>spin</c> turns a conic picture under the
/// ring; a <c>pulse</c> fades the sprite. The sprite is the card view's child visual, so a press scales it and a page slide
/// carries it. The animations run in steps of <see cref="StepsPerSecond"/> and only while the <see cref="MotionGate"/> lets
/// them: they start together, stop for a hidden deck or a display that is off, and start again from the beginning.
/// </summary>
internal sealed class BorderMotion : IDisposable
{
    // Shared with the frames the page renderer draws (BorderFrame), so a preview's border moves as this one does.
    internal const int StepsPerSecond = BorderFrame.StepsPerSecond;
    private const float PulseLow = BorderFrame.PulseLow;
    private const float ConicScale = BorderFrame.ConicScale;

    private readonly MotionGate _gate;
    private readonly string _name;
    private readonly List<Sprite> _sprites = [];
    private readonly List<IDisposable> _owned = [];
    private bool _running;

    public BorderMotion(MotionGate gate, string name)
    {
        _gate = gate;
        _name = name;
        _gate.MayRunChanged += OnMayRunChanged;
        _running = gate.MayRun;
    }

    /// <summary>One card's sprite, and what to animate on it.</summary>
    private sealed record Sprite(SpriteVisual Visual, CompositionSurfaceBrush? Conic, BorderAnimation Animation);

    /// <summary>
    /// Adds the sprite for <paramref name="view"/>, whose card is <paramref name="size"/> DIPs at <paramref name="scale"/> physical
    /// pixels per DIP, with the border of <paramref name="style"/> (the style with its border, not the one the card back uses).
    /// </summary>
    public void Attach(WidgetView view, ResolvedCardStyle style, System.Drawing.Size size, double scale)
    {
        var animation = style.BorderAnimation!;
        var px = new System.Drawing.Size(Math.Max(1, (int)Math.Round(size.Width * scale)), Math.Max(1, (int)Math.Round(size.Height * scale)));
        var ring = style.BorderRing(px);
        if (ring == null)
            return;

        var compositor = ElementCompositionPreview.GetElementVisual(view).Compositor;
        CompositionSurfaceBrush? conic = null;
        CompositionBrush source;
        if (style.BorderConicSource() is { } picture)
        {
            conic = Own(compositor.CreateSurfaceBrush(Surface(picture)));
            conic.Stretch = CompositionStretch.UniformToFill;
            conic.CenterPoint = new Vector2(size.Width / 2f, size.Height / 2f);
            conic.Scale = new Vector2(ConicScale, ConicScale);
            source = conic;
        }
        else
        {
            source = Own(PulseBrush(compositor, style.BorderBrush(px)!, scale));
        }

        var mask = Own(compositor.CreateMaskBrush());
        mask.Source = source;
        mask.Mask = RingBrush(compositor, ring, scale);

        var visual = Own(compositor.CreateSpriteVisual());
        visual.Brush = mask;
        visual.Size = new Vector2(size.Width, size.Height);
        ElementCompositionPreview.SetElementChildVisual(view, visual);
        _sprites.Add(new Sprite(visual, conic, animation));
    }

    private CompositionBrush RingBrush(Compositor compositor, BorderPicture ring, double scale)
    {
        var source = Own(compositor.CreateSurfaceBrush(Surface(ring)));
        source.Stretch = CompositionStretch.Fill;
        if (!ring.IsNineGrid)
            return source;

        // The insets are in the picture's pixels; the sprite is in DIPs.
        var nine = Own(compositor.CreateNineGridBrush());
        nine.Source = source;
        nine.SetInsets(ring.Inset);
        nine.SetInsetScales((float)(1 / scale));
        nine.IsCenterHollow = true;
        return nine;
    }

    /// <summary>A pulsing border that is not conic: its colour, or the linear gradient placed as the still border places it.</summary>
    private static CompositionBrush PulseBrush(Compositor compositor, BorderBrushSpec spec, double scale)
    {
        if (spec.IsSolid)
            return compositor.CreateColorBrush(ToColor(spec.Colors[0]));

        var gradient = compositor.CreateLinearGradientBrush();
        gradient.MappingMode = CompositionMappingMode.Absolute;
        gradient.StartPoint = new Vector2((float)(spec.Start.X / scale), (float)(spec.Start.Y / scale));
        gradient.EndPoint = new Vector2((float)(spec.End.X / scale), (float)(spec.End.Y / scale));
        for (int i = 0; i < spec.Colors.Length; i++)
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(i / (float)(spec.Colors.Length - 1), ToColor(spec.Colors[i])));
        return gradient;
    }

    private static Windows.UI.Color ToColor(SkiaSharp.SKColor c) => Windows.UI.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);

    /// <summary>The picture's decoded surface, which the picture keeps for every page and releases with the style.</summary>
    private static LoadedImageSurface Surface(BorderPicture picture)
    {
        if (picture.HostSurface is LoadedImageSurface surface)
            return surface;

        surface = LoadedImageSurface.StartLoadFromStream(new MemoryStream(picture.Png).AsRandomAccessStream());
        surface.LoadCompleted += (_, e) =>
        {
            if (e.Status != LoadedImageSourceLoadStatus.Success)
                UrDeckLog.Warn($"Border: a {picture.PixelWidth}x{picture.PixelHeight} {picture.Kind} picture did not load ({e.Status}); its cards have no animated border");
        };
        picture.HostSurface = surface;
        return surface;
    }

    private void OnMayRunChanged(bool mayRun, string reason)
    {
        if (mayRun == _running)
            return;
        _running = mayRun;
        foreach (var sprite in _sprites)
        {
            if (mayRun)
                Start(sprite);
            else
                Stop(sprite);
        }

        if (_sprites.Count > 0)
            Log(mayRun ? "started" : "stopped", reason);
    }

    private void Log(string what, string reason)
    {
        var first = _sprites[0].Animation;
        UrDeckLog.Info(string.Create(CultureInfo.InvariantCulture,
            $"Border animation {what} ({reason}): {first.Mode.ToString().ToLowerInvariant()}, {first.PeriodSeconds:0.##} s, {_sprites.Count} cards, page '{_name}'"));
    }

    /// <summary>Starts the animation of a card from its beginning; all of a page's start in the same call, so they turn in phase.</summary>
    private static void Start(Sprite sprite)
    {
        var compositor = sprite.Visual.Compositor;
        var period = TimeSpan.FromSeconds(sprite.Animation.PeriodSeconds);
        int steps = Math.Max(1, (int)Math.Round(sprite.Animation.PeriodSeconds * StepsPerSecond));
        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = period;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;

        if (sprite.Animation.Mode == BorderAnimationMode.Spin && sprite.Conic != null)
        {
            animation.InsertKeyFrame(0f, 0f);
            animation.InsertKeyFrame(1f, 360f, compositor.CreateStepEasingFunction(steps));
            sprite.Conic.StartAnimation("RotationAngleInDegrees", animation);
        }
        else
        {
            // Down and back up, each half in its own steps.
            int half = Math.Max(1, steps / 2);
            animation.InsertKeyFrame(0f, 1f);
            animation.InsertKeyFrame(0.5f, PulseLow, compositor.CreateStepEasingFunction(half));
            animation.InsertKeyFrame(1f, 1f, compositor.CreateStepEasingFunction(half));
            sprite.Visual.StartAnimation("Opacity", animation);
        }
    }

    /// <summary>Stops a card's animation and returns it to rest, where it looks like the still border.</summary>
    private static void Stop(Sprite sprite)
    {
        if (sprite.Animation.Mode == BorderAnimationMode.Spin && sprite.Conic != null)
        {
            sprite.Conic.StopAnimation("RotationAngleInDegrees");
            sprite.Conic.RotationAngleInDegrees = 0;
        }
        else
        {
            sprite.Visual.StopAnimation("Opacity");
            sprite.Visual.Opacity = 1;
        }
    }

    /// <summary>Starts every card's animation in the same call if the gate allows it; called once the page's cards are attached.</summary>
    public void Begin()
    {
        if (!_running || _sprites.Count == 0)
            return;
        foreach (var sprite in _sprites)
            Start(sprite);
        Log("started", "page rebuilt");
    }

    private T Own<T>(T item)
        where T : IDisposable
    {
        _owned.Add(item);
        return item;
    }

    public void Dispose()
    {
        _gate.MayRunChanged -= OnMayRunChanged;
        foreach (var sprite in _sprites)
            Stop(sprite);
        _sprites.Clear();
        foreach (var item in _owned)
            item.Dispose();
        _owned.Clear();
    }
}
