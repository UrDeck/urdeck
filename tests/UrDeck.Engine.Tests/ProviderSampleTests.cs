// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using UrDeck.Engine.Data;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Machine;
using UrDeck.Providers.Media;
using UrDeck.Providers.Weather;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The first-party providers' samples cover what the providers offer (sample-data, "Samples Cover Their Provider";
/// data-providers, "First-Party Providers Carry Samples").
/// </summary>
public sealed partial class ProviderSampleTests
{
    private static SampleFile Sample(Type typeInAssembly, string id)
    {
        var provider = new ProviderDescriptor(id, id, typeInAssembly, 1000, 250, typeInAssembly.Assembly.GetName().Name!);
        Assert.True(ProviderSamples.Carries(typeInAssembly.Assembly, id), $"the assembly of '{id}' carries no {ProviderSamples.ResourceName(id)}");
        var file = ProviderSamples.Find(provider);
        Assert.NotNull(file);
        return file!;
    }

    private static SampleFile Weather => Sample(typeof(WeatherProvider), "weather");

    // MemoryReadings, not SystemProvider: it has no Windows-only attribute, and it lives in the same assembly.
    private static SampleFile System => Sample(typeof(MemoryReadings), "system");

    private static SampleFile Media => Sample(typeof(MediaProvider), "media");

    [Fact]
    public void AllSamples_ParseWithoutFindings()
    {
        Assert.NotEmpty(Weather.Readings);
        Assert.NotEmpty(System.Readings);
        Assert.NotEmpty(Media.Readings);
    }

    [Fact]
    public void TheMediaSample_DescribesEveryReadingOfTheCatalog_AsTheCatalogDoes()
    {
        var sample = Media.Describe().ToDictionary(d => d.Path);
        using var provider = new MediaProvider();
        var catalog = provider.Describe();

        foreach (var entry in catalog)
        {
            Assert.True(sample.TryGetValue(entry.Path, out var described), $"the media sample has no '{entry.Path}'");
            Assert.True(entry == described, Difference(entry, described!));
        }

        Assert.Equal(catalog.Count, sample.Count);
    }

    [Fact]
    public void TheMediaSample_IsAPlayingTrack_ThatTheWidgetReadsAtAnyMoment()
    {
        var moment = new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);
        ReadingValue At(string path) => Media.Match(path, out var bindings)!.ValueAt(0, moment, bindings)!.Value;

        Assert.Equal("playing", At("status").Text);
        Assert.NotEmpty(At("title").Text);
        Assert.NotEmpty(At("artist").Text);
        Assert.InRange(At("position").Number, 1, At("duration").Number - 1);
        Assert.Equal("", At("art").Text);
        Assert.True(At("can/previous").IsOn && At("can/next").IsOn && At("can/playpause").IsOn);

