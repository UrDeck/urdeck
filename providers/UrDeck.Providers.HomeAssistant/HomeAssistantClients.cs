// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>Home Assistant's WebSocket API, one JSON message at a time; the seam the tests script.</summary>
internal interface IHomeAssistantSocket : IAsyncDisposable
{
    Task ConnectAsync(Uri address, CancellationToken cancellationToken);

    Task SendAsync(string json, CancellationToken cancellationToken);

    /// <summary>The next message; null when the other side closed the connection. Throws when the connection broke.</summary>
    Task<JsonElement?> ReceiveAsync(CancellationToken cancellationToken);
}

/// <summary>An answer of Home Assistant's REST API.</summary>
internal readonly record struct RestAnswer(int Status, string Body);

/// <summary>Home Assistant's REST API; the seam the tests script. Throws on a network failure or a timeout.</summary>
internal interface IHomeAssistantRest : IDisposable
{
    Task<RestAnswer> GetAsync(string path, CancellationToken cancellationToken);

    Task<RestAnswer> PostAsync(string path, string json, CancellationToken cancellationToken);
}

/// <summary>Makes the clients for one address and token.</summary>
internal interface IHomeAssistantClients
{
    IHomeAssistantSocket CreateSocket();

    IHomeAssistantRest CreateRest(Uri address, string token);
}

/// <summary>The real clients: <see cref="ClientWebSocket"/> and one pooled <see cref="HttpClient"/> per provider run.</summary>
internal sealed class HomeAssistantClients : IHomeAssistantClients
{
    public static readonly HomeAssistantClients Default = new();

    public IHomeAssistantSocket CreateSocket() => new WebSocketClient();

    public IHomeAssistantRest CreateRest(Uri address, string token) => new RestClient(address, token);

    internal static string UserAgent()
    {
        string version = typeof(HomeAssistantClients).Assembly.GetName().Version?.ToString(3) ?? "0";
        return string.Create(CultureInfo.InvariantCulture, $"UrDeck/{version} (+https://github.com/UrDeck/urdeck)");
    }
}

/// <summary>The real <see cref="IHomeAssistantSocket"/>.</summary>
internal sealed class WebSocketClient : IHomeAssistantSocket
{
    // The first event of an unfiltered subscription is about 300 KiB; a listing of a large house a few MiB.
    private const int MaxMessageBytes = 32 * 1024 * 1024;

    private readonly ClientWebSocket _socket = new();
    private readonly byte[] _buffer = new byte[16 * 1024];

    public WebSocketClient()
    {
        // Home Assistant's own ping keeps the connection honest; the provider checks with a ping message of its own.
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _socket.Options.SetRequestHeader("User-Agent", HomeAssistantClients.UserAgent());
    }

    public Task ConnectAsync(Uri address, CancellationToken cancellationToken) => _socket.ConnectAsync(address, cancellationToken);

    public Task SendAsync(string json, CancellationToken cancellationToken) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    public async Task<JsonElement?> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(_buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            message.Write(_buffer, 0, result.Count);
            if (message.Length > MaxMessageBytes)
                throw new InvalidDataException($"Home Assistant sent a message larger than {MaxMessageBytes / 1024 / 1024} MiB");
            if (!result.EndOfMessage)
                continue;
            using var document = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
            return document.RootElement.Clone();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Closing is a courtesy; the socket is released either way.
        }

        _socket.Dispose();
    }
}

/// <summary>The real <see cref="IHomeAssistantRest"/>: one client, the token in its header, a time limit on every request.</summary>
internal sealed class RestClient : IHomeAssistantRest
{
    private readonly HttpClient _client;

    public RestClient(Uri address, string token)
    {
        var handler = new SocketsHttpHandler
        {
            // Pooled, so a tap after the first costs a few milliseconds on a LAN; a short lifetime follows DNS changes.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };
        _client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(address.AbsoluteUri.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 32 * 1024 * 1024,
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(HomeAssistantClients.UserAgent());
    }

    public async Task<RestAnswer> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(path, cancellationToken).ConfigureAwait(false);
        return new RestAnswer((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<RestAnswer> PostAsync(string path, string json, CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
        return new RestAnswer((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    public void Dispose() => _client.Dispose();
}
