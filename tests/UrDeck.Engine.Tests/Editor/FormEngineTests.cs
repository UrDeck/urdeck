// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Data;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

/// <summary>What the editor's forms need from the engine (editor-forms D4, D5, D12 and the actions and icon listings).</summary>
public sealed class FormEngineTests
{
    private sealed class Sample : WidgetConfig
    {
        [Setting("Flag")]
        public bool Flag { get; set; }

        [Setting("Mode")]
        [Option("", "Automatic")]
        [Option("fast", "Fast")]
        public string? Mode { get; set; }

        [Setting("Name")]
        public string? Name { get; set; }

        [Setting("Label", AllowsNone = true)]
        public string? Label { get; set; }

        [Setting("Decimals")]
        [Number(Min = 0, Max = 3)]
        public int? Decimals { get; set; }

        [Setting("Level")]
        [Number(Min = 0, Max = 10, Step = 0.5)]
        public double? Level { get; set; }

        [Setting("Open")]
        [Number(Min = 5)]
        public double? Open { get; set; }

        [Setting("Close")]
        [Number(Max = 5)]
        public double? Close { get; set; }

        [Setting("Days")]
        [Slider(1, 7)]
        public JsonElement Days { get; set; }

        [Setting("Count")]
        [Option("3", "Three")]
        [Option("5", "Five")]
        public JsonElement Count { get; set; }

        [Setting("Raw")]
        [Toggle]
        public JsonElement Raw { get; set; }
    }

    private static readonly SettingSchema Schema = SettingSchema.Read(typeof(Sample), typeof(WidgetConfig));

    private static SettingDescriptor Setting(string key) => Schema.Settings.Single(s => s.Key == key);

    private static JsonNode? Node(string json) => JsonNode.Parse(json);

    // SettingValues

    [Fact]
    public void Read_Absent_IsNull() => Assert.All(Schema.Settings, s => Assert.Null(SettingValues.Read(null, s)));

    [Fact]
    public void Read_Toggle_AcceptsABoolAndItsText()
    {
        Assert.Equal(true, SettingValues.Read(Node("true"), Setting("flag")));
        Assert.Equal(false, SettingValues.Read(Node("\"false\""), Setting("flag")));
        Assert.Null(SettingValues.Read(Node("\"maybe\""), Setting("flag")));
        Assert.Equal(true, SettingValues.Read(Node("true"), Setting("raw")));
    }

    [Fact]
    public void Read_Number_AcceptsANumberAndItsText()
    {
        Assert.Equal(2.0, SettingValues.Read(Node("2"), Setting("decimals")));
        Assert.Equal(2.5, SettingValues.Read(Node("\"2.5\""), Setting("level")));
        Assert.Null(SettingValues.Read(Node("\"two\""), Setting("level")));
        Assert.Null(SettingValues.Read(Node("[1]"), Setting("level")));
    }

    [Fact]
    public void Read_JsonElementSetting_ReadsTheNode()
    {
        Assert.Equal(5.0, SettingValues.Read(Node("5"), Setting("days")));
        Assert.Equal("5", SettingValues.Read(Node("5"), Setting("count")));
        Assert.Equal("5", SettingValues.Read(Node("\"5\""), Setting("count")));
    }

    [Fact]
    public void Read_Choice_KeepsAnUnknownOptionAsText()
    {
        Assert.Equal("fast", SettingValues.Read(Node("\"fast\""), Setting("mode")));
        Assert.Equal("slow", SettingValues.Read(Node("\"slow\""), Setting("mode")));
    }

    [Fact]
    public void Read_Text_ThreeStates()
    {
        Assert.Null(SettingValues.Read(null, Setting("label")));
        Assert.Equal("", SettingValues.Read(Node("\"\""), Setting("label")));
        Assert.Equal("Kitchen", SettingValues.Read(Node("\"Kitchen\""), Setting("label")));
    }

    [Fact]
    public void Write_Toggle_WritesABool()
    {
        Assert.True(SettingValues.Write(true, Setting("flag"))!.GetValue<bool>());
        Assert.False(SettingValues.Write(false, Setting("flag"))!.GetValue<bool>());
        Assert.Null(SettingValues.Write(null, Setting("flag")));
    }

    [Fact]
    public void Write_Number_IsWholeForAWholeNumberSetting()
    {
        Assert.Equal("2", SettingValues.Write(2.0, Setting("decimals"))!.ToJsonString());
        Assert.Equal("2.5", SettingValues.Write(2.5, Setting("level"))!.ToJsonString());
        Assert.Equal("0.3333", SettingValues.Write(1.0 / 3, Setting("level"))!.ToJsonString());
        Assert.Null(SettingValues.Write(double.NaN, Setting("level")));
        Assert.Null(SettingValues.Write(null, Setting("level")));
    }

    [Fact]
    public void Write_Range_WritesTheNumber() => Assert.Equal("4", SettingValues.Write(4.0, Setting("days"))!.ToJsonString());

