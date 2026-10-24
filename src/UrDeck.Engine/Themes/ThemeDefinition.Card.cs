// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeCard(ThemeValueCheck check)
    {
        if (Card is not { } card)
            return;
        card.Radius = check.Number("card.radius", card.Radius, 0, 1);
        card.BorderWidth = check.Number("card.borderWidth", card.BorderWidth, 0, 1);
        card.Gap = check.Number("card.gap", card.Gap, 0, 1);
        card.Padding = check.Number("card.padding", card.Padding, 0, 1);
        card.Sanitize(check, "card");
    }
}

/// <summary>Card shape, as fractions of the grid cell.</summary>
public sealed partial class ThemeCardDefinition
{
    public double? Radius { get; set; }
    public double? BorderWidth { get; set; }
    public double? Gap { get; set; }
    public double? Padding { get; set; }

    /// <summary>'chamfer' cuts corners with straight lines (Ember). Default round.</summary>
    public string? Shape { get; set; }

    /// <summary>Chamfer size, fraction of a cell, capped at half the smaller side.</summary>
    public double? Cut { get; set; }

    /// <summary>Which corners are cut. Default [tr, bl].</summary>
    public List<string>? CutCorners { get; set; }

    /// <summary>Replaces cardFill with a gradient.</summary>
    public ThemeGradientDefinition? FillGradient { get; set; }

    /// <summary>Default solid.</summary>
    public string? BorderStyle { get; set; }

    /// <summary>Dash pattern for dashed/dotted.</summary>
    public ThemeCardBorderDashDefinition? BorderDash { get; set; }

    /// <summary>Gradient border (Rigline, Ember, Obsidian hairline).</summary>
    public ThemeCardBorderGradientDefinition? BorderGradient { get; set; }

    /// <summary>Animated border. Bounded by motion.level.</summary>
    public ThemeCardBorderAnimateDefinition? BorderAnimate { get; set; }

    /// <summary>Registration marks (Carbon, Blueprint). Drawn outside the clip, so needs a bleed margin.</summary>
    public ThemeCardCornerMarksDefinition? CornerMarks { get; set; }

    /// <summary>Drop shadow. Bleeds outside the card.</summary>
    public ThemeCardShadowDefinition? Shadow { get; set; }

    /// <summary>Outer glow. Bleeds outside the card.</summary>
    public ThemeGlowDefinition? Glow { get; set; }

    /// <summary>Inset edge light (Prism).</summary>
    public ThemeCardHighlightDefinition? Highlight { get; set; }

    /// <summary>A radial light patch inside the card (Prism).</summary>
    public ThemeCardSheenDefinition? Sheen { get; set; }

    /// <summary>A lit line along one edge of every card, inside the clip (Carbon scan line, Rigline tile strips).</summary>
    public ThemeCardAccentStripDefinition? AccentStrip { get; set; }

    /// <summary>'cycle' fills cards from colors.cardTints in page order.</summary>
    public string? Tint { get; set; }

    /// <summary>A partial override applied to cards of 1x1 cell or less and to dock slots.</summary>
    public ThemeCardCompactDefinition? Compact { get; set; }

    /// <summary>
    /// The card values of a compact card: a copy of this card with the compact radius, border width and (round or chamfer)
    /// shape, and the compact strip and marks merged key by key over this card's.
    /// </summary>
    /// <summary>A copy to change a value on (<see cref="ResolvedCardStyle.WithoutBorder"/>).</summary>
    internal ThemeCardDefinition MemberwiseCloneForBorder() => (ThemeCardDefinition)MemberwiseClone();

