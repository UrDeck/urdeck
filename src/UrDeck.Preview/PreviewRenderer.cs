// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Preview;

/// <summary>Which background a preview is drawn over.</summary>
public enum PreviewBackgroundKind
{
    /// <summary>The configuration's <c>background</c> (absent is the theme's choice).</summary>
    Configuration,

    /// <summary>None: the theme's background colour.</summary>
    None,

    /// <summary>The theme's choice, as the deck makes it: its default background, else its still background, else its colour.</summary>
    Theme,

    /// <summary>A background by name: a built-in one, or a folder in the renderer's backgrounds directory.</summary>
    Named,
}

/// <summary>A preview's background.</summary>
public sealed record PreviewBackground(PreviewBackgroundKind Kind, string? Name = null)
{
    public static PreviewBackground Configuration { get; } = new(PreviewBackgroundKind.Configuration);

    public static PreviewBackground None { get; } = new(PreviewBackgroundKind.None);

    public static PreviewBackground Theme { get; } = new(PreviewBackgroundKind.Theme);

    public static PreviewBackground Named(string name) => new(PreviewBackgroundKind.Named, name);
}

/// <summary>One widget type drawn alone at one of its sizes, with a configuration (null for its default configuration).</summary>
public sealed record PreviewWidget(string TypeId, System.Drawing.Size Size, WidgetConfig? Config = null);

/// <summary>What a preview draws (preview-rendering, "What A Preview Is Made Of").</summary>
public sealed record PreviewRequest
{
    /// <summary>The configuration; null is the sample page (<see cref="PreviewConfig.Sample"/>).</summary>
    public UrDeckConfig? Config { get; init; }

    /// <summary>The page to draw, from 0; null is the configuration's active page.</summary>
    public int? Page { get; init; }

    /// <summary>The page as the deck shows it, with the dock and the page indicator; false draws the page alone.</summary>
    public bool WithDock { get; init; }

    /// <summary>One widget alone instead of a page; <see cref="Width"/> is then the width of the four-cell panel.</summary>
    public PreviewWidget? Widget { get; init; }

    /// <summary>The theme; null is the configuration's.</summary>
    public LoadedTheme? Theme { get; init; }

    public PreviewBackground Background { get; init; } = PreviewBackground.Configuration;

    /// <summary>The picture's width in pixels; for a widget alone, the panel's.</summary>
    public int Width { get; init; } = 1100;

    /// <summary>The picture's height in pixels; ignored for a widget alone, whose picture is its cells.</summary>
    public int Height { get; init; } = 1920;

    /// <summary>The moment drawn (local time); null is the sample page's (<see cref="PreviewConfig.SampleMoment"/>).</summary>
    public DateTime? Moment { get; init; }

    /// <summary>The motion level the deck would run at; the theme's ceiling still applies, as on the deck.</summary>
    public MotionLevel Motion { get; init; } = MotionLevel.Full;
}

/// <summary>A preview that cannot be drawn, with the reason: an unknown widget, a size it does not declare, a page that does not exist.</summary>
public sealed class PreviewException(string message) : Exception(message);

/// <summary>
/// Draws a preview (preview-rendering): a configuration's page, or one widget alone, in a theme over a background, with the
/// providers' sample data, laid out and drawn by the page renderer the deck's snapshots use. The readings, the time and the
/// icons come from the request alone, so the same request gives the same pixels; it runs on Windows and Linux.
/// </summary>
/// <param name="themes">Where a theme named by a configuration is found.</param>
/// <param name="shaders">Where a background named by a configuration or a request is found.</param>
/// <param name="appDirectory">Where a relative picture path of a background starts.</param>
public sealed class PreviewRenderer(ThemeStore themes, ShaderStore shaders, string appDirectory)
{
    private static readonly JsonSerializerOptions CloneOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>A renderer with the built-in themes and backgrounds and the folders next to the executable.</summary>
    public PreviewRenderer()
        : this(new ThemeStore(Path.Combine(AppContext.BaseDirectory, "themes")),
               new ShaderStore(Path.Combine(AppContext.BaseDirectory, "backgrounds")),
               AppContext.BaseDirectory)
    {
    }

