// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net;

namespace UrDeck.Engine.Icons;

/// <summary>What a request gave: the bytes, and the address they finally came from after redirects.</summary>
internal sealed record IconHttpResponse(Uri FinalUri, byte[] Body);

/// <summary>A request that gave nothing usable: no connection, a timeout, a refusal, a certificate error, too many bytes.</summary>
internal sealed class IconRequestException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The one door to the network for site icons, so that discovery is tested on recorded pages.</summary>
internal interface IIconHttp
{
    /// <summary>
    /// The body of the answer to a GET, at most <paramref name="maxBytes"/> long. The status code and content type of a
    /// successful answer are not reported: callers judge the bytes. Every failure is an <see cref="IconRequestException"/>.
    /// </summary>
    Task<IconHttpResponse> GetAsync(Uri url, int maxBytes, CancellationToken cancellationToken);
}

/// <summary>Creates the real client on the first request, so a run without one never opens a socket.</summary>
internal sealed class DeferredIconHttp(Func<IIconHttp> create) : IIconHttp
{
    public Task<IconHttpResponse> GetAsync(Uri url, int maxBytes, CancellationToken cancellationToken) =>
        create().GetAsync(url, maxBytes, cancellationToken);
}

/// <summary>
/// The real <see cref="IIconHttp"/>: anonymous (no cookies, no credentials), at most five redirects, ten seconds per
/// request, a size limit on every body and a <c>User-Agent</c> that says who is asking. Certificates are checked as
/// usual; an invalid one is a failure.
/// </summary>
internal sealed class IconHttp : IIconHttp, IDisposable
{
    public const int MaxRedirects = 5;

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client;

    public IconHttp()
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            Credentials = null,
            PreAuthenticate = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = MaxRedirects,
            ConnectTimeout = Timeout,
            // Icons are fetched in a burst and then not for weeks: keep no connection around afterwards.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        string version = typeof(IconHttp).Assembly.GetName().Version?.ToString(3) ?? "0";
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(string.Create(CultureInfo.InvariantCulture, $"UrDeck/{version} (+https://github.com/UrDeck/urdeck)"));
    }

    public async Task<IconHttpResponse> GetAsync(Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        // One limit for the whole exchange, body included: HttpClient's own timeout stops at the headers here.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);
        try
        {
            using HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new IconRequestException($"{url.Host} answered {(int)response.StatusCode}");
            if (response.Content.Headers.ContentLength > maxBytes)
                throw new IconRequestException($"{url.Host} sent more than {maxBytes / 1024} KB");

            await using Stream stream = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
            using var body = new MemoryStream();
            byte[] buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, limit.Token).ConfigureAwait(false)) > 0)
            {
                if (body.Length + read > maxBytes)
                    throw new IconRequestException($"{url.Host} sent more than {maxBytes / 1024} KB");
                body.Write(buffer, 0, read);
            }

            return new IconHttpResponse(response.RequestMessage?.RequestUri ?? url, body.ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IconRequestException($"{url.Host} did not answer within {Timeout.TotalSeconds:0} seconds");
        }
        catch (HttpRequestException ex)
        {
            // The inner exception names the cause (name not resolved, certificate not trusted); the outer one rarely does.
            throw new IconRequestException(ex.InnerException?.Message ?? ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new IconRequestException(ex.Message, ex);
        }
    }

    public void Dispose() => _client.Dispose();
}