    internal ThemeCardDefinition ForCompact(ThemeCardCompactDefinition compact)
    {
        var result = (ThemeCardDefinition)MemberwiseClone();
        result.Compact = null;
        result.Radius = compact.Radius ?? Radius;
        result.BorderWidth = compact.BorderWidth ?? BorderWidth;
        if (compact.Shape is "round" or "chamfer")
            result.Shape = compact.Shape;
        if (compact.AccentStrip is { } strip)
            result.AccentStrip = new ThemeCardAccentStripDefinition
            {
                Side = strip.Side ?? AccentStrip?.Side,
                Thickness = strip.Thickness ?? AccentStrip?.Thickness,
                Length = strip.Length ?? AccentStrip?.Length,
                Colors = strip.Colors ?? AccentStrip?.Colors,
                Fade = AccentStrip?.Fade,
                Glow = strip.Glow ?? AccentStrip?.Glow,
            };
        if (compact.CornerMarks is { } marks)
            result.CornerMarks = new ThemeCardCornerMarksDefinition
            {
                Style = marks.Style ?? CornerMarks?.Style,
                Corners = marks.Corners ?? CornerMarks?.Corners,
                Size = marks.Size ?? CornerMarks?.Size,
                Thickness = marks.Thickness ?? CornerMarks?.Thickness,
                Inset = marks.Inset ?? CornerMarks?.Inset,
                Color = marks.Color ?? CornerMarks?.Color,
            };
        return result;
    }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Shape = check.Enum($"{path}.shape", Shape, "round", "chamfer");
        Cut = check.Number($"{path}.cut", Cut, 0, 0.5);
        CutCorners = check.NameList($"{path}.cutCorners", CutCorners, "tl", "tr", "br", "bl");
        if (FillGradient is { } fillGradientValue)
        {
            bool had = fillGradientValue.Colors is not null;
            fillGradientValue.Sanitize(check, $"{path}.fillGradient");
            if (fillGradientValue.Colors is null)
            {
                if (!had)
                    check.Missing($"{path}.fillGradient", "colors");
                FillGradient = null;
            }
        }
        BorderStyle = check.Enum($"{path}.borderStyle", BorderStyle, "solid", "dashed", "none");
        BorderDash?.Sanitize(check, $"{path}.borderDash");
        if (BorderGradient is { } borderGradientValue)
        {
            bool had = borderGradientValue.Colors is not null;
            borderGradientValue.Sanitize(check, $"{path}.borderGradient");
            if (borderGradientValue.Colors is null)
            {
                if (!had)
                    check.Missing($"{path}.borderGradient", "colors");
                BorderGradient = null;
            }
        }
        BorderAnimate?.Sanitize(check, $"{path}.borderAnimate");
        CornerMarks?.Sanitize(check, $"{path}.cornerMarks");
        Shadow?.Sanitize(check, $"{path}.shadow");
        Glow?.Sanitize(check, $"{path}.glow");
        Highlight?.Sanitize(check, $"{path}.highlight");
        Sheen?.Sanitize(check, $"{path}.sheen");
        AccentStrip?.Sanitize(check, $"{path}.accentStrip");
        Tint = check.Enum($"{path}.tint", Tint, "none", "cycle");
        Compact?.Sanitize(check, $"{path}.compact");
    }

    internal ThemeCardDefinition MergeOver(ThemeCardDefinition? b)
    {
        var result = new ThemeCardDefinition
        {
            Radius = Radius ?? b?.Radius,
            BorderWidth = BorderWidth ?? b?.BorderWidth,
            Gap = Gap ?? b?.Gap,
            Padding = Padding ?? b?.Padding,
        };
        result.Shape = Shape;
        result.Cut = Cut;
        result.CutCorners = CutCorners;
        result.FillGradient = FillGradient;
        result.BorderStyle = BorderStyle;
        result.BorderDash = BorderDash;
        result.BorderGradient = BorderGradient;
        result.BorderAnimate = BorderAnimate;
        result.CornerMarks = CornerMarks;
        result.Shadow = Shadow;
        result.Glow = Glow;
        result.Highlight = Highlight;
        result.Sheen = Sheen;
        result.AccentStrip = AccentStrip;
        result.Tint = Tint;
        result.Compact = Compact;
        return result;
    }
}

