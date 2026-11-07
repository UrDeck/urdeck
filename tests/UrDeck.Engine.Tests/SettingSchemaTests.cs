// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Engine.Data;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Settings;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
using UrDeck.Widgets.Clock.Config;
using UrDeck.Widgets.Control;
using UrDeck.Widgets.NowPlaying;
using UrDeck.Widgets.Shortcut;
using UrDeck.Widgets.Stats;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>What the engine reads from a declaration (settings-metadata, "Declared Settings", "Setting Modifiers", "Unknown Kind").</summary>
public class SettingSchemaTests
{
    private sealed class Inference : WidgetConfig
    {
        [Setting("Flag")]
        public bool Flag { get; set; } = true;

        [Setting("Name", Help = "A name.")]
        public string? Name { get; set; }

        [Setting("Count")]
        public int Count { get; set; } = 3;

        [Setting("Ratio")]
        public double? Ratio { get; set; }

        [Setting("Mode")]
        [Option("simple", "Simple")]
        [Option("flap", "Split-flap")]
        public string Mode { get; set; } = "simple";

        [Setting("Level")]
        [Option("1", "One")]
        [Option("2", "Two")]
        public int Level { get; set; } = 1;

        [Setting("Days")]
        [Slider(1, 7)]
        public JsonElement? Days { get; set; }

        [Setting("Hidden by order", Group = "More", Order = 2)]
        public bool Late { get; set; }

        [Setting("Hidden by order 2", Group = "More", Order = 1)]
        public bool Early { get; set; }

        [Setting("Ungrouped last", Order = 9)]
        public bool Last { get; set; }

        public bool Undeclared { get; set; } = true;

        [JsonIgnore]
        public bool Ignored { get; set; }

        public bool ReadOnly { get; } = true;
    }

    private static SettingDescriptor Of(SettingSchema schema, string key) => schema.Settings.Single(s => s.Key == key);

    [Fact]
    public void The_kind_is_taken_from_the_property_type()
    {
        var schema = SettingSchema.Read(typeof(Inference), typeof(WidgetConfig));

        Assert.Equal(SettingKinds.Toggle, Of(schema, "flag").Kind);
        Assert.Equal(SettingKinds.Text, Of(schema, "name").Kind);
        Assert.Equal(SettingKinds.Number, Of(schema, "count").Kind);
        Assert.True(Of(schema, "count").Number!.WholeNumber);
        Assert.False(Of(schema, "ratio").Number!.WholeNumber);
        Assert.Equal(SettingKinds.Choice, Of(schema, "mode").Kind);
        Assert.Equal(SettingKinds.Choice, Of(schema, "level").Kind);
        Assert.Equal(SettingKinds.Range, Of(schema, "days").Kind);
        Assert.Equal(1, Of(schema, "days").Number!.Min);
        Assert.Equal(7, Of(schema, "days").Number!.Max);
    }

    [Fact]
    public void A_toggle_has_the_types_default_and_the_label_and_help()
    {
        var schema = SettingSchema.Read(typeof(Inference), typeof(WidgetConfig));

        var flag = Of(schema, "flag");
        Assert.Equal("Flag", flag.Label);
        Assert.Equal(true, flag.Default);
        Assert.Equal("A name.", Of(schema, "name").Help);
        Assert.Equal(3, Of(schema, "count").Default);
    }

    [Fact]
    public void A_choice_lists_its_options_in_order()
    {
        var schema = SettingSchema.Read(typeof(Inference), typeof(WidgetConfig));

        Assert.Equal([("simple", "Simple"), ("flap", "Split-flap")], Of(schema, "mode").Options.Select(o => (o.Value, o.Label)));
    }

    [Fact]
    public void Undeclared_ignored_and_read_only_properties_are_not_listed()
    {
        var schema = SettingSchema.Read(typeof(Inference), typeof(WidgetConfig));

        Assert.DoesNotContain(schema.Settings, s => s.Key is "undeclared" or "ignored" or "readOnly");
    }

