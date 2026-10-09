// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;

namespace UrDeck.Widgets.Stats;

[Widget("Stats", "Shows readings from any data provider", Id = "urdeck.widgets.stats")]
[WidgetSize(1, 1)]
[WidgetSize(2, 2)]
[WidgetSize(4, 2)]
[WidgetSize(4, 4)]
[RefreshOnData]
[Category("System")]
public class StatsWidget : Widget<StatsConfig>
{
    private const string DefaultReading = "system:cpu/load";
    private const double EaseMilliseconds = 300;

    /// <summary>The text row's height in grid cells; the same at 4x2 and 4x4.</summary>
    private const float TextRowCells = 0.5f;

    private enum PositionKind { Single, Gauge, Text }

    /// <summary>What a slot asks of the gauge, before the theme and the reading have their say.</summary>
    private enum StyleRequest { Absent, Plain, ThemeDefault, Ring, Bar, VerticalBar }

    /// <summary>One place on the card and the slot (or the default reading, when null) that fills it.</summary>
    private readonly record struct Position(PositionKind Kind, int Cell, StatsSlot? Slot);

    /// <summary>What a paint puts on a position at rest; two equal values draw the same pixels.</summary>
    private readonly record struct Shown(ReadingText Text, string? Label, float? Fraction, GaugeLevel Level, string? Credit);

    /// <summary>The fill of one position moving from one fraction to another.</summary>
    private struct Ease
    {
        public float? From;
        public float? To;
        public DateTime Start;
        public float? Drawn;
    }

    private string[]? _subscriptions;
    private Position[]? _positions;
    private Shown[]? _drawn;
    private Ease[] _eases = [];
    private bool _animating;