    [Fact]
    public void Write_Choice_EmptyRemovesTheKey()
    {
        Assert.Equal("\"fast\"", SettingValues.Write("fast", Setting("mode"))!.ToJsonString());
        Assert.Null(SettingValues.Write("", Setting("mode")));
        Assert.Null(SettingValues.Write(null, Setting("mode")));
    }

    [Fact]
    public void Write_Choice_ANumericOptionOfANonTextPropertyIsANumber()
    {
        Assert.Equal("3", SettingValues.Write("3", Setting("count"))!.ToJsonString());
        Assert.Equal("\"3\"", SettingValues.Write("3", Setting("mode"))!.ToJsonString());
    }

    [Fact]
    public void Write_Text_EmptyRemoves_ExceptWhereNoneIsAllowed()
    {
        Assert.Null(SettingValues.Write("", Setting("name")));
        Assert.Equal("\"x\"", SettingValues.Write("x", Setting("name"))!.ToJsonString());
        Assert.Equal("\"\"", SettingValues.Write("", Setting("label"))!.ToJsonString());
        Assert.Null(SettingValues.Write(null, Setting("label")));
        Assert.Equal("\"Hall\"", SettingValues.Write("Hall", Setting("label"))!.ToJsonString());
    }

    [Fact]
    public void Invalid_SaysTheRange()
    {
        Assert.Null(SettingValues.Invalid(3, Setting("decimals")));
        Assert.Equal("Between 0 and 3.", SettingValues.Invalid(9, Setting("decimals")));
        Assert.Equal("Between 0 and 3.", SettingValues.Invalid(-1, Setting("decimals")));
        Assert.Equal("A whole number.", SettingValues.Invalid(1.5, Setting("decimals")));
        Assert.Equal("At least 5.", SettingValues.Invalid(1, Setting("open")));
        Assert.Equal("At most 5.", SettingValues.Invalid(6, Setting("close")));
    }

    // Fallbacks and units

    private static ReadingDescriptor Temperature() =>
        new("gpu/temperature", ReadingKind.Temperature, "GPU temperature", "GPU temperature") { Min = 0, Max = 100, Warning = 75, Critical = 90 };

    private static SettingDescriptor FromReading(string key, string? words = null) => Setting("name") with
    {
        Key = key,
        Fallback = new Fallback(words, FallbackSource.Reading, "reading"),
    };

    [Theory]
    [InlineData("label", "GPU temperature")]
    [InlineData("unit", "°C")]
    [InlineData("min", "0°C")]
    [InlineData("max", "100°C")]
    [InlineData("warning", "75°C")]
    [InlineData("critical", "90°C")]
    public void Placeholder_FollowsTheReading(string key, string expected) =>
        Assert.Equal(expected, FormRules.Placeholder(FromReading(key, "Off"), Temperature(), DisplayUnit.Celsius, false));

    [Fact]
    public void Placeholder_ShowsLevelsInTheDisplayUnit() =>
        Assert.Equal("167°F", FormRules.Placeholder(FromReading("warning", "Off"), Temperature(), DisplayUnit.Fahrenheit, false));

    [Fact]
    public void Placeholder_WithoutLevels_UsesTheWords()
    {
        var plain = new ReadingDescriptor("x", ReadingKind.Number, "X", "X");

        Assert.Equal("Off", FormRules.Placeholder(FromReading("warning", "Off"), plain, DisplayUnit.Celsius, false));
        Assert.Equal("From the reading", FormRules.Placeholder(FromReading("min", "Off"), plain, DisplayUnit.Celsius, false));
        Assert.Equal("Off", FormRules.Placeholder(FromReading("warning", "Off"), null, DisplayUnit.Celsius, false));
    }

    [Fact]
    public void Placeholder_RegionAndTheme()
    {
        var region = Setting("name") with { Fallback = new Fallback(null, FallbackSource.Region, null) };
        var theme = Setting("name") with { Fallback = new Fallback("The theme decides", FallbackSource.Theme, null) };

        Assert.Equal("°C (your region)", FormRules.Placeholder(region, null, DisplayUnit.Celsius, false));
        Assert.Equal("°F (your region)", FormRules.Placeholder(region, null, DisplayUnit.Celsius, true));
        Assert.Equal("The theme decides", FormRules.Placeholder(theme, null, DisplayUnit.Celsius, true));
    }

    [Fact]
    public void ResolveUnit_ItemThenReadingThenRegion()
    {
        var preferred = Temperature() with { DisplayUnit = DisplayUnit.Fahrenheit };

        Assert.Equal(DisplayUnit.Celsius, FormRules.ResolveUnit("celsius", preferred, true));
        Assert.Equal(DisplayUnit.Fahrenheit, FormRules.ResolveUnit(null, preferred, false));
        Assert.Equal(DisplayUnit.Fahrenheit, FormRules.ResolveUnit("", Temperature(), true));
        Assert.Equal(DisplayUnit.Celsius, FormRules.ResolveUnit("kelvin", Temperature(), false));
    }