public sealed partial class ThemeCardBorderDashDefinition
{
    /// <summary>Dash length.</summary>
    public double? On { get; set; }

    /// <summary>Gap length.</summary>
    public double? Off { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        On = check.Number($"{path}.on", On, 0.002, 1);
        Off = check.Number($"{path}.off", Off, 0.002, 1);
    }
}

public sealed partial class ThemeCardBorderGradientDefinition
{
    /// <summary>Stops.</summary>
    public List<string>? Colors { get; set; }

    /// <summary>conic walks round the card, so it can rotate.</summary>
    public string? Kind { get; set; }

    /// <summary>Linear only.</summary>
    public double? Angle { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Colors = check.Colors($"{path}.colors", Colors, 2, 8);
        Kind = check.Enum($"{path}.kind", Kind, "linear", "conic");
        Angle = check.Angle($"{path}.angle", Angle, -360, 360);
    }
}

public sealed partial class ThemeCardBorderAnimateDefinition
{
    /// <summary>spin rotates a conic gradient; pulse breathes the alpha.</summary>
    public string? Mode { get; set; }

    /// <summary>Seconds per cycle.</summary>
    public double? Period { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Mode = check.Enum($"{path}.mode", Mode, "none", "spin", "pulse");
        Period = check.Number($"{path}.period", Period, 1, 120);
    }
}

public sealed partial class ThemeCardCornerMarksDefinition
{
    /// <summary>Shape of each mark.</summary>
    public string? Style { get; set; }

    /// <summary>Default [tl, br].</summary>
    public List<string>? Corners { get; set; }

    /// <summary>Arm length.</summary>
    public double? Size { get; set; }

    /// <summary>Line width.</summary>
    public double? Thickness { get; set; }

    /// <summary>Offset from the corner; negative sits outside the card.</summary>
    public double? Inset { get; set; }

    public string? Color { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Style = check.Enum($"{path}.style", Style, "none", "bracket", "tick");
        Corners = check.NameList($"{path}.corners", Corners, "tl", "tr", "br", "bl");
        Size = check.Number($"{path}.size", Size, 0.01, 0.5);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.002, 0.1);
        Inset = check.Number($"{path}.inset", Inset, -0.1, 0.1);
        Color = check.Color($"{path}.color", Color);
    }
}

public sealed partial class ThemeCardShadowDefinition
{
    /// <summary>Offset.</summary>
    public double? X { get; set; }

    /// <summary>Offset.</summary>
    public double? Y { get; set; }

    /// <summary>0 = hard offset (Pop).</summary>
    public double? Blur { get; set; }

    public double? Spread { get; set; }

    public string? Color { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        X = check.Number($"{path}.x", X, -0.5, 0.5);
        Y = check.Number($"{path}.y", Y, -0.5, 0.5);
        Blur = check.Number($"{path}.blur", Blur, 0, 1);
        Spread = check.Number($"{path}.spread", Spread, -0.5, 0.5);
        Color = check.Color($"{path}.color", Color);
    }
}

public sealed partial class ThemeCardHighlightDefinition
{
    /// <summary>Specular line along the top edge.</summary>
    public string? Top { get; set; }

    /// <summary>Rim light along the bottom, inside.</summary>
    public string? Bottom { get; set; }

    public double? Thickness { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Top = check.Color($"{path}.top", Top);
        Bottom = check.Color($"{path}.bottom", Bottom);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.001, 0.1);
    }
}

public sealed partial class ThemeCardSheenDefinition
{
    public string? Color { get; set; }

    /// <summary>Centre, fraction of card width.</summary>
    public double? X { get; set; }

    /// <summary>Centre, fraction of card height.</summary>
    public double? Y { get; set; }

