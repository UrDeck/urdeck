// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using SkiaSharp;
using UrDeck.Engine.Rendering;

namespace UrDeck.Engine.Themes;

/// <summary>The corners of a card, for <c>card.cutCorners</c>.</summary>
[Flags]
public enum CardCorners
{
    None = 0,
    TopLeft = 1,
    TopRight = 2,
    BottomRight = 4,
    BottomLeft = 8,
}

/// <summary>
/// A loaded theme's card values in pixels and colours, resolved for one cell size as <see cref="Sdk.Theme"/> is, and owned by
/// the same caller: the host holds one for the page and one for the dock, and disposes them with their themes. It also
/// keeps what each card size needs (<see cref="SizedCardChrome"/>), so cards of one size share it across pages. Engine
/// only: the SDK does not see it.
/// </summary>
public sealed class ResolvedCardStyle : IDisposable
{
    // The card shape values; every other card value is card chrome, and a theme that sets none draws the plain card.
    private static readonly string[] CardShapeKeys = [nameof(ThemeCardDefinition.Radius), nameof(ThemeCardDefinition.BorderWidth), nameof(ThemeCardDefinition.Gap), nameof(ThemeCardDefinition.Padding)];

    private static readonly PropertyInfo[] CardChromeKeys = typeof(ThemeCardDefinition)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => !CardShapeKeys.Contains(p.Name))
        .ToArray();

    // The defaults of the published schema (docs/schemas/v1/theme.json).
    private const double DefaultCut = 0.14;
    private const double DefaultFillAngle = 90;
    private const double DefaultBorderAngle = 135;
    private const double DefaultBorderPeriod = 7;
    private const double DefaultHighlightThickness = 0.01;
    private const double DefaultSheenX = 0.1;
    private const double DefaultSheenY = 0;
    private const double DefaultSheenSize = 0.7;
    private const double DefaultStripThickness = 0.03;
    private const double DefaultStripLength = 0.72;
    private const double DefaultGlowAlpha = 0.6;
    private const double DefaultMarkInset = 0;
    private const double DefaultShadowOffset = 0;
    private const double DefaultShadowSpread = 0;

    // The schema gives no default for a shadow's colour and blur: black at 40 %, and a hard edge.
    private static readonly SKColor DefaultShadowColor = new(0, 0, 0, 0x66);

    // The schema gives no default for a mark's size and thickness.
    private const double DefaultMarkSize = 0.08;
    private const double DefaultMarkThickness = 0.01;

    private readonly Dictionary<(int Tint, int Strip, int Width, int Height), SizedCardChrome> _sized = [];

    // The compact variant when the theme sets card.compact; owned, and disposed with this style.
    private ResolvedCardStyle? _compact;
    private LoadedTheme _loaded = null!;
    private bool _circle;

    // What Build was given, so that WithoutBorder can build the same style again with no border.
    private ThemeCardDefinition _card = null!;
    private float _cellPx;
    private bool _isCompactBuild;
    private ResolvedCardStyle? _withoutBorder;

    // The pictures of a moving border: the conic source (one per style) and a ring per corner reach, cut and width.
    private BorderPicture? _conicSource;
    private readonly Dictionary<(bool Chamfer, int Reach, CardCorners Cut, int Width, int CardWidth, int CardHeight), BorderPicture> _rings = [];

    // A moving border drawn into a frame (Rendering.BorderFrame): the conic source as an image, and the ring at each card size.
    private SKImage? _frameConic;
    private readonly Dictionary<(int Width, int Height), SKImage> _frameRings = [];

    // The underlay pictures: the nine-grid one under (0, 0), an exact one under its card's size.
    private readonly Dictionary<(int Width, int Height), UnderlayPicture> _underlays = [];
    private int _backImagesMade;
    private bool _disposed;

    private ResolvedCardStyle()
    {
    }

    /// <summary>
    /// True when the theme sets no card chrome value: the card is drawn exactly as before card chrome existed, and its
    /// border width is not rounded.
    /// </summary>
    public bool IsPlain { get; private init; }

    internal SKColor Fill { get; private init; }

    internal SKColor Border { get; private init; }

    internal float Radius { get; private init; }

    /// <summary>The border width in pixels; for a style that is not plain a width of 2 pixels or less is whole.</summary>
    internal float BorderWidth { get; private init; }

    internal bool Chamfer { get; private init; }

    /// <summary>The chamfer's cut in pixels, before it is limited to half of a card's smaller side.</summary>
    internal float Cut { get; private init; }

    internal CardCorners CutCorners { get; private init; }

    internal SKColor[]? FillGradient { get; private init; }

    internal float FillAngle { get; private init; }

    internal bool Dashed { get; private init; }

    /// <summary>False for <c>borderStyle</c> <c>none</c>.</summary>
    internal bool HasBorderStyle { get; private init; } = true;

    internal float DashOn { get; private init; }

    internal float DashOff { get; private init; }

    internal SKColor[]? BorderGradient { get; private init; }

    internal bool BorderConic { get; private init; }

    internal float BorderAngle { get; private init; }

    /// <summary>
    /// How the host moves the border (<c>card.borderAnimate</c>); null for none: mode <c>none</c>, <c>spin</c> without a conic
    /// gradient, a dashed border, no border at all. The snapshot and a still picture draw the still border; a frame of a run
    /// draws it moving (<see cref="Rendering.BorderFrame"/>).
    /// </summary>
    public BorderAnimation? BorderAnimation { get; private init; }

    /// <summary>The colours of <c>tint: cycle</c>, or null when cards are not tinted.</summary>
    internal SKColor[]? Tints { get; private init; }

    /// <summary>The edge light, drawn over the content; null for none.</summary>
    internal CardHighlight? Highlight { get; private init; }

    /// <summary>The radial light over the fill; null for none.</summary>
    internal CardSheen? Sheen { get; private init; }

    /// <summary>The lit line along one edge; null for none.</summary>
    internal CardStrip? Strip { get; private init; }

    /// <summary>The corner marks; null for none.</summary>
    internal CardMarks? Marks { get; private init; }

    /// <summary>The shadow, its reach limited on translucent cards; null for none.</summary>
    internal CardShadow? Shadow { get; private init; }

    /// <summary>The glow, its reach limited on translucent cards; null for none.</summary>
    internal CardGlow? Glow { get; private init; }

    /// <summary>
    /// How many whole pixels past the card's edge the underlay draws (shadow, glow, the outside part of the marks); 0 when it
    /// draws nothing.
    /// </summary>
    public int Bleed { get; private init; }

    /// <summary>
    /// How far into the card along an edge the underlay still varies, in whole pixels: the corner's radius or cut, plus the
    /// blur's falloff and the offset of what is blurred, or a mark's arm. A nine-grid picture stretches what lies beyond it.
    /// </summary>
    public int CornerZone { get; private init; }

    /// <summary>True when the theme draws something outside its cards: the host then makes an underlay.</summary>
    public bool HasOutside => Bleed > 0;

    /// <summary>True when every piece of the underlay is blurred (no hard shadow, no marks): its pictures are made at half size.</summary>
    internal bool UnderlayIsSoft { get; private init; }

    /// <summary>How many underlay pictures this style has made (tests check that one is made per size class, not per page).</summary>
    internal int UnderlayPicturesMade { get; private set; }

    /// <summary>How many back images this style has drawn (tests check that one is made per size, not per paint).</summary>
    internal int BackImagesMade => Volatile.Read(ref _backImagesMade);

    /// <summary>
    /// Resolves the card values of <paramref name="loaded"/> for a surface whose grid cell is <paramref name="cellPx"/>
    /// physical pixels wide. The caller owns the result and must dispose it.
    /// </summary>
    public static ResolvedCardStyle Resolve(LoadedTheme loaded, float cellPx)
    {
        var card = loaded.Definition.Card!;
        var style = Build(loaded, cellPx, card, circle: false, compact: false);
        if (card.Compact is { } compact)
            style._compact = Build(loaded, cellPx, card.ForCompact(compact), compact.Shape == "circle", compact: true);
        return style;
    }

    /// <summary>
    /// The style of the cards that are compact (a dock slot, a page card of one cell): the theme's <c>card.compact</c> values
    /// over the page card's. This style itself when the theme sets none.
    /// </summary>
    public ResolvedCardStyle Compact => _compact ?? this;

    /// <summary>The style of a page card: <see cref="Compact"/> for a card of one grid cell, this style otherwise.</summary>
    public ResolvedCardStyle ForCard(Layout.WidgetLayoutItem item) =>
        item.GridSize.Width <= 1 && item.GridSize.Height <= 1 ? Compact : this;

    /// <summary>
    /// The same style with no border, for cards whose border the host draws as a moving sprite: its backs hold everything
    /// but the ring. Made on first use and owned by this style (the compact variant has its own). Never used by the snapshot
    /// or a still picture, which draw the still border, so the goldens do not depend on it; a frame of a run whose border moves
    /// draws its card from it (<see cref="Rendering.PageRenderer"/>).
    /// </summary>
    public ResolvedCardStyle WithoutBorder()
    {
        if (BorderWidth <= 0)
            return this;
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_withoutBorder == null)
            {
                var card = _card.MemberwiseCloneForBorder();
                card.BorderWidth = 0;
                _withoutBorder = Build(_loaded, _cellPx, card, _circle, _isCompactBuild);
            }
            return _withoutBorder;
        }
    }

    /// <summary>
    /// The conic gradient of an animated border, drawn once on a square for the compositor to turn (a spin) or to fade (a pulse);
    /// null unless this style's animated border is conic. Kept until the style is disposed, so every card and page shares it.
    /// </summary>
    public BorderPicture? BorderConicSource()
    {
        if (BorderAnimation == null || !BorderConic || BorderGradient == null)
            return null;
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _conicSource ??= BorderPicture.MakeConicSource(this);
        }
    }

    /// <summary>
    /// The ring a moving border is masked to for a card of <paramref name="cardPx"/> physical pixels: the border's shape
    /// drawn the way the still border draws it, to be stretched round the card as a nine-grid (<see cref="BorderPicture.Inset"/>).
    /// Cards whose corners reach the same distance share one; a card too small for its two insets (a circle, a card as small as
    /// its corners) has a ring drawn at its exact size. Null unless this style's border animates.
    /// </summary>
    public BorderPicture? BorderRing(System.Drawing.Size cardPx)
    {
        if (BorderAnimation == null || cardPx.Width <= 0 || cardPx.Height <= 0)
            return null;
        float reach = Math.Min(Chamfer ? Cut : Radius, Math.Min(cardPx.Width, cardPx.Height) / 2f);
        int inset = BorderPicture.InsetFor(this, reach);
        bool exact = cardPx.Width < 2 * inset || cardPx.Height < 2 * inset;
        var key = (Chamfer, (int)MathF.Round(reach * 100), Chamfer ? CutCorners : CardCorners.None, (int)MathF.Round(BorderWidth * 100),
            exact ? cardPx.Width : 0, exact ? cardPx.Height : 0);
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_rings.TryGetValue(key, out var ring))
                _rings[key] = ring = BorderPicture.MakeRing(this, reach, exact ? cardPx : null);
            return ring;
        }
    }

    /// <summary>
    /// The conic source of <see cref="BorderConicSource"/> as an image, for a frame drawn by the page renderer; null unless this
    /// style's animated border is conic. Kept until the style is disposed.
    /// </summary>
    internal SKImage? FrameConicImage()
    {
        if (BorderAnimation == null || !BorderConic || BorderGradient == null)
            return null;
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_frameConic == null)
            {
                using var bitmap = BorderPicture.DrawConicSource(this);
                _frameConic = SKImage.FromBitmap(bitmap);
            }

            return _frameConic;
        }
    }

    /// <summary>
    /// The ring of a moving border drawn at a card's exact size, in white, for a frame drawn by the page renderer: the shape
    /// <see cref="BorderRing"/> draws, without the nine-grid. Null unless this style's border animates. Kept until disposed.
    /// </summary>
    internal SKImage? FrameRingImage(System.Drawing.Size cardPx)
    {
        if (BorderAnimation == null || cardPx.Width <= 0 || cardPx.Height <= 0)
            return null;
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_frameRings.TryGetValue((cardPx.Width, cardPx.Height), out var ring))
            {
                float reach = Math.Min(Chamfer ? Cut : Radius, Math.Min(cardPx.Width, cardPx.Height) / 2f);
                using var bitmap = BorderPicture.DrawRing(this, reach, cardPx);
                _frameRings[(cardPx.Width, cardPx.Height)] = ring = SKImage.FromBitmap(bitmap);
            }

            return ring;
        }
    }

    /// <summary>
    /// The paint of a pulsing border's ring on a card of <paramref name="cardPx"/>: its colour, or the gradient between the
    /// points the still border places it with. Null for a style without an animated border, and for a conic one, which is
    /// <see cref="BorderConicSource"/>'s picture.
    /// </summary>
    public BorderBrushSpec? BorderBrush(System.Drawing.Size cardPx)
    {
        if (BorderAnimation == null || BorderConic)
            return null;
        if (BorderGradient is not { } colors)
            return new BorderBrushSpec([Border], default, default);
        var (start, end) = SizedCardChrome.LinearPoints(SKRect.Create(cardPx.Width, cardPx.Height), BorderAngle);
        return new BorderBrushSpec(colors, start, end);
    }

    private static ResolvedCardStyle Build(LoadedTheme loaded, float cellPx, ThemeCardDefinition card, bool circle, bool compact)
    {
        var d = loaded.Definition;
        var colors = d.Colors!;
        // The compact variant is never plain: the plain path draws with the SDK theme's radius, which is the page's.
        bool plain = !compact && CardChromeKeys.All(p => p.GetValue(card) == null);

        float borderWidth = Px(card.BorderWidth, cellPx);
        if (!plain)
            borderWidth = Hairline(loaded, "card.borderWidth", borderWidth);

        var accent = SKColor.Parse(colors.Accent);
        var fill = SKColor.Parse(colors.CardFill);
        var border = SKColor.Parse(colors.CardBorder);
        var fillGradient = Parse(card.FillGradient?.Colors);
        var tints = card.Tint == "cycle" ? Parse(colors.CardTints) : null;
        var marks = ResolveMarks(loaded, card.CornerMarks, accent, cellPx);
        // A circle is a radius of half a cell: a card is never larger, so its shape limits it to half its smaller side.
        float radius = circle ? cellPx / 2 : Px(card.Radius, cellPx);
        bool chamfer = !circle && card.Shape == "chamfer";
        float cut = (float)(card.Cut ?? DefaultCut) * cellPx;

        var shadow = ResolveShadow(card.Shadow, cellPx);
        var glow = ResolveGlow(card.Glow, border, cellPx);
        bool translucent = fillGradient != null
            ? fillGradient.Any(c => c.Alpha < 255)
            : fill.Alpha < 255 || (tints?.Any(c => c.Alpha < 255) ?? false);
        if (translucent)
            LimitReach(loaded, (float)card.Gap!.Value * cellPx, ref shadow, ref glow);
        var outsideMarks = marks is { ReachesOutside: true } ? marks : null;

        return new ResolvedCardStyle
        {
            IsPlain = plain,
            _loaded = loaded,
            _card = card,
            _cellPx = cellPx,
            _isCompactBuild = compact,
            _circle = circle,
            Fill = fill,
            Border = border,
            Radius = radius,
            BorderWidth = borderWidth,
            Chamfer = chamfer,
            Cut = cut,
            CutCorners = Corners(card.CutCorners ?? ["tr", "bl"]),
            FillGradient = fillGradient,
            FillAngle = (float)(card.FillGradient?.Angle ?? DefaultFillAngle),
            Dashed = card.BorderStyle == "dashed",
            HasBorderStyle = card.BorderStyle != "none",
            // Three and two border widths when the theme gives no dash.
            DashOn = card.BorderDash?.On is { } on ? (float)on * cellPx : 3 * borderWidth,
            DashOff = card.BorderDash?.Off is { } off ? (float)off * cellPx : 2 * borderWidth,
            BorderGradient = Parse(card.BorderGradient?.Colors),
            BorderConic = card.BorderGradient?.Kind == "conic",
            BorderAngle = (float)(card.BorderGradient?.Angle ?? DefaultBorderAngle),
            BorderAnimation = ResolveBorderAnimation(card, borderWidth),
            Tints = tints,
            Highlight = ResolveHighlight(loaded, card.Highlight, cellPx),
            Sheen = card.Sheen is { Color: { } sheen } s
                ? new CardSheen(SKColor.Parse(sheen), (float)(s.X ?? DefaultSheenX), (float)(s.Y ?? DefaultSheenY), (float)(s.Size ?? DefaultSheenSize))
                : null,
            Strip = ResolveStrip(loaded, card.AccentStrip, accent, cellPx),
            Marks = marks,
            Shadow = shadow,
            Glow = glow,
            Bleed = UnderlayBleed(shadow, glow, outsideMarks),
            CornerZone = UnderlayCornerZone(chamfer ? cut : radius, shadow, glow, outsideMarks),
            UnderlayIsSoft = (shadow != null || glow != null) && shadow is not { Blur: <= 0 } && outsideMarks == null,
        };
    }

    /// <summary>The animation of a border the host can move: a solid ring of some width (a nine-grid stretches a dash pattern).</summary>
    private static BorderAnimation? ResolveBorderAnimation(ThemeCardDefinition card, float borderWidth)
    {
        if (card.BorderAnimate is not { Mode: "spin" or "pulse" } animate || borderWidth <= 0 || card.BorderStyle is "none" or "dashed")
            return null;
        bool conic = card.BorderGradient?.Kind == "conic";
        if (animate.Mode == "spin" && !conic)
            return null;
        return new BorderAnimation(
            animate.Mode == "spin" ? BorderAnimationMode.Spin : BorderAnimationMode.Pulse,
            animate.Period ?? DefaultBorderPeriod);
    }

    private static CardShadow? ResolveShadow(ThemeCardShadowDefinition? shadow, float cellPx)
    {
        if (shadow == null)
            return null;
        var color = ParseOne(shadow.Color) ?? DefaultShadowColor;
        if (color.Alpha == 0)
            return null;
        return new CardShadow(
            (float)(shadow.X ?? DefaultShadowOffset) * cellPx,
            (float)(shadow.Y ?? DefaultShadowOffset) * cellPx,
            (float)(shadow.Blur ?? 0) * cellPx,
            (float)(shadow.Spread ?? DefaultShadowSpread) * cellPx,
            color);
    }

    /// <summary>A glow without blur draws nothing; its colour is the border's when absent, with the glow's alpha applied.</summary>
    private static CardGlow? ResolveGlow(ThemeGlowDefinition? glow, SKColor border, float cellPx)
    {
        float blur = (float)(glow?.Blur ?? 0) * cellPx;
        if (glow == null || blur <= 0)
            return null;
        var color = ParseOne(glow.Color) ?? border;
        color = color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(glow.Alpha ?? DefaultGlowAlpha, 0, 1)));
        return color.Alpha == 0 ? null : new CardGlow(blur, color);
    }

    /// <summary>
    /// On translucent cards a neighbour's shadow or glow would show through the fill: each is scaled down, offset and blur
    /// together, until it reaches no further than the gap. One information line per loaded theme.
    /// </summary>
    private static void LimitReach(LoadedTheme loaded, float gap, ref CardShadow? shadow, ref CardGlow? glow)
    {
        float before = MathF.Max(shadow?.Reach ?? 0, glow?.Reach ?? 0);
        if (before <= gap)
            return;
        if (shadow is { } s && s.Reach > gap)
        {
            // The spread is not scaled; the offset and the blur make up the rest of the gap.
            float spread = MathF.Min(s.Spread, gap);
            float scaled = MathF.Max(MathF.Abs(s.X), MathF.Abs(s.Y)) + s.Blur * 1.5f;
            float k = scaled > 0 ? Math.Clamp((gap - spread) / scaled, 0, 1) : 1;
            shadow = s with { X = s.X * k, Y = s.Y * k, Blur = s.Blur * k, Spread = spread };
        }
        if (glow is { } g && g.Reach > gap)
            glow = g with { Blur = g.Blur * gap / g.Reach };
        loaded.Warnings.Once("reach", new ThemeFinding(ThemeCodes.EffectReach, ThemeSeverity.Warning, "card.shadow",
            $"the cards are translucent, so the shadow and glow reach no further than the gap: " +
            $"{before:0} px limited to {gap:0} px.")
        { LoggedAsInfo = true });
    }

    private static int UnderlayBleed(CardShadow? shadow, CardGlow? glow, CardMarks? outsideMarks)
    {
        float bleed = 0;
        if (shadow != null)
            bleed = MathF.Max(bleed, shadow.Reach);
        if (glow != null)
            bleed = MathF.Max(bleed, glow.Reach);
        if (outsideMarks != null)
            bleed = MathF.Max(bleed, -outsideMarks.Inset + outsideMarks.Thickness);
        return (int)MathF.Ceiling(MathF.Max(0, bleed - 0.001f));
    }

    private static int UnderlayCornerZone(float corner, CardShadow? shadow, CardGlow? glow, CardMarks? outsideMarks)
    {
        if (shadow == null && glow == null && outsideMarks == null)
            return 0;
        float zone = corner;
        if (shadow != null)
            zone = MathF.Max(zone, corner + shadow.Blur * 1.5f + MathF.Max(MathF.Abs(shadow.X), MathF.Abs(shadow.Y)) + MathF.Max(0, shadow.Spread));
        if (glow != null)
            zone = MathF.Max(zone, corner + glow.Reach);
        if (outsideMarks != null)
            zone = MathF.Max(zone, outsideMarks.Size + outsideMarks.Thickness);
        return (int)MathF.Ceiling(zone - 0.001f);
    }

    private static CardHighlight? ResolveHighlight(LoadedTheme loaded, ThemeCardHighlightDefinition? highlight, float cellPx)
    {
        if (highlight == null || (highlight.Top == null && highlight.Bottom == null))
            return null;
        float thickness = Hairline(loaded, "card.highlight.thickness", (float)(highlight.Thickness ?? DefaultHighlightThickness) * cellPx);
        return new CardHighlight(ParseOne(highlight.Top), ParseOne(highlight.Bottom), thickness);
    }

    private static CardStrip? ResolveStrip(LoadedTheme loaded, ThemeCardAccentStripDefinition? strip, SKColor accent, float cellPx)
    {
        if (strip == null)
            return null;
        var glow = strip.Glow;
        return new CardStrip(
            strip.Side switch
            {
                "top" => CardSide.Top,
                "left" => CardSide.Left,
                "right" => CardSide.Right,
                _ => CardSide.Bottom,
            },
            Hairline(loaded, "card.accentStrip.thickness", (float)(strip.Thickness ?? DefaultStripThickness) * cellPx),
            (float)(strip.Length ?? DefaultStripLength),
            Parse(strip.Colors) ?? [accent],
            strip.Fade == true,
            (float)(glow?.Blur ?? 0) * cellPx,
            ParseOne(glow?.Color),
            (float)(glow?.Alpha ?? DefaultGlowAlpha));
    }

    private static CardMarks? ResolveMarks(LoadedTheme loaded, ThemeCardCornerMarksDefinition? marks, SKColor accent, float cellPx)
    {
        CornerMarkStyle? style = marks?.Style switch
        {
            "bracket" => CornerMarkStyle.Bracket,
            "tick" => CornerMarkStyle.Tick,
            _ => null,
        };
        if (style == null)
            return null;
        var corners = Corners(marks!.Corners ?? ["tl", "br"]);
        if (corners == CardCorners.None)
            return null;
        return new CardMarks(
            style.Value,
            corners,
            (float)(marks.Size ?? DefaultMarkSize) * cellPx,
            Hairline(loaded, "card.cornerMarks.thickness", (float)(marks.Thickness ?? DefaultMarkThickness) * cellPx),
            (float)(marks.Inset ?? DefaultMarkInset) * cellPx,
            ParseOne(marks.Color) ?? accent);
    }

    private static CardCorners Corners(IEnumerable<string> names)
    {
        var corners = CardCorners.None;
        foreach (string name in names)
            corners |= name switch
            {
                "tl" => CardCorners.TopLeft,
                "tr" => CardCorners.TopRight,
                "br" => CardCorners.BottomRight,
                "bl" => CardCorners.BottomLeft,
                _ => CardCorners.None,
            };
        return corners;
    }

    /// <summary>
    /// The tint a card takes at <paramref name="index"/> (its <see cref="Layout.CardOrder"/> place, or a dock slot's position),
    /// or -1 for the card fill: no tint, a fill gradient (which wins), or no index.
    /// </summary>
    internal int TintSlot(int index) =>
        FillGradient == null && Tints is { Length: > 0 } tints && index >= 0 ? index % tints.Length : -1;

    /// <summary>The fill colour of a card in <paramref name="tintSlot"/> (see <see cref="TintSlot"/>).</summary>
    internal SKColor FillFor(int tintSlot) => tintSlot >= 0 ? Tints![tintSlot] : Fill;

    /// <summary>What a card of this size and index needs, made on first use and kept until the style is disposed.</summary>
    internal SizedCardChrome For(int index, System.Drawing.Size size)
    {
        int tint = TintSlot(index);
        int strip = Strip?.Slot(index) ?? 0;
        var key = (tint, strip, size.Width, size.Height);
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sized.TryGetValue(key, out var chrome))
            {
                if (_circle && Math.Abs(size.Width - size.Height) > 1)
                    _loaded.Warnings.InfoOnce("capsule", ThemeCodes.CompactCapsule, "card.compact.shape",
                        $"card.compact.shape circle on a card of {size.Width}x{size.Height} px is a capsule; " +
                        "it has room for one information line.");
                _sized[key] = chrome = new SizedCardChrome(this, tint, strip, size);
                if (chrome.HasBackImage)
                    Interlocked.Increment(ref _backImagesMade);
            }
            return chrome;
        }
    }

    /// <summary>
    /// The underlay picture for a card of <paramref name="cardPx"/> physical pixels, or null when the style draws nothing
    /// outside its cards. Cards at least twice the corner zone in both directions share one nine-grid picture; a smaller
    /// card gets one at its exact size. Made on first use and kept until the style is disposed, so a page after the first
    /// finds its pictures made.
    /// </summary>
    public UnderlayPicture? Underlay(System.Drawing.Size cardPx)
    {
        if (!HasOutside || cardPx.Width <= 0 || cardPx.Height <= 0)
            return null;
        bool nineGrid = cardPx.Width >= 2 * CornerZone && cardPx.Height >= 2 * CornerZone;
        var key = nineGrid ? (0, 0) : (cardPx.Width, cardPx.Height);
        lock (_sized)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_underlays.TryGetValue(key, out var picture))
            {
                _underlays[key] = picture = nineGrid ? UnderlayPicture.MakeNineGrid(this) : UnderlayPicture.MakeExact(this, cardPx);
                UnderlayPicturesMade++;
            }
            return picture;
        }
    }

    public void Dispose()
    {
        lock (_sized)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var chrome in _sized.Values)
                chrome.Dispose();
            _sized.Clear();
            foreach (var picture in _underlays.Values)
                picture.Dispose();
            _underlays.Clear();
        }
        _conicSource?.Dispose();
        foreach (var ring in _rings.Values)
            ring.Dispose();
        _rings.Clear();
        _frameConic?.Dispose();
        foreach (var ring in _frameRings.Values)
            ring.Dispose();
        _frameRings.Clear();
        _withoutBorder?.Dispose();
        _compact?.Dispose();
    }

    private static float Px(double? fraction, float cellPx) => (float)fraction!.Value * cellPx;

    /// <summary>
    /// A line of 2 pixels or less is drawn at a whole number of pixels, never under 1, so it is crisp; one warning per key
    /// and loaded theme when it resolves under a pixel. Zero stays zero (no line).
    /// </summary>
    private static float Hairline(LoadedTheme loaded, string key, float px)
    {
        if (px <= 0 || px > 2)
            return px;
        if (px < 1)
            loaded.Warnings.Once($"hairline:{key}", ThemeCodes.Hairline, key,
                $"{key} resolves to {px:0.##} px, under one pixel; it is drawn 1 px wide.");
        return Math.Max(1, MathF.Round(px, MidpointRounding.AwayFromZero));
    }

    private static SKColor? ParseOne(string? color) => color == null ? null : SKColor.Parse(color);

    private static SKColor[]? Parse(List<string>? colors) => colors is { Count: > 0 } ? colors.Select(SKColor.Parse).ToArray() : null;
}