    [Fact]
    public void Units_FahrenheitThresholdRoundTrips()
    {
        var reading = Temperature();

        Assert.Equal(167, FormRules.ToForm(75, reading, DisplayUnit.Fahrenheit, 1));
        Assert.Equal(75, FormRules.FromForm(167, reading, DisplayUnit.Fahrenheit));
        Assert.Equal(75, FormRules.FromForm(FormRules.ToForm(75, reading, DisplayUnit.Fahrenheit, 1), reading, DisplayUnit.Fahrenheit));
        Assert.Equal(75, FormRules.ToForm(75, reading, DisplayUnit.Celsius, 1));
        Assert.Equal("°F", FormRules.UnitSuffix(reading, DisplayUnit.Fahrenheit));
        Assert.Equal("", FormRules.UnitSuffix(null, DisplayUnit.Celsius));
    }

    [Theory]
    [InlineData(ReadingKind.Temperature, 2)]
    [InlineData(ReadingKind.Speed, 2)]
    [InlineData(ReadingKind.Depth, 2)]
    [InlineData(ReadingKind.Percent, 0)]
    [InlineData(ReadingKind.Number, 0)]
    public void UnitChoices_OnlyForKindsWithTwoUnits(ReadingKind kind, int count) => Assert.Equal(count, FormRules.UnitChoices(kind).Count);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0.05, 2)]
    public void Decimals_FollowTheStep(double step, int decimals) => Assert.Equal(decimals, FormRules.Decimals(step));

    // Actions of a reading

    [Fact]
    public void ActionsFor_NaturalActionFirst_ThenTheOnesThatBelong()
    {
        var provider = new ActionsProvider();
        var registry = new ProviderRegistry();
        registry.TryRegister(provider.Descriptor(), out _);
        using var hub = new ReadingHub(registry, new FakeTime(), regionUsesFahrenheit: false);

        var actions = hub.ActionsFor("lights:light.desk/brightness");

        Assert.Equal(["light.desk/toggle", "light.desk/turn_on", "light.desk/turn_off"], actions.Select(a => a.Path));
        Assert.Equal(["light.desk/toggle"], hub.ActionsFor("lights:light.desk").Select(a => a.Path).Take(1));
    }

    [Fact]
    public void ActionsFor_UnknownOrActionlessIsEmpty()
    {
        var registry = new ProviderRegistry();
        registry.TryRegister(new ActionsProvider().Descriptor(), out _);
        registry.TryRegister(new FakeProvider(new FakeTime()).Descriptor("plain"), out _);
        using var hub = new ReadingHub(registry, new FakeTime(), regionUsesFahrenheit: false);

        Assert.Empty(hub.ActionsFor("nobody:x"));
        Assert.Empty(hub.ActionsFor("plain:a"));
        Assert.Empty(hub.ActionsFor("not a reading"));
        Assert.Empty(hub.ActionsFor("lights:fan.bedroom"));
    }

    // Icon sets

    [Fact]
    public void KeysOf_TheBuiltInSet_IsTheProductsKeysInOrder()
    {
        var set = IconSetStore.BuiltIn(IconSetStore.BuiltInName)!;

        var keys = IconSets.KeysOf(set);

        Assert.Equal(keys.Distinct(), keys);
        Assert.Contains("home/light", keys);
        Assert.Contains("weather/rain", keys);
        Assert.Equal(IconKeys.All.Where(keys.Contains), keys.Where(IconKeys.All.Contains));
        Assert.All(keys, key => Assert.True(set.TryGet(key, out _), key));
    }

    private sealed class ActionsProvider : IDataProvider, IActionProvider
    {
        public ProviderDescriptor Descriptor() => new("lights", "Lights", typeof(ActionsProvider), 1000, 250, "tests") { Factory = () => this };

        public IReadOnlyList<ReadingDescriptor> Describe() =>
        [
            new ReadingDescriptor("light.desk", ReadingKind.OnOff, "Desk", "Desk") { Action = "light.desk/toggle" },
            new ReadingDescriptor("light.desk/brightness", ReadingKind.Percent, "Desk brightness", "Desk brightness") { Action = "light.desk/toggle" },
            new ReadingDescriptor("fan.bedroom", ReadingKind.OnOff, "Fan", "Fan"),
        ];

        public IReadOnlyList<ActionDescriptor> DescribeActions() =>
        [
            new ActionDescriptor("light.desk/turn_on", "Turn on", "Turn on the desk light", true),
            new ActionDescriptor("light.desk/turn_off", "Turn off", "Turn off the desk light"),
            new ActionDescriptor("light.desk/toggle", "Toggle", "Toggle the desk light"),
            new ActionDescriptor("scene.movie/activate", "Activate", "Activate the movie scene"),
        ];

        public Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken) => Task.FromResult(ActionResult.Done);

        public void Start(IReadingSink sink, ProviderSettings settings)
        {
        }

        public void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Shutdown()
        {
        }
    }
}
