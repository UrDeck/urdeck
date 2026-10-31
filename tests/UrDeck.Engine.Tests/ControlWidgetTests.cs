// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Control;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The control widget (widget-control spec) over fake readings, descriptions and an action invoker that ends when told.</summary>
public sealed class ControlWidgetTests : IDisposable
{
    private const string Light = "homeassistant:light.desk";
    private const string Brightness = "homeassistant:light.desk/brightness";
    private const int Cell = 275;

    private static readonly DateTime T0 = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Local);

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), Cell);
    private readonly Host _host = new();

    public ControlWidgetTests()
    {
        _host.Describe(Light, new ReadingDescriptor("light.desk", ReadingKind.OnOff, "Desk", "Desk lamp") { Action = "light.desk/toggle", Icon = "home/light" });
        _host.Describe(Brightness, new ReadingDescriptor("light.desk/brightness", ReadingKind.Percent, "Brightness", "Desk lamp brightness") { Action = "light.desk/turn_on" });
        _host.Values[Light] = Reading.Ok(false);
        _host.Values[Brightness] = Reading.Ok(40);
    }

    public void Dispose() => _theme.Dispose();

    /// <summary>The services the widget uses, recorded; every action ends when the test says so.</summary>
    private sealed class Host : IWidgetHost, IReadingSource, IActionInvoker
    {
        private readonly Dictionary<string, ReadingDescriptor> _descriptors = [];

        public Dictionary<string, Reading> Values { get; } = [];

        public List<(string Id, double? Argument)> Invoked { get; } = [];

        public List<string> Logged { get; } = [];

        public List<TaskCompletionSource<ActionResult>> Running { get; } = [];

        /// <summary>When set, an action ends at once with this result.</summary>
        public ActionResult? Immediately { get; set; }

        public IReadingSource Readings => this;

        public IActionInvoker Actions => this;

        public bool RegionUsesFahrenheit => false;

        public void Describe(string id, ReadingDescriptor descriptor) => _descriptors[id] = descriptor;

        public void Log(string message) => Logged.Add(message);

        public Reading Read(string id) => Values.TryGetValue(id, out var reading) ? reading : Reading.Unavailable("test");

        ReadingDescriptor? IReadingSource.Describe(string id) => _descriptors.GetValueOrDefault(id);

        public Task<ActionResult> Invoke(string id, ReadingValue? argument = null)
        {
            Invoked.Add((id, argument is { } value ? value.Number : null));
            if (Immediately is { } result)
                return Task.FromResult(result);
            var source = new TaskCompletionSource<ActionResult>();
            Running.Add(source);
            return source.Task;
        }
    }

    private ControlWidget Create(string json)
    {
        var read = JsonSerializer.Deserialize<WidgetConfig>(json, UrDeckJson.Options)!;
        var widget = new ControlWidget();
        widget.Attach(_host);
        widget.Configure(read.ToConcrete(typeof(ControlConfig)));
        return widget;
    }

    private ControlWidget Switch(string extra = "") =>
        Create($$"""{ "typeId": "urdeck.widgets.control", "reading": "{{Light}}"{{extra}} }""");

    private ControlWidget Dimmer(string extra = "", int width = 2) =>
        Create($$"""{ "typeId": "urdeck.widgets.control", "width": {{width}}, "reading": "{{Light}}", "style": "dimmer", "level": "{{Brightness}}"{{extra}} }""");

    private static System.Drawing.Size SizeOf(ControlWidget widget) => new(Cell * widget.Config.Width, Cell * widget.Config.Height);

    private SKBitmap Paint(ControlWidget widget, DateTime? at = null)
    {
        var size = SizeOf(widget);
        var bitmap = new SKBitmap(size.Width, size.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        WidgetPainter.Paint(widget, canvas, size, _theme, at ?? T0, CancellationToken.None);
        return bitmap;
    }

    private static int Count(SKBitmap bitmap, SKColor color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == color)
                    count++;
            }
        }

        return count;
    }

    private static SKPoint Middle(ControlWidget widget) => new(Cell * widget.Config.Width / 2f, Cell * widget.Config.Height / 2f);

    private static bool AnyTapPoint(ControlWidget widget)
    {
        var size = SizeOf(widget);
        for (int y = 0; y < size.Height; y += 10)
            for (int x = 0; x < size.Width; x += 10)
                if (widget.CanTap(new SKPoint(x, y)))
                    return true;
        return false;
    }

    /// <summary>The x range of a 2x1 dimmer's bar: where a tap invokes the level action. Each probe is a fresh tile.</summary>
    private (int From, int To) BarSpan()
    {
        int from = -1;
        int to = -1;
        for (int x = 0; x < Cell * 2; x += 5)
        {
            var probe = Dimmer();
            Paint(probe);
            int before = _host.Invoked.Count;
            var point = new SKPoint(x, Cell / 2f);
            if (!probe.CanTap(point))
                continue;
            probe.OnTap(point);
            if (_host.Invoked.Count > before && _host.Invoked[^1].Id.EndsWith("/turn_on", StringComparison.Ordinal))
            {
                from = from < 0 ? x : from;
                to = x;
            }
        }

        _host.Invoked.Clear();
        _host.Running.Clear();
        return (from, to);
    }

    private void End(ActionResult result, int index = 0) => _host.Running[index].SetResult(result);

    // Identity

    [Fact]
    public void TheWidget_IsRegisteredAsAOneByOneAndTwoByOneControl_ThatRefreshesOnData()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(ControlWidget), out string? reason);

        Assert.Null(reason);
        Assert.Equal("urdeck.widgets.control", descriptor!.Id);
        Assert.Equal([new System.Drawing.Size(1, 1), new System.Drawing.Size(2, 1)], descriptor.SupportedSizes);
        Assert.Equal("Control", descriptor.Category);
        Assert.Equal(RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Equal(typeof(ControlConfig), descriptor.ConfigType);
    }

    [Fact]
    public void Subscriptions_AreTheReadingAndTheLevel()
    {
        Assert.Equal([Light], Switch().Subscriptions);
        Assert.Equal([Light, Brightness], Dimmer().Subscriptions);
        Assert.Empty(Create("""{ "typeId": "urdeck.widgets.control", "action": "x:y/z" }""").Subscriptions);
    }

    [Fact]
    public void AnIdleTile_IsNeverRepainted_AndWakesNothing()
    {
        var widget = Switch();
        Paint(widget);

        Assert.False(widget.NeedsRender(T0.AddSeconds(1)));
        Assert.False(widget.NeedsRender(T0.AddMinutes(10)));
        Assert.Null(widget.NextAnimationAt);

        // A reading the tile does not show changes: nothing to repaint.
        _host.Values["homeassistant:light.other"] = Reading.Ok(true);
        Assert.False(widget.NeedsRender(T0.AddMinutes(11)));
    }

    // Configuration and its defaults

    [Fact]
    public void OnlyAReading_TakesItsActionFromTheDescription_AndTapsToggleIt()
    {
        var widget = Switch();
        Paint(widget);

        Assert.True(widget.CanTap(Middle(widget)));
        widget.OnTap(Middle(widget));

        Assert.Equal([("homeassistant:light.desk/toggle", (double?)null)], _host.Invoked);
    }

    [Fact]
    public void AnActionWithoutAProvider_IsTakenUnderTheReadingsProvider()
    {
        var widget = Switch(""", "action": "light.desk/turn_off" """);
        Paint(widget);

        widget.OnTap(Middle(widget));

        Assert.Equal("homeassistant:light.desk/turn_off", _host.Invoked[^1].Id);
    }

    [Fact]
    public void AnActionWithAProvider_IsUsedAsGiven()
    {
        var widget = Switch(""", "action": "media:control/next" """);
        Paint(widget);

        widget.OnTap(Middle(widget));

        Assert.Equal("media:control/next", _host.Invoked[^1].Id);
    }

    [Fact]
    public void AnEmptyAction_MakesTheTileDisplayOnly()
    {
        var widget = Switch(""", "action": "" """);
        Paint(widget);

        Assert.False(AnyTapPoint(widget));
        widget.OnTap(Middle(widget));
        Assert.Empty(_host.Invoked);
    }

    [Fact]
    public void ADescriptionWithoutAnAction_MakesTheTileDisplayOnly()
    {
        _host.Describe("system:cpu/load", new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load"));
        _host.Values["system:cpu/load"] = Reading.Ok(12);
        var widget = Create("""{ "typeId": "urdeck.widgets.control", "reading": "system:cpu/load" }""");
        Paint(widget);

        Assert.False(AnyTapPoint(widget));
    }

    [Fact]
    public void AnActionWithoutAReading_IsANeutralButton_ThatRunsTheScene()
    {
        var widget = Create("""{ "typeId": "urdeck.widgets.control", "action": "homeassistant:scene.movie_night/toggle", "label": "Movie" }""");
        var bitmap = Paint(widget);

        Assert.Equal(0, Count(bitmap, _theme.Accent));
        Assert.True(widget.CanTap(Middle(widget)));
        widget.OnTap(Middle(widget));
        Assert.Equal("homeassistant:scene.movie_night/toggle", _host.Invoked[^1].Id);
    }

    [Fact]
    public void NoLabelAndNoIcon_StillDrawsAndTaps()
    {
        var widget = Switch(""", "label": "", "icon": "" """);

        Paint(widget);

        Assert.True(widget.CanTap(Middle(widget)));
        Assert.False(widget.NeedsRender(T0));
    }

    [Fact]
    public void AnUnknownStyle_CountsAsASwitch()
    {
        var widget = Create($$"""{ "typeId": "urdeck.widgets.control", "width": 2, "reading": "{{Light}}", "style": "slider", "level": "{{Brightness}}" }""");
        Paint(widget);

        // The whole card taps the toggle; there is no bar.
        widget.OnTap(new SKPoint(Cell * 1.8f, Cell / 2f));
        Assert.Equal("homeassistant:light.desk/toggle", _host.Invoked[^1].Id);
    }

    // State

    [Fact]
    public void LightOn_DrawsAccent_LightOff_DoesNot()
    {
        _host.Values[Light] = Reading.Ok(true);
        int on = Count(Paint(Switch()), _theme.Accent);
        _host.Values[Light] = Reading.Ok(false);
        int off = Count(Paint(Switch()), _theme.Accent);

        Assert.True(on > 0);
        Assert.Equal(0, off);
    }

    [Theory]
    [InlineData("playing", true)]
    [InlineData("Open", true)]
    [InlineData("home", true)]
    [InlineData("paused", false)]
    [InlineData("closed", false)]
    public void ATextValue_IsOnOnlyWhenItIsOneOfTheWords(string text, bool on)
    {
        _host.Values["media:status"] = Reading.Ok(text);
        var widget = Create("""{ "typeId": "urdeck.widgets.control", "reading": "media:status", "action": "media:control/playpause" }""");

        Assert.Equal(on, Count(Paint(widget), _theme.Accent) > 0);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void ANumber_IsOnWhenAboveZero(double number, bool on)
    {
        _host.Values["x:n"] = Reading.Ok(number);
        var widget = Create("""{ "typeId": "urdeck.widgets.control", "reading": "x:n", "action": "x:go" }""");

        Assert.Equal(on, Count(Paint(widget), _theme.Accent) > 0);
    }

    [Fact]
    public void AnUnavailableReading_IsNeutral_AndStillTaps()
    {
        _host.Values[Light] = Reading.Unavailable("offline");
        var widget = Switch();
        var bitmap = Paint(widget);

        Assert.Equal(0, Count(bitmap, _theme.Accent));
        Assert.True(widget.CanTap(Middle(widget)));
    }

    [Fact]
    public void AStaleReading_KeepsItsLastState()
    {
        _host.Values[Light] = Reading.Stale(true);
        var widget = Switch();

        Paint(widget);

        Assert.False(widget.NeedsRender(T0));
        _host.Values[Light] = Reading.Ok(true);
        Assert.True(widget.NeedsRender(T0), "stale becoming current changes how the tile is drawn");
    }

    // Tap and pending

    [Fact]
    public void ATap_MakesTheTilePending_UntilTheValueChanges()
    {
        var widget = Switch();
        Paint(widget);

        widget.OnTap(Middle(widget));
        Assert.True(widget.NeedsRender(T0), "pending is a new look");
        Paint(widget, T0);
        Assert.False(widget.CanTap(Middle(widget)));
        Assert.NotNull(widget.NextAnimationAt);

        _host.Values[Light] = Reading.Ok(true);
        Assert.True(widget.NeedsRender(T0.AddMilliseconds(200)));
        Paint(widget, T0.AddMilliseconds(200));
        Assert.True(widget.CanTap(Middle(widget)));
        Assert.Null(widget.NextAnimationAt);
        Assert.True(Count(Paint(widget, T0.AddMilliseconds(250)), _theme.Accent) > 0);
    }

    [Fact]
    public void TappingFast_InvokesOneAction()
    {
        var widget = Switch();
        Paint(widget);

        for (int i = 0; i < 5; i++)
        {
            if (widget.CanTap(Middle(widget)))
                widget.OnTap(Middle(widget));
            widget.NeedsRender(T0.AddMilliseconds(100 * i));
        }

        Assert.Single(_host.Invoked);
    }

    [Fact]
    public void ADoneAction_WithNoChange_ReturnsToTheStateAfterOneSecond()
    {
        var widget = Switch();
        Paint(widget);
        widget.OnTap(Middle(widget));
        Paint(widget, T0);

        End(ActionResult.Done);
        Assert.False(widget.NeedsRender(T0.AddMilliseconds(100)), "still pending: nothing new to draw");
        Assert.False(widget.CanTap(Middle(widget)));

        Assert.True(widget.NeedsRender(T0.AddMilliseconds(1200)));
        Paint(widget, T0.AddMilliseconds(1200));
        Assert.True(widget.CanTap(Middle(widget)));
    }

    [Fact]
    public void AFailedAction_ShowsTheFailedLookForTwoSeconds()
    {
        var widget = Switch();
        Paint(widget);
        widget.OnTap(Middle(widget));
        widget.NeedsRender(T0);

        End(ActionResult.Failed("no such service"));
        Assert.True(widget.NeedsRender(T0.AddMilliseconds(100)));
        Assert.True(Count(Paint(widget, T0.AddMilliseconds(100)), _theme.Critical) > 0);
        Assert.NotNull(widget.NextAnimationAt);

        Assert.True(widget.NeedsRender(T0.AddMilliseconds(2200)));
        Assert.Equal(0, Count(Paint(widget, T0.AddMilliseconds(2200)), _theme.Critical));
        Assert.Null(widget.NextAnimationAt);
    }

    [Fact]
    public void AnActionThatNeverEnds_IsGivenUpOn()
    {
        var widget = Switch();
        Paint(widget);
        widget.OnTap(Middle(widget));
        widget.NeedsRender(T0);

        Assert.True(widget.NeedsRender(T0.AddSeconds(16)));
        Assert.True(Count(Paint(widget, T0.AddSeconds(16)), _theme.Critical) > 0);
    }

    [Fact]
    public void ACompletionAfterANewConfiguration_IsIgnored()
    {
        var widget = Switch();
        Paint(widget);
        widget.OnTap(Middle(widget));
        widget.NeedsRender(T0);

        widget.Configure(widget.Config);
        Paint(widget);
        End(ActionResult.Failed("late"));

        Assert.False(widget.NeedsRender(T0.AddMilliseconds(100)));
        Assert.True(widget.CanTap(Middle(widget)));
    }

    // Dimmer

    [Fact]
    public void ADimmer_TapOnTheBar_SetsTheLevelFromThePosition()
    {
        var (from, to) = BarSpan();
        Assert.True(to > from && from > 0, "the bar is on the right of the button");
        var widget = Dimmer();
        Paint(widget);

        float x = from + (to - from) * 0.7f;
        Assert.True(widget.CanTap(new SKPoint(x, Cell / 2f)));
        widget.OnTap(new SKPoint(x, Cell / 2f));

        var (id, argument) = Assert.Single(_host.Invoked);
        Assert.Equal("homeassistant:light.desk/turn_on", id);
        Assert.InRange(argument!.Value, 60, 80);
    }

    [Fact]
    public void ADimmer_TapsAtTheEndsGiveZeroAndOneHundred()
    {
        var (from, to) = BarSpan();
        var widget = Dimmer();
        Paint(widget);
        widget.OnTap(new SKPoint(from, Cell / 2f));
        Assert.Equal(0, _host.Invoked[^1].Argument);

        var other = Dimmer();
        Paint(other);
        other.OnTap(new SKPoint(to, Cell / 2f));
        Assert.Equal(100, _host.Invoked[^1].Argument);
    }

    [Fact]
    public void ADimmer_TapOnTheButton_TogglesTheLight()
    {
        var widget = Dimmer();
        Paint(widget);

        widget.OnTap(new SKPoint(Cell / 3f, Cell / 2f));

        Assert.Equal("homeassistant:light.desk/toggle", Assert.Single(_host.Invoked).Id);
    }

    [Fact]
    public void ADimmer_KeepsOnlyTheLatestBarTapWhilePending()
    {
        var (from, to) = BarSpan();
        float At(double fraction) => from + (to - from) * (float)fraction;
        var widget = Dimmer();
        Paint(widget);

        widget.OnTap(new SKPoint(At(0.3), Cell / 2f));
        widget.NeedsRender(T0);
        Assert.True(widget.CanTap(new SKPoint(At(0.5), Cell / 2f)), "the bar takes taps while pending");
        Assert.False(widget.CanTap(new SKPoint(Cell / 3f, Cell / 2f)), "the button does not");
        widget.OnTap(new SKPoint(At(0.5), Cell / 2f));
        widget.OnTap(new SKPoint(At(0.8), Cell / 2f));
        Assert.Single(_host.Invoked);

        _host.Values[Brightness] = Reading.Ok(30);
        widget.NeedsRender(T0.AddMilliseconds(200));

        Assert.Equal(2, _host.Invoked.Count);
        Assert.InRange(_host.Invoked[0].Argument!.Value, 25, 35);
        Assert.InRange(_host.Invoked[1].Argument!.Value, 75, 85);
    }

    [Fact]
    public void ADimmer_AtOneByOne_DrawsAndBehavesAsASwitch()
    {
        var widget = Dimmer(width: 1);
        Paint(widget);

        widget.OnTap(new SKPoint(Cell * 0.9f, Cell / 2f));

        Assert.Equal("homeassistant:light.desk/toggle", Assert.Single(_host.Invoked).Id);
        Assert.Empty(_host.Logged);
    }

    [Fact]
    public void ADimmerWithoutALevel_IsASwitch_AndSaysSoOnce()
    {
        var widget = Create($$"""{ "typeId": "urdeck.widgets.control", "width": 2, "reading": "{{Light}}", "style": "dimmer" }""");
        Paint(widget);
        widget.Configure(widget.Config);

        widget.OnTap(new SKPoint(Cell * 1.8f, Cell / 2f));

        Assert.Equal("homeassistant:light.desk/toggle", Assert.Single(_host.Invoked).Id);
        Assert.Single(_host.Logged, line => line.Contains("level", StringComparison.Ordinal));
    }

    [Fact]
    public void TheBar_FollowsTheLevelReading()
    {
        var widget = Dimmer();
        int before = Count(Paint(widget), _theme.Accent);

        _host.Values[Brightness] = Reading.Ok(90);

        Assert.True(widget.NeedsRender(T0));
        Assert.True(Count(Paint(widget), _theme.Accent) > before);
    }
}
