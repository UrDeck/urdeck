// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>Per-role fonts, the fallback chain, the weight warnings and the optical size (theme-typography).</summary>
public sealed class ThemeFontTests : IDisposable
{
    private static readonly SKFourByteTag Wght = SKFourByteTag.Parse("wght");
    private static readonly SKFourByteTag Opsz = SKFourByteTag.Parse("opsz");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-font-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public ThemeFontTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        ThemeResolver.TypefaceCreated = null;
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private static int LinesWith(string text) => Log.Split('\n').Count(l => l.Contains(text, StringComparison.Ordinal));

    private ThemeStore Store() => new(Path.Combine(_root, "themes"));

    private string WriteTheme(string name, string json)
    {
        string dir = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
        return dir;
    }

    /// <summary>Copies the embedded Inter into the theme folder under <paramref name="fileName"/>.</summary>
    private static void CopyInter(string dir, string fileName)
    {
        using var inter = typeof(ThemeStore).Assembly.GetManifestResourceStream("UrDeck.Themes.InterVariable.ttf")!;
        using var file = File.Create(Path.Combine(dir, fileName));
        inter.CopyTo(file);
    }

    private static float Position(SKTypeface typeface, SKFourByteTag axis) =>
        typeface.VariationDesignPosition.First(c => c.Axis == axis).Value;

    private const string EqualWeights = """ "weights": { "value": 400, "unit": 400, "label": 400, "body": 400, "title": 400 } """;

    // ---------------------------------------------------------------- per-role fonts

    [Theory]
    [InlineData("value", TextRole.Value)]
    [InlineData("unit", TextRole.Unit)]
    [InlineData("label", TextRole.Label)]
    public void ARoleFont_IsUsedForThatRoleOnly(string key, TextRole role)
    {
        string dir = WriteTheme("roles", $$"""{ "typography": { {{EqualWeights}}, "fonts": { "{{key}}": "own.ttf" } } }""");
        CopyInter(dir, "own.ttf");

        using var theme = ThemeResolver.Resolve(Store().Load("roles"), 275);

        // Every role asks for weight 400, so without the role's font all five would share one typeface.
        var others = Enum.GetValues<TextRole>().Where(r => r != role).Select(theme.GetTypeface).Distinct().ToList();
        Assert.Single(others);
        Assert.NotSame(others[0], theme.GetTypeface(role));
        Assert.Equal("", Log);
    }

    [Fact]
    public void RolesThatAgree_ShareOneTypeface()
    {
        string dir = WriteTheme("shared", $$"""{ "typography": { {{EqualWeights}}, "fonts": { "value": "own.ttf", "unit": "own.ttf" } } }""");
        CopyInter(dir, "own.ttf");

        using var theme = ThemeResolver.Resolve(Store().Load("shared"), 275);

        Assert.Same(theme.GetTypeface(TextRole.Value), theme.GetTypeface(TextRole.Unit));
        Assert.NotSame(theme.GetTypeface(TextRole.Value), theme.GetTypeface(TextRole.Label));
    }

    public static TheoryData<string, string> EscapesPerRole()
    {
        var data = new TheoryData<string, string>();
        foreach (string role in new[] { "value", "unit", "label" })
            foreach (string path in new[] { "../other/font.ttf", "..\\\\other\\\\font.ttf", "sub/..\\\\..\\\\font.ttf" })
                data.Add(role, path);
        return data;
    }

    [Theory]
    [MemberData(nameof(EscapesPerRole))]
    public void ARoleFontThatLeavesTheFolder_IsRefused(string key, string path)
    {
        WriteTheme("escape", $$"""{ "typography": { "fonts": { "{{key}}": "{{path}}" } } }""");

        var loaded = Store().Load("escape");
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Empty(loaded.RoleFonts);
        Assert.Null(loaded.Definition.Typography!.Fonts!.GetType().GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.GetValue(loaded.Definition.Typography.Fonts));
        Assert.Equal(1, LinesWith("leaves the theme folder"));
        Assert.Contains($"for the {key} role", Log);
    }

