// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Input;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The control tile drawn in a dark and a light built-in theme, compared with the pictures in <c>Goldens/</c>: a switch on, off,
/// neutral, display-only, pending, failed, unavailable and bare; a dimmer at 0, 40 and 100 and a 2x1 switch. Windows only:
/// FreeType and DirectWrite rasterise text differently. See CONTRIBUTING.md, "Golden images".
/// </summary>
public sealed class ControlGoldenTests
{
    private const int Width = 1100;
    private const int Height = 1100;

    private static readonly DateTime Moment = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    /// <summary>Readings with their descriptions, and actions that never end (<c>test:hang</c>) or fail (<c>test:fail</c>).</summary>
    private sealed class ControlHost : IWidgetHost, IReadingSource, IActionInvoker
    {
        private static readonly Dictionary<string, (Reading Reading, ReadingDescriptor Descriptor)> Table = Build();

        public IReadingSource Readings => this;

        public IActionInvoker Actions => this;

        public bool RegionUsesFahrenheit => false;

        public Reading Read(string id) => Table.TryGetValue(id, out var entry) ? entry.Reading : Reading.Unavailable("offline");

        public ReadingDescriptor? Describe(string id) => Table.TryGetValue(id, out var entry) ? entry.Descriptor : null;

        public Task<ActionResult> Invoke(string id, ReadingValue? argument = null) =>
            id == "test:fail" ? Task.FromResult(ActionResult.Failed("no such service")) : new TaskCompletionSource<ActionResult>().Task;

        private static Dictionary<string, (Reading, ReadingDescriptor)> Build()
        {
            var table = new Dictionary<string, (Reading, ReadingDescriptor)>();
            Add(table, "lamp", ReadingKind.OnOff, "Desk lamp", "home/light", true, "toggle");
            Add(table, "fan", ReadingKind.OnOff, "Bedroom fan", "home/fan", false, "toggle");
            Add(table, "door", ReadingKind.OnOff, "Back door", "home/door", false, null);
            Add(table, "level0", ReadingKind.Percent, "Hallway", "home/light", 0, "turn_on");
            Add(table, "level40", ReadingKind.Percent, "Desk", "home/light", 40, "turn_on");
            Add(table, "level100", ReadingKind.Percent, "Kitchen", "home/light", 100, "turn_on");
            Add(table, "lock", ReadingKind.Text, "Front door", "home/lock", "locked", "toggle");
            return table;
        }

        private static void Add(Dictionary<string, (Reading, ReadingDescriptor)> table, string path, ReadingKind kind, string label, string icon, ReadingValue value, string? action) =>
            table["t:" + path] = (Reading.Ok(value), new ReadingDescriptor(path, kind, label, label) { Icon = icon, Action = action == null ? null : "t:" + path + "/" + action });
    }

    private const string Type = "urdeck.widgets.control";
    private const char Chr = (char)39;
    private const char Quote = (char)34;

    private static SKBitmap Render(string theme)
    {
        var loaded = new ThemeStore("").Load(theme);
        var tiles = new[]
        {
            Tile(0, 0, 1, "'reading': 't:lamp', 'action': 't:lamp/toggle'"), // on
            Tile(1, 0, 1, "'reading': 't:fan', 'action': 't:fan/toggle'"), // off
            Tile(2, 0, 1, "'action': 't:scene/toggle', 'label': 'Movie night', 'icon': 'home/scene'"), // neutral
            Tile(3, 0, 1, "'reading': 't:door'"), // display-only
            Tile(0, 1, 2, "'reading': 't:lamp', 'style': 'dimmer', 'level': 't:level0'"),
            Tile(2, 1, 2, "'reading': 't:lamp', 'style': 'dimmer', 'level': 't:level40'"),
            Tile(0, 2, 2, "'reading': 't:lamp', 'style': 'dimmer', 'level': 't:level100'"),
            Tile(2, 2, 2, "'reading': 't:lock', 'action': 't:lock/toggle'"),
            Tile(0, 3, 1, "'reading': 't:lamp', 'action': 'test:hang'"), // pending
            Tile(1, 3, 1, "'reading': 't:lamp', 'action': 'test:fail'"), // failed
            Tile(2, 3, 1, "'reading': 't:missing', 'action': 't:missing/toggle', 'label': 'Offline'"), // unavailable
            Tile(3, 3, 1, "'reading': 't:fan', 'action': 't:fan/toggle', 'label': '', 'icon': ''"), // bare
        };

        var config = new UrDeckConfig
        {
            Theme = theme,
            Pager = new PagerConfig { Indicator = "off" },
            Pages = [new PageConfig { Name = "Control", Widgets = [.. tiles] }],
            Dock = [],
        };

        using var page = PreviewPage.Build(config, 0, false, new ControlHost());

        // Tap the pending and the failed tile, then the first paint is the look the tap leads to.
        foreach (int index in new[] { 8, 9 })
            ((ITapTarget)page.Widgets[index].Widget).OnTap(new SKPoint(1, 1));

        return PageRenderer.RenderToBitmap(Width, Height, page.Widgets, loaded, Moment);
    }

    private static WidgetConfig Tile(int col, int row, int width, string settings)
    {
        string json = $$"""{ "typeId": "{{Type}}", "col": {{col}}, "row": {{row}}, "width": {{width}}, "height": 1, {{settings.Replace(Chr, Quote)}} }""";
        return ConfigStore.Parse($$"""{ "pages": [ { "name": "p", "widgets": [ {{json}} ] } ] }""").Pages[0].Widgets[0];
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    public void Tiles(string theme)
    {
        using var bitmap = Render(theme);
        GoldenImage.Compare(bitmap, $"control-tiles-{theme}");
    }

    [Fact]
    public void TheTiles_RenderToTheSameBytesTwice()
    {
        using var first = Render("default-dark");
        using var second = Render("default-dark");

        Assert.True(first.Bytes.AsSpan().SequenceEqual(second.Bytes));
    }
}
