// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The plugin loader in the host's demo mode (host-shell, "Demo Mode"; data-providers, "First-Party Providers Carry Samples").</summary>
public sealed class DemoModeTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-demo-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public DemoModeTests()
    {
        Directory.CreateDirectory(_dir);
        UrDeckLog.LogPath = Path.Combine(_dir, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort: a plugin context may still hold its copy */ }
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private const string Source = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using UrDeck.Sdk.Data;

        [DataProvider("{ID}", "Plug")]
        public class PlugProvider : IDataProvider
        {
            public static int Created;
            private IReadingSink _sink;
            public PlugProvider() { Created++; }
            public IReadOnlyList<ReadingDescriptor> Describe() => new[] { new ReadingDescriptor("v", ReadingKind.Number, "V", "Value") };
            public void Start(IReadingSink sink, ProviderSettings settings) { _sink = sink; }
            public void SetDemand(IReadOnlyCollection<string> paths) { }
            public Task SampleAsync(CancellationToken cancellationToken) { _sink.Publish("v", 3); return Task.CompletedTask; }
            public void Shutdown() { }
        }
        """;

    private const string Sample = """
        { "version": 1, "readings": { "v": { "kind": "number", "label": "V", "name": "Value", "value": 42 } } }
        """;

    /// <summary>A provider plugin with the id, carrying <paramref name="sample"/> as its sample file (null for none).</summary>
    private void WritePlugin(string id, string? sample)
    {
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        var compilation = CSharpCompilation.Create("Demo_" + id.Replace('-', '_'),
            [CSharpSyntaxTree.ParseText(Source.Replace("{ID}", id, StringComparison.Ordinal))], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var resources = sample == null
            ? null
            : new[] { new ResourceDescription(ProviderSamples.ResourceName(id), () => new MemoryStream(Encoding.UTF8.GetBytes(sample)), isPublic: true) };

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, manifestResources: resources);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        File.WriteAllBytes(Path.Combine(_dir, $"{id}.dll"), stream.ToArray());
    }

    private static Reading WaitFor(WidgetPluginLoader loader, string id)
    {
        loader.Readings.Subscribe([id]);
        Assert.True(TestImages.WaitFor(() => loader.Readings.Read(id).State != ReadingState.Pending), $"{id} stayed pending");
        return loader.Readings.Read(id);
    }

    [Fact]
    public void InDemoMode_AProviderWithASample_IsReplaced_AndOneWithoutRunsAsUsual()
    {
        WritePlugin("with-sample", Sample);
        WritePlugin("without-sample", null);
        using var loader = new WidgetPluginLoader(demo: new SampleClock(() => Start, Start));

        loader.ScanAndLoadPlugins(_dir);

        Assert.Equal(["with-sample"], loader.SampledProviders);
        Assert.Equal(Reading.Ok(42), WaitFor(loader, "with-sample:v"));
        Assert.Equal(Reading.Ok(3), WaitFor(loader, "without-sample:v"));
        Assert.Single(Log.Split('\n'), line => line.Contains("Demo mode: sample data for with-sample", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutDemoMode_EveryProviderRuns()
    {
        WritePlugin("with-sample", Sample);
        using var loader = new WidgetPluginLoader();

        loader.ScanAndLoadPlugins(_dir);

        Assert.Empty(loader.SampledProviders);
        Assert.Equal(Reading.Ok(3), WaitFor(loader, "with-sample:v"));
        Assert.DoesNotContain("Demo mode", Log, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstPartyProviders_AreReplacedByTheirSamples()
    {
        using var loader = new WidgetPluginLoader(demo: new SampleClock(() => Start, Start));

        loader.LoadAssembly(typeof(UrDeck.Providers.Weather.WeatherProvider).Assembly);

        Assert.Equal(["weather"], loader.SampledProviders);
        Assert.Equal(Reading.Ok(18), WaitFor(loader, "weather:Zurich/current/temperature"));
        Assert.Equal(Reading.Ok("Zurich"), WaitFor(loader, "weather:Zurich/place"));
    }
}
