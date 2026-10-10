// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>Answers from a table of recorded bodies and remembers what was asked for. Nothing here touches the network.</summary>
internal sealed class FakeIconHttp : IIconHttp
{
    public Dictionary<string, byte[]> Answers { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, the answer to every address that has none of its own: a sign-in page.</summary>
    public byte[]? Everything { get; set; }

    /// <summary>Addresses that answer from another address (a redirect that was followed).</summary>
    public Dictionary<string, string> Redirects { get; } = new(StringComparer.Ordinal);

    public string? Failure { get; set; }

    public List<string> Requests { get; } = [];

    public static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "icons", name));

    public Task<IconHttpResponse> GetAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        lock (Requests)
            Requests.Add(url.AbsoluteUri);
        if (Failure != null)
            throw new IconRequestException(Failure);

        var final = Redirects.TryGetValue(url.AbsoluteUri, out string? target) ? new Uri(target) : url;
        if (!Answers.TryGetValue(final.AbsoluteUri, out byte[]? body))
            body = Everything;
        if (body == null)
            throw new IconRequestException($"{url.Host} answered 404");
        if (body.Length > maxBytes)
            throw new IconRequestException($"{url.Host} sent more than {maxBytes / 1024} KB");
        return Task.FromResult(new IconHttpResponse(final, body));
    }
}

public class HtmlLinksTests
{
    [Fact]
    public void Attributes_AreReadInAnyOrder_WithAnyQuotes_OrNone()
    {
        var links = HtmlLinks.Scan("""
            <link rel="icon" href="/a.png" sizes="32x32" type="image/png">
            <LINK HREF='/b.png' REL='apple-touch-icon'>
            <link rel=manifest href=/c.json>
            <link   href = "/d.png"   rel = "shortcut icon" />
            """);

        Assert.Equal(4, links.Count);
        Assert.Equal(new HtmlLink("icon", "/a.png", "32x32", "image/png"), links[0]);
        Assert.Equal(new HtmlLink("apple-touch-icon", "/b.png", null, null), links[1]);
        Assert.Equal(new HtmlLink("manifest", "/c.json", null, null), links[2]);
        Assert.Equal("/d.png", links[3].Href);
        Assert.True(links[3].HasRel("ICON"));
        Assert.True(links[3].HasRel("shortcut"));
        Assert.False(links[3].HasRel("shortcut icon"));
    }

    [Fact]
    public void EntitiesInAnAddress_AreDecoded() =>
        Assert.Equal("/i.png?a=1&b=2", Assert.Single(HtmlLinks.Scan("""<link rel="icon" href="/i.png?a=1&amp;b=2">""")).Href);

    [Theory]
    [InlineData("<linker rel=\"icon\" href=\"/a.png\">")]
    [InlineData("<link rel=\"icon\">")]
    [InlineData("<link href=\"/a.png\">")]
    [InlineData("<link rel=\"icon\" href=\"\">")]
    [InlineData("<link")]
    [InlineData("")]
    public void WhatIsNotALinkWithARelAndAnAddress_IsLeftOut(string html) =>
        Assert.Empty(HtmlLinks.Scan(html));

    [Theory]
    [InlineData("<link rel=\"icon\" href=\"/a.png")]
    [InlineData("<link rel=icon href=/a.png")]
    [InlineData("<link rel=\"icon\" = href=\"/a.png\" \"stray>")]
    public void ABrokenTag_DoesNotThrow(string html)
    {
        var links = HtmlLinks.Scan(html);

        Assert.True(links.Count <= 1);
    }

    [Fact]
    public void OnlyManifestAndIconLinks_AreKept_HoweverManyOthersComeFirst()
    {
        // A large site: hundreds of stylesheets and preloads, then the icons.
        string html = string.Concat(Enumerable.Repeat("<link rel=\"stylesheet\" href=\"/a.css\"><link rel=\"preload\" href=\"/a.js\" as=\"script\">", 300))
            + "<link rel=\"mask-icon\" href=\"/mask.svg\"><link rel=\"alternate icon\" href=\"/alt.png\"><link rel=\"manifest\" href=\"/m.json\">";

        var links = HtmlLinks.Scan(html);

        Assert.Equal(["/alt.png", "/m.json"], links.Select(l => l.Href));
    }

    [Fact]
    public void APageWithEndlessLinks_IsCutOff()
    {
        string html = string.Concat(Enumerable.Repeat("<link rel=\"icon\" href=\"/a.png\">", 500));

        Assert.Equal(64, HtmlLinks.Scan(html).Count);
    }
}

