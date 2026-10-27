// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>
/// A provider that replays a sample file: its catalog is the file's descriptions (placeholders kept, so the hub resolves
/// <c>weather:Zurich/...</c> as it does for the real provider), and a sample publishes every demanded reading's value at the
/// seconds since <c>start</c> by the clock it is given. It reads no sensor, makes no request and completes synchronously.
/// </summary>
public sealed class SampleProvider : IDataProvider
{
    private readonly SampleFile _file;
    private readonly Func<DateTime> _now;
    private readonly DateTime _start;
    private IReadingSink? _sink;
    private string[] _demand = [];

    public SampleProvider(SampleFile file, Func<DateTime> now, DateTime start)
    {
        _file = file;
        _now = now;
        _start = start;
    }

    public IReadOnlyList<ReadingDescriptor> Describe() => _file.Describe();

    public void Start(IReadingSink sink, ProviderSettings settings) => _sink = sink;

    public void SetDemand(IReadOnlyCollection<string> paths) => Volatile.Write(ref _demand, [.. paths]);

    public Task SampleAsync(CancellationToken cancellationToken)
    {
        if (_sink is not { } sink)
            return Task.CompletedTask;

        DateTime now = _now();
        double seconds = (now - _start).TotalSeconds;
        foreach (string path in Volatile.Read(ref _demand))
        {
            var reading = _file.Match(path, out var bindings);
            if (reading == null)
                continue;
            if (reading.ValueAt(seconds, now, bindings) is { } value)
                sink.Publish(path, value);
            else
                sink.Unavailable(path, reading.Unavailable ?? "no sample value");
        }

        return Task.CompletedTask;
    }

    public void Shutdown() => _sink = null;
}

/// <summary>
/// The clock samples are read by in the host's demo mode: the moment now, and the moment a drawing started (keyframes count
/// their seconds from it).
/// </summary>
public sealed record SampleClock(Func<DateTime> Now, DateTime Start);

/// <summary>Finds the sample file a provider's assembly carries and registers the sample in the provider's place.</summary>
public static class ProviderSamples
{
    /// <summary>The logical name of a provider's sample file in its assembly: <c>urdeck.sample.&lt;id&gt;.json</c>.</summary>
    public static string ResourceName(string providerId) => $"urdeck.sample.{providerId}.json";

    /// <summary>Whether <paramref name="assembly"/> carries a sample file for the provider, readable or not.</summary>
    public static bool Carries(Assembly assembly, string providerId) =>
        assembly.GetManifestResourceInfo(ResourceName(providerId)) != null;

    /// <summary>
    /// The sample the provider's assembly carries, or null when it carries none. A file that cannot be read is logged once,
    /// as a warning naming the provider and every finding, and gives no sample.
    /// </summary>
    public static SampleFile? Find(ProviderDescriptor provider)
    {
        var assembly = provider.ProviderType.Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName(provider.Id));
        if (stream == null)
            return null;

        string json;
        using (var reader = new StreamReader(stream))
            json = reader.ReadToEnd();
        var file = SampleFile.Parse(json, out var findings);
        if (file == null)
            UrDeckLog.Warn($"Provider '{provider.Id}' ({provider.Name}): its sample file cannot be used, so it has no sample: {string.Join("; ", findings)}");
        return file;
    }

    /// <summary>
    /// The provider's descriptor with the sample in its place: the same id, name and intervals, an instance that replays
    /// <paramref name="file"/> at the seconds since <paramref name="start"/> by <paramref name="now"/>.
    /// </summary>
    public static ProviderDescriptor Replace(ProviderDescriptor provider, SampleFile file, Func<DateTime> now, DateTime start) =>
        provider with
        {
            Factory = () => new SampleProvider(file, now, start),
            // A sample needs no settings, so a provider that requires some still shows its sample.
            SettingsTypeOverride = typeof(ProviderSettings),
        };
}