    [Fact]
    public void Settings_are_listed_by_group_then_order_then_declaration()
    {
        var schema = SettingSchema.Read(typeof(Inference), typeof(WidgetConfig));

        string[] keys = [.. schema.Settings.Select(s => s.Key)];
        Assert.Equal("last", keys[^3]);
        Assert.Equal(["early", "late"], keys[^2..]);
        Assert.True(Array.IndexOf(keys, "flag") < Array.IndexOf(keys, "name"));
    }

    private sealed class Modifiers : WidgetConfig
    {
        [Setting("Mode")]
        [Option("a", "A")]
        [Option("b", "B")]
        public string Mode { get; set; } = "a";

        [Setting("Reading")]
        [Reading(Filter = ReadingFilter.HasAction | ReadingFilter.HasRange, Provider = "x")]
        public string? Reading { get; set; }

        [Setting("Action", Fallback = "Own", Sizes = ["4x4"], AllowsNone = true, DisabledInRoles = ["text"], DisabledHelp = "Why.")]
        [Action("reading")]
        [ShownWhen("mode", "b")]
        public string? Action { get; set; }

        [Setting("Min", FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
        [Number(Min = 0, Max = 10, Step = 0.5, UnitFromReading = "reading")]
        public double? Min { get; set; }

        [Setting("Place")]
        [Place("weather", LatitudeSetting = "lat", LongitudeSetting = "lon")]
        public string? Place { get; set; }

        [SettingPart("place")]
        [JsonPropertyName("lat")]
        public double? Latitude { get; set; }

        [SettingPart("place")]
        [JsonPropertyName("lon")]
        public double? Longitude { get; set; }

        [Setting("Target")]
        [LaunchTarget(Allowed = LaunchKinds.App | LaunchKinds.Address)]
        public string? Target { get; set; }

        [Setting("Icon")]
        [Icon(AllowFile = false)]
        public string? Icon { get; set; }
    }

    [Fact]
    public void Every_modifier_is_read()
    {
        var schema = SettingSchema.Read(typeof(Modifiers), typeof(WidgetConfig));

        var reading = Of(schema, "reading");
        Assert.Equal(SettingKinds.Reading, reading.Kind);
        Assert.Equal(ReadingFilter.HasAction | ReadingFilter.HasRange, reading.Reading!.Filter);
        Assert.Equal("x", reading.Reading.Provider);

        var action = Of(schema, "action");
        Assert.Equal(SettingKinds.Action, action.Kind);
        Assert.Equal("reading", action.OwnerReading);
        Assert.Equal("Own", action.Fallback.Text);
        Assert.Equal(["4x4"], action.Sizes);
        Assert.True(action.AllowsNone);
        Assert.Equal("mode", action.When!.Key);
        Assert.Equal(["b"], action.When.Values);
        Assert.Equal(["text"], action.DisabledInRoles);
        Assert.Equal("Why.", action.DisabledHelp);

        var min = Of(schema, "min");
        Assert.Equal(FallbackSource.Reading, min.Fallback.From);
        Assert.Equal("reading", min.Fallback.ReadingSetting);
        Assert.Equal(0, min.Number!.Min);
        Assert.Equal(10, min.Number.Max);
        Assert.Equal(0.5, min.Number.Step);
        Assert.Equal("reading", min.Number.UnitFromReading);

        var place = Of(schema, "place");
        Assert.Equal(SettingKinds.Place, place.Kind);
        Assert.Equal("weather", place.Place!.Source);
        Assert.Equal(["lat", "lon"], schema.Parts);
        Assert.DoesNotContain(schema.Settings, s => s.Key is "lat" or "lon");

        Assert.Equal(LaunchKinds.App | LaunchKinds.Address, Of(schema, "target").Launch);
        Assert.False(Of(schema, "icon").IconAllowsFile);
    }

    private sealed class UnknownKindAttribute : SettingKindAttribute
    {
        public override string Kind => "colourRole";
    }

    private sealed class FromTheFuture : WidgetConfig
    {
        [Setting("Colour")]
        [UnknownKind]
        public string? Colour { get; set; }
    }

    [Fact]
    public void An_unknown_kind_is_kept_as_declared()
    {
        var schema = SettingSchema.Read(typeof(FromTheFuture), typeof(WidgetConfig));

        Assert.Equal("colourRole", Of(schema, "colour").Kind);
    }

    private sealed class MissingKey : WidgetConfig
    {
        [Setting("Extra")]
        [ShownWhen("mode", "advanced")]
        public string? Extra { get; set; }
    }

    private sealed class KindlessJson : WidgetConfig
    {
        [Setting("Anything")]
        public JsonElement? Anything { get; set; }
    }

    private sealed class KindlessList : WidgetConfig
    {
        [Setting("Items")]
        public List<string> Items { get; set; } = [];
    }

    private sealed class OptionsOnBool : WidgetConfig
    {
        [Setting("Flag")]
        [Option("true", "Yes")]
        public bool Flag { get; set; }
    }

    private sealed class MissingFallbackReading : WidgetConfig
    {
        [Setting("Label", FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
        public string? Label { get; set; }
    }

    private sealed class MissingPartOwner : WidgetConfig
    {
        [SettingPart("place")]
        public double? Latitude { get; set; }
    }

    [Theory]
    [InlineData(typeof(MissingKey), "'mode'")]
    [InlineData(typeof(KindlessJson), "needs a kind attribute")]
    [InlineData(typeof(KindlessList), "needs a kind attribute")]
    [InlineData(typeof(OptionsOnBool), "bool")]
    [InlineData(typeof(MissingFallbackReading), "'reading'")]
    [InlineData(typeof(MissingPartOwner), "'place'")]
    public void An_invalid_declaration_is_rejected_with_a_reason(Type type, string expected)
    {
        Assert.False(SettingSchema.TryRead(type, typeof(WidgetConfig), out var schema, out string? error));

        Assert.Null(schema);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void A_widget_with_an_invalid_declaration_is_rejected_by_the_descriptor()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(BadWidget), out string? reason);

        Assert.Null(descriptor);
        Assert.Contains("'mode'", reason);
    }

    [Widget("bad", "Bad")]
    [WidgetSize(2, 2)]
    [RefreshOnData]
    private sealed class BadWidget : Widget<MissingKey>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }

    private sealed class WrongSize : WidgetConfig
    {
        [Setting("Days", Sizes = ["4x4"])]
        public int Days { get; set; }
    }

    [Widget("wrong", "Wrong")]
    [WidgetSize(2, 2)]
    [RefreshOnData]
    private sealed class WrongSizeWidget : Widget<WrongSize>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }

    [Fact]
    public void A_setting_naming_a_size_the_widget_lacks_rejects_the_widget()
    {
        Assert.Null(WidgetDescriptor.TryCreate(typeof(WrongSizeWidget), out string? reason));
        Assert.Contains("4x4", reason);
    }

    private sealed class ItemSettings : WidgetConfig
    {
        [Setting("Items")]
        [Positions("2x2", "a", "b")]
        public List<Item> Items { get; set; } = [];
    }

    private sealed class Item
    {
        [Setting("Title")]
        public string? Title { get; set; }
    }

    [Fact]
    public void A_list_of_positions_reads_its_item_type()
    {
        var items = Of(SettingSchema.Read(typeof(ItemSettings), typeof(WidgetConfig)), "items");

        Assert.Equal(SettingKinds.Positions, items.Kind);
        Assert.Equal(["a", "b"], items.Positions!.RolesAt("2x2"));
        Assert.Null(items.Positions.RolesAt("4x4"));
        Assert.Equal("title", items.Item!.Settings.Single().Key);
    }

    [Fact]
    public void The_home_assistant_settings_keep_their_shape()
    {
        var schema = ProviderSchema.TryRead(typeof(UrDeck.Providers.HomeAssistant.HomeAssistantSettings), out _)!;

        var url = schema.Settings.Single(s => s.Key == "url");
        Assert.Equal(SettingKinds.Text, url.Kind);
        Assert.True(url.Required);
        var token = schema.Settings.Single(s => s.Key == "token");
        Assert.Equal(SettingKinds.Secret, token.Kind);
        Assert.True(token.IsSecret);
        Assert.Equal("url", token.ContextKey);
        Assert.Null(token.Default);
    }