    public override IReadOnlyCollection<string> Subscriptions =>
        _subscriptions ??= Positions.Select(p => ReadingId(p.Slot)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public override bool IsAnimating => _animating;

    private Position[] Positions => _positions ??= Compose();

    protected override void OnConfigured()
    {
        _subscriptions = null;
        _positions = null;
        _drawn = null;
        _eases = [];
        _animating = false;
    }

    public override bool NeedsRender(DateTime now)
    {
        if (_drawn is not { } drawn)
            return true;
        var positions = Positions;
        for (int i = 0; i < positions.Length; i++)
            if (drawn[i] != Current(positions[i]))
                return true;
        return false;
    }

    public override void Render(WidgetRenderContext context)
    {
        var theme = context.Theme;
        var positions = Positions;
        var shown = new Shown[positions.Length];
        var styles = new GaugeStyle[positions.Length];
        bool first = _drawn == null;
        if (_eases.Length != positions.Length)
            _eases = new Ease[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            shown[i] = Current(positions[i]);
            styles[i] = ResolveStyle(theme, positions[i], shown[i]);
            Advance(ref _eases[i], shown[i].Fraction, first, context.Time);
        }

        _drawn = shown;
        _animating = _eases.Any(e => e.To != null && e.Drawn != e.To);

        // A credit the data asks for is drawn once for the whole card, in a strip taken off the bottom of the content.
        var content = context.ContentRect;
        var credits = Credits(positions);
        float strip = Attribution.Measure(theme, credits, content.Width);
        if (strip > 0)
        {
            Attribution.Draw(context.Canvas, theme, SKRect.Create(content.Left, content.Bottom - strip, content.Width, strip), credits, HorizontalAlign.Center);
            content = new SKRect(content.Left, content.Top, content.Right, content.Bottom - strip - theme.Gap);
        }

        var rects = Layout(context, content, positions);
        float[] scales = ReadoutScales(theme, positions, rects, shown, styles);
        for (int i = 0; i < positions.Length; i++)
        {
            var options = new GaugeOptions
            {
                Label = string.IsNullOrEmpty(shown[i].Label) ? null : shown[i].Label,
                Fraction = _eases[i].Drawn,
                TargetFraction = shown[i].Fraction,
                Level = shown[i].Level,
                ReadoutScale = scales[i],
            };
            Gauge.Draw(context.Canvas, theme, rects[i], styles[i], shown[i].Text, options);
        }
    }

    /// <summary>Moves the drawn fraction of one position toward its target as of <paramref name="now"/>.</summary>
    private static void Advance(ref Ease ease, float? target, bool atRest, DateTime now)
    {
        if (atRest || target == null)
        {
            // The first paint shows the target as it is, and a reading without a value has no fill to move.
            ease = new Ease { From = target, To = target, Start = now, Drawn = target };
            return;
        }

        if (ease.To != target)
            ease = new Ease { From = ease.Drawn ?? 0f, To = target, Start = now, Drawn = ease.Drawn ?? 0f };

        double progress = Math.Clamp((now - ease.Start).TotalMilliseconds / EaseMilliseconds, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);
        ease.Drawn = progress >= 1 ? target : (float)(ease.From!.Value + (target.Value - ease.From.Value) * eased);
    }

    private Position[] Compose()
    {
        var slots = Config.Slots;
        var result = new List<Position>();
        (int gauges, int texts) = Config is { Width: >= 4, Height: >= 4 } ? (4, 3)
            : Config is { Width: >= 4, Height: >= 2 } ? (2, 3)
            : (0, 0);

        if (gauges == 0)
        {
            result.Add(new Position(PositionKind.Single, 0, slots.Count > 0 ? slots[0] : null));
            return [.. result];
        }

        // With no slot at all the first position still shows the total CPU load.
        if (slots.Count == 0)
            result.Add(new Position(PositionKind.Gauge, 0, null));
        for (int i = 0; i < Math.Min(slots.Count, gauges + texts); i++)
            result.Add(new Position(i < gauges ? PositionKind.Gauge : PositionKind.Text, i, slots[i]));
        return [.. result];
    }

    private static string ReadingId(StatsSlot? slot) =>
        string.IsNullOrWhiteSpace(slot?.Reading) ? DefaultReading : slot.Reading.Trim();

    private Shown Current(Position position)
    {
        var slot = position.Slot;
        string id = ReadingId(slot);
        var reading = Readings.Read(id);
        var descriptor = Readings.Describe(id);

        var options = new ReadingFormatOptions { Decimals = slot?.Decimals, DisplayUnit = ParseUnit(slot?.Unit) };
        var text = ReadingFormatter.Format(reading, descriptor, options, Readings);
        string? credit = descriptor?.Attribution?.Text;

        // No label set: the catalog's. A reading that does not exist has none, so name the id to make the mistake findable.
        string? label = slot?.Label
            ?? descriptor?.Label
            ?? (reading.State == ReadingState.Unavailable ? id : null);

        if (Request(position) == StyleRequest.Plain)
            return new Shown(text, label, null, GaugeLevel.Normal, credit);

        var scale = GaugeScale.Resolve(reading, descriptor, new GaugeScaleOptions
        {
            Min = slot?.Min,
            Max = slot?.Max,
            Warning = slot?.Warning,
            Critical = slot?.Critical,
            Decimals = slot?.Decimals,
        });
        return new Shown(text, label, scale.Fraction, scale.Level, credit);
    }

    /// <summary>The style a position ends up with. A text position is always plain.</summary>
    private static StyleRequest Request(Position position)
    {
        if (position.Kind == PositionKind.Text)
            return StyleRequest.Plain;
        return position.Slot?.Style?.Trim().ToLowerInvariant() switch
        {
            "plain" => StyleRequest.Plain,
            "gauge" => StyleRequest.ThemeDefault,
            "ring" => StyleRequest.Ring,
            "bar" => StyleRequest.Bar,
            "verticalbar" => StyleRequest.VerticalBar,
            _ => position.Kind == PositionKind.Single ? StyleRequest.Plain : StyleRequest.ThemeDefault,
        };
    }

    private static GaugeStyle ResolveStyle(Theme theme, Position position, Shown shown)
    {
        var style = Request(position) switch
        {
            StyleRequest.Ring => GaugeStyle.Ring,
            StyleRequest.Bar => GaugeStyle.Bar,
            StyleRequest.VerticalBar => GaugeStyle.VerticalBar,
            StyleRequest.ThemeDefault => theme.GaugeStyle,
            _ => GaugeStyle.Plain,
        };

        // A value with nothing to measure it against (a text, a number without a range) says more as plain text.
        bool hasValue = shown.Text.Value != ReadingFormatter.Dash;
        return style != GaugeStyle.Plain && hasValue && shown.Fraction == null ? GaugeStyle.Plain : style;
    }

    /// <summary>The attributions of the readings the card shows, taken from their descriptions.</summary>
    private List<ReadingAttribution> Credits(Position[] positions)
    {
        var credits = new List<ReadingAttribution>();
        foreach (var position in positions)
        {
            var credit = Readings.Describe(ReadingId(position.Slot))?.Attribution;
            if (credit != null)
                credits.Add(credit);
        }

        return credits;
    }

    private static SKRect[] Layout(WidgetRenderContext context, SKRect content, Position[] positions)
    {
        var rects = new SKRect[positions.Length];
        if (positions.Length == 0 || positions[0].Kind == PositionKind.Single)
        {
            Array.Fill(rects, content);
            return rects;
        }

        float gap = context.Theme.Gap;
        float cell = context.PixelSize.Width / (float)Math.Max(1, context.Config.Width);
        float textHeight = Math.Min(cell * TextRowCells, content.Height * 0.4f);
        bool four = context.Config.Height >= 4;
        int rows = four ? 2 : 1;
        float gaugeBottom = content.Bottom - textHeight - gap;
        float gaugeHeight = Math.Max(1f, (gaugeBottom - content.Top - gap * (rows - 1)) / rows);
        float gaugeWidth = Math.Max(1f, (content.Width - gap) / 2);
        float textWidth = Math.Max(1f, (content.Width - gap * 2) / 3);

        for (int i = 0; i < positions.Length; i++)
        {
            int cellIndex = positions[i].Cell;
            if (positions[i].Kind == PositionKind.Gauge)
            {
                float left = content.Left + (cellIndex % 2) * (gaugeWidth + gap);
                float top = content.Top + (cellIndex / 2) * (gaugeHeight + gap);
                rects[i] = SKRect.Create(left, top, gaugeWidth, gaugeHeight);
            }
            else
            {
                int column = cellIndex - (four ? 4 : 2);
                rects[i] = SKRect.Create(content.Left + column * (textWidth + gap), content.Bottom - textHeight, textWidth, textHeight);
            }
        }

        return rects;
    }

    /// <summary>One readout size for all gauge positions: each is drawn at the smallest fitted size among them.</summary>
    private static float[] ReadoutScales(Theme theme, Position[] positions, SKRect[] rects, Shown[] shown, GaugeStyle[] styles)
    {
        float[] scales = new float[positions.Length];
        Array.Fill(scales, 1f);
        float[] sizes = new float[positions.Length];
        float smallest = float.MaxValue;
        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i].Kind != PositionKind.Gauge)
                continue;
            sizes[i] = Gauge.Measure(theme, rects[i], styles[i], shown[i].Text,
                new GaugeOptions { Label = string.IsNullOrEmpty(shown[i].Label) ? null : shown[i].Label });
            if (sizes[i] > 0)
                smallest = Math.Min(smallest, sizes[i]);
        }

        for (int i = 0; i < positions.Length; i++)
            if (sizes[i] > 0 && smallest < float.MaxValue)
                scales[i] = Math.Clamp(smallest / sizes[i], 0.1f, 1f);
        return scales;
    }

    private static DisplayUnit? ParseUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "celsius" => DisplayUnit.Celsius,
        "fahrenheit" => DisplayUnit.Fahrenheit,
        _ => null,
    };
}