public class SiteIconFinderTests
{
    private const string Site = "https://example.com/";

    private readonly FakeIconHttp _http = new();

    private SiteIconResult Find(string address = Site, int wanted = 256) =>
        new SiteIconFinder(_http).FindAsync(new Uri(address), wanted, CancellationToken.None).GetAwaiter().GetResult();

    private void Serve(string url, byte[] body) => _http.Answers[url] = body;

    private void ServeManifestSite()
    {
        Serve(Site, FakeIconHttp.Fixture("manifest-page.html"));
        Serve("https://example.com/static/icons/site.webmanifest", FakeIconHttp.Fixture("site.webmanifest"));
        foreach (int size in new[] { 192, 384, 512 })
            Serve($"https://example.com/static/icons/icon-{size}.png", TestImages.Png(size, size));
        Serve("https://example.com/static/icons/maskable-1024.png", TestImages.Png(1024, 1024));
        Serve("https://example.com/static/icons/mono-512.png", TestImages.Png(512, 512));
        Serve("https://example.com/static/icons/touch-180.png", TestImages.Png(180, 180));
        Serve("https://example.com/static/icons/favicon-32.png", TestImages.Png(32, 32));
    }

    [Fact]
    public void AManifestWithSizedIcons_GivesTheSmallestOneAtLeastAsLargeAsWanted()
    {
        ServeManifestSite();

        var result = Find(wanted: 256);
        using var icon = result.Bitmap;

        Assert.NotNull(icon);
        Assert.Equal(384, icon!.Width);
        Assert.Equal("https://example.com/static/icons/icon-384.png", result.Detail);
        // The page, the manifest and one icon: nothing else was asked for.
        Assert.Equal(
            [Site, "https://example.com/static/icons/site.webmanifest", "https://example.com/static/icons/icon-384.png"],
            _http.Requests);
    }

    [Theory]
    [InlineData(64, 192)]
    [InlineData(192, 192)]
    [InlineData(193, 384)]
    [InlineData(512, 512)]
    [InlineData(600, 512)]
    public void TheWantedSize_PicksTheManifestIcon(int wanted, int expected)
    {
        ServeManifestSite();

        using var icon = Find(wanted: wanted).Bitmap;

        Assert.Equal(expected, icon!.Width);
    }

    [Fact]
    public void WhenTheBestCandidateFails_TheLargerOnesAreTried_ThenTheSmallerFromLargeToSmall()
    {
        ServeManifestSite();
        _http.Answers.Remove("https://example.com/static/icons/icon-384.png");
        _http.Answers.Remove("https://example.com/static/icons/icon-512.png");

        using var icon = Find(wanted: 256).Bitmap;

        Assert.Equal(192, icon!.Width);
        Assert.Equal(
            ["icon-384.png", "icon-512.png", "icon-192.png"],
            _http.Requests.Skip(2).Select(r => r[(r.LastIndexOf('/') + 1)..]));
    }

    [Fact]
    public void MaskableMonochromeAndSvgIcons_AreNeverAskedFor()
    {
        ServeManifestSite();

        Find(wanted: 1024).Bitmap?.Dispose();

        Assert.DoesNotContain(_http.Requests, r => r.Contains("maskable") || r.Contains("mono") || r.EndsWith(".svg", StringComparison.Ordinal));
    }

    [Fact]
    public void APageWithOnlyAnAppleTouchIcon_GivesThatIcon()
    {
        Serve(Site, FakeIconHttp.Fixture("touch-page.html"));
        Serve("https://example.com/images/touch.png?v=3&t=1", TestImages.Png(180, 180));

        var result = Find();
        using var icon = result.Bitmap;

        Assert.Equal(180, icon!.Width);
        Assert.Equal("https://example.com/images/touch.png?v=3&t=1", result.Detail);
    }

    [Fact]
    public void APageWithNoIconLinks_FallsBackToTheFaviconAtTheSitesRoot()
    {
        Serve("https://example.com/app/start", FakeIconHttp.Fixture("bare-page.html"));
        Serve("https://example.com/favicon.ico", TestImages.Ico(64));

        var result = Find("https://example.com/app/start");
        using var icon = result.Bitmap;

        Assert.Equal(64, icon!.Width);
        Assert.Equal("https://example.com/favicon.ico", result.Detail);
    }

