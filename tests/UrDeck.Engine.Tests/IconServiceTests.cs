// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class IconServiceTests : IDisposable
{
    private const string Exe = @"C:\Apps\Steam\steam.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-iconservice-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly FakeLoaders _loaders = new();
    private readonly IconService _service;

    public IconServiceTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
        _service = new IconService(_loaders);
    }

    public void Dispose()
    {
        _loaders.Hold.Set();
        _service.Dispose();
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    /// <summary>Stands in for the disk, the shell and the web: records what was asked and answers with plain squares.</summary>
    private sealed class FakeLoaders : IIconLoaders
    {
        public List<string> Calls { get; } = [];

        /// <summary>Loads wait here; reset it to keep an icon "loading" for as long as a test needs.</summary>
        public ManualResetEventSlim Hold { get; } = new(true);

        public Func<string, SKBitmap?> File { get; set; } = _ => Square(128);

        public Func<string, SKBitmap?> Shell { get; set; } = _ => Square(256);

        public Func<Uri, SKBitmap?> Site { get; set; } = _ => Square(192);

        public bool Disposed { get; private set; }

        public static SKBitmap Square(int size) => ImageFiles.Decode(TestImages.Png(size, size))!;

        private T Record<T>(string call, Func<T> answer)
        {
            lock (Calls)
                Calls.Add(call);
            Hold.Wait(10000);
            return answer();
        }

        public SKBitmap? LoadFile(string path) => Record($"file:{path}", () => File(path));

        public SKBitmap? LoadShell(string parsingName) => Record($"shell:{parsingName}", () => Shell(parsingName));

        public SKBitmap? LoadSite(Uri address, int wantedSize) => Record($"site:{address}@{wantedSize}", () => Site(address));

        public void Dispose() => Disposed = true;
    }

    private sealed class NoReadings : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class NoLauncher : ILauncher
    {
        public bool Launch(string target, string? arguments = null) => false;
    }

    /// <summary>One widget's services, counting the repaints the engine asks for.</summary>
    private sealed class Holder
    {
        private int _repaints;

        public Holder(IconService service)
        {
            Services = new WidgetServices(new NoReadings(), new NoLauncher(), service);
            Services.RepaintRequested += () => Interlocked.Increment(ref _repaints);
        }

        public WidgetServices Services { get; }

        public int Repaints => Volatile.Read(ref _repaints);

        public IconResult Get(string source, int size = 200) => Services.Icons.GetIcon(source, size);
    }

    private Holder NewHolder() => new(_service);

    private void Settle() => Assert.True(TestImages.WaitFor(() => !_service.AnyPending), "the icon never settled");

    private string[] Calls
    {
        get
        {
            lock (_loaders.Calls)
                return [.. _loaders.Calls];
        }
    }

    [Fact]
    public void TheFirstRequest_AnswersLoading_AndTheWidgetIsRepaintedOnceWhenTheIconIsReady()
    {
        _loaders.Hold.Reset();
        var widget = NewHolder();

        var first = widget.Get(Exe);
        Assert.Null(first.Image);
        Assert.True(first.IsLoading);
        Assert.True(_service.AnyPending);
        Assert.Equal(0, widget.Repaints);

        _loaders.Hold.Set();
        Settle();
        Assert.True(TestImages.WaitFor(() => widget.Repaints == 1));

        var later = widget.Get(Exe);
        var again = widget.Get(Exe);
        Assert.NotNull(later.Image);
        Assert.False(later.IsLoading);
        Assert.Same(later.Image, again.Image);
        // Asking again loads nothing and repaints nothing.
        Assert.Single(Calls);
        Assert.Equal(1, widget.Repaints);
    }

    [Fact]
    public void AnIconThatCannotBeHad_AnswersNone_AfterOneRepaint()
    {
        _loaders.Shell = _ => null;
        var widget = NewHolder();

        Assert.True(widget.Get(Exe).IsLoading);
        Settle();
        Assert.True(TestImages.WaitFor(() => widget.Repaints == 1));

        var given = widget.Get(Exe);
        Assert.Null(given.Image);
        Assert.False(given.IsLoading);
        Assert.Single(Calls);
        Assert.Contains(Exe, Log);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"tools\relative.exe")]
    [InlineData("https://")]
    public void AnEmptyOrInvalidSource_IsNoneAtOnce_AndNothingIsLoadedOrRepainted(string source)
    {
        var widget = NewHolder();

        var result = widget.Get(source);

        Assert.Equal(IconResult.None, result);
        Assert.False(_service.AnyPending);
        Assert.Equal(0, _service.Count);
        Assert.Empty(Calls);
        Assert.Equal(0, widget.Repaints);
    }

    [Fact]
    public void TwoWidgetsWithTheSameSourceAndASimilarSize_ShareOneImage_AndBothAreRepainted()
    {
        var a = NewHolder();
        var b = NewHolder();

        a.Get(Exe, 200);
        b.Get(Exe.ToUpperInvariant(), 250);
        Settle();
        Assert.True(TestImages.WaitFor(() => a.Repaints == 1 && b.Repaints == 1));

        Assert.Single(Calls);
        Assert.Equal(1, _service.Count);
        Assert.Same(a.Get(Exe, 200).Image, b.Get(Exe, 250).Image);
    }

    [Fact]
    public void AMuchSmallerSize_IsItsOwnSmallerImage()
    {
        var widget = NewHolder();

        widget.Get(Exe, 250);
        widget.Get(Exe, 60);
        Settle();

        Assert.Equal(2, _service.Count);
        Assert.Equal(256, widget.Get(Exe, 250).Image!.Width);
        // The shell's 256 pixel icon is not handed out at four times the size asked for.
        Assert.Equal(64, widget.Get(Exe, 60).Image!.Width);
    }

    [Theory]
    [InlineData(1, 64)]
    [InlineData(64, 64)]
    [InlineData(65, 128)]
    [InlineData(200, 256)]
    [InlineData(257, 512)]
    [InlineData(4000, 512)]
    public void ASizeFallsIntoTheSmallestBucketAtOrAboveIt(int size, int bucket) =>
        Assert.Equal(bucket, IconService.BucketFor(size));

    [Fact]
    public void AnImage_IsReleasedWhenTheLastWidgetThatAskedForItIsDisposed()
    {
        var a = NewHolder();
        var b = NewHolder();
        a.Get(Exe);
        b.Get(Exe);
        Settle();
        var image = a.Get(Exe).Image!;

        a.Services.Dispose();
        Assert.Equal(1, _service.Count);
        Assert.NotEqual(IntPtr.Zero, image.Handle);
        Assert.Same(image, b.Get(Exe).Image);

        b.Services.Dispose();
        Assert.Equal(0, _service.Count);
        Assert.Equal(IntPtr.Zero, image.Handle);
    }

    [Fact]
    public void ADisposedWidget_GetsNoIcons_AndIsNeverRepainted()
    {
        _loaders.Hold.Reset();
        var widget = NewHolder();
        widget.Get(Exe);

        widget.Services.Dispose();
        _loaders.Hold.Set();
        Settle();

        Assert.Equal(0, widget.Repaints);
        Assert.Equal(0, _service.Count);
        Assert.Equal(IconResult.None, widget.Get(Exe));
        Assert.Single(Calls);
    }

    [Fact]
    public void AfterTheImageWasReleased_AskingAgainLoadsItAgain()
    {
        var first = NewHolder();
        first.Get(Exe);
        Settle();
        first.Services.Dispose();

        var second = NewHolder();
        Assert.True(second.Get(Exe).IsLoading);
        Settle();

        Assert.NotNull(second.Get(Exe).Image);
        Assert.Equal(2, Calls.Length);
    }

    [Theory]
    [InlineData(@"C:\Icons\steam.png", "file:")]
    [InlineData(@"C:\Icons\steam.ICO", "file:")]
    [InlineData(@"C:\Apps\Steam\steam.exe", "shell:")]
    [InlineData(@"C:\Links\Steam.lnk", "shell:")]
    [InlineData(@"C:\Users\someone\Documents", "shell:")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "shell:")]
    [InlineData("steam://open/main", "shell:")]
    public void AFileAShellItemAndAnotherAddress_NeverGoNearTheWeb(string source, string loader)
    {
        var widget = NewHolder();

        widget.Get(source);
        Settle();

        string call = Assert.Single(Calls);
        Assert.StartsWith(loader, call);
        Assert.NotNull(widget.Get(source).Image);
    }

    [Fact]
    public void AWebAddress_GivesTheSitesIcon_AtTheBucketSize()
    {
        var widget = NewHolder();

        widget.Get("https://example.com/app", 200);
        Settle();

        Assert.Equal(["site:https://example.com/app@256"], Calls);
        Assert.Equal(192, widget.Get("https://example.com/app", 200).Image!.Width);
    }

    [Fact]
    public void AWebAddressWhoseSiteYieldsNoIcon_GivesWhatWindowsShowsForIt()
    {
        _loaders.Site = _ => null;
        var widget = NewHolder();

        widget.Get("https://example.com/app");
        Settle();

        Assert.Equal(["site:https://example.com/app@256", "shell:https://example.com/app"], Calls);
        Assert.Equal(256, widget.Get("https://example.com/app").Image!.Width);
    }

    [Fact]
    public void AMissingImageFile_IsNone_AndLoggedOnce_HoweverOftenItsPageIsShown()
    {
        _loaders.File = _ => null;
        for (int visit = 0; visit < 3; visit++)
        {
            var widget = NewHolder();
            widget.Get(@"C:\Icons\gone.png");
            Settle();
            Assert.Equal(new IconResult(null, false), widget.Get(@"C:\Icons\gone.png"));
            widget.Services.Dispose();
        }

        Assert.Single(Log.Split('\n'), line => line.Contains(@"C:\Icons\gone.png"));
        // An image file is never handed to the shell for a document icon.
        Assert.All(Calls, call => Assert.StartsWith("file:", call));
    }

    [Fact]
    public void ALoaderThatThrows_GivesNone_AndTheServiceCarriesOn()
    {
        _loaders.Shell = name => name.Contains("bad") ? throw new InvalidOperationException("shell hiccup") : FakeLoaders.Square(256);
        var widget = NewHolder();

        widget.Get(@"C:\Apps\bad.exe");
        widget.Get(Exe);
        Settle();

        Assert.Null(widget.Get(@"C:\Apps\bad.exe").Image);
        Assert.NotNull(widget.Get(Exe).Image);
        Assert.Contains("shell hiccup", Log);
    }

    [Fact]
    public void DisposingTheService_ReleasesEveryImage_AndItsLoaders()
    {
        var widget = NewHolder();
        widget.Get(Exe);
        Settle();
        var image = widget.Get(Exe).Image!;

        _service.Dispose();

        Assert.Equal(IntPtr.Zero, image.Handle);
        Assert.True(_loaders.Disposed);
        Assert.Equal(IconResult.None, widget.Get(Exe));
    }

    [Fact]
    public void ServicesWithoutAnIconService_HaveNoIcons()
    {
        using var services = new WidgetServices(new NoReadings(), new NoLauncher(), null);

        Assert.Equal(IconResult.None, services.Icons.GetIcon(Exe, 128));
    }
}

public sealed class RealIconSourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-realicons-tests-" + Guid.NewGuid().ToString("N"));

    public RealIconSourcesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void LocalSources_NeedNoNetwork_AndCreateNoCacheFolder()
    {
        string cache = Path.Combine(_root, "cache", "icons");
        string png = Path.Combine(_root, "icon.png");
        File.WriteAllBytes(png, TestImages.Png(96, 96));
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        using var loaders = new IconLoaders(cache, TimeProvider.System);
        using var file = loaders.LoadFile(png);
        using var shell = loaders.LoadShell(explorer);

        Assert.Equal(96, file!.Width);
        Assert.Equal(256, shell!.Width);
        Assert.False(Directory.Exists(cache));
    }
}
