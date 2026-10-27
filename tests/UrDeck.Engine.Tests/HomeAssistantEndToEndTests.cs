// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.HomeAssistant;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The Home Assistant provider against a real installation, through the hub as the deck runs it. Opt-in only (see
/// <see cref="HomeAssistantFactAttribute"/>); written for the <c>demo</c> integration, whose lights include <c>light.bed_light</c>.
/// </summary>
public sealed class HomeAssistantEndToEndTests : IDisposable
{
    private const string Light = "homeassistant:light.bed_light";

    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"urdeck-ha-e2e-{Guid.NewGuid():N}.log");
    private readonly ReadingHub _hub;

    public HomeAssistantEndToEndTests()
    {
        UrDeckLog.LogPath = _logPath;
        var registry = new ProviderRegistry();
        registry.TryRegister(ProviderDescriptor.TryCreate(typeof(HomeAssistantProvider), out _)!, out _);
        _hub = new ReadingHub(registry, secrets: new SecretStore(null));
        _hub.ApplySettings(new Dictionary<string, ProviderSettings>
        {
            // As the configuration file gives it: the provider's keys as they are written there.
            ["homeassistant"] = new ProviderSettings
            {
                ExtensionData = new() { ["url"] = System.Text.Json.JsonSerializer.SerializeToElement(Environment.GetEnvironmentVariable(HomeAssistantFactAttribute.UrlVariable)) },
            },
        });
    }

    public void Dispose()
    {
        _hub.Dispose();
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }

    private static bool Wait(Func<bool> condition, int seconds = 15) => HubTestExtensions.WaitFor(condition, seconds * 1000);

    [HomeAssistantFact]
    public async Task ConnectsBrowsesReadsAndToggles()
    {
        var listing = await _hub.BrowseAsync("homeassistant", TestContext.Current.CancellationToken);
        Assert.True(listing.Succeeded, listing.Message);
        Assert.Contains(listing.Readings!, d => d.Path == "light.bed_light" && d.Kind == ReadingKind.OnOff && d.Action == "light.bed_light/toggle");

        _hub.Subscribe([Light]);
        Assert.True(Wait(() => _hub.Read(Light).State == ReadingState.Ok), $"{_hub.Read(Light)}; log: {File.ReadAllText(_logPath)}");
        Assert.Equal(ReadingKind.OnOff, _hub.Describe(Light)!.Kind);
        Assert.Contains("Connected to Home Assistant", File.ReadAllText(_logPath));
        bool before = _hub.Read(Light).Value!.Value.IsOn;

        var result = await _hub.Invoke($"{Light}/toggle");

        Assert.True(result.Succeeded, result.Message);
        Assert.True(Wait(() => _hub.Read(Light).Value is { } value && value.IsOn != before), "the light did not change");
        await _hub.Invoke($"{Light}/toggle");
    }

    [HomeAssistantFact]
    public async Task TheConnectionTest_NamesTheVersion()
    {
        var settings = new HomeAssistantSettings
        {
            Url = Environment.GetEnvironmentVariable(HomeAssistantFactAttribute.UrlVariable),
            Token = new SecretValue(Environment.GetEnvironmentVariable(HomeAssistantFactAttribute.TokenVariable)!),
        };

        string result = await new HomeAssistantProvider().ValidateAsync(settings, TestContext.Current.CancellationToken);

        Assert.StartsWith("Connected to Home Assistant ", result);
        Assert.EndsWith(" entities", result);
    }
}