    [Fact]
    public void ASixteenPixelFavicon_IsRejected()
    {
        Serve(Site, FakeIconHttp.Fixture("bare-page.html"));
        Serve("https://example.com/favicon.ico", TestImages.Ico(16));

        var result = Find();

        Assert.Null(result.Bitmap);
        Assert.Contains("no usable icon", result.Detail);
    }

    [Theory]
    [InlineData(47, false)]
    [InlineData(48, true)]
    public void TheShorterSide_MustBeAtLeastFortyEightPixels(int side, bool accepted)
    {
        Serve(Site, FakeIconHttp.Fixture("bare-page.html"));
        Serve("https://example.com/favicon.ico", TestImages.Png(200, side));

        using var icon = Find().Bitmap;

        Assert.Equal(accepted, icon != null);
    }

    [Fact]
    public void ADirectImageAddress_IsUsed_AndNoPageIsParsed()
    {
        Serve("https://example.com/art/logo.png", TestImages.Png(300, 300));

        var result = Find("https://example.com/art/logo.png");
        using var icon = result.Bitmap;

        Assert.Equal(300, icon!.Width);
        Assert.Equal(["https://example.com/art/logo.png"], _http.Requests);
    }

    [Fact]
    public void ATinyDirectImage_IsRejected_WithoutFurtherRequests()
    {
        Serve("https://example.com/dot.png", TestImages.Png(16, 16));

        var result = Find("https://example.com/dot.png");

        Assert.Null(result.Bitmap);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public void ASiteThatAnswersEverythingWithASignInPage_YieldsNoIcon_AfterAtMostFourDownloads()
    {
        _http.Everything = FakeIconHttp.Fixture("signin-page.html");

        var result = Find("https://signin.example.com/dashboard");

        Assert.Null(result.Bitmap);
        Assert.Contains("no usable icon", result.Detail);
        // The page and the "manifest" (the same page again), then four candidates and no more.
        Assert.Equal(2 + SiteIconFinder.MaxDownloads, _http.Requests.Count);
        Assert.All(_http.Requests, r => Assert.StartsWith("https://signin.example.com/", r));
    }

    [Fact]
    public void AManifestWithOnlyMaskableIcons_IsPassedOver_ForThePagesOwnIcon()
    {
        Serve(Site, FakeIconHttp.Fixture("maskable-page.html"));
        Serve("https://example.com/manifest.webmanifest", FakeIconHttp.Fixture("maskable.webmanifest"));
        Serve("https://example.com/maskable-512.png", TestImages.Png(512, 512));
        Serve("https://example.com/mono-512.png", TestImages.Png(512, 512));
        Serve("https://example.com/icon-96.png", TestImages.Png(96, 96));

        var result = Find();
        using var icon = result.Bitmap;

        Assert.Equal(96, icon!.Width);
        Assert.Equal("https://example.com/icon-96.png", result.Detail);
    }

    [Fact]
    public void AnIconLinkToAnotherHost_IsFollowedOverHttpsOnly()
    {
        Serve(Site, FakeIconHttp.Fixture("cross-host-page.html"));
        Serve("http://cdn.example.net/icon-256.png", TestImages.Png(256, 256));
        Serve("https://cdn.example.org/icon-128.png", TestImages.Png(128, 128));

        var result = Find();
        using var icon = result.Bitmap;

        Assert.Equal(128, icon!.Width);
        Assert.DoesNotContain(_http.Requests, r => r.StartsWith("http://", StringComparison.Ordinal) || r.StartsWith("data:", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnencryptedSite_MayUseItsOwnHost_ButNoOtherUnencryptedOne()
    {
        // A device on the local network: the address the user typed is http, and so are its icons.
        Serve("http://deck.example.com:8123/", FakeIconHttp.Fixture("cross-host-page.html"));
        Serve("http://cdn.example.net/icon-256.png", TestImages.Png(256, 256));
        Serve("http://deck.example.com:8123/favicon.ico", TestImages.Ico(64));

        using var icon = Find("http://deck.example.com:8123/").Bitmap;

        Assert.Equal(64, icon!.Width);
        Assert.DoesNotContain("http://cdn.example.net/icon-256.png", _http.Requests);
    }

    [Fact]
    public void ACutOffIcon_IsRejected_AndTheNextCandidateIsTried()
    {
        byte[] whole = TestImages.NoisePng(180, 180);
        Serve(Site, FakeIconHttp.Fixture("touch-page.html"));
        Serve("https://example.com/images/touch.png?v=3&t=1", whole[..(whole.Length / 2)]);
        Serve("https://example.com/favicon.ico", TestImages.Ico(64));

        var result = Find();
        using var icon = result.Bitmap;

        Assert.Equal("https://example.com/favicon.ico", result.Detail);
    }

    [Fact]
    public void AnIconLargerThanTheSizeLimit_IsRejected()
    {
        Serve(Site, FakeIconHttp.Fixture("bare-page.html"));
        Serve("https://example.com/favicon.ico", new byte[SiteIconFinder.ImageLimit + 1]);

        Assert.Null(Find().Bitmap);
    }

    [Fact]
    public void WhenThePageWasRedirected_LinksResolveAgainstWhereItCameFrom()
    {
        _http.Redirects[Site] = "https://www.example.com/app/";
        Serve("https://www.example.com/app/", FakeIconHttp.Fixture("touch-page.html"));
        Serve("https://www.example.com/app/images/touch.png?v=3&t=1", TestImages.Png(180, 180));

        var result = Find();
        using var icon = result.Bitmap;

        Assert.Equal("https://www.example.com/app/images/touch.png?v=3&t=1", result.Detail);
    }

    [Fact]
    public void WhenTheSiteCannotBeReached_TheReasonIsReported_AndNothingElseIsAsked()
    {
        _http.Failure = "No such host is known.";

        var result = Find();

        Assert.Null(result.Bitmap);
        Assert.Equal("No such host is known.", result.Detail);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public void AManifestThatIsNotJson_IsIgnored()
    {
        Serve(Site, FakeIconHttp.Fixture("maskable-page.html"));
        Serve("https://example.com/manifest.webmanifest", "<html>not a manifest</html>"u8.ToArray());
        Serve("https://example.com/icon-96.png", TestImages.Png(96, 96));

        using var icon = Find().Bitmap;

        Assert.Equal(96, icon!.Width);
    }
}

public sealed class SiteIconCacheTests : IDisposable
{
    private static readonly Uri Address = new("https://example.com/app");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-iconcache-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly FakeTime _time = new();
    private readonly string _folder;
    private int _asked;
    private Func<SiteIconResult> _answer = () => new SiteIconResult(ImageFiles.Decode(TestImages.Png(128, 128)), "https://example.com/icon.png");

    public SiteIconCacheTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
        _folder = Path.Combine(_root, "cache", "icons");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    /// <summary>A cache as a newly started application has it: nothing in memory, the folder as it was left.</summary>
    private SiteIconCache Start(string? folder = null) =>
        new(folder ?? _folder, (_, _, _) =>
        {
            Interlocked.Increment(ref _asked);
            return Task.FromResult(_answer());
        }, _time);

    private static SiteIconResult NoIcon() => new(null, "the site offers no usable icon");

    [Fact]
    public void AFetchedIcon_IsWrittenAsOnePngNamedByAHash_InsideTheFolder()
    {
        using var icon = Start().Load(Address, 256);

        Assert.NotNull(icon);
        string file = Assert.Single(Directory.GetFiles(_folder));
        Assert.Equal(_folder, Path.GetDirectoryName(file));
        Assert.Matches("^[0-9a-f]{64}\\.png$", Path.GetFileName(file));
        using var stored = ImageFiles.Decode(File.ReadAllBytes(file));
        Assert.Equal(128, stored!.Width);
        Assert.Contains("fetched from https://example.com/icon.png", Log);
    }

    [Fact]
    public void AnAddressCannotNameAPathOutsideTheFolder()
    {
        var hostile = new Uri("https://example.com/..%2F..%2F..%2Fwindows/x.png?a=..\\..\\b");

        Start().Load(hostile, 256)?.Dispose();

        Assert.Equal(_folder, Path.GetDirectoryName(Assert.Single(Directory.GetFiles(_root, "*.png", SearchOption.AllDirectories))));
    }

    [Fact]
    public void ARestartADayLater_ShowsTheIconFromDisk_WithoutARequest()
    {
        Start().Load(Address, 256)?.Dispose();
        _time.Advance(TimeSpan.FromDays(1));

        using var icon = Start().Load(Address, 256);

        Assert.NotNull(icon);
        Assert.Equal(1, _asked);
        Assert.Contains("from the cache, no request", Log);
    }

    [Fact]
    public void AnIconLargerThanTheLimit_IsStoredAtTheLimit()
    {
        _answer = () => new SiteIconResult(ImageFiles.Decode(TestImages.Png(1024, 512)), "x");

        using var icon = Start().Load(Address, 512);

        Assert.Equal((512, 256), (icon!.Width, icon.Height));
        using var stored = ImageFiles.Decode(File.ReadAllBytes(Assert.Single(Directory.GetFiles(_folder))));
        Assert.Equal((512, 256), (stored!.Width, stored.Height));
    }

    [Fact]
    public async Task AfterThirtyDays_TheKeptIconIsUsed_AndRefreshedInTheBackground_Once()
    {
        Start().Load(Address, 256)?.Dispose();
        _time.Advance(TimeSpan.FromDays(31));
        _answer = () => new SiteIconResult(ImageFiles.Decode(TestImages.Png(200, 200)), "https://example.com/new.png");

        var cache = Start();
        using var first = cache.Load(Address, 256);
        await cache.Refreshing.WaitAsync(TimeSpan.FromSeconds(5));
        using var second = cache.Load(Address, 256);

        // The first answer is the old icon: nobody waits for the network. The refreshed one is there afterwards.
        Assert.Equal(128, first!.Width);
        Assert.Equal(200, second!.Width);
        Assert.Equal(2, _asked);
        Assert.Contains("refreshed from https://example.com/new.png", Log);
    }

    [Fact]
    public async Task ARefreshThatFails_KeepsTheOldIcon_AndIsNotRepeatedInThisRun()
    {
        Start().Load(Address, 256)?.Dispose();
        _time.Advance(TimeSpan.FromDays(40));
        _answer = NoIcon;

        var cache = Start();
        using var first = cache.Load(Address, 256);
        await cache.Refreshing.WaitAsync(TimeSpan.FromSeconds(5));
        using var second = cache.Load(Address, 256);
        await cache.Refreshing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(128, first!.Width);
        Assert.Equal(128, second!.Width);
        Assert.Equal(2, _asked);
        Assert.Contains("keeping the cached one", Log);
    }

    [Fact]
    public void AnAddressThatYieldedNoIcon_IsNotAskedAgainInThisRun_ButIsAfterARestart()
    {
        _answer = NoIcon;
        var cache = Start();

        Assert.Null(cache.Load(Address, 256));
        Assert.Null(cache.Load(Address, 256));
        Assert.Null(cache.Load(Address, 64));
        Assert.Equal(1, _asked);
        Assert.Contains("No site icon for example.com: the site offers no usable icon", Log);

        Assert.Null(Start().Load(Address, 256));
        Assert.Equal(2, _asked);
        Assert.Empty(Directory.Exists(_folder) ? Directory.GetFiles(_folder) : []);
    }

    [Fact]
    public void ADamagedCacheFile_IsIgnored_LookedUpAgain_AndReplaced()
    {
        var cache = Start();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(cache.PathOf(Address), "these bytes are not an image");

        using var icon = cache.Load(Address, 256);

        Assert.Equal(128, icon!.Width);
        Assert.Equal(1, _asked);
        using var stored = ImageFiles.Decode(File.ReadAllBytes(cache.PathOf(Address)));
        Assert.NotNull(stored);
    }

    [Fact]
    public void DeletingTheFolder_IsSafe_TheIconIsFetchedAgain()
    {
        Start().Load(Address, 256)?.Dispose();
        Directory.Delete(_folder, recursive: true);

        using var icon = Start().Load(Address, 256);

        Assert.NotNull(icon);
        Assert.Equal(2, _asked);
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void AFolderThatCannotBeWritten_FallsBackToMemory_WithOneLogLine()
    {
        // A file where the folder should be: the folder can be neither created nor written.
        string blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "in the way");
        var cache = Start(blocked);

        using var first = cache.Load(Address, 256);
        using var again = cache.Load(Address, 256);
        using var other = cache.Load(new Uri("https://example.org/"), 256);

        Assert.NotNull(first);
        Assert.NotNull(again);
        Assert.NotNull(other);
        // Each address was fetched once; the second request for the first came from memory.
        Assert.Equal(2, _asked);
        Assert.Single(Log.Split('\n'), line => line.Contains("cannot be written"));
    }

    [Fact]
    public void TwoAddresses_HaveTwoFiles()
    {
        var cache = Start();

        cache.Load(new Uri("https://example.com/a"), 256)?.Dispose();
        cache.Load(new Uri("https://example.com/b"), 256)?.Dispose();

        Assert.Equal(2, Directory.GetFiles(_folder).Length);
        Assert.NotEqual(SiteIconCache.KeyOf(new Uri("https://example.com/a")), SiteIconCache.KeyOf(new Uri("https://example.com/b")));
    }
}
