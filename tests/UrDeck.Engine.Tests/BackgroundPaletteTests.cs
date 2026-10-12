// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class BackgroundPaletteTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private void WriteTheme(string name, string json)
    {
        string dir = Path.Combine(_fixture.Root, "themes", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
    }

    [Theory]
    [InlineData("default-dark", "#0f0f1a", "#24507a")]
    [InlineData("default-light", "#e9ebf2", "#a6dbee")]
    [InlineData("glass", "#131c4f", "#6b3fa0")]
    public void Each_built_in_theme_has_a_palette(string theme, string first, string last)
    {
        var palette = BackgroundPalette.Resolve(null, _fixture.Theme(theme));

        Assert.Equal(SKColor.Parse(first), palette[0]);
        Assert.Equal(SKColor.Parse(last), palette[3]);
        Assert.NotNull(_fixture.Theme(theme).Definition.Colors!.BackgroundPalette);
    }

    [Fact]
    public void A_user_theme_without_a_palette_gets_one_derived_from_its_own_colours_without_a_warning()
    {
        WriteTheme("plain", """{ "colors": { "background": "#000000", "accent": "#ff0000" } }""");

        var theme = _fixture.Theme("plain");
        var palette = BackgroundPalette.Resolve(null, theme);

        Assert.Null(theme.Definition.Colors!.BackgroundPalette);
        Assert.Equal(new SKColor(0, 0, 0), palette[0]);
        Assert.Equal(new SKColor(51, 0, 0), palette[1]);
        Assert.Equal(new SKColor(102, 0, 0), palette[2]);
        Assert.Equal(new SKColor(153, 0, 0), palette[3]);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void A_light_user_theme_does_not_inherit_the_dark_palette()
    {
        WriteTheme("pale", """{ "colors": { "background": "#ffffff", "accent": "#0000ff" } }""");

        var palette = BackgroundPalette.Resolve(null, _fixture.Theme("pale"));

        Assert.Equal(new SKColor(255, 255, 255), palette[0]);
        Assert.NotEqual(SKColor.Parse("#0f0f1a"), palette[0]);
    }

    [Fact]
    public void A_partial_theme_with_only_a_palette_uses_it()
    {
        WriteTheme("only", """{ "colors": { "backgroundPalette": ["#112233", "#445566"] } }""");

        var palette = BackgroundPalette.Resolve(null, _fixture.Theme("only"));

        Assert.Equal(SKColor.Parse("#112233"), palette[0]);
        Assert.Equal(SKColor.Parse("#445566"), palette[1]);
        Assert.Equal("", _fixture.Log);
    }

    [Theory]
    [InlineData("""["#112233", "blue"]""")]
    [InlineData("""[]""")]
    [InlineData("""["#000001", "#000002", "#000003", "#000004", "#000005"]""")]
    public void An_invalid_palette_warns_and_is_derived(string palette)
    {
        WriteTheme("broken", $$"""{ "colors": { "background": "#000000", "accent": "#ff0000", "backgroundPalette": {{palette}} } }""");

        var theme = _fixture.Theme("broken");
        var colours = BackgroundPalette.Resolve(null, theme);

        Assert.Null(theme.Definition.Colors!.BackgroundPalette);
        Assert.Contains("colors.backgroundPalette", _fixture.Log);
        Assert.Equal(new SKColor(0, 0, 0), colours[0]);
        Assert.Equal(new SKColor(153, 0, 0), colours[3]);
    }

    [Fact]
    public void Fewer_than_four_colours_repeat_the_last()
    {
        var one = BackgroundPalette.Resolve(["#003300"], _fixture.Theme());
        var two = BackgroundPalette.Resolve(["#003300", "#00aa55"], _fixture.Theme());
        var four = BackgroundPalette.Resolve(["#000001", "#000002", "#000003", "#000004"], _fixture.Theme());

        Assert.All(one, c => Assert.Equal(SKColor.Parse("#003300"), c));
        Assert.Equal([SKColor.Parse("#003300"), SKColor.Parse("#00aa55"), SKColor.Parse("#00aa55"), SKColor.Parse("#00aa55")], two);
        Assert.Equal([SKColor.Parse("#000001"), SKColor.Parse("#000002"), SKColor.Parse("#000003"), SKColor.Parse("#000004")], four);
    }

    [Fact]
    public void Colours_from_the_configuration_win_over_the_theme()
    {
        var palette = BackgroundPalette.Resolve(["#abcdef"], _fixture.Theme("glass"));

        Assert.All(palette, c => Assert.Equal(SKColor.Parse("#abcdef"), c));
    }

    [Fact]
    public void Alpha_is_ignored()
    {
        var palette = BackgroundPalette.Resolve(["#40ff0000"], _fixture.Theme());

        Assert.All(palette, c => Assert.Equal((byte)255, c.Alpha));
        Assert.Equal((byte)255, palette[0].Red);
    }
}
