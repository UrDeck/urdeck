// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net;
using System.Text.Json;

namespace UrDeck.Providers.Steam;

/// <summary>
/// <see cref="ISteamWeb"/> over HTTPS to <c>api.steampowered.com</c>, <c>store.steampowered.com</c> and the avatar host, and to
/// nothing else. The calls are Steam's documented Web API (<c>ISteamUser</c>), made with the user's own API key. No key, name or id
/// is ever written to a log or an exception message here.
/// </summary>
internal sealed class SteamWebClient : ISteamWeb, IDisposable
{
    private const string Api = "https://api.steampowered.com/";
    private const string Store = "https://store.steampowered.com/api/appdetails";
    private const string AvatarHost = "https://avatars.fastly.steamstatic.com/";
    private const long MaxAvatarBytes = 512 * 1024;

    private readonly HttpClient _http;

    public SteamWebClient()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
    {
    }

    internal SteamWebClient(HttpClient http)
    {
        _http = http;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("UrDeck");
    }

    public void Dispose() => _http.Dispose();

    public async Task<IReadOnlyList<long>> GetFriendsAsync(string apiKey, long steamId, CancellationToken cancellationToken)
    {
        using var reply = await GetAsync("ISteamUser/GetFriendList/v1/",
            [("key", apiKey), ("steamid", steamId.ToString(CultureInfo.InvariantCulture)), ("relationship", "friend")],
            friendList: true, cancellationToken);
        var ids = new List<long>();
        if (reply.Root.TryGetProperty("friendslist", out var list) && list.TryGetProperty("friends", out var friends))
        {
            foreach (var friend in friends.EnumerateArray())
            {
                if (long.TryParse(Text(friend, "steamid"), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    public async Task<IReadOnlyList<FriendState>> GetStatesAsync(string apiKey, IReadOnlyList<long> steamIds, CancellationToken cancellationToken)
    {
        string ids = string.Join(',', steamIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        using var reply = await GetAsync("ISteamUser/GetPlayerSummaries/v2/", [("key", apiKey), ("steamids", ids)], friendList: false, cancellationToken);
        var states = new List<FriendState>();
        if (!reply.Root.TryGetProperty("response", out var response) || !response.TryGetProperty("players", out var players))
            return states;
        foreach (var player in players.EnumerateArray())
        {
            if (!long.TryParse(Text(player, "steamid"), NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id == 0)
                continue;
            ulong game = ulong.TryParse(Text(player, "gameid"), NumberStyles.None, CultureInfo.InvariantCulture, out ulong g) ? g : 0;
            states.Add(new FriendState(id, Text(player, "personaname"), (int)Number(player, "personastate", 0), game, SteamCaches.NormalizeAvatarHash(Text(player, "avatarhash"))));
        }

        return states;
    }

    public async Task<string?> GetAppNameAsync(uint appId, CancellationToken cancellationToken)
    {
        string id = appId.ToString(CultureInfo.InvariantCulture);
        using var response = await _http.GetAsync($"{Store}?appids={id}&filters=basic", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The store answered {(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(id, out var app)
            && app.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True
            && app.TryGetProperty("data", out var data))
        {
            string name = Text(data, "name");
            return name.Length > 0 ? name : null;
        }

        return null;
    }

    public async Task<byte[]?> GetAvatarAsync(string sha, CancellationToken cancellationToken)
    {
        if (!SteamCaches.IsAvatarHash(sha))
            return null;
        using var response = await _http.GetAsync($"{AvatarHost}{sha}_medium.jpg", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The avatar host answered {(int)response.StatusCode}");
        if (response.Content.Headers.ContentLength > MaxAvatarBytes)
            return null;
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length is > 0 and <= (int)MaxAvatarBytes ? bytes : null;
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                _ => "",
            }
            : "";

    private static double Number(JsonElement element, string name, double fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : fallback;

    private async Task<Reply> GetAsync(string method, IEnumerable<(string, string)> query, bool friendList, CancellationToken cancellationToken)
    {
        string url = Api + method + "?" + string.Join('&', query.Select(q => Uri.EscapeDataString(q.Item1) + "=" + Uri.EscapeDataString(q.Item2)));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, cancellationToken);

        // A key Steam does not know is a 403. A friend list the profile keeps private is a 401 (the key is fine).
        if (response.StatusCode == HttpStatusCode.Unauthorized && friendList)
            throw new SteamFriendsPrivateException("Steam answered 401 for the friend list");
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SteamRejectedException($"Steam answered {(int)response.StatusCode}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Steam answered {(int)response.StatusCode}");

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return new Reply(JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body));
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException("Steam answered something that is not JSON", ex);
        }
    }

    private sealed class Reply(JsonDocument document) : IDisposable
    {
        public JsonElement Root { get; } = document.RootElement;

        public void Dispose() => document.Dispose();
    }
}
