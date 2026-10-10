// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace UrDeck.Engine.Icons;

/// <summary>A <c>&lt;link&gt;</c> tag of a page, as far as icons care.</summary>
internal readonly record struct HtmlLink(string Rel, string Href, string? Sizes, string? Type)
{
    /// <summary>Whether the space-separated <c>rel</c> value contains the word.</summary>
    public bool HasRel(string word) =>
        Rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(word, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Finds the manifest and icon <c>&lt;link&gt;</c> tags of a page without parsing it: real pages are sloppy, and the
/// attributes come in any order, with any quotes or none. Nothing else of the page is looked at.
/// </summary>
internal static class HtmlLinks
{
    private const int MaxLinks = 64;

    public static List<HtmlLink> Scan(string html)
    {
        var links = new List<HtmlLink>();
        int at = 0;
        while (links.Count < MaxLinks && (at = html.IndexOf("<link", at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            at += 5;
            if (at >= html.Length || !(char.IsWhiteSpace(html[at]) || html[at] == '/'))
                continue;

            var attributes = ReadAttributes(html, ref at);
            if (!attributes.TryGetValue("rel", out string? rel) || !attributes.TryGetValue("href", out string? href) || href.Length == 0)
                continue;

            // Only what discovery uses is kept: a large page has hundreds of stylesheet and preload links before its icons.
            var link = new HtmlLink(rel, href, attributes.GetValueOrDefault("sizes"), attributes.GetValueOrDefault("type"));
            if (Wanted.Any(link.HasRel))
                links.Add(link);
        }

        return links;
    }

    private static readonly string[] Wanted = ["manifest", "icon", "apple-touch-icon", "apple-touch-icon-precomposed"];

    /// <summary>Reads <c>name</c>, <c>name=value</c>, <c>name="value"</c> and <c>name='value'</c> up to the tag's end.</summary>
    private static Dictionary<string, string> ReadAttributes(string html, ref int at)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (at < html.Length)
        {
            while (at < html.Length && (char.IsWhiteSpace(html[at]) || html[at] == '/'))
                at++;
            if (at >= html.Length || html[at] == '>')
                break;

            int nameStart = at;
            while (at < html.Length && !char.IsWhiteSpace(html[at]) && html[at] is not ('=' or '>' or '/'))
                at++;
            string name = html[nameStart..at];

            while (at < html.Length && char.IsWhiteSpace(html[at]))
                at++;

            string value = "";
            if (at < html.Length && html[at] == '=')
            {
                at++;
                while (at < html.Length && char.IsWhiteSpace(html[at]))
                    at++;
                if (at < html.Length && html[at] is '"' or '\'')
                {
                    char quote = html[at++];
                    int end = html.IndexOf(quote, at);
                    if (end < 0)
                        end = html.Length;
                    value = html[at..end];
                    at = Math.Min(end + 1, html.Length);
                }
                else
                {
                    int valueStart = at;
                    while (at < html.Length && !char.IsWhiteSpace(html[at]) && html[at] != '>')
                        at++;
                    value = html[valueStart..at];
                }
            }

            if (name.Length == 0)
            {
                // A stray character that is neither a name nor the tag's end: step over it.
                at++;
                continue;
            }

            attributes.TryAdd(name, WebUtility.HtmlDecode(value).Trim());
        }

        return attributes;
    }
}

/// <summary>The icon a site offered, or why there is none.</summary>
/// <param name="Bitmap">The decoded icon; the receiver owns it. Null when none was found.</param>
/// <param name="Detail">Where the icon came from, or the reason there is none.</param>
internal sealed record SiteIconResult(SKBitmap? Bitmap, string Detail);

/// <summary>
/// Looks for a site's own icon: the address itself when it is an image, then the icons of the web manifest the page
/// links, then the page's icon links, then <c>/favicon.ico</c>. A candidate counts only when its bytes decode as a
/// raster image that is big enough; the status and the declared type of an answer are not trusted, because a sign-in
/// page answers every path with 200 and HTML.
/// </summary>
internal sealed class SiteIconFinder(IIconHttp http)
{
    /// <summary>An icon whose shorter side is smaller than this is not worth showing on a card.</summary>
    public const int MinSide = 48;

    /// <summary>At most this many icon candidates are downloaded for one address.</summary>
    public const int MaxDownloads = 4;

    public const int PageLimit = 2 * 1024 * 1024;
    public const int ImageLimit = 5 * 1024 * 1024;

    private readonly record struct Candidate(Uri Url, int Size);

    public async Task<SiteIconResult> FindAsync(Uri address, int wantedSize, CancellationToken cancellationToken)
    {
        IconHttpResponse page;
        try
        {
            page = await http.GetAsync(address, ImageLimit, cancellationToken).ConfigureAwait(false);
        }
        catch (IconRequestException ex)
        {
            return new SiteIconResult(null, ex.Message);
        }

        // The address may itself be a picture; then there is no page to read.
        if (ImageFiles.Decode(page.Body) is { } direct)
        {
            if (Math.Min(direct.Width, direct.Height) >= MinSide)
                return new SiteIconResult(direct, page.FinalUri.AbsoluteUri);
            direct.Dispose();
            return new SiteIconResult(null, $"the image at the address is smaller than {MinSide} pixels");
        }

        if (page.Body.Length > PageLimit)
            return new SiteIconResult(null, "the page is too large to look for an icon in");

        var links = HtmlLinks.Scan(Encoding.UTF8.GetString(page.Body));
        var candidates = new List<Candidate>();

        var manifest = links.FirstOrDefault(link => link.HasRel("manifest"));
        if (manifest.Href != null && Resolve(page.FinalUri, manifest.Href) is { } manifestUrl && MayRequest(page.FinalUri, manifestUrl))
            candidates.AddRange(Order(await ReadManifestAsync(manifestUrl, cancellationToken).ConfigureAwait(false), wantedSize));

        candidates.AddRange(Order(LinkIcons(links, page.FinalUri), wantedSize));
        candidates.Add(new Candidate(new Uri(page.FinalUri, "/favicon.ico"), 0));

        int downloads = 0;
        var tried = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (downloads == MaxDownloads)
                break;
            if (!MayRequest(page.FinalUri, candidate.Url) || !tried.Add(candidate.Url.AbsoluteUri))
                continue;

            downloads++;
            try
            {
                var answer = await http.GetAsync(candidate.Url, ImageLimit, cancellationToken).ConfigureAwait(false);
                if (ImageFiles.Decode(answer.Body) is not { } icon)
                    continue;
                if (Math.Min(icon.Width, icon.Height) >= MinSide)
                    return new SiteIconResult(icon, candidate.Url.AbsoluteUri);
                icon.Dispose();
            }
            catch (IconRequestException)
            {
                // One candidate less; the next may still work.
            }
        }

        return new SiteIconResult(null, "the site offers no usable icon");
    }

    private async Task<List<Candidate>> ReadManifestAsync(Uri url, CancellationToken cancellationToken)
    {
        var icons = new List<Candidate>();
        try
        {
            var answer = await http.GetAsync(url, PageLimit, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(answer.Body, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("icons", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return icons;
            }

            foreach (var icon in list.EnumerateArray())
            {
                if (icon.ValueKind != JsonValueKind.Object || Text(icon, "src") is not { Length: > 0 } src)
                    continue;
                // Maskable icons have padding for a shaped mask and monochrome ones are silhouettes: neither is the site's icon.
                if (Text(icon, "purpose") is { } purpose && !Words(purpose).Contains("any", StringComparer.OrdinalIgnoreCase))
                    continue;
                if (IsSvg(src, Text(icon, "type")) || Resolve(answer.FinalUri, src) is not { } iconUrl)
                    continue;
                icons.Add(new Candidate(iconUrl, DeclaredSize(Text(icon, "sizes"))));
            }
        }
        catch (Exception ex) when (ex is IconRequestException or JsonException)
        {
            // No manifest, or the sign-in page in its place.
        }

        return icons;
    }

    /// <summary>The page's own icon links: <c>apple-touch-icon</c> first, then <c>icon</c>.</summary>
    private static List<Candidate> LinkIcons(List<HtmlLink> links, Uri pageUrl)
    {
        var icons = new List<Candidate>();
        foreach (bool touch in new[] { true, false })
        {
            foreach (var link in links)
            {
                bool isTouch = link.HasRel("apple-touch-icon") || link.HasRel("apple-touch-icon-precomposed");
                if (isTouch != touch || (!isTouch && !link.HasRel("icon")))
                    continue;
                if (IsSvg(link.Href, link.Type) || Resolve(pageUrl, link.Href) is not { } url)
                    continue;
                icons.Add(new Candidate(url, DeclaredSize(link.Sizes)));
            }
        }

        return icons;
    }

    /// <summary>
    /// The smallest candidate at least as large as wanted first, then the larger ones, then the smaller ones from large to
    /// small, then those that declare no size, in the order they were listed.
    /// </summary>
    private static IEnumerable<Candidate> Order(List<Candidate> candidates, int wantedSize) =>
        candidates.Where(c => c.Size >= wantedSize && c.Size > 0).OrderBy(c => c.Size)
            .Concat(candidates.Where(c => c.Size < wantedSize && c.Size > 0).OrderByDescending(c => c.Size))
            .Concat(candidates.Where(c => c.Size == 0));

    /// <summary>The largest size in a <c>sizes</c> value such as <c>"192x192 512x512"</c>; 0 when there is none (or <c>any</c>).</summary>
    private static int DeclaredSize(string? sizes)
    {
        int largest = 0;
        foreach (string token in Words(sizes ?? ""))
        {
            string[] parts = token.Split('x', 'X');
            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height))
            {
                largest = Math.Max(largest, Math.Min(width, height));
            }
        }

        return largest;
    }

    private static string[] Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsSvg(string href, string? type)
    {
        if (type != null && type.Contains("svg", StringComparison.OrdinalIgnoreCase))
            return true;
        int end = href.IndexOfAny(['?', '#']);
        string path = end < 0 ? href : href[..end];
        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? Resolve(Uri baseUrl, string href) =>
        Uri.TryCreate(baseUrl, href, out var url) ? url : null;

    /// <summary>
    /// Whether discovery may ask for <paramref name="url"/>: any encrypted address, and an unencrypted one only on the
    /// host of the page itself (a device on the local network). Other schemes (<c>data:</c>) are never requested.
    /// </summary>
    private static bool MayRequest(Uri page, Uri url) =>
        url.Scheme == Uri.UriSchemeHttps
        || (url.Scheme == Uri.UriSchemeHttp && string.Equals(url.Host, page.Host, StringComparison.OrdinalIgnoreCase));
}
