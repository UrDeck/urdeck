// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>Icon sets (theme-icons): the file rules, the manifest, the built-in <c>outline</c> set and the resolution chain.</summary>
public sealed class IconSetTests : IDisposable
{
    private const string Rain = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M4 14h16"/><circle cx="12" cy="12" r="3"/></svg>""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-iconset-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public IconSetTests()
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

    private string MakeSet(string name, string manifest, params (string File, string Text)[] files)
    {
        string dir = Path.Combine(_root, "themes", "probe", "icons", name);
        Directory.CreateDirectory(dir);
        if (manifest != null)
            File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest);
        foreach (var (file, text) in files)
        {
            string path = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        return dir;
    }

    private LoadedTheme LoadProbe(string themeJson)
    {
        string dir = Path.Combine(_root, "themes", "probe");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), themeJson);
        return new ThemeStore(Path.Combine(_root, "themes")).Load("probe");
    }

    // The file rules

    [Fact]
    public void Parser_ReadsPathsAndPrimitives()
    {
        var result = IconSvgParser.Parse(Rain);

        Assert.Null(result.Reason);
        Assert.Equal(2, result.Glyph!.Shapes.Count);
        Assert.All(result.Glyph.Shapes, s => Assert.False(s.Filled));
    }

    [Fact]
    public void Parser_MarksCurrentColourShapesAsFilled_AlsoThroughAGroup()
    {
        var own = IconSvgParser.Parse("""<svg viewBox="0 0 24 24"><circle cx="5" cy="5" r="2" fill="currentColor"/><path d="M0 0h4"/></svg>""");
        var group = IconSvgParser.Parse("""<svg viewBox="0 0 24 24" fill="none"><g fill="currentColor"><circle cx="5" cy="5" r="2"/></g><rect x="1" y="1" width="4" height="4" rx="1"/></svg>""");

        Assert.True(own.Glyph!.Shapes[0].Filled);
        Assert.False(own.Glyph.Shapes[1].Filled);
        Assert.True(group.Glyph!.Shapes[0].Filled);
        Assert.False(group.Glyph.Shapes[1].Filled);
    }

    [Theory]
    [InlineData("""<svg viewBox="0 0 24 24"><path d="M0 0h4" transform="scale(2)"/></svg>""", "transform")]
    [InlineData("""<svg viewBox="0 0 24 24"><image href="x.png"/></svg>""", "image")]
    [InlineData("""<svg viewBox="0 0 24 24"><defs><path id="a" d="M0 0h4"/></defs></svg>""", "defs")]
    [InlineData("""<svg viewBox="0 0 24 24"><use href="#a"/></svg>""", "use")]
    [InlineData("""<svg viewBox="0 0 24 24"><text>x</text></svg>""", "text")]
    [InlineData("""<svg viewBox="0 0 24 24"><style>a{}</style></svg>""", "style")]
    [InlineData("""<svg viewBox="0 0 16 16"><path d="M0 0h4"/></svg>""", "viewBox")]
    [InlineData("""<svg><path d="M0 0h4"/></svg>""", "viewBox")]
    [InlineData("""<svg viewBox="0 0 24 24"><path d="M0 0h4"/>""", "XML")]
    [InlineData("""<!DOCTYPE svg [<!ENTITY a "b">]><svg viewBox="0 0 24 24"><path d="M0 0h4"/></svg>""", "XML")]
    [InlineData("""<svg viewBox="0 0 24 24"></svg>""", "no shape")]
    public void Parser_RejectsWhatTheFileRulesForbid(string svg, string mention)
    {
        var result = IconSvgParser.Parse(svg);

        Assert.Null(result.Glyph);
        Assert.Contains(mention, result.Reason);
    }

    [Fact]
    public void Parser_RejectsAFileOver4KB()
    {
        string big = "<svg viewBox=\"0 0 24 24\"><path d=\"M0 0h4\"/>" + new string(' ', 5000) + "</svg>";

        Assert.Contains("4 KB", IconSvgParser.Parse(big).Reason);
    }

    // The built-in set

    [Fact]
    public void Outline_AnswersEveryKeyOfTheLists_AndChecksClean()
    {
        var set = IconSetStore.BuiltIn("outline")!;
        foreach (string key in IconKeys.All)
            Assert.True(set.TryGet(key, out _), key);

        var reading = IconSetReader.Read(new EmbeddedIconFiles("outline"), thorough: true);
        Assert.Empty(reading.Findings);
        Assert.Equal("1.60.0", set.UpstreamVersion);
        Assert.Contains("Lucide", set.Source);
        Assert.Contains("ISC", set.License);
    }

    [Fact]
    public void Outline_HasTheFourMediaGlyphs_AndASetWithoutThemIsAnsweredByTheChain()
    {
        var set = IconSetStore.BuiltIn("outline")!;
        Assert.Equal(["media/play", "media/pause", "media/previous", "media/next"], IconKeys.Media);
        foreach (string key in IconKeys.Media)
        {
            Assert.True(set.TryGet(key, out var glyph), key);
            Assert.NotEmpty(glyph.Shapes);
        }

        MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "x.svg" } }""", ("x.svg", Rain));
        using var theme = ThemeResolver.Resolve(LoadProbe("""{ "icons": { "set": "mine" } }"""), 275);

        foreach (string key in IconKeys.Media)
            Assert.True(theme.Icons.Set.TryGet(key, out _), key);
    }

    [Fact]
    public void Outline_HasTheHomeGlyphs_AndASetWithoutThemIsAnsweredByTheChain()
    {
        var set = IconSetStore.BuiltIn("outline")!;
        Assert.Equal(21, IconKeys.Home.Count);
        Assert.All(IconKeys.Home, key => Assert.StartsWith("home/", key));
        foreach (string key in IconKeys.Home)
        {
            Assert.True(set.TryGet(key, out var glyph), key);
            Assert.NotEmpty(glyph.Shapes);
        }

        MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "x.svg" } }""", ("x.svg", Rain));
        using var theme = ThemeResolver.Resolve(LoadProbe("""{ "icons": { "set": "mine" } }"""), 275);

        foreach (string key in IconKeys.Home)
            Assert.True(theme.Icons.Set.TryGet(key, out _), key);
    }

    [Fact]
    public void Outline_ThunderstormsShareOneFileAndOneGlyph()
    {
        var set = IconSetStore.BuiltIn("outline")!;

        set.TryGet("weather/thunderstorms-day", out var day);
        set.TryGet("weather/thunderstorms-night", out var night);
        Assert.Same(day, night);
    }

    // A theme folder's set

    [Fact]
    public void ASetWithAManifest_GivesItsKeysAndOnlyThose()
    {
        string dir = MakeSet("mine", """{ "version": 1, "name": "mine", "source": "me", "license": "MIT", "keys": { "weather/rain": "cloud-rain.svg" } }""", ("cloud-rain.svg", Rain));

        var reading = IconSetReader.Read(IconSetStore.FilesOf(Path.GetDirectoryName(Path.GetDirectoryName(dir))!, "mine"), thorough: false);

        Assert.NotNull(reading.Set);
        Assert.True(reading.Set!.TryGet("weather/rain", out _));
        Assert.False(reading.Set.TryGet("weather/snow", out _));
        Assert.Empty(reading.Findings);
    }

    [Fact]
    public void TwoKeysNamingOneFile_ShareTheGlyph()
    {
        string dir = MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "x.svg", "weather/drizzle": "x.svg" } }""", ("x.svg", Rain));

        var set = IconSetReader.Read(IconSetStore.FilesOf(Path.GetDirectoryName(Path.GetDirectoryName(dir))!, "mine"), false).Set!;

        set.TryGet("weather/rain", out var a);
        set.TryGet("weather/drizzle", out var b);
        Assert.Same(a, b);
    }

    [Fact]
    public void AFolderWithNoManifest_IsNotASet_AndTheFindingNamesTheFile()
    {
        MakeSet("mine", null!, ("weather/rain.svg", Rain));
        var files = IconSetStore.FilesOf(Path.Combine(_root, "themes", "probe"), "mine");

        var reading = IconSetReader.Read(files, thorough: false);

        Assert.Null(reading.Set);
        var finding = Assert.Single(reading.Findings);
        Assert.Equal(ThemeCodes.IconManifest, finding.Code);
        Assert.Contains("manifest.json", finding.Message);
        Assert.Contains("icons/mine", finding.Message);
    }

    [Theory]
    [InlineData("..\\other\\x.svg")]
    [InlineData("../other/x.svg")]
    [InlineData("/etc/x.svg")]
    [InlineData("C:\\x.svg")]
    [InlineData("rain.png")]
    public void AnEntryThatLeavesTheFolderOrIsNotSvg_GivesNoIcon_AndReadsNothingOutside(string path)
    {
        File.WriteAllText(Path.Combine(_root, "x.svg"), Rain);
        Directory.CreateDirectory(Path.Combine(_root, "themes", "probe", "icons", "other"));
        File.WriteAllText(Path.Combine(_root, "themes", "probe", "icons", "other", "x.svg"), Rain);
        string json = "{ \"version\": 1, \"name\": \"m\", \"source\": \"s\", \"license\": \"l\", \"keys\": { \"weather/rain\": \"" + path.Replace("\\", "\\\\") + "\" } }";
        MakeSet("mine", json);
        var files = IconSetStore.FilesOf(Path.Combine(_root, "themes", "probe"), "mine");

        var reading = IconSetReader.Read(files, thorough: false);

        Assert.False(reading.Set!.TryGet("weather/rain", out _));
        Assert.Equal(ThemeCodes.IconEntry, Assert.Single(reading.Findings).Code);
    }

    [Fact]
    public void ABadFile_GivesItsKeyNoIcon_AndIsReportedAsAFileFinding()
    {
        MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "a.svg", "weather/snow": "b.svg" } }""",
            ("a.svg", """<svg viewBox="0 0 24 24"><path d="M0 0h4" transform="x"/></svg>"""), ("b.svg", Rain));
        var files = IconSetStore.FilesOf(Path.Combine(_root, "themes", "probe"), "mine");

        var reading = IconSetReader.Read(files, thorough: false);

        Assert.False(reading.Set!.TryGet("weather/rain", out _));
        Assert.True(reading.Set.TryGet("weather/snow", out _));
        Assert.Equal(ThemeCodes.IconFile, Assert.Single(reading.Findings).Code);
    }

    [Fact]
    public void AWrongVersion_IsNotASet()
    {
        MakeSet("mine", """{ "version": 2, "name": "m", "source": "s", "license": "l", "keys": {} }""");

        var reading = IconSetReader.Read(IconSetStore.FilesOf(Path.Combine(_root, "themes", "probe"), "mine"), false);

        Assert.Null(reading.Set);
        Assert.Equal(ThemeCodes.IconManifest, Assert.Single(reading.Findings).Code);
    }

    // The chain

    [Fact]
    public void TheThemesSetComesFirst_AndOutlineAnswersWhatItLacks()
    {
        MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "x.svg" } }""", ("x.svg", Rain));
        var loaded = LoadProbe("""{ "icons": { "set": "mine" } }""");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.True(theme.Icons.NamesSet);
        theme.Icons.Set.TryGet("weather/rain", out var rain);
        Assert.Equal(2, rain.Shapes.Count);
        Assert.True(theme.Icons.Set.TryGet("app/bolt", out _));
        Assert.Empty(Log);
    }

    [Fact]
    public void AnUnknownSetName_FallsBackToOutline_LogsOnce_AndNamesNoSet()
    {
        var loaded = LoadProbe("""{ "icons": { "set": "nosuch" } }""");

        using var first = ThemeResolver.Resolve(loaded, 275);
        using var second = ThemeResolver.Resolve(loaded, 400);

        Assert.False(first.Icons.NamesSet);
        Assert.True(first.Icons.Set.TryGet("app/file", out _));
        Assert.Single(Log.Split('\n', StringSplitOptions.RemoveEmptyEntries), l => l.Contains("nosuch", StringComparison.Ordinal));
    }

    [Fact]
    public void ASetNameThatEscapes_IsNotASet()
    {
        var loaded = LoadProbe("""{ "icons": { "set": "..\\x" } }""");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.False(theme.Icons.NamesSet);
    }

    [Fact]
    public void ABuiltInThemeNamingOutline_NamesASet()
    {
        var loaded = LoadProbe("""{ "icons": { "set": "outline" } }""");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.True(theme.Icons.NamesSet);
        Assert.True(theme.Icons.Set.TryGet("weather/rain", out _));
    }

    [Fact]
    public void ATheme_WithNoSet_StillAnswersFromOutline()
    {
        var loaded = LoadProbe("{}");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.False(theme.Icons.NamesSet);
        Assert.True(theme.Icons.Set.TryGet("app/folder", out _));
    }

    [Fact]
    public void ASecondResolve_SharesTheSet_AndReadsNoFile()
    {
        string dir = MakeSet("mine", """{ "version": 1, "name": "m", "source": "s", "license": "l", "keys": { "weather/rain": "x.svg" } }""", ("x.svg", Rain));
        var loaded = LoadProbe("""{ "icons": { "set": "mine" } }""");
        var first = ThemeResolver.Resolve(loaded, 275);
        var set = first.Icons.Set;
        first.Dispose();

        Directory.Delete(dir, recursive: true);
        using var second = ThemeResolver.Resolve(loaded, 410, compact: true);

        Assert.Same(set, second.Icons.Set);
        Assert.True(second.Icons.Set.TryGet("weather/rain", out _));
    }

    [Fact]
    public void ValuesAreReadFromTheTheme_AndSizesAreInPixels()
    {
        var loaded = LoadProbe("""{ "icons": { "set": "outline", "stroke": 0.08, "tint": "ramp", "glow": { "blur": 0.05, "alpha": 0.5 } } }""");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(0.08f, theme.Icons.Stroke, 4);
        Assert.Equal(IconTint.Ramp, theme.Icons.Tint);
        Assert.Equal(13.75f, theme.Icons.Glow!.BlurPx, 2);
        Assert.Equal(0.5f, theme.Icons.Glow.Opacity, 3);
    }

    [Fact]
    public void AFixedColourTint_IsKept()
    {
        var loaded = LoadProbe("""{ "icons": { "tint": "#112233" } }""");

        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(IconTint.Fixed, theme.Icons.Tint);
        Assert.Equal(SKColor.Parse("#112233"), theme.Icons.TintColor);
    }
}