    /// <summary>
    /// The longest history a preview fills before its first frame, in paints of the page (one per second of the theme's
    /// sparkline span).
    /// </summary>
    public const int MaxPreRollPaints = 600;

    /// <summary>The preview as one still picture: the background and an animated border drawn still. The caller owns the bitmap.</summary>
    public SKBitmap Render(PreviewRequest request)
    {
        SKBitmap? result = null;
        Draw(request, 1, 1, frames: false, (_, bitmap) => result = bitmap);
        return result!;
    }

    /// <summary>
    /// The preview as <paramref name="count"/> frames, frame <c>i</c> at the moment plus <c>i / fps</c> seconds: the widgets,
    /// the readings, a moving background and an animated border move as on the panel, and frame 0 equals
    /// <see cref="Render"/>. Each frame is handed to <paramref name="sink"/> and disposed when it returns.
    /// </summary>
    public void RenderFrames(PreviewRequest request, int count, double fps, Action<int, SKBitmap> sink)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (!(fps > 0) || !double.IsFinite(fps))
            throw new ArgumentOutOfRangeException(nameof(fps), fps, "the frame rate must be above zero");
        Draw(request, count, fps, frames: true, (i, bitmap) =>
        {
            using (bitmap)
                sink(i, bitmap);
        });
    }

    private void Draw(PreviewRequest request, int count, double fps, bool frames, Action<int, SKBitmap> sink)
    {
        var source = request.Config ?? PreviewConfig.Sample();
        var (config, pageIndex, cut) = Arrange(request, source);
        var theme = request.Theme ?? themes.Load(config.Theme);
        int width = request.Width;
        int height = cut is { } c ? c.Height : request.Height;
        if (width <= 0 || height <= 0)
            throw new PreviewException($"the picture's size {width}x{height} is not a size in pixels.");

        DateTime moment = request.Moment ?? PreviewConfig.SampleMoment;
        DateTime now = moment;
        using var hub = new ReadingHub(PreviewProviders.Registry(() => now, moment),
            regionUsesFahrenheit: false, regionUses24HourClock: true, sampling: ReadingHubSampling.Manual);
        using var page = PreviewPage.Build(config, pageIndex, request.WithDock && request.Widget == null, new PreviewHost(hub));
        hub.Subscribe(page.Subscriptions);

        var decision = MotionPolicy.ForSnapshot(MotionName(request.Motion), theme.Definition.Motion?.Level);
        var settings = Settings(request.Background, config);
        using var background = BackgroundPlan.Resolve(settings, theme, decision.User).BuildPrepared(shaders, appDirectory, width, height);

        PreRoll(page, theme, width, height, moment, value => now = value, hub);

        for (int i = 0; i < count; i++)
        {
            double elapsed = i / fps;
            now = moment + TimeSpan.FromSeconds(elapsed);
            hub.SampleNow();
            var bitmap = PageRenderer.RenderToBitmap(width, height, page.Widgets, theme, now, page.Chrome, page.Dock, background,
                frames ? new FrameTime(elapsed, decision.Effective) : null);
            sink(i, cut is { } area ? Cut(bitmap, area) : bitmap);
        }
    }

    /// <summary>
    /// The configuration to draw and which page, and for a widget alone the area its cells take: a page holding only the
    /// widget at the top left, as tall as the widget, on a panel four cells wide.
    /// </summary>
    private static (UrDeckConfig Config, int Page, System.Drawing.Size? Cut) Arrange(PreviewRequest request, UrDeckConfig source)
    {
        if (request.Widget is not { } alone)
        {
            if (!PageSelection.TryChoose(source.Pages.Count, source.ActivePage, request.Page, out int index, out string? error))
                throw new PreviewException(error!);
            return (source, index, null);
        }

        var descriptor = PreviewWidgets.Get(alone.TypeId)
            ?? throw new PreviewException($"the widget type '{alone.TypeId}' is not one the preview knows; it knows {string.Join(", ", PreviewWidgets.Descriptors.Select(d => d.Id))}.");
        if (!descriptor.SupportedSizes.Contains(alone.Size))
        {
            throw new PreviewException($"the {descriptor.Name.ToLowerInvariant()} widget ('{descriptor.Id}') has no size {alone.Size.Width}x{alone.Size.Height}; " +
                $"it declares {string.Join(", ", descriptor.SupportedSizes.Select(s => $"{s.Width}x{s.Height}"))}.");
        }

        var widget = alone.Config is { } given ? Clone(given) : PreviewWidgets.DefaultConfig(descriptor);
        widget.WidgetTypeId = descriptor.Id;
        widget.Col = 0;
        widget.Row = 0;
        widget.Width = alone.Size.Width;
        widget.Height = alone.Size.Height;
        widget.IsVisible = true;

        var config = new UrDeckConfig
        {
            Theme = source.Theme,
            Background = source.Background,
            Motion = source.Motion,
            Pager = new PagerConfig { Indicator = "off" },
            Pages = [new PageConfig { Name = descriptor.Name, Widgets = [widget] }],
            Dock = [],
        };

        double cell = request.Width / 4.0;
        var cut = new System.Drawing.Size((int)(alone.Size.Width * cell), (int)(alone.Size.Height * cell));
        return (config, 0, cut);
    }

    /// <summary>A copy of a widget's configuration, so placing it alone does not move the caller's.</summary>
    private static WidgetConfig Clone(WidgetConfig config) =>
        (WidgetConfig)JsonSerializer.Deserialize(JsonSerializer.Serialize(config, config.GetType(), CloneOptions), config.GetType(), CloneOptions)!;

    private static SKBitmap Cut(SKBitmap page, System.Drawing.Size area)
    {
        using (page)
        {
            using var subset = new SKBitmap();
            page.ExtractSubset(subset, SKRectI.Create(area.Width, area.Height));
            return subset.Copy();
        }
    }

    private static BackgroundSettings Settings(PreviewBackground background, UrDeckConfig config) => background.Kind switch
    {
        PreviewBackgroundKind.None => BackgroundSettings.Disabled,
        PreviewBackgroundKind.Theme => BackgroundSettings.None,
        PreviewBackgroundKind.Named => BackgroundSettings.Parse(JsonSerializer.SerializeToElement(new { type = "shader", name = background.Name })),
        _ => BackgroundSettings.Parse(config.Background),
    };

    private static string MotionName(MotionLevel level) => level switch
    {
        MotionLevel.Off => "off",
        MotionLevel.Reduced => "reduced",
        _ => "full",
    };

    /// <summary>
    /// A widget whose readings change keeps a history as it paints (a stats card's sparkline), so before the first frame the
    /// page is painted off-screen once a second over the theme's sparkline span, with the readings of each second, at the
    /// picture's size (a card decides from its size whether it has a sparkline). The last of these paints shows the first
    /// frame's readings a second early, so a gauge has finished easing towards them by the first frame. Nothing is painted
    /// when the theme has no sparkline.
    /// </summary>
    private static void PreRoll(PreviewPage page, LoadedTheme loaded, int width, int height, DateTime moment, Action<DateTime> setNow, ReadingHub hub)
    {
        int span;
        using (var theme = ThemeResolver.Resolve(loaded, width / 4f))
            span = Math.Min(theme.Gauge.Sparkline?.HistorySeconds ?? 0, MaxPreRollPaints);
        if (span <= 0)
            return;

        using var scratch = new SKBitmap(width, height);
        using var canvas = new SKCanvas(scratch);
        for (int second = -span; second <= 0; second++)
        {
            setNow(moment.AddSeconds(second));
            hub.SampleNow();
            PageRenderer.Render(canvas, width, height, page.Widgets, loaded, moment.AddSeconds(Math.Min(second, -1)), page.Chrome, page.Dock);
        }
    }
}