    [Fact]
    public void Every_first_party_widget_declares_a_label_and_help_for_every_setting()
    {
        foreach (var type in new[]
        {
            typeof(UrDeck.Widgets.Clock.ClockWidget), typeof(StatsWidget), typeof(WeatherWidget), typeof(UrDeck.Widgets.Shortcut.ShortcutWidget),
            typeof(UrDeck.Widgets.NowPlaying.NowPlayingWidget), typeof(UrDeck.Widgets.Control.ControlWidget),
        })
        {
            var descriptor = WidgetDescriptor.TryCreate(type, out string? reason);
            Assert.True(descriptor != null, $"{type.Name}: {reason}");
            Assert.NotEmpty(descriptor.Settings.Settings);
            AssertDescribed(type.Name, descriptor.Settings);
        }
    }

    private static void AssertDescribed(string owner, SettingSchema schema)
    {
        foreach (var setting in schema.Settings)
        {
            Assert.False(string.IsNullOrWhiteSpace(setting.Label), $"{owner}.{setting.Key} has no label");
            Assert.False(string.IsNullOrWhiteSpace(setting.Help), $"{owner}.{setting.Key} has no help");
            if (setting.Item != null)
                AssertDescribed(owner + "." + setting.Key, setting.Item);
        }
    }

    [Fact]
    public void Every_public_settable_property_of_a_first_party_config_is_declared()
    {
        foreach (var type in new[] { typeof(ClockConfig), typeof(StatsConfig), typeof(StatsSlot), typeof(WeatherConfig), typeof(ShortcutConfig), typeof(NowPlayingConfig), typeof(ControlConfig) })
        {
            var schema = SettingSchema.Read(type, type.IsSubclassOf(typeof(WidgetConfig)) ? typeof(WidgetConfig) : null);
            var declared = schema.Settings.Select(s => s.Key).Concat(schema.Parts).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var property in type.GetProperties().Where(p => p.SetMethod is { IsPublic: true }
                && p.DeclaringType == type && p.GetCustomAttributes(typeof(JsonExtensionDataAttribute), true).Length == 0))
                Assert.Contains(SettingSchema.KeyOf(property), declared);
        }
    }

    [Fact]
    public void The_clock_has_no_colour_or_size_setting()
    {
        var keys = SettingSchema.Read(typeof(ClockConfig), typeof(WidgetConfig)).Settings.Select(s => s.Key).ToList();

        Assert.DoesNotContain("textColor", keys);
        Assert.DoesNotContain("fontSize", keys);
        Assert.Contains("header", keys);
        Assert.Equal("", WidgetDescriptor.TryCreate(typeof(UrDeck.Widgets.Clock.ClockWidget), out _)!.Settings.Settings.Single(s => s.Key == "header").Options[0].Value);
    }

    [Fact]
    public void Weather_declares_its_place_with_the_coordinates_as_parts_and_days_at_4x4()
    {
        var schema = SettingSchema.Read(typeof(WeatherConfig), typeof(WidgetConfig));

        var location = Of(schema, "location");
        Assert.Equal(SettingKinds.Place, location.Kind);
        Assert.Equal(["latitude", "longitude"], schema.Parts);
        Assert.Equal(["4x4"], Of(schema, "days").Sizes);
        Assert.Equal(SettingKinds.Toggle, Of(schema, "animateForecast").Kind);
    }
}