    [Fact]
    public void AMissingRoleFont_FallsBackToTheThemeFont_WithOneWarningAcrossResolves()
    {
        WriteTheme("missing", $$"""{ "typography": { {{EqualWeights}}, "fonts": { "label": "missing.ttf" } } }""");
        var loaded = Store().Load("missing");

        for (int i = 0; i < 3; i++)
        {
            using var theme = ThemeResolver.Resolve(loaded, 275f + i);
            Assert.Same(theme.GetTypeface(TextRole.Body), theme.GetTypeface(TextRole.Label));
        }
        using var dock = ThemeResolver.Resolve(loaded, 120);

        Assert.Equal(1, LinesWith("missing.ttf"));
        Assert.Contains("Theme 'missing'", Log);
        Assert.Contains("label role", Log);
    }

    [Fact]
    public void AMissingRoleFont_AndAMissingThemeFont_FallBackToTheDefaultTheme_OneStepAtATime()
    {
        WriteTheme("chain", """{ "typography": { "font": "gone.ttf", "fonts": { "value": "missing.ttf" } } }""");
        var loaded = Store().Load("chain");

        using (ThemeResolver.Resolve(loaded, 275))
        {
        }
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(1, LinesWith("'missing.ttf' for the value role"));
        Assert.Equal(1, LinesWith("'gone.ttf' could not be loaded; using the default theme's font"));
        Assert.Contains("Inter", theme.GetTypeface(TextRole.Value).FamilyName);
    }

    [Fact]
    public void ReloadingTheTheme_WarnsAgain()
    {
        WriteTheme("again", """{ "typography": { "fonts": { "unit": "missing.ttf" } } }""");

        using (ThemeResolver.Resolve(Store().Load("again"), 275))
        {
        }
        using var theme = ThemeResolver.Resolve(Store().Load("again"), 275);

        Assert.Equal(2, LinesWith("missing.ttf"));
    }

    // ---------------------------------------------------------------- weights

    [Fact]
    public void AWeightOutsideTheAxis_IsClamped_AndWarnedOncePerRole()
    {
        WriteTheme("heavy", """{ "typography": { "weights": { "label": 950 } } }""");
        var loaded = Store().Load("heavy");

        using (ThemeResolver.Resolve(loaded, 275))
        {
        }
        using var theme = ThemeResolver.Resolve(loaded, 140);

        Assert.Equal(900f, Position(theme.GetTypeface(TextRole.Label), Wght));
        Assert.Equal(1, LinesWith("weight"));
        Assert.Contains("the label weight 950 is outside the weight range 100..900", Log);
        Assert.Contains("Theme 'heavy'", Log);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void AWeightAStaticFontDoesNotHave_IsWarnedOnce()
    {
        string arial = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        string dir = WriteTheme("static", """{ "typography": { "font": "static.ttf", "weights": { "value": 400, "unit": 400, "label": 400, "body": 500, "title": 400 } } }""");
        File.Copy(arial, Path.Combine(dir, "static.ttf"));
        var loaded = Store().Load("static");

        using (ThemeResolver.Resolve(loaded, 275))
        {
        }
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(1, LinesWith("weight"));
        Assert.Contains("the body weight 500 has no effect: font 'static.ttf' has one weight (400)", Log);
    }

    [Fact]
    public void AnInstalledFamily_IsNotChecked()
    {
        WriteTheme("family", """{ "typography": { "font": "Definitely Not Installed", "weights": { "label": 950 } } }""");

        using var theme = ThemeResolver.Resolve(Store().Load("family"), 275);

        // The family is missing and falls back to the default theme's Inter, whose range is checked; the family itself is not.
        Assert.Equal(1, LinesWith("could not be loaded"));
        Assert.Equal(1, LinesWith("outside the weight range"));
    }

    // ---------------------------------------------------------------- version 1 themes

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void TheVersion1Themes_LogNothing_AndCreateOneTypefacePerWeight(string name)
    {
        var created = new List<SKTypeface>();
        ThemeResolver.TypefaceCreated = created.Add;
        var loaded = ThemeStore.LoadBuiltIn(name)!;
        var w = loaded.Definition.Typography!.Weights!;
        int weights = new[] { w.Value, w.Unit, w.Label, w.Body, w.Title }.Distinct().Count();

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal("", Log);
        Assert.Equal(weights + 1, created.Count); // the base typeface read from the file, and a clone per weight
        Assert.Equal(weights, theme.Typefaces.Values.Distinct().Count());
        foreach (var typeface in theme.Typefaces.Values)
            Assert.Equal(14f, Position(typeface, Opsz)); // Inter's own default: the optical size is not touched
    }

    [Fact]
    public void ResolvingAndDisposing_LeavesNoTypefaceBehind()
    {
        var created = new List<SKTypeface>();
        ThemeResolver.TypefaceCreated = created.Add;
        string dir = WriteTheme("many", """{ "typography": { "opticalSize": "auto", "fonts": { "value": "own.ttf", "label": "missing.ttf" } } }""");
        CopyInter(dir, "own.ttf");

        for (int i = 0; i < 20; i++)
        {
            foreach (var loaded in new[] { Store().Load("many"), ThemeStore.LoadBuiltIn("glass")! })
            {
                var page = ThemeResolver.Resolve(loaded, 275);
                var dock = ThemeResolver.Resolve(loaded, 120);
                page.Dispose();
                dock.Dispose();
            }
        }

        Assert.NotEmpty(created);
        Assert.All(created, typeface => Assert.Equal(IntPtr.Zero, typeface.Handle));
    }

    // ---------------------------------------------------------------- optical size

    private static int Ink(SKTypeface typeface, string text = "0123456789")
    {
        using var font = new SKFont(typeface, 200) { Edging = SKFontEdging.Antialias };
        using var bitmap = new SKBitmap(1400, 260);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, 10, 220, SKTextAlign.Left, font, paint);
        int ink = 0;
        foreach (var pixel in bitmap.Pixels)
            ink += pixel.Alpha;
        return ink;
    }

