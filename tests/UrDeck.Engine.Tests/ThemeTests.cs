// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ThemeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-theme-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public ThemeTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private void WriteTheme(string name, string json)
    {
        string dir = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
    }

    private ThemeStore Store() => new(Path.Combine(_root, "themes"));

    [Fact]
    public void BuiltInThemes_AreAvailableWithNoThemesFolder()
    {
        var store = Store();

        Assert.Equal("default-dark", store.Load("default-dark").Name);
        Assert.DoesNotContain("not found", Log);
        var light = store.Load("default-light");
        Assert.Equal("default-light", light.Name);
        Assert.NotEqual(store.Load("default-dark").Definition.Colors!.Background, light.Definition.Colors!.Background);
    }

    [Fact]
    public void GlassTheme_IsBuiltInWithATranslucentCard()
    {
        var glass = Store().Load("glass");

        Assert.Equal("glass", glass.Name);
        Assert.DoesNotContain("not found", Log);
        Assert.Contains("glass", ThemeStore.BuiltInNames);
        Assert.True(SKColor.Parse(glass.Definition.Colors!.CardFill).Alpha < 255);
        Assert.True(glass.Definition.Card!.BorderWidth > 0);
        // Anything the theme leaves out comes from the default.
        Assert.Equal(Store().Load("default-dark").Definition.Colors!.Good, glass.Definition.Colors.Good);
    }

    [Fact]
    public void PartialUserTheme_TakesTheRestFromTheDefault()
    {
        WriteTheme("mine", """{ "colors": { "accent": "#ff00ff" }, "card": { "radius": 0 } }""");

        var loaded = Store().Load("mine");
        var dark = Store().Load("default-dark");

        Assert.Equal("#ff00ff", loaded.Definition.Colors!.Accent);
        Assert.Equal(0, loaded.Definition.Card!.Radius);
        Assert.Equal(dark.Definition.Colors!.Text, loaded.Definition.Colors.Text);
        Assert.Equal(dark.Definition.Card!.Gap, loaded.Definition.Card.Gap);
        Assert.Equal(dark.Definition.Typography!.LabelSize, loaded.Definition.Typography!.LabelSize);
    }

    [Fact]
    public void GaugeStyle_IsRingInTheBuiltInThemes_AndAUserThemeCanChangeIt()
    {
        foreach (string name in ThemeStore.BuiltInNames)
        {
            using var builtIn = ThemeResolver.Resolve(Store().Load(name), 275);
            Assert.Equal(UrDeck.Sdk.Components.GaugeStyle.Ring, builtIn.GaugeStyle);
        }

        WriteTheme("bars", """{ "gauge": { "style": "bar" } }""");
        using var bars = ThemeResolver.Resolve(Store().Load("bars"), 275);

        Assert.Equal(UrDeck.Sdk.Components.GaugeStyle.Bar, bars.GaugeStyle);
    }

    [Fact]
    public void APartialUserTheme_InheritsTheGaugeStyle()
    {
        WriteTheme("mine", """{ "colors": { "accent": "#ff00ff" } }""");
        using var theme = ThemeResolver.Resolve(Store().Load("mine"), 275);

        Assert.Equal(UrDeck.Sdk.Components.GaugeStyle.Ring, theme.GaugeStyle);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("disc")]
    public void APlainOrUnknownGaugeStyle_FallsBackToRingWithAWarning(string style)
    {
        WriteTheme("odd", "{ \"gauge\": { \"style\": \"" + style + "\" } }");
        using var theme = ThemeResolver.Resolve(Store().Load("odd"), 275);

        Assert.Equal(UrDeck.Sdk.Components.GaugeStyle.Ring, theme.GaugeStyle);
        Assert.Contains("gauge.style", Log);
    }

    [Fact]
    public void UnknownTheme_FallsBackToDefaultWithWarning()
    {
        var loaded = Store().Load("no-such-theme");

        Assert.Equal("default-dark", loaded.Name);
        Assert.Contains("no-such-theme", Log);
    }

    [Fact]
    public void UnreadableThemeFile_IsSkippedWithWarning()
    {
        WriteTheme("broken", "{ this is not json");

        var loaded = Store().Load("broken");

        Assert.Equal("default-dark", loaded.Name);
        Assert.Contains("broken", Log);
        Assert.Equal("default-light", Store().Load("default-light").Name);
    }

    [Fact]
    public void InvalidColour_UsesTheDefaultValueWithWarning()
    {
        WriteTheme("badcolour", """{ "colors": { "text": "not a colour", "accent": "#00ff00" } }""");

        var loaded = Store().Load("badcolour");

        Assert.Equal("badcolour", loaded.Name);
        Assert.Equal(Store().Load("default-dark").Definition.Colors!.Text, loaded.Definition.Colors!.Text);
        Assert.Equal("#00ff00", loaded.Definition.Colors.Accent);
        Assert.Contains("colors.text", Log);
    }

    [Fact]
    public void UserThemeWithBuiltInName_TakesPrecedence()
    {
        WriteTheme("default-light", """{ "colors": { "background": "#123456" } }""");

        Assert.Equal("#123456", Store().Load("default-light").Definition.Colors!.Background);
    }

    [Fact]
    public void ThemeNameThatEscapesTheFolder_IsNotATheme()
    {
        Assert.Equal("default-dark", Store().Load("..").Name);
        Assert.Equal("default-dark", Store().Load("..\\themes").Name);
    }

    [Fact]
    public void FontPathOutsideTheFolder_IsRejected()
    {
        WriteTheme("escape", """{ "typography": { "font": "..\\other\\evil.ttf" } }""");

        var loaded = Store().Load("escape");

        Assert.Equal("InterVariable.ttf", loaded.Font.Spec);
        Assert.Contains("leaves the theme folder", Log);
    }

    [Fact]
    public void MissingFontFile_FallsBackToTheDefaultFont()
    {
        WriteTheme("nofont", """{ "typography": { "font": "missing.ttf" } }""");

        using var theme = ThemeResolver.Resolve(Store().Load("nofont"), 275);

        Assert.Contains("missing.ttf", Log);
        Assert.Contains("Inter", theme.GetTypeface(TextRole.Value).FamilyName);
    }

    [Fact]
    public void UninstalledFamily_FallsBackToTheDefaultFont()
    {
        WriteTheme("nofamily", """{ "typography": { "font": "Definitely Not An Installed Font" } }""");

        using var theme = ThemeResolver.Resolve(Store().Load("nofamily"), 275);

        Assert.Contains("Inter", theme.GetTypeface(TextRole.Label).FamilyName);
    }

    [Fact]
    public void BundledFontFile_InTheThemeFolder_IsUsed()
    {
        WriteTheme("bundled", """{ "typography": { "font": "copy.ttf" } }""");
        using (var inter = typeof(ThemeStore).Assembly.GetManifestResourceStream("UrDeck.Themes.InterVariable.ttf"))
        using (var file = File.Create(Path.Combine(_root, "themes", "bundled", "copy.ttf")))
            inter!.CopyTo(file);

        using var theme = ThemeResolver.Resolve(Store().Load("bundled"), 275);

        Assert.DoesNotContain("copy.ttf", Log);
        Assert.Contains("Inter", theme.GetTypeface(TextRole.Value).FamilyName);
    }

    [Fact]
    public void Weights_AreAppliedPerRole()
    {
        using var theme = ThemeResolver.Resolve(Store().Load("default-dark"), 275);

        // Heavier weights draw wider glyphs; the value role (600) must be wider than the title role (300).
        using var value = new SKFont(theme.GetTypeface(TextRole.Value), 100);
        using var title = new SKFont(theme.GetTypeface(TextRole.Title), 100);
        Assert.True(value.MeasureText("0123456789") > title.MeasureText("0123456789"));
    }

    [Fact]
    public void Sizes_DoubleWhenTheCellSizeDoubles()
    {
        var loaded = Store().Load("default-dark");
        using var small = ThemeResolver.Resolve(loaded, 275);
        using var big = ThemeResolver.Resolve(loaded, 550);

        Assert.Equal(small.CardRadius * 2, big.CardRadius, 3);
        Assert.Equal(small.Gap * 2, big.Gap, 3);
        Assert.Equal(small.Padding * 2, big.Padding, 3);
        Assert.Equal(small.LabelSize * 2, big.LabelSize, 3);
        Assert.Equal(small.TitleSize * 2, big.TitleSize, 3);
        Assert.Equal(small.UnitRatio, big.UnitRatio);
    }

    [Fact]
    public void EveryBuiltInTheme_HasPressValues()
    {
        foreach (string name in ThemeStore.BuiltInNames)
        {
            var press = PressStyle.Resolve(Store().Load(name));

            Assert.InRange(press.Scale, 0.5f, 1f);
            Assert.InRange(press.Opacity, 0.1f, 1f);
            Assert.True(press.IsVisible);
        }
    }

    [Fact]
    public void AnOlderThemeWithoutPressValues_TakesTheDefaults_WithoutAWarning()
    {
        WriteTheme("old", """{ "colors": { "accent": "#ff00ff" } }""");

        var press = PressStyle.Resolve(Store().Load("old"));

        Assert.Equal(PressStyle.Resolve(Store().Load("default-dark")), press);
        Assert.DoesNotContain("press", Log);
    }

    [Fact]
    public void AUserTheme_CanOverrideOneOfTheTwoPressValues()
    {
        WriteTheme("firm", """{ "press": { "scale": 0.9 } }""");

        var press = PressStyle.Resolve(Store().Load("firm"));
        var defaults = PressStyle.Resolve(Store().Load("default-dark"));

        Assert.Equal(0.9f, press.Scale);
        Assert.Equal(defaults.Opacity, press.Opacity);
        Assert.DoesNotContain("press", Log);
    }

    [Theory]
    [InlineData("scale", "2")]
    [InlineData("scale", "0.4")]
    [InlineData("opacity", "0")]
    [InlineData("opacity", "1.5")]
    public void APressValueOutOfRange_FallsBackToTheDefault_WithAWarning(string name, string value)
    {
        WriteTheme("wild", "{ \"press\": { \"" + name + "\": " + value + " } }");

        var press = PressStyle.Resolve(Store().Load("wild"));

        Assert.Equal(PressStyle.Resolve(Store().Load("default-dark")), press);
        Assert.Contains("press." + name, Log);
    }

    [Fact]
    public void PressValuesOfOne_SwitchTheFeedbackOff()
    {
        WriteTheme("still", """{ "press": { "scale": 1, "opacity": 1 } }""");

        var press = PressStyle.Resolve(Store().Load("still"));

        Assert.Equal(new PressStyle(1f, 1f), press);
        Assert.False(press.IsVisible);
        Assert.DoesNotContain("press", Log);
    }

    [Fact]
    public void Colours_CarryTransparency()
    {
        WriteTheme("glass", """{ "colors": { "cardFill": "#cc102030" } }""");

        using var theme = ThemeResolver.Resolve(Store().Load("glass"), 275);

        Assert.Equal(new SKColor(0x10, 0x20, 0x30, 0xcc), theme.CardFill);
    }
}
