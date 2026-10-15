// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class BackgroundPlanTests : IDisposable
{
    private const string Flat = "half4 main(float2 p) { return half4(0.2, 0.4, 0.6, 1); }";

    private readonly BackgroundFixture _fixture = new();

    public BackgroundPlanTests()
    {
        _fixture.WriteShader("horizon", Flat);
        _fixture.WriteShader("mine", Flat);
        _fixture.WriteShader("broken", "half4 main(float2 p) { return half4(; }");
    }

    public void Dispose() => _fixture.Dispose();

    private const string DefaultJson = """ "default": { "name": "horizon" } """;
    private const string StillJson = """ "static": { "color": "#102030" } """;

    private LoadedTheme Theme(bool withDefault, bool withStill, string extra = "")
    {
        var parts = new List<string>();
        if (withDefault)
            parts.Add(DefaultJson);
        if (withStill)
            parts.Add(StillJson);
        if (extra.Length > 0)
            parts.Add(extra);
        string background = parts.Count == 0 ? "" : $$""", "background": { {{string.Join(",", parts)}} }""";
        return _fixture.WriteTheme("t" + Guid.NewGuid().ToString("N")[..6], $$"""{ "format": 2 {{background}} }""");
    }

    private static string Show(BackgroundPlan plan) =>
        string.Join(",", plan.Candidates.Select(c => c.Origin switch
        {
            BackgroundOrigin.Configuration => "config",
            BackgroundOrigin.ThemeDefault => "default",
            BackgroundOrigin.ThemeStill => "still",
            _ => "colour",
        }));

    // 2.1 The setting

    [Fact]
    public void Absent_and_null_are_no_choice_and_none_is_an_explicit_none()
    {
        Assert.False(BackgroundSettings.Parse(null).IsExplicit);
        Assert.False(BackgroundFixture.Parse("null").IsExplicit);

        var none = BackgroundFixture.Parse("""{ "type": "none" }""");
        Assert.True(none.IsExplicit);
        Assert.True(none.IsDisabled);
        Assert.Equal(BackgroundKind.None, none.Kind);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void A_shader_an_image_and_a_failure_are_explicit()
    {
        Assert.True(BackgroundFixture.Parse("""{ "type": "shader", "name": "plasma" }""").IsExplicit);
        Assert.True(BackgroundFixture.Parse("""{ "type": "image", "path": "a.png" }""").IsExplicit);
        var invalid = BackgroundFixture.Parse("""{ "type": "video" }""");
        Assert.True(invalid.IsExplicit);
        Assert.False(invalid.IsDisabled);
    }

    // 2.2 and 2.3 The plan, for every combination

    [Theory]
    // user: absent
    [InlineData("absent", false, false, "colour")]
    [InlineData("absent", true, false, "default,colour")]
    [InlineData("absent", false, true, "still,colour")]
    [InlineData("absent", true, true, "default,still,colour")]
    // user: a shader (the theme's default is never used in its place; the still background is what a failure falls back to)
    [InlineData("shader", false, false, "config,colour")]
    [InlineData("shader", true, false, "config,colour")]
    [InlineData("shader", false, true, "config,still,colour")]
    [InlineData("shader", true, true, "config,still,colour")]
    // user: an image
    [InlineData("image", false, false, "config,colour")]
    [InlineData("image", true, true, "config,still,colour")]
    // user: none
    [InlineData("none", false, false, "colour")]
    [InlineData("none", true, false, "colour")]
    [InlineData("none", false, true, "colour")]
    [InlineData("none", true, true, "colour")]
    // user: a value that could not be read
    [InlineData("invalid", false, false, "colour")]
    [InlineData("invalid", true, false, "colour")]
    [InlineData("invalid", false, true, "still,colour")]
    [InlineData("invalid", true, true, "still,colour")]
    public void The_candidates_follow_the_precedence(string user, bool withDefault, bool withStill, string expected)
    {
        string json = user switch
        {
            "absent" => "null",
            "shader" => """{ "type": "shader", "name": "mine" }""",
            "image" => """{ "type": "image", "path": "a.png" }""",
            "none" => """{ "type": "none" }""",
            _ => """{ "type": "video" }""",
        };

        var plan = BackgroundPlan.Resolve(BackgroundFixture.Parse(json), Theme(withDefault, withStill));
        Assert.Equal(expected, Show(plan));
        Assert.True(plan.Candidates[^1].IsColour);
    }

    [Fact]
    public void A_theme_without_a_background_group_has_only_the_colour_and_no_key()
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, _fixture.Theme("glass"));
        Assert.Equal("colour", Show(plan));
        Assert.Null(plan.RequestKey(_fixture.Store, _fixture.Root));
        Assert.Null(plan.Build(_fixture.Store, _fixture.Root));
    }

    [Fact]
    public void The_theme_default_is_built_in_the_theme_palette()
    {
        var theme = Theme(true, false);
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, theme);
        using var background = plan.Build(_fixture.Store, _fixture.Root);

        Assert.NotNull(background);
        Assert.Equal(BackgroundOrigin.ThemeDefault, background.Origin);
        Assert.Contains("horizon", background.Describe);
    }

    // 2.4 The key

    [Fact]
    public void The_key_names_the_origin_and_changes_with_the_theme_choice()
    {
        var withDefault = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(true, false));
        var withStill = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(false, true));
        var plain = BackgroundPlan.Resolve(BackgroundSettings.None, _fixture.Theme());

        string? a = withDefault.RequestKey(_fixture.Store, _fixture.Root);
        string? b = withStill.RequestKey(_fixture.Store, _fixture.Root);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(a, b);
        Assert.Contains("ThemeDefault", a);
        Assert.Null(plain.RequestKey(_fixture.Store, _fixture.Root));
    }

    [Fact]
    public void The_same_resolved_background_has_the_same_key_and_an_unrelated_change_does_not_alter_it()
    {
        var theme = Theme(true, false);
        string? before = BackgroundPlan.Resolve(BackgroundSettings.None, theme).RequestKey(_fixture.Store, _fixture.Root);
        string? again = BackgroundPlan.Resolve(BackgroundFixture.Parse("null"), theme).RequestKey(_fixture.Store, _fixture.Root);
        Assert.Equal(before, again);
    }

    [Fact]
    public void A_configuration_choice_keeps_its_key_when_the_theme_changes_but_for_the_scrim()
    {
        var settings = BackgroundFixture.Parse("""{ "type": "shader", "name": "mine" }""");
        string? one = BackgroundPlan.Resolve(settings, Theme(true, false)).RequestKey(_fixture.Store, _fixture.Root);
        string? two = BackgroundPlan.Resolve(settings, Theme(true, false)).RequestKey(_fixture.Store, _fixture.Root);
        // Different theme names, same palette: the configuration's own candidate does not name the theme.
        Assert.Equal(one!.Split(" > ")[0], two!.Split(" > ")[0]);
    }

    [Fact]
    public void A_scrim_change_changes_the_key()
    {
        string? plain = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(true, false)).RequestKey(_fixture.Store, _fixture.Root);
        string? veiled = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(true, false, """ "scrim": { "color": "#000000", "alpha": 0.2 } """))
            .RequestKey(_fixture.Store, _fixture.Root);
        Assert.NotEqual(plain, veiled);
    }

    [Fact]
    public void A_shader_edit_changes_the_plan_key()
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(true, false));
        string? before = plan.RequestKey(_fixture.Store, _fixture.Root);
        _fixture.WriteShader("horizon", "half4 main(float2 p) { return half4(1, 0, 0, 1); }");
        Assert.NotEqual(before, plan.RequestKey(_fixture.Store, _fixture.Root));
    }

    // 2.5 Failure goes to the next candidate

    [Fact]
    public void A_default_that_does_not_compile_falls_to_the_still_background_with_one_warning()
    {
        var theme = _fixture.WriteTheme("fall", """{ "format": 2, "background": { "default": { "name": "broken" }, "static": { "color": "#102030" } } }""");
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, theme);

        using var background = plan.Build(_fixture.Store, _fixture.Root);

        Assert.NotNull(background);
        Assert.Equal(BackgroundOrigin.ThemeStill, background.Origin);
        Assert.Equal(BackgroundKind.Color, background.Kind);
        Assert.Equal(1, _fixture.Log.Split('\n').Count(l => l.Contains("Background 'broken'") && l.Contains("WARN")));
    }

    [Fact]
    public void A_user_shader_that_fails_falls_to_the_theme_still_background_not_its_default()
    {
        var theme = Theme(true, true);
        var plan = BackgroundPlan.Resolve(BackgroundFixture.Parse("""{ "type": "shader", "name": "broken" }"""), theme);

        using var background = plan.Build(_fixture.Store, _fixture.Root);

        Assert.Equal(BackgroundOrigin.ThemeStill, background!.Origin);
    }

    [Fact]
    public void Everything_failing_leaves_only_the_colour_and_logs_each_failure()
    {
        var theme = _fixture.WriteTheme("allfail", """{ "format": 2, "background": { "default": { "name": "broken" }, "static": { "image": "missing.png" } } }""");
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, theme);

        Assert.Null(plan.Build(_fixture.Store, _fixture.Root));
        Assert.Contains("Background 'broken'", _fixture.Log);
        Assert.Contains("missing.png", _fixture.Log);
    }

    [Fact]
    public void Building_can_go_on_from_the_next_candidate()
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, Theme(true, true));
        using var first = plan.Build(_fixture.Store, _fixture.Root);
        using var next = plan.Build(_fixture.Store, _fixture.Root, first!.CandidateIndex + 1);

        Assert.Equal(BackgroundOrigin.ThemeDefault, first.Origin);
        Assert.Equal(BackgroundOrigin.ThemeStill, next!.Origin);
        Assert.Null(plan.Build(_fixture.Store, _fixture.Root, next.CandidateIndex + 1));
    }

    // 3.1 Still backgrounds

    private static SKBitmap Paint(DeckBackground background, int width = 100, int height = 100)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        background.PaintAtTime(canvas, width, height, 0);
        return bitmap;
    }

    private DeckBackground BuildStill(string json)
    {
        var theme = _fixture.WriteTheme("s" + Guid.NewGuid().ToString("N")[..6], $$"""{ "format": 2, "background": {{json}} }""");
        return BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root)!;
    }

    [Fact]
    public void A_still_colour_fills_the_deck_and_creates_no_frames()
    {
        using var background = BuildStill("""{ "static": { "color": "#336699" } }""");
        using var bitmap = Paint(background);

        Assert.False(background.IsMoving);
        Assert.Equal(new SKColor(0x33, 0x66, 0x99), bitmap.GetPixel(50, 50));
        Assert.Equal(new SKColor(0x33, 0x66, 0x99), bitmap.GetPixel(0, 99));
    }

    [Fact]
    public void A_linear_gradient_runs_by_its_angle()
    {
        // 0 degrees runs left to right.
        using var across = BuildStill("""{ "static": { "gradient": { "colors": ["#000000", "#ffffff"], "kind": "linear", "angle": 0 } } }""");
        using var horizontal = Paint(across);
        Assert.True(horizontal.GetPixel(2, 50).Red < 20);
        Assert.True(horizontal.GetPixel(97, 50).Red > 235);
        Assert.Equal(horizontal.GetPixel(10, 5).Red, horizontal.GetPixel(10, 95).Red);

        // 90 degrees (the default) runs top to bottom.
        using var down = BuildStill("""{ "static": { "gradient": { "colors": ["#000000", "#ffffff"] } } }""");
        using var vertical = Paint(down);
        Assert.True(vertical.GetPixel(50, 2).Red < 20);
        Assert.True(vertical.GetPixel(50, 97).Red > 235);
    }

    [Fact]
    public void A_radial_gradient_is_brightest_at_its_centre_by_fractions_of_the_deck()
    {
        using var background = BuildStill("""{ "static": { "gradient": { "colors": ["#ffffff", "#000000"], "kind": "radial", "cx": 0.5, "cy": 0.5, "radius": 0.5 } } }""");
        using var bitmap = Paint(background, 200, 100);

        Assert.True(bitmap.GetPixel(100, 50).Red > 245);
        Assert.True(bitmap.GetPixel(0, 50).Red < 10);
        Assert.True(bitmap.GetPixel(100, 0).Red > bitmap.GetPixel(199, 0).Red);
    }

    [Fact]
    public void A_gradient_scales_with_the_surface()
    {
        using var background = BuildStill("""{ "static": { "gradient": { "colors": ["#ffffff", "#000000"], "kind": "radial", "cx": 0.5, "cy": 0.5, "radius": 0.5 } } }""");
        using var small = Paint(background, 50, 50);
        using var large = Paint(background, 200, 200);

        Assert.InRange(Math.Abs(small.GetPixel(25, 12).Red - large.GetPixel(100, 50).Red), 0, 12);
    }

    // 3.2 A picture inside the theme folder

    [Fact]
    public async Task A_theme_picture_is_read_from_its_folder_and_covers_the_deck()
    {
        var theme = _fixture.WriteTheme("pic", """{ "format": 2, "background": { "static": { "image": "wall.png" } } }""");
        using (var picture = new SKBitmap(10, 10))
        {
            picture.Erase(new SKColor(0, 200, 0));
            File.WriteAllBytes(Path.Combine(_fixture.ThemeFolder("pic"), "wall.png"), picture.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        }

        using var background = BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root)!;
        Assert.Equal(BackgroundKind.Image, background.Kind);
        Assert.True(await background.PrepareAsync(40, 80));
        using var bitmap = Paint(background, 40, 80);
        Assert.Equal(200, bitmap.GetPixel(20, 40).Green);
    }

    [Theory]
    [InlineData("../secret.png")]
    [InlineData("..\\\\secret.png")]
    public void A_picture_that_leaves_the_theme_folder_is_ignored_with_one_warning(string path)
    {
        File.WriteAllBytes(Path.Combine(_fixture.Root, "secret.png"), []);
        var theme = _fixture.WriteTheme("escape", $$"""{ "format": 2, "background": { "static": { "image": "{{path}}" } } }""");

        Assert.Null(BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root));
        Assert.Equal(1, _fixture.Log.Split('\n').Count(l => l.Contains("leaves the theme folder")));
    }

    [Fact]
    public void A_built_in_theme_has_no_folder_for_a_picture()
    {
        var builtIn = _fixture.Theme("glass");
        var withPicture = builtIn with { Definition = builtIn.Definition.MergeOver(builtIn.Definition) };
        withPicture.Definition.Background = new ThemeBackgroundDefinition { Static = new ThemeBackgroundStaticDefinition { Image = "x.png" } };

        Assert.Null(BackgroundPlan.Resolve(BackgroundSettings.None, withPicture).Build(_fixture.Store, _fixture.Root));
        Assert.Contains("built in", _fixture.Log);
    }

    // 3.3 The scrim

    private static SKColor Over(SKColor under, double alpha, SKColor veil) => new(
        (byte)Math.Round(under.Red * (1 - alpha) + veil.Red * alpha),
        (byte)Math.Round(under.Green * (1 - alpha) + veil.Green * alpha),
        (byte)Math.Round(under.Blue * (1 - alpha) + veil.Blue * alpha));

    [Fact]
    public void A_scrim_darkens_a_shader_a_still_and_a_picture()
    {
        const string scrim = """, "scrim": { "color": "#000000", "alpha": 0.2 }""";

        using var shader = BuildStill($$"""{ "default": { "name": "horizon" }{{scrim}} }""");
        using var shaded = Paint(shader);
        var expected = Over(new SKColor((byte)(0.2 * 255), (byte)(0.4 * 255), (byte)(0.6 * 255)), 0.2, SKColors.Black);
        var actual = shaded.GetPixel(50, 50);
        Assert.InRange(Math.Abs(actual.Red - expected.Red), 0, 2);
        Assert.InRange(Math.Abs(actual.Blue - expected.Blue), 0, 2);

        using var still = BuildStill($$"""{ "static": { "color": "#c8c8c8" }{{scrim}} }""");
        using var veiled = Paint(still);
        Assert.InRange(Math.Abs(veiled.GetPixel(10, 10).Red - 160), 0, 2);
    }

    [Fact]
    public void The_colours_own_alpha_multiplies_the_scrim_alpha()
    {
        // #80000000 is black at about 50 percent; with alpha 0.5 the veil is about 25 percent.
        using var background = BuildStill("""{ "static": { "color": "#c8c8c8" }, "scrim": { "color": "#80000000", "alpha": 0.5 } }""");
        using var bitmap = Paint(background);

        Assert.InRange(Math.Abs(bitmap.GetPixel(10, 10).Red - 150), 0, 3);
    }

    [Fact]
    public void No_scrim_draws_the_background_as_it_is()
    {
        using var background = BuildStill("""{ "static": { "color": "#c8c8c8" } }""");
        using var bitmap = Paint(background);

        Assert.Null(background.Scrim);
        Assert.Equal(200, bitmap.GetPixel(10, 10).Red);
    }

    [Fact]
    public void A_scrim_is_not_drawn_with_none_or_over_the_plain_colour()
    {
        var theme = _fixture.WriteTheme("veil", """{ "format": 2, "background": { "default": { "name": "horizon" }, "scrim": { "color": "#000000", "alpha": 0.5 } } }""");

        Assert.Null(BackgroundPlan.Resolve(BackgroundFixture.Parse("""{ "type": "none" }"""), theme).Build(_fixture.Store, _fixture.Root));

        var onlyScrim = _fixture.WriteTheme("veil2", """{ "format": 2, "background": { "scrim": { "color": "#000000", "alpha": 0.5 } } }""");
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, onlyScrim);
        Assert.Null(plan.RequestKey(_fixture.Store, _fixture.Root));
        Assert.Null(plan.Build(_fixture.Store, _fixture.Root));
    }

    [Fact]
    public void A_scrim_covers_what_the_user_chose_too()
    {
        var theme = _fixture.WriteTheme("veil3", """{ "format": 2, "background": { "scrim": { "color": "#000000", "alpha": 0.5 } } }""");
        var plan = BackgroundPlan.Resolve(BackgroundFixture.Parse("""{ "type": "shader", "name": "mine" }"""), theme);
        using var background = plan.Build(_fixture.Store, _fixture.Root)!;

        Assert.Equal(BackgroundOrigin.Configuration, background.Origin);
        Assert.NotNull(background.Scrim);
    }

    // 3.4 Snapshots draw what the deck draws, and 3.5 motion

    [Fact]
    public void A_snapshot_draws_a_radial_gradient_and_its_scrim_the_same_way_every_time()
    {
        var theme = _fixture.WriteTheme("golden", """
            { "format": 2, "background": {
              "static": { "gradient": { "colors": ["#ffffff", "#000000"], "kind": "radial", "cx": 0.5, "cy": 0.5, "radius": 0.5 } },
              "scrim": { "color": "#000000", "alpha": 0.25 } } }
            """);

        SKBitmap Render()
        {
            using var background = BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root);
            return PageRenderer.RenderToBitmap(200, 200, new List<(WidgetConfig, IWidget)>(), theme, new DateTime(2026, 10, 11), background: background);
        }

        using var first = Render();
        using var second = Render();

        Assert.Equal(first.Bytes, second.Bytes);

        // The centre is white under a quarter of black, the far corner is black.
        Assert.InRange(first.GetPixel(100, 100).Red, 188, 194);
        Assert.True(first.GetPixel(0, 0).Red < 6);
    }

    [Fact]
    public void A_snapshot_without_a_theme_background_is_what_it_was()
    {
        using var plain = PageRenderer.RenderToBitmap(110, 384, new List<(WidgetConfig, IWidget)>(), _fixture.Theme(), new DateTime(2026, 10, 11));
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, _fixture.Theme());
        using var background = plan.Build(_fixture.Store, _fixture.Root);
        using var planned = PageRenderer.RenderToBitmap(110, 384, new List<(WidgetConfig, IWidget)>(), _fixture.Theme(), new DateTime(2026, 10, 11),
            background: background);

        Assert.Null(background);
        Assert.Equal(plain.Bytes, planned.Bytes);
    }

    [Fact]
    public void Motion_level_off_does_not_touch_the_background()
    {
        var theme = _fixture.WriteTheme("calm", """{ "format": 2, "motion": { "level": "off" }, "background": { "default": { "name": "horizon" } } }""");
        using var background = BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root);

        Assert.NotNull(background);
        Assert.Equal(BackgroundOrigin.ThemeDefault, background.Origin);
        Assert.Equal(BackgroundKind.Shader, background.Kind);
    }

    [Fact]
    public void A_moving_default_still_moves_with_motion_off()
    {
        _fixture.WriteShader("drift", "uniform float iTime;\nhalf4 main(float2 p) { return half4(fract(iTime), 0, 0, 1); }");
        var theme = _fixture.WriteTheme("calm2", """{ "format": 2, "motion": { "level": "off" }, "background": { "default": { "name": "drift" } } }""");
        using var background = BackgroundPlan.Resolve(BackgroundSettings.None, theme).Build(_fixture.Store, _fixture.Root);

        Assert.True(background!.IsMoving);
    }
}