    /// <summary>Radius, fraction of card width.</summary>
    public double? Size { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Color = check.Color($"{path}.color", Color);
        X = check.Number($"{path}.x", X, -1, 2);
        Y = check.Number($"{path}.y", Y, -1, 2);
        Size = check.Number($"{path}.size", Size, 0.1, 3);
    }
}

public sealed partial class ThemeCardAccentStripDefinition
{
    public string? Side { get; set; }

    public double? Thickness { get; set; }

    /// <summary>Fraction of the side.</summary>
    public double? Length { get; set; }

    /// <summary>Cycled by slot index (compact cards) or by card index. Default [accent].</summary>
    public List<string>? Colors { get; set; }

    /// <summary>Fade to transparent along the length (Carbon's scan line).</summary>
    public bool? Fade { get; set; }

    public ThemeGlowDefinition? Glow { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Side = check.Enum($"{path}.side", Side, "top", "bottom", "left", "right");
        Thickness = check.Number($"{path}.thickness", Thickness, 0.005, 0.2);
        Length = check.Number($"{path}.length", Length, 0.1, 1);
        Colors = check.Colors($"{path}.colors", Colors, 1, 8);
        Glow?.Sanitize(check, $"{path}.glow");
    }
}

public sealed partial class ThemeCardCompactDefinition
{
    public double? Radius { get; set; }

    public double? BorderWidth { get; set; }

    /// <summary>'circle' makes square cards round (Obsidian dock).</summary>
    public string? Shape { get; set; }

    /// <summary>A lit strip on compact cards (Rigline tiles). Overrides card.accentStrip for them.</summary>
    public ThemeCardCompactAccentStripDefinition? AccentStrip { get; set; }

    /// <summary>Registration marks on compact cards. Overrides card.cornerMarks for them.</summary>
    public ThemeCardCompactCornerMarksDefinition? CornerMarks { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Radius = check.Number($"{path}.radius", Radius, 0, 1);
        BorderWidth = check.Number($"{path}.borderWidth", BorderWidth, 0, 1);
        Shape = check.Enum($"{path}.shape", Shape, "round", "chamfer", "circle");
        AccentStrip?.Sanitize(check, $"{path}.accentStrip");
        CornerMarks?.Sanitize(check, $"{path}.cornerMarks");
    }
}

public sealed partial class ThemeCardCompactAccentStripDefinition
{
    public string? Side { get; set; }

    public double? Thickness { get; set; }

    /// <summary>Fraction of the side.</summary>
    public double? Length { get; set; }

    /// <summary>Cycled by slot index (compact cards) or by card index. Default [accent].</summary>
    public List<string>? Colors { get; set; }

    public ThemeGlowDefinition? Glow { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Side = check.Enum($"{path}.side", Side, "top", "bottom", "left", "right");
        Thickness = check.Number($"{path}.thickness", Thickness, 0.005, 0.2);
        Length = check.Number($"{path}.length", Length, 0.1, 1);
        Colors = check.Colors($"{path}.colors", Colors, 1, 8);
        Glow?.Sanitize(check, $"{path}.glow");
    }
}

public sealed partial class ThemeCardCompactCornerMarksDefinition
{
    /// <summary>Shape of each mark.</summary>
    public string? Style { get; set; }

    /// <summary>Default [tl, br].</summary>
    public List<string>? Corners { get; set; }

    /// <summary>Arm length.</summary>
    public double? Size { get; set; }

    /// <summary>Line width.</summary>
    public double? Thickness { get; set; }

    /// <summary>Offset from the corner; negative sits outside the card.</summary>
    public double? Inset { get; set; }

    public string? Color { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Style = check.Enum($"{path}.style", Style, "none", "bracket", "tick");
        Corners = check.NameList($"{path}.corners", Corners, "tl", "tr", "br", "bl");
        Size = check.Number($"{path}.size", Size, 0.01, 0.5);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.002, 0.1);
        Inset = check.Number($"{path}.inset", Inset, -0.1, 0.1);
        Color = check.Color($"{path}.color", Color);
    }
}
