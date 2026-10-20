// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.Clock.Config;
using UrDeck.Widgets.Shortcut;
using UrDeck.Widgets.Stats;
using UrDeck.Widgets.Weather;

namespace UrDeck.Preview;

/// <summary>
/// The page every picture of a theme starts from: a clock (4x1), two stats cards (2x1: CPU, GPU), a weather card (4x2) and
/// four shortcuts, with fixed readings and a fixed time, so rendering it twice gives the same bytes. The shortcuts have no
/// icon yet, so they draw their letter tile.
/// </summary>
public sealed class SamplePage : IDisposable
{
    /// <summary>13 October 2026, 21:47:32.</summary>
    public static readonly DateTime Time = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    private readonly Host _host = new();
    private readonly List<IWidget> _owned = [];

    private SamplePage()
    {
    }

    /// <summary>The page's widgets with their configurations, in layout order.</summary>
    public List<(WidgetConfig Config, IWidget Widget)> Widgets { get; } = [];

    /// <summary>Four shortcuts for the dock.</summary>
    public List<(WidgetConfig Config, IWidget? Widget)> Dock { get; } = [];

    public static SamplePage Build()
    {
        var page = new SamplePage();
        page.Fill();
        return page;
    }

    public void Dispose()
    {
        foreach (var widget in _owned)
            (widget as IDisposable)?.Dispose();
    }

    private void Fill()
    {
        const string place = "Portland, OR";

        var clock = new ClockWidget();
        var clockConfig = new ClockConfig { WidgetTypeId = "urdeck.widgets.clock", Col = 0, Row = 0, Width = 4, Height = 1 };
        clock.Configure(clockConfig);
        Add(clockConfig, clock);

        _host.Describe("system:cpu/load", ReadingKind.Percent, "CPU", 0, 100);
        _host.Describe("system:gpu/load", ReadingKind.Percent, "GPU", 0, 100);
        _host.Set("system:cpu/load", Reading.Ok(68));
        _host.Set("system:gpu/load", Reading.Ok(82));

        // A 2x1 stats card shows one reading, so CPU and GPU are two cards side by side.
        foreach ((string reading, int col) in new[] { ("system:cpu/load", 0), ("system:gpu/load", 2) })
        {
            var stats = new StatsWidget();
            stats.Attach(_host);
            var statsConfig = new StatsConfig
            {
                WidgetTypeId = "urdeck.widgets.stats",
                Col = col,
                Row = 1,
                Width = 2,
                Height = 1,
                Slots = [new StatsSlot { Reading = reading }],
            };
            stats.Configure(statsConfig);
            Add(statsConfig, stats);
        }

        string weatherId = "weather:" + place + "/";
        foreach ((string path, ReadingKind kind, string label) in new[]
        {
            ("place", ReadingKind.Text, "Place"), ("current/temperature", ReadingKind.Temperature, "Now"),
            ("current/condition", ReadingKind.Text, "Condition"), ("current/is-day", ReadingKind.OnOff, "Daytime"),
            ("daily/0/high", ReadingKind.Temperature, "High"), ("daily/0/low", ReadingKind.Temperature, "Low"),
            ("daily/0/sunrise", ReadingKind.Time, "Sunrise"), ("daily/0/sunset", ReadingKind.Time, "Sunset"),
        })
        {
            _host.Describe(weatherId + path, kind, label, kind == ReadingKind.Temperature ? -60 : null, kind == ReadingKind.Temperature ? 60 : null);
        }

        _host.Set(weatherId + "place", Reading.Ok(place));
        _host.Set(weatherId + "current/temperature", Reading.Ok(18));
        _host.Set(weatherId + "current/condition", Reading.Ok("partly-cloudy"));
        _host.Set(weatherId + "current/is-day", Reading.Ok(false));
        _host.Set(weatherId + "daily/0/high", Reading.Ok(21));
        _host.Set(weatherId + "daily/0/low", Reading.Ok(14));
        _host.Set(weatherId + "daily/0/sunrise", Reading.Ok(new DateTimeOffset(2026, 10, 13, 7, 14, 0, TimeSpan.FromHours(-7))));
        _host.Set(weatherId + "daily/0/sunset", Reading.Ok(new DateTimeOffset(2026, 10, 13, 18, 21, 0, TimeSpan.FromHours(-7))));
        var weather = new WeatherWidget();
        weather.Attach(_host);
        var weatherConfig = new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Col = 0, Row = 2, Width = 4, Height = 2, Location = place };
        weather.Configure(weatherConfig);
        Add(weatherConfig, weather);

        string[] names = { "Terminal", "Browser", "Music", "Files" };
        for (int i = 0; i < names.Length; i++)
        {
            Add(Shortcut(names[i], col: i, row: 4), out var config, out var widget);
            Widgets.Add((config, widget));
            Add(Shortcut(names[i], col: 0, row: 0, width: 1), out var dockConfig, out var dockWidget);
            Dock.Add((dockConfig, dockWidget));
        }
    }

    private static ShortcutConfig Shortcut(string label, int col, int row, int width = 1) =>
        new() { WidgetTypeId = "urdeck.widgets.shortcut", Col = col, Row = row, Width = width, Height = 1, Target = "https://example.invalid/" + label.ToLowerInvariant(), Label = label };

    private void Add(ShortcutConfig config, out WidgetConfig outConfig, out IWidget widget)
    {
        var shortcut = new ShortcutWidget();
        shortcut.Attach(_host);
        shortcut.Configure(config);
        _owned.Add(shortcut);
        outConfig = config;
        widget = shortcut;
    }

    private void Add(WidgetConfig config, IWidget widget)
    {
        _owned.Add(widget);
        Widgets.Add((config, widget));
    }

    /// <summary>The services of the widgets: recorded readings, no launches, no icons.</summary>
    private sealed class Host : IWidgetHost, ILauncher, IIconSource, IReadingSource
    {
        private readonly Dictionary<string, Reading> _readings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ReadingDescriptor> _catalog = new(StringComparer.OrdinalIgnoreCase);

        public IReadingSource Readings => this;

        public ILauncher Launcher => this;

        public IIconSource Icons => this;

        public bool RegionUsesFahrenheit => false;

        public bool RegionUses24HourClock => true;

        public void Describe(string id, ReadingKind kind, string label, double? min, double? max)
        {
            string path = id[(id.IndexOf(':') + 1)..];
            _catalog[id] = new ReadingDescriptor(path, kind, label, label) { Min = min, Max = max };
        }

        public void Set(string id, Reading reading) => _readings[id] = reading;

        public Reading Read(string id) => _readings.TryGetValue(id, out var r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => _catalog.TryGetValue(id, out var d) ? d : null;

        public void Log(string message)
        {
        }

        public bool Launch(string target, string? arguments = null) => false;

        public IconResult GetIcon(string source, int pixelSize) => IconResult.Loading;
    }
}
