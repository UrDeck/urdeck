// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using UrDeck.Engine.Data;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary>The scenarios of the <c>urdeck-cli</c> spec for <c>readings browse</c>, against a pretend provider that can list its readings.</summary>
public sealed class ReadingsBrowseTests : IDisposable
{
    private const string Token = "t0ken-never-printed";

    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    private static Run Browse(ReadingsBrowseOptions options, string? token = Token)
    {
        var registry = new ProviderRegistry();
        registry.TryRegister(ProviderDescriptor.TryCreate(typeof(ListingProvider), out _)!, out _);
        registry.TryRegister(ProviderDescriptor.TryCreate(typeof(UrDeck.Providers.Weather.WeatherProvider), out _)!, out _);
        var secrets = new SecretStore(null, name => name == "URDECK_SECRET_FAKEHA_TOKEN" ? token : null);
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        int code;
        try
        {
            code = ReadingsBrowseRunner.Run(options, output, error, registry, secrets, TimeSpan.FromSeconds(1));
        }
        catch (ToolFailureException ex)
        {
            error.WriteLine($"error: {ex.Message}");
            code = ExitCodes.Failure;
        }

        return new Run(code, output.ToString(), error.ToString());
    }

    private static ReadingsBrowseOptions Options(string? search = null, string? domain = null, string? config = null, string format = "text") =>
        new("fakeha", search, domain, "http://ha.test:8123", config, format);

    [Fact]
    public void Find_the_basement_temperature()
    {
        var run = Browse(Options(search: "basement", domain: "sensor"));

        Assert.Equal(0, run.Code);
        // The value is shown in the region's unit (65.3 ° in Fahrenheit, 18.5 ° in Celsius).
        string line = Assert.Single(run.Out.TrimEnd('\n').Split('\n'));
        Assert.Matches(@"^fakeha:sensor\.basement_temperature  temperature  (18\.5|65\.3) °  Basement temperature$", line);
        Assert.DoesNotContain(Token, run.Out + run.Error);
    }

