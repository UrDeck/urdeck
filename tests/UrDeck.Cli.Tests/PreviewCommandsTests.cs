// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary>The scenarios of the <c>urdeck-cli</c> spec for <c>preview render</c> and <c>catalog export</c>.</summary>
public sealed class PreviewCommandsTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    // ------------------------------------------------------------------ preview render

    [Fact]
    public void A_page_in_a_theme_over_its_own_background()
    {
        var run = Workspace.Invoke("preview", "render", "--theme", "ember", "--background", "theme", "-o", "out.png");

        Assert.Equal(0, run.Code);
        Assert.Equal("wrote out.png (1100x1920)\n", run.Out);
        using var picture = Workspace.Decode("out.png");
        Assert.Equal((1100, 1920), (picture.Width, picture.Height));

        Workspace.Invoke("preview", "render", "--theme", "ember", "--background", "none", "-o", "plain.png");
        Assert.NotEqual(File.ReadAllBytes("out.png"), File.ReadAllBytes("plain.png"));
    }

    [Fact]
    public void A_widget_alone()
    {
        var run = Workspace.Invoke("preview", "render", "--widget", "urdeck.widgets.weather", "--widget-size", "4x4", "--theme", "paper", "-o", "w.png");

        Assert.Equal(0, run.Code);
        using var picture = Workspace.Decode("w.png");
        Assert.Equal((1100, 1100), (picture.Width, picture.Height));
    }

    [Fact]
    public void Frames_are_written_into_a_folder_and_move()
    {
        Directory.CreateDirectory("frames");
        File.WriteAllBytes(Path.Combine("frames", "frame-0099.png"), [1]);

        var run = Workspace.Invoke("preview", "render", "--theme", "rigline", "--background", "theme", "--frames", "3", "--size", "275x480", "-o", "frames");

        Assert.Equal(0, run.Code);
        Assert.Equal(["frame-0000.png", "frame-0001.png", "frame-0002.png"],
            Directory.GetFiles("frames").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.NotEqual(File.ReadAllBytes(Path.Combine("frames", "frame-0000.png")), File.ReadAllBytes(Path.Combine("frames", "frame-0002.png")));
    }

    [Fact]
    public void An_unknown_widget_is_refused_naming_the_known_ones_and_nothing_is_written()
    {
        var run = Workspace.Invoke("preview", "render", "--widget", "example.unknown", "--widget-size", "1x1", "-o", "x.png");

        Assert.Equal(1, run.Code);
        Assert.Contains("urdeck.widgets.clock, urdeck.widgets.control, urdeck.widgets.nowplaying, urdeck.widgets.shortcut, urdeck.widgets.stats, urdeck.widgets.weather", run.Error);
        Assert.False(File.Exists("x.png"));
    }

    [Fact]
    public void A_size_the_widget_does_not_declare_is_refused()
    {
        var run = Workspace.Invoke("preview", "render", "--widget", "urdeck.widgets.weather", "--widget-size", "1x1", "--frames", "2", "-o", "f");

        Assert.Equal(1, run.Code);
        Assert.Contains("4x2, 4x4", run.Error);
        Assert.False(Directory.Exists("f"));
    }

    [Fact]
    public void A_configuration_is_drawn_at_its_page_and_left_as_it_was()
    {
        const string json = """
            { "theme": "glass", "pages": [
                { "widgets": [ { "typeId": "urdeck.widgets.clock", "col": 0, "row": 0, "width": 4, "height": 1 } ] },
                { "widgets": [ { "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 2 } ] } ] }
            """;
        File.WriteAllText("urdeck-config.json", json);

        var second = Workspace.Invoke("preview", "render", "--config", "urdeck-config.json", "--page", "1", "--size", "275x480", "-o", "two.png");
        var first = Workspace.Invoke("preview", "render", "--config", "urdeck-config.json", "--page", "0", "--size", "275x480", "-o", "one.png");
        var missing = Workspace.Invoke("preview", "render", "--config", "urdeck-config.json", "--page", "5", "-o", "five.png");

        Assert.Equal((0, 0, 1), (second.Code, first.Code, missing.Code));
        Assert.NotEqual(File.ReadAllBytes("one.png"), File.ReadAllBytes("two.png"));
        Assert.Equal(json, File.ReadAllText("urdeck-config.json"));
        Assert.False(File.Exists("five.png"));
    }

    [Fact]
    public void A_moment_and_the_same_picture_twice()
    {
        Workspace.Invoke("preview", "render", "--theme", "carbon", "--time", "2026-10-13T08:05:00", "--size", "275x480", "-o", "a.png");
        Workspace.Invoke("preview", "render", "--theme", "carbon", "--time", "2026-10-13T08:05:00", "--size", "275x480", "-o", "b.png");
        Workspace.Invoke("preview", "render", "--theme", "carbon", "--size", "275x480", "-o", "c.png");

        Assert.Equal(File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png"));
        Assert.NotEqual(File.ReadAllBytes("a.png"), File.ReadAllBytes("c.png"));
    }

    [Theory]
    [InlineData("--theme", "ember", "--theme-dir", "x")]
    [InlineData("--widget", "urdeck.widgets.clock")]
    [InlineData("--size", "big")]
    [InlineData("--motion", "wild")]
    [InlineData("--time", "yesterday")]
    [InlineData("--page", "first")]
    public void Usage_errors_are_exit_code_2(params string[] arguments)
    {
        var run = Workspace.Invoke(["preview", "render", "-o", "out.png", .. arguments]);

        Assert.Equal(2, run.Code);
        Assert.False(File.Exists("out.png"));
    }

    [Theory]
    [InlineData("--theme", "nosuch")]
    [InlineData("--background", "nosuch")]
    [InlineData("--config", "missing.json")]
    public void An_input_that_cannot_be_used_is_exit_code_1(params string[] arguments)
    {
        var run = Workspace.Invoke(["preview", "render", "-o", "out.png", .. arguments]);

        Assert.Equal(1, run.Code);
        Assert.StartsWith("error: ", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void A_theme_folders_findings_go_to_the_error_stream()
    {
        _work.Theme("THEME080", "mine");

        var run = Workspace.Invoke("preview", "render", "--theme-dir", "mine", "--size", "275x480", "-o", "out.png");

        Assert.Equal(0, run.Code);
        Assert.Contains("THEME080", run.Error);
        Assert.Equal(1, Workspace.Invoke("preview", "render", "--theme-dir", "mine", "--size", "275x480", "-o", "out.png", "--fail-on-warnings").Code);
    }

    // ------------------------------------------------------------------ catalog export

    [Fact]
    public void The_catalog_lists_what_the_site_reads()
    {
        var run = Workspace.Invoke("catalog", "export", "-o", "catalog.json");

        Assert.Equal(0, run.Code);
        using var document = JsonDocument.Parse(File.ReadAllText("catalog.json"));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(ThemeStore.BuiltInNames.Order(StringComparer.Ordinal), Names(root.GetProperty("themes"), "name"));
        Assert.Equal(ShaderStore.BuiltInNames.Order(StringComparer.Ordinal), Names(root.GetProperty("backgrounds"), "name"));
        Assert.Equal(["urdeck.widgets.clock", "urdeck.widgets.control", "urdeck.widgets.nowplaying", "urdeck.widgets.shortcut", "urdeck.widgets.stats", "urdeck.widgets.weather"],
            Names(root.GetProperty("widgets"), "typeId"));
        Assert.Equal(["homeassistant", "media", "system", "weather"], Names(root.GetProperty("providers"), "id"));
        Assert.All(root.GetProperty("providers").EnumerateArray(), p => Assert.True(p.GetProperty("sample").GetBoolean()));

        var rigline = root.GetProperty("themes").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "rigline");
        Assert.Equal("horizon", rigline.GetProperty("defaultBackground").GetString());
        var stats = root.GetProperty("widgets").EnumerateArray().Single(w => w.GetProperty("typeId").GetString() == "urdeck.widgets.stats");
        Assert.Equal(4, stats.GetProperty("sizes").GetArrayLength());
        Assert.Equal(4, stats.GetProperty("defaultConfig").GetProperty("slots").GetArrayLength());
        Assert.False(stats.GetProperty("defaultConfig").TryGetProperty("width", out _));
    }

    [Fact]
    public void Only_Home_Assistant_declares_settings_beyond_the_base_ones()
    {
        using var document = JsonDocument.Parse(Workspace.Invoke("catalog", "export").Out);

        var providers = document.RootElement.GetProperty("providers").EnumerateArray().ToList();
        Assert.All(providers.Where(p => p.GetProperty("id").GetString() != "homeassistant"), p => Assert.Equal(0, p.GetProperty("settings").GetArrayLength()));
        var homeAssistant = providers.Single(p => p.GetProperty("id").GetString() == "homeassistant").GetProperty("settings");
        Assert.Equal(["token", "url"], homeAssistant.EnumerateArray().Select(s => s.GetProperty("key").GetString()));
        Assert.Equal("URDECK_SECRET_HOMEASSISTANT_TOKEN", homeAssistant[0].GetProperty("environmentVariable").GetString());
    }

    [Fact]
    public void A_providers_secret_is_listed_with_its_environment_variable_and_never_a_value()
    {
        var descriptor = UrDeck.Engine.Data.ProviderDescriptor.TryCreate(typeof(SecretProvider), out _)!;

        var settings = CatalogExportRunner.ProviderSettingsOf(descriptor).Select(s => s.AsObject()).ToList();

        Assert.Equal(["token", "url"], settings.Select(s => (string)s["key"]!));
        var token = settings[0];
        Assert.Equal("secret", (string)token["type"]!);
        Assert.True((bool)token["secret"]!);
        Assert.True((bool)token["required"]!);
        Assert.Equal("URDECK_SECRET_SECRETS_TOKEN", (string)token["environmentVariable"]!);
        Assert.Null(token["default"]);
        Assert.Equal("Issued by the service.", (string)token["help"]!);
        var url = settings[1];
        Assert.Equal("text", (string)url["type"]!);
        Assert.False((bool)url["secret"]!);
        Assert.Null(url["environmentVariable"]);
    }

    [UrDeck.Sdk.Data.DataProvider("secrets", "Secrets")]
    private sealed class SecretProvider : UrDeck.Sdk.Data.DataProvider<SecretSettings>
    {
        public override IReadOnlyList<UrDeck.Sdk.Data.ReadingDescriptor> Describe() => [];

        public override void Start(UrDeck.Sdk.Data.IReadingSink sink, SecretSettings settings)
        {
        }

        public override void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public override Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Shutdown()
        {
        }
    }

    private sealed class SecretSettings : UrDeck.Sdk.Data.ProviderSettings
    {
        [UrDeck.Sdk.Data.Setting("Address", Help = "Where the service is.", Required = true)]
        public string? Url { get; set; }

        [UrDeck.Sdk.Data.Setting("Token", Help = "Issued by the service.", Required = true)]
        [UrDeck.Sdk.Data.Secret(ContextSetting = "url")]
        public UrDeck.Sdk.Data.SecretValue? Token { get; set; }
    }

    [Fact]
    public void A_widgets_settings_name_their_type_values_and_default()
    {
        var run = Workspace.Invoke("catalog", "export");
        using var document = JsonDocument.Parse(run.Out);

        var clock = document.RootElement.GetProperty("widgets").EnumerateArray().Single(w => w.GetProperty("typeId").GetString() == "urdeck.widgets.clock");
        var settings = clock.GetProperty("settings").EnumerateArray().ToDictionary(s => s.GetProperty("name").GetString()!);
        Assert.Equal("values", settings["format"].GetProperty("type").GetString());
        Assert.Equal(["24h", "12h"], settings["format"].GetProperty("values").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("24h", settings["format"].GetProperty("default").GetString());
        Assert.Equal("onOff", settings["showDate"].GetProperty("type").GetString());
        Assert.True(settings["showDate"].GetProperty("default").GetBoolean());
        Assert.Equal(JsonValueKind.Null, settings["textColor"].GetProperty("default").ValueKind);
    }

    [Fact]
    public void The_output_is_stable_sorted_and_LF()
    {
        var one = Workspace.Invoke("catalog", "export");
        var two = Workspace.Invoke("catalog", "export");

        Assert.Equal(one.Out, two.Out);
        Assert.DoesNotContain("\r", one.Out);
        Assert.EndsWith("}\n", one.Out);
        using var document = JsonDocument.Parse(one.Out);
        Assert.Equal(document.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal),
            document.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Every_finding_code_the_engine_defines_is_listed_with_a_summary()
    {
        var defined = new[] { typeof(ThemeCodes), typeof(ShaderCodes) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(Workspace.Invoke("catalog", "export").Out);
        var findings = document.RootElement.GetProperty("findings").EnumerateArray().ToList();

        Assert.Equal(defined, findings.Select(f => f.GetProperty("code").GetString()));
        Assert.All(findings, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("summary").GetString()));
            Assert.Matches("^(info|warning|error)", f.GetProperty("severity").GetString()!);
        });
    }

    [Fact]
    public void A_users_folders_are_listed_besides_the_built_in_ones()
    {
        _work.Theme("THEME080", "mine", "pass");
        _work.Background("glow", parent: "bgs");
        Directory.CreateDirectory("themes");
        Directory.Move("mine", Path.Combine("themes", "mine"));

        var run = Workspace.Invoke("catalog", "export", "--theme-dir", "themes", "--background-dir", "bgs");
        using var document = JsonDocument.Parse(run.Out);

        Assert.Contains("mine", Names(document.RootElement.GetProperty("themes"), "name"));
        Assert.Contains("glow", Names(document.RootElement.GetProperty("backgrounds"), "name"));
    }

    private static List<string> Names(JsonElement list, string key) =>
        [.. list.EnumerateArray().Select(e => e.GetProperty(key).GetString()!)];
}
