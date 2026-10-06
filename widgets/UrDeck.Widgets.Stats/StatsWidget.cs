// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;

namespace UrDeck.Widgets.Stats;

[Widget("Stats", "Shows a reading from any data provider", Id = "urdeck.widgets.stats")]
[WidgetSize(1, 1)]
[WidgetSize(2, 2)]
[RefreshOnData]
[Category("System")]
public class StatsWidget : Widget<StatsConfig>
{
    private const string DefaultReading = "system:cpu/load";

    /// <summary>What a paint puts on the card; two equal values draw the same pixels.</summary>
    private readonly record struct Shown(ReadingText Text, string? Label);

    private string[]? _subscriptions;
    private Shown? _drawn;

    public override IReadOnlyCollection<string> Subscriptions => _subscriptions ??= [ReadingId];

    private StatsSlot? Slot => Config.Slots.Count > 0 ? Config.Slots[0] : null;

    private string ReadingId => string.IsNullOrWhiteSpace(Slot?.Reading) ? DefaultReading : Slot!.Reading.Trim();

    protected override void OnConfigured()
    {
        _subscriptions = null;
        _drawn = null;
    }

    public override bool NeedsRender(DateTime now) => _drawn is not { } drawn || drawn != Current();

    public override void Render(WidgetRenderContext context)
    {
        var shown = Current();
        _drawn = shown;
        var options = new ReadoutOptions { Label = string.IsNullOrEmpty(shown.Label) ? null : shown.Label };
        Readout.Draw(context.Canvas, context.Theme, context.ContentRect, shown.Text, options);
    }

    private Shown Current()
    {
        string id = ReadingId;
        var reading = Readings.Read(id);
        var descriptor = Readings.Describe(id);
        var slot = Slot;

        var options = new ReadingFormatOptions { Decimals = slot?.Decimals, DisplayUnit = ParseUnit(slot?.Unit) };
        var text = ReadingFormatter.Format(reading, descriptor, options, Readings);

        // No label set: the catalog's. A reading that does not exist has none, so name the id to make the mistake findable.
        string? label = slot?.Label
            ?? descriptor?.Label
            ?? (reading.State == ReadingState.Unavailable ? id : null);
        return new Shown(text, label);
    }

    private static DisplayUnit? ParseUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "celsius" => DisplayUnit.Celsius,
        "fahrenheit" => DisplayUnit.Fahrenheit,
        _ => null,
    };
}