    [Fact]
    public void OpticalSizeAuto_TakesTheTopOfTheAxisForValuesAndUnits_AndTheSizeForTheOthers()
    {
        WriteTheme("auto", """{ "typography": { "opticalSize": "auto", "labelSize": 0.04, "bodySize": 0.1, "titleSize": 0.2 } }""");
        var loaded = Store().Load("auto");

        using var big = ThemeResolver.Resolve(loaded, 275);   // label 11 px, body 27.5 px, title 55 px
        using var small = ThemeResolver.Resolve(loaded, 600); // label 24 px

        Assert.Equal(32f, Position(big.GetTypeface(TextRole.Value), Opsz));
        Assert.Equal(32f, Position(big.GetTypeface(TextRole.Unit), Opsz));
        Assert.Equal(14f, Position(big.GetTypeface(TextRole.Label), Opsz));
        Assert.Equal(27.5f, Position(big.GetTypeface(TextRole.Body), Opsz), 3);
        Assert.Equal(32f, Position(big.GetTypeface(TextRole.Title), Opsz));
        Assert.Equal(24f, Position(small.GetTypeface(TextRole.Label), Opsz), 3);
        Assert.Equal("", Log);
    }

    [Fact]
    public void OpticalSizeAuto_ChangesTheDigitsInk_AndOffDoesNot()
    {
        WriteTheme("auto", """{ "typography": { "opticalSize": "auto" } }""");
        WriteTheme("off", """{ "typography": { "opticalSize": "off" } }""");

        using var auto = ThemeResolver.Resolve(Store().Load("auto"), 275);
        using var off = ThemeResolver.Resolve(Store().Load("off"), 275);
        using var plain = ThemeResolver.Resolve(ThemeStore.LoadBuiltIn("default-dark")!, 275);

        Assert.NotEqual(Ink(plain.GetTypeface(TextRole.Value)), Ink(auto.GetTypeface(TextRole.Value)));
        Assert.Equal(Ink(plain.GetTypeface(TextRole.Value)), Ink(off.GetTypeface(TextRole.Value)));
        Assert.Equal(Position(plain.GetTypeface(TextRole.Label), Opsz), Position(off.GetTypeface(TextRole.Label), Opsz));
    }
}
