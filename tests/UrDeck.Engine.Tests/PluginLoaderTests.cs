// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-plugin-tests-" + Guid.NewGuid().ToString("N"));
    private readonly WidgetPluginLoader _loader = new();

    public PluginLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _loader.Dispose();
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private static byte[] BuildPlugin(string widgetId)
    {
        string source = $@"
using SkiaSharp;
using UrDeck.Sdk;
public class TestCfg : WidgetConfig {{ }}
[Widget(""T"", ""d"", Id = ""{widgetId}"")]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
public class TestWidget : Widget<TestCfg>
{{
    public override void Render(WidgetRenderContext context) {{ }}
}}";
        return Compile(source);
    }

    private const string ProviderSource = @"
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UrDeck.Sdk.Data;
[DataProvider(""{ID}"", ""Plug"")]
public class PlugProvider : IDataProvider
{
    private IReadingSink _sink;
    public IReadOnlyList<ReadingDescriptor> Describe() => new[] { new ReadingDescriptor(""v"", ReadingKind.Number, ""V"", ""Value"") };
    public void Start(IReadingSink sink) { _sink = sink; }
    public void SetDemand(IReadOnlyCollection<string> paths) { }
    public Task SampleAsync(CancellationToken cancellationToken) { _sink.Publish(""v"", 3); return Task.CompletedTask; }
    public void Shutdown() { }
}";

    private static byte[] Compile(string source)
    {
        var refs = new List<MetadataReference>();
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string p in tpa.Split(Path.PathSeparator))
            refs.Add(MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create("TestPlugin_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return ms.ToArray();
    }

    private string WritePlugin(string widgetId, string fileName = "TestPlugin.dll")
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, BuildPlugin(widgetId));
        return path;
    }

    private string WriteSource(string fileName, string source)
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, Compile(source));
        return path;
    }

    private static string ProviderPlugin(string providerId, string? widgetId = null)
    {
        string source = ProviderSource.Replace("{ID}", providerId);
        if (widgetId == null)
            return source;
        return source + $@"
public class TestCfg : UrDeck.Sdk.WidgetConfig {{ }}
[UrDeck.Sdk.Widget(""T"", ""d"", Id = ""{widgetId}"")]
[UrDeck.Sdk.WidgetSize(1, 1)]
[UrDeck.Sdk.RefreshOnData]
public class TestWidget : UrDeck.Sdk.Widget<TestCfg>
{{
    public override void Render(UrDeck.Sdk.WidgetRenderContext context) {{ }}
}}";
    }

    /// <summary>Runs the action with the log redirected to a file of its own, and returns what was logged meanwhile.</summary>
    private string CaptureLog(Action action)
    {
        string original = UrDeck.Engine.Diagnostics.UrDeckLog.LogPath;
        string path = Path.Combine(_dir, "captured.log");
        UrDeck.Engine.Diagnostics.UrDeckLog.LogPath = path;
        try
        {
            action();
        }
        finally
        {
            UrDeck.Engine.Diagnostics.UrDeckLog.LogPath = original;
        }

        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    [Fact]
    public void AProviderPlugin_IsRegisteredUnderItsId()
    {
        WriteSource("Provider.dll", ProviderPlugin("plug"));

        string log = CaptureLog(() => _loader.ScanAndLoadPlugins(_dir));

        var descriptor = _loader.Providers.Get("PLUG");
        Assert.NotNull(descriptor);
        Assert.Equal("Plug", descriptor!.Name);
        Assert.Equal(1000, descriptor.DefaultIntervalMs);
        Assert.Equal(250, descriptor.MinIntervalMs);
        Assert.Empty(_loader.GetRegisteredWidgetTypes());
        Assert.Contains("Registered provider 'plug'", log);
    }

    [Fact]
    public void AnAssemblyWithAProviderAndAWidget_RegistersBoth()
    {
        WriteSource("Both.dll", ProviderPlugin("plug", "both.widget"));

        _loader.ScanAndLoadPlugins(_dir);

        Assert.NotNull(_loader.Providers.Get("plug"));
        Assert.Contains("both.widget", _loader.GetRegisteredWidgetTypes());
    }

    [Fact]
    public void TwoProvidersWithTheSameId_KeepTheFirstByFileName_AndWarnNamingBoth()
    {
        WriteSource("a.dll", ProviderPlugin("plug"));
        WriteSource("b.dll", ProviderPlugin("plug"));

        string log = CaptureLog(() => _loader.ScanAndLoadPlugins(_dir));

        var kept = _loader.Providers.Get("plug");
        Assert.Single(_loader.Providers.Descriptors);
        Assert.Contains("rejected", log);
        Assert.Contains(kept!.AssemblyName, log);
        Assert.Contains("already provides it", log);
    }

    [Fact]
    public void AnAssemblyWithNeitherAWidgetNorAProvider_IsReportedAsNotAPlugin()
    {
        WriteSource("Plain.dll", "public class JustAClass { }");

        string log = CaptureLog(() => _loader.ScanAndLoadPlugins(_dir));

        Assert.Contains("No widgets or providers found in", log);
        Assert.Empty(_loader.Providers.Descriptors);
    }

    [Fact]
    public void AProviderBuiltAgainstAnotherSdk_IsNotRegistered_AndTheLogSaysSo()
    {
        // The plugin brings its own copy of the provider contract, so its type does not implement the host's.
        WriteSource("Foreign.dll", @"
namespace UrDeck.Sdk.Data
{
    public sealed class DataProviderAttribute : System.Attribute { public DataProviderAttribute(string id, string name) { } }
    public interface IReadingSink { }
    public interface IDataProvider { void Start(IReadingSink sink); void Shutdown(); }
}
[UrDeck.Sdk.Data.DataProvider(""foreign"", ""Foreign"")]
public class ForeignProvider : UrDeck.Sdk.Data.IDataProvider
{
    public void Start(UrDeck.Sdk.Data.IReadingSink sink) { }
    public void Shutdown() { }
}");

        string log = CaptureLog(() => _loader.ScanAndLoadPlugins(_dir));

        Assert.Null(_loader.Providers.Get("foreign"));
        Assert.Contains("built against a different UrDeck.Sdk", log);
    }

    [Fact]
    public void AProviderThatBreaksTheRules_IsSkippedWithAReason()
    {
        WriteSource("Bad.dll", ProviderPlugin("Bad_Id"));

        string log = CaptureLog(() => _loader.ScanAndLoadPlugins(_dir));

        Assert.Empty(_loader.Providers.Descriptors);
        Assert.Contains("Skipping provider", log);
        Assert.Contains("lowercase", log);
    }

    [Fact]
    public void Reload_ReleasesTheProviderBeforeItsPluginIsUnloaded_AndTheContextIsCollected()
    {
        string path = WriteSource("Provider.dll", ProviderPlugin("plug"));
        _loader.ScanAndLoadPlugins(_dir);
        var contextRef = SubscribeAndGetProviderContext();

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Null(_loader.Providers.Get("plug"));
        Assert.Equal(UrDeck.Sdk.Data.ReadingState.Unavailable, _loader.Readings.Read("plug:v").State);
        for (int i = 0; i < 50 && contextRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }

        Assert.False(contextRef.IsAlive, "The provider plugin context is still referenced after a reload.");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference SubscribeAndGetProviderContext()
    {
        _loader.Readings.Subscribe(["plug:v"]);
        long started = Environment.TickCount64;
        while (_loader.Readings.Read("plug:v").State != UrDeck.Sdk.Data.ReadingState.Ok && Environment.TickCount64 - started < 5000)
            Thread.Sleep(10);
        Assert.Equal(UrDeck.Sdk.Data.ReadingState.Ok, _loader.Readings.Read("plug:v").State);
        return new WeakReference(AssemblyLoadContext.GetLoadContext(_loader.Providers.Get("plug")!.ProviderType.Assembly));
    }

    [Fact]
    public void LoadsPlugin_InOwnContext_WithSharedContractTypes()
    {
        WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);

        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());
        var widget = _loader.CreateWidget(new WidgetConfig { WidgetTypeId = "test.widget" });

        Assert.NotNull(widget);
        var alc = AssemblyLoadContext.GetLoadContext(widget!.GetType().Assembly);
        Assert.NotNull(alc);
        Assert.NotSame(AssemblyLoadContext.Default, alc);
        Assert.True(alc!.IsCollectible);
        Assert.IsAssignableFrom<IWidget>(widget);
    }

    [Fact]
    public void OriginalFile_IsNotLocked_WhileLoaded()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());

        File.WriteAllBytes(path, BuildPlugin("test.widget.v2"));
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Reload_PicksUpReplacedPlugin()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.Contains("test.widget", _loader.GetRegisteredWidgetTypes());

        File.WriteAllBytes(path, BuildPlugin("test.widget.v2"));
        _loader.ReloadPlugins();

        var types = _loader.GetRegisteredWidgetTypes();
        Assert.Contains("test.widget.v2", types);
        Assert.DoesNotContain("test.widget", types);
        Assert.NotNull(_loader.CreateWidget(new WidgetConfig { WidgetTypeId = "test.widget.v2" }));
    }

    [Fact]
    public void Reload_AfterDelete_LeavesRegistryEmpty()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        Assert.NotEmpty(_loader.GetRegisteredWidgetTypes());

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Empty(_loader.GetRegisteredWidgetTypes());
    }

    [Fact]
    public void Reload_AllowsOldPluginContextToBeCollected()
    {
        string path = WritePlugin("test.widget");
        _loader.ScanAndLoadPlugins(_dir);
        var contextRef = CreateAndRenderWidget();

        File.Delete(path);
        _loader.ReloadPlugins();

        // The loader nudges System.Text.Json's accessor cache ~1.5s after unload; allow for that.
        for (int i = 0; i < 50 && contextRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }
        Assert.False(contextRef.IsAlive, "Unloaded plugin context is still referenced (leak on hot-reload).");
    }

    // Separate non-inlined method so no locals keep the widget or its context alive in the caller.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference CreateAndRenderWidget()
    {
        var config = new WidgetConfig { WidgetTypeId = "test.widget", Width = 1, Height = 1 };
        var widget = _loader.CreateWidget(config)!;
        using var bitmap = UrDeck.Engine.Rendering.PageRenderer.RenderToBitmap(
            100, 100, new[] { (config, widget) }, new UrDeck.Engine.Themes.ThemeStore("").Load(UrDeck.Engine.Themes.ThemeStore.DefaultName), DateTime.Now);
        return new WeakReference(AssemblyLoadContext.GetLoadContext(widget.GetType().Assembly));
    }

    [Fact]
    public void GarbageDll_IsSkipped_OthersStillLoad()
    {
        File.WriteAllText(Path.Combine(_dir, "bad.dll"), "this is not a .NET assembly");
        WritePlugin("test.widget");

        _loader.ScanAndLoadPlugins(_dir);

        Assert.Equal(new[] { "test.widget" }, _loader.GetRegisteredWidgetTypes());
    }
}
