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
    private static readonly PropertyInfo[] Format2CardKeys = typeof(ThemeCardDefinition)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.IsDefined(typeof(Format2Attribute)))
        .ToArray();

    // The defaults of the published schema (docs/schemas/v2/theme.json).
    private const double DefaultCut = 0.14;
    private const double DefaultFillAngle = 90;
    private const double DefaultBorderAngle = 135;
    private const double DefaultHighlightThickness = 0.01;
    private const double DefaultSheenX = 0.1;
    private const double DefaultSheenY = 0;
    private const double DefaultSheenSize = 0.7;
    private const double DefaultStripThickness = 0.03;
    private const double DefaultStripLength = 0.72;
    private const double DefaultGlowAlpha = 0.6;
    private const double DefaultMarkInset = 0;

    // The schema gives no default for a mark's size and thickness.
    private const double DefaultMarkSize = 0.08;
    private const double DefaultMarkThickness = 0.01;

    private readonly Dictionary<(int Tint, int Strip, int Width, int Height), SizedCardChrome> _sized = [];
    private int _backImagesMade;
    private bool _disposed;

    private ResolvedCardStyle()
    {
    }

    /// <summary>
    /// True when the theme sets no card value of format 2: the card is drawn exactly as before card chrome existed, and its
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

    /// <summary>How many back images this style has drawn (tests check that one is made per size, not per paint).</summary>
    internal int BackImagesMade => Volatile.Read(ref _backImagesMade);

    /// <summary>
    /// Resolves the card values of <paramref name="loaded"/> for a surface whose grid cell is <paramref name="cellPx"/>
    /// physical pixels wide. The caller owns the result and must dispose it.
    /// </summary>
    public static ResolvedCardStyle Resolve(LoadedTheme loaded, float cellPx)
    {
        var d = loaded.Definition;
        var card = d.Card!;
        var colors = d.Colors!;
        bool plain = Format2CardKeys.All(p => p.GetValue(card) == null);

        float borderWidth = Px(card.BorderWidth, cellPx);
        if (!plain)
            borderWidth = Hairline(loaded, "card.borderWidth", borderWidth);

        var accent = SKColor.Parse(colors.Accent);

        return new ResolvedCardStyle
        {
            IsPlain = plain,
            Fill = SKColor.Parse(colors.CardFill),
            Border = SKColor.Parse(colors.CardBorder),
            Radius = Px(card.Radius, cellPx),
            BorderWidth = borderWidth,
            Chamfer = card.Shape == "chamfer",
            Cut = (float)(card.Cut ?? DefaultCut) * cellPx,
            CutCorners = Corners(card.CutCorners ?? ["tr", "bl"]),
            FillGradient = Parse(card.FillGradient?.Colors),
            FillAngle = (float)(card.FillGradient?.Angle ?? DefaultFillAngle),
            Dashed = card.BorderStyle == "dashed",
            HasBorderStyle = card.BorderStyle != "none",
            // Three and two border widths when the theme gives no dash.
            DashOn = card.BorderDash?.On is { } on ? (float)on * cellPx : 3 * borderWidth,
            DashOff = card.BorderDash?.Off is { } off ? (float)off * cellPx : 2 * borderWidth,
            BorderGradient = Parse(card.BorderGradient?.Colors),
            BorderConic = card.BorderGradient?.Kind == "conic",
            BorderAngle = (float)(card.BorderGradient?.Angle ?? DefaultBorderAngle),
            Tints = card.Tint == "cycle" ? Parse(colors.CardTints) : null,
            Highlight = ResolveHighlight(loaded, card.Highlight, cellPx),
            Sheen = card.Sheen is { Color: { } sheen } s
                ? new CardSheen(SKColor.Parse(sheen), (float)(s.X ?? DefaultSheenX), (float)(s.Y ?? DefaultSheenY), (float)(s.Size ?? DefaultSheenSize))
                : null,
            Strip = ResolveStrip(loaded, card.AccentStrip, accent, cellPx),
            Marks = ResolveMarks(loaded, card.CornerMarks, accent, cellPx),
        };
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
                _sized[key] = chrome = new SizedCardChrome(this, tint, strip, size);
                if (chrome.HasBackImage)
                    Interlocked.Increment(ref _backImagesMade);
            }
            return chrome;
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
        }
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
            loaded.Warnings.Once($"hairline:{key}",
                $"Theme '{loaded.Name}': {key} resolves to {px:0.##} px, under one pixel; it is drawn 1 px wide.");
        return Math.Max(1, MathF.Round(px, MidpointRounding.AwayFromZero));
    }

    private static SKColor? ParseOne(string? color) => color == null ? null : SKColor.Parse(color);

    private static SKColor[]? Parse(List<string>? colors) => colors is { Count: > 0 } ? colors.Select(SKColor.Parse).ToArray() : null;
}