    [Fact]
    public void Every_reading_is_listed_with_its_kind_value_and_label()
    {
        var run = Browse(Options());

        Assert.Equal(0, run.Code);
        string[] lines = run.Out.TrimEnd('\n').Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("fakeha:light.desk ", lines[0]);
        Assert.Contains(" onOff ", lines[0]);
        Assert.EndsWith("Desk lamp", lines[0]);
        Assert.Contains("(pending)", lines.Single(l => l.StartsWith("fakeha:sensor.silent", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_domain_keeps_only_its_entities()
    {
        var run = Browse(Options(domain: "light"));

        Assert.Equal("fakeha:light.desk", run.Out.Split(' ')[0]);
        Assert.Single(run.Out.TrimEnd('\n').Split('\n'));
    }

    [Fact]
    public void Json_lists_the_same_readings()
    {
        var run = Browse(Options(domain: "light", format: "json"));

        using var document = JsonDocument.Parse(run.Out);
        var light = document.RootElement.EnumerateArray().Single();
        Assert.Equal("fakeha:light.desk", light.GetProperty("id").GetString());
        Assert.Equal("onOff", light.GetProperty("kind").GetString());
        Assert.Equal("ok", light.GetProperty("state").GetString());
        Assert.Equal("light.desk/toggle", light.GetProperty("action").GetString());
        Assert.Equal("home/light", light.GetProperty("icon").GetString());
    }

    [Fact]
    public void Settings_come_from_the_configuration_and_the_url_overrides_its_address()
    {
        File.WriteAllText("urdeck-config.json", """{ "version": 1, "pages": [], "providers": { "fakeha": { "url": "http://from-config:8123" } } }""");

        var fromConfig = Browse(Options(search: "silent", config: "urdeck-config.json") with { Url = null });
        var overridden = Browse(Options(search: "silent", config: "urdeck-config.json"));

        Assert.Equal(0, fromConfig.Code);
        Assert.Contains("http://from-config:8123", fromConfig.Out);
        Assert.Contains("http://ha.test:8123", overridden.Out);
    }

    [Fact]
    public void A_missing_secret_ends_with_exit_code_2_and_the_reason()
    {
        var run = Browse(Options(), token: null);

        Assert.Equal(2, run.Code);
        Assert.Equal("error: provider 'fakeha': set Token for Fake Home Assistant in Settings\n", run.Error);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void A_provider_without_a_listing_ends_with_exit_code_2()
    {
        var run = Browse(Options() with { Provider = "weather" });

        Assert.Equal(2, run.Code);
        Assert.Equal("error: provider 'weather' cannot list its readings\n", run.Error);
    }

    [Fact]
    public void An_unknown_provider_ends_with_exit_code_2()
    {
        var run = Workspace.Invoke("readings", "browse", "nosuch", "--config", "missing.json");

        Assert.Equal(2, run.Code);
        Assert.StartsWith("error: --config ", run.Error);

        File.WriteAllText("empty.json", """{ "version": 1, "pages": [] }""");
        run = Workspace.Invoke("readings", "browse", "nosuch", "--config", "empty.json");
        Assert.Equal(2, run.Code);
        Assert.Equal("error: no data provider 'nosuch' is registered\n", run.Error);
    }

    [Fact]
    public void Weather_cannot_list_its_readings()
    {
        File.WriteAllText("empty.json", """{ "version": 1, "pages": [] }""");

        var run = Workspace.Invoke("readings", "browse", "weather", "--config", "empty.json");

        Assert.Equal(2, run.Code);
        Assert.Equal("error: provider 'weather' cannot list its readings\n", run.Error);
    }

    public sealed class ListingSettings : ProviderSettings
    {
        [Setting("Address", Required = true)]
        public string? Url { get; set; }

        [Setting("Token", Required = true)]
        [Secret(ContextSetting = "url")]
        public SecretValue? Token { get; set; }
    }

    /// <summary>Four entities; the silent one's label carries the address it was given, so a test can see which one was used.</summary>
    [DataProvider("fakeha", "Fake Home Assistant")]
    public sealed class ListingProvider : DataProvider<ListingSettings>, IReadingBrowser
    {
        private IReadingSink? _sink;
        private ListingSettings _settings = new();
        private string[] _demand = [];

        public override IReadOnlyList<ReadingDescriptor> Describe() => [new("{entity}", ReadingKind.Text, "{entity}", "An entity")];

        public override void Start(IReadingSink sink, ListingSettings settings)
        {
            if (settings.Token?.Reveal() != Token)
                throw new InvalidOperationException("wrong token");
            _sink = sink;
            _settings = settings;
        }

        public override void SetDemand(IReadOnlyCollection<string> paths) => _demand = [.. paths];

        public override Task SampleAsync(CancellationToken cancellationToken)
        {
            foreach (string path in _demand)
            {
                switch (path)
                {
                    case "light.desk":
                        _sink!.Publish(path, true);
                        break;
                    case "sensor.basement_temperature":
                        _sink!.Publish(path, 18.5);
                        break;
                    case "sensor.office_power":
                        _sink!.Publish(path, 431);
                        break;
                }
            }

            return Task.CompletedTask;
        }

        public override void Shutdown()
        {
        }

        public Task<IReadOnlyList<ReadingDescriptor>> BrowseAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReadingDescriptor>>(
            [
                new("sensor.office_power", ReadingKind.Number, "Office power", "sensor.office_power") { Unit = "W" },
                new("light.desk", ReadingKind.OnOff, "Desk lamp", "light.desk") { Action = "light.desk/toggle", Icon = "home/light" },
                new("sensor.basement_temperature", ReadingKind.Temperature, "Basement temperature", "sensor.basement_temperature"),
                new("sensor.silent", ReadingKind.Text, $"Silent at {_settings.Url}", "sensor.silent"),
            ]);
    }
}