        // Stamped late in the day, so the widget's extrapolation (zero when the stamp is ahead of the clock) holds the bar still.
        Assert.True(At("position/at").Time.LocalDateTime > moment);
    }

    [Fact]
    public void ASampleProvider_OffersNoActions()
    {
        Assert.False(typeof(IActionProvider).IsAssignableFrom(typeof(SampleProvider)));
    }

    [Fact]
    public void TheWeatherSample_DescribesEveryReadingOfTheCatalog_AsTheCatalogDoes()
    {
        var sample = Weather.Describe().ToDictionary(d => d.Path);
        var catalog = WeatherCatalog.Describe();

        foreach (var entry in catalog)
        {
            Assert.True(sample.TryGetValue(entry.Path, out var described), $"the weather sample has no '{entry.Path}'");
            Assert.True(entry == described, Difference(entry, described!));
        }

        Assert.Equal(catalog.Count, sample.Count);
    }

    [Fact]
    public void TheWeatherSample_HasTheValuesTheSamplePageAlwaysShowed()
    {
        var moment = new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);
        ReadingValue At(string path) => Weather.Match(path, out var bindings)!.ValueAt(0, moment, bindings)!.Value;

        Assert.Equal("Portland, OR", At("Portland, OR/place").Text);
        Assert.Equal(18, At("Portland, OR/current/temperature").Number);
        Assert.Equal("partly-cloudy", At("Portland, OR/current/condition").Text);
        Assert.False(At("Portland, OR/current/is-day").IsOn);
        Assert.Equal(21, At("Portland, OR/daily/0/high").Number);
        Assert.Equal(14, At("Portland, OR/daily/0/low").Number);
        Assert.Equal(new TimeSpan(7, 14, 0), At("Portland, OR/daily/0/sunrise").Time.TimeOfDay);
        Assert.Equal(new TimeSpan(18, 21, 0), At("Portland, OR/daily/0/sunset").Time.TimeOfDay);
        Assert.Equal(new DateTime(2026, 10, 19), At("Portland, OR/daily/6/date").Time.Date);
    }

    [Fact]
    public void TheSystemSample_IsAPlausibleMachine_WithLoadsThatMove()
    {
        var paths = System.Describe().Select(d => d.Path).ToHashSet();
        var moment = new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);
        double At(string path, double seconds) => System.Match(path, out _)!.ValueAt(seconds, moment)!.Value.Number;

        Assert.True(Enumerable.Range(1, 8).All(n => paths.Contains($"cpu/core/{n}/load")), "the sample machine has eight cores");
        foreach (string part in new[] { "load", "temperature", "power", "clock" })
        {
            Assert.Contains($"gpu/{part}", paths);
            Assert.Contains($"gpu/1/{part}", paths);
        }

        Assert.Equal(68, At("cpu/load", 0));
        Assert.Equal(82, At("gpu/load", 0));
        foreach (string path in new[] { "cpu/load", "gpu/load", "memory/load" })
        {
            Assert.NotEqual(At(path, 0), At(path, 1.5));
            Assert.NotNull(System.Match(path, out _)!.Period);
        }
    }

    /// <summary>Every reading of this machine's catalog has a sample of its family with the same description, the device's name excepted.</summary>
    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void TheSystemSample_CoversThisMachinesCatalog()
    {
        var sample = Families(System.Describe());

        foreach (var entry in new SystemProvider().Describe())
        {
            var family = Family(entry);
            Assert.True(sample.TryGetValue(family.Path, out var described), $"the system sample has no reading of the family of '{entry.Path}'");
            Assert.True(family == described, Difference(family, described!));
        }
    }

    /// <summary>The families the describe routines can write (a machine with a GPU), and no other, are in the sample.</summary>
    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void TheSystemSample_HasEveryFamilyTheProviderCanOffer_AndNoOther()
    {
        var adapter = new GpuAdapter(1, "Any GPU", 8UL << 30, 0x10de, false, 1, 0, 0);
        var catalog = Families(new SystemProvider(new OneAdapter(adapter)).Describe());
        var sample = Families(System.Describe());

        Assert.Equal(catalog.Keys.Order(StringComparer.Ordinal), sample.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, entry) in catalog)
            Assert.True(entry == sample[path], Difference(entry, sample[path]));
    }

    private static Dictionary<string, ReadingDescriptor> Families(IEnumerable<ReadingDescriptor> entries) =>
        entries.Select(Family).GroupBy(d => d.Path).ToDictionary(g => g.Key, g => g.First());

    /// <summary>The description with every number in the path, label and name made <c>#</c>, and no device.</summary>
    private static ReadingDescriptor Family(ReadingDescriptor d) =>
        d with { Path = Number().Replace(d.Path, "#"), Label = Number().Replace(d.Label, "#"), Name = Number().Replace(d.Name, "#"), Device = null };

    [GeneratedRegex("[0-9]+")]
    private static partial Regex Number();

    private static string Difference(ReadingDescriptor expected, ReadingDescriptor actual) =>
        $"the sample of '{expected.Path}' is described as {actual}; the provider describes it as {expected}";

    private sealed class OneAdapter(GpuAdapter adapter) : IGpuPlatform
    {
        public IReadOnlyList<GpuAdapter> Adapters() => [adapter];

        public long Now() => 0;

        public int EngineCount(GpuAdapter adapter) => 0;

        public bool TryReadRunningTime(GpuAdapter adapter, int engine, out long runningTime)
        {
            runningTime = 0;
            return false;
        }

        public bool TryReadTemperature(GpuAdapter adapter, out double celsius)
        {
            celsius = 0;
            return false;
        }

        public IGpuVendorReader? CreateVendorReader(GpuAdapter adapter) => null;
    }
}
