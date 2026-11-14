// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net;
using System.Text.Json;

namespace UrDeck.Providers.Steam;

/// <summary>
/// <see cref="ISteamWeb"/> over HTTPS to <c>api.steampowered.com</c>, <c>store.steampowered.com</c> and the avatar host, and to
/// nothing else. The methods are Steam's service API (the calls Steam's own mobile app makes), not the keyed Web API. No token,
/// secret, name or id is ever written to a log or an exception message here.
/// </summary>
internal sealed class SteamWebClient : ISteamWeb, IDisposable
{
    private const string Api = "https://api.steampowered.com/";
    private const string Store = "https://store.steampowered.com/api/appdetails";
    private const string AvatarHost = "https://avatars.fastly.steamstatic.com/";
    private const long MaxAvatarBytes = 512 * 1024;

    private static readonly int[] RejectedResults = [5, 15, 63];

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

    public async Task<QrSession> BeginQrAsync(CancellationToken cancellationToken)
    {
        using var reply = await PostAsync("IAuthenticationService/BeginAuthSessionViaQR/v1/",
        [
            ("device_friendly_name", "UrDeck"),
            ("platform_type", "3"),
            ("website_id", "Mobile"),
        ], cancellationToken);
        var response = reply.Response;
        return new QrSession(
            Text(response, "client_id"),
            Text(response, "request_id"),
            Text(response, "challenge_url"),
            TimeSpan.FromSeconds(Math.Clamp(Number(response, "interval", 5), 1, 30)));
    }

    public async Task<QrPoll> PollQrAsync(QrSession session, CancellationToken cancellationToken)
    {
        using var reply = await PostAsync("IAuthenticationService/PollAuthSessionStatus/v1/",
        [
            ("client_id", session.ClientId),
            ("request_id", session.RequestId),
        ], cancellationToken, allowFailure: true);
        if (reply.EResult != 1)
            throw new SteamSignInFailedException($"Steam answered eresult {reply.EResult}");
        var response = reply.Response;
        QrApproval? approved = Text(response, "refresh_token") is { Length: > 0 } refresh
            ? new QrApproval(refresh, Text(response, "account_name"))
            : null;
        string newClient = Text(response, "new_client_id");
        return new QrPoll(
            newClient.Length > 0 ? newClient : null,
            newClient.Length > 0 ? Text(response, "new_challenge_url") : null,
            response.TryGetProperty("had_remote_interaction", out var seen) && seen.ValueKind == JsonValueKind.True,
            approved);
    }

    public async Task<AccessGrant> RenewAsync(string refreshToken, long steamId, CancellationToken cancellationToken)
    {
        using var reply = await PostAsync("IAuthenticationService/GenerateAccessTokenForApp/v1/",
        [
            ("refresh_token", refreshToken),
            ("steamid", steamId.ToString(CultureInfo.InvariantCulture)),
        ], cancellationToken);
        string token = Text(reply.Response, "access_token");
        if (token.Length == 0)
            throw new SteamRejectedException("Steam gave no access token");
        string rotated = Text(reply.Response, "refresh_token");
        return new AccessGrant(token, SteamJwt.Expires(token) ?? DateTimeOffset.UtcNow.AddHours(1), rotated.Length > 0 ? rotated : null);
    }

    public async Task RevokeAsync(string refreshToken, long steamId, string accessToken, CancellationToken cancellationToken)
    {
        using var reply = await PostAsync("IAuthenticationService/RevokeRefreshToken/v1/",
        [
            ("refresh_token", refreshToken),
            ("steamid", steamId.ToString(CultureInfo.InvariantCulture)),
        ], cancellationToken, accessToken);
    }

    public async Task<IReadOnlyList<long>> GetFriendsAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var reply = await GetAsync("IFriendsListService/GetFriendsList/v1/", [], accessToken, cancellationToken);
        var ids = new List<long>();
        if (reply.Response.TryGetProperty("friendslist", out var list) && list.TryGetProperty("friends", out var friends))
        {
            foreach (var friend in friends.EnumerateArray())
            {
                // Relationship 3 is a friend; the list also holds invitations, blocked accounts and the like.
                if (Number(friend, "efriendrelationship", 0) == 3 && long.TryParse(Text(friend, "ulfriendid"), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    public async Task<IReadOnlyList<FriendState>> GetStatesAsync(string accessToken, IReadOnlyList<long> steamIds, CancellationToken cancellationToken)
    {
        var query = new List<(string, string)>();
        for (int i = 0; i < steamIds.Count; i++)
            query.Add(($"steamids[{i.ToString(CultureInfo.InvariantCulture)}]", steamIds[i].ToString(CultureInfo.InvariantCulture)));

        using var reply = await GetAsync("IPlayerService/GetPlayerLinkDetails/v1/", query, accessToken, cancellationToken);
        var states = new List<FriendState>();
        if (!reply.Response.TryGetProperty("accounts", out var accounts))
            return states;
        foreach (var account in accounts.EnumerateArray())
        {
            account.TryGetProperty("public_data", out var pub);
            account.TryGetProperty("private_data", out var priv);
            long id = long.TryParse(Text(pub, "steamid"), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
            if (id == 0)
                continue;
            ulong game = ulong.TryParse(Text(priv, "game_id"), NumberStyles.None, CultureInfo.InvariantCulture, out ulong g) ? g : 0;
            states.Add(new FriendState(id, Text(pub, "persona_name"), (int)Number(priv, "persona_state", 0), game, SteamCaches.NormalizeAvatarHash(Text(pub, "sha_digest_avatar"))));
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

    private Task<Reply> PostAsync(string method, IEnumerable<(string, string)> fields, CancellationToken cancellationToken, string? accessToken = null, bool allowFailure = false)
    {
        string url = Api + method + (accessToken is null ? "" : "?access_token=" + Uri.EscapeDataString(accessToken));
        var content = new FormUrlEncodedContent(fields.Select(f => KeyValuePair.Create(f.Item1, f.Item2)));
        return SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = content }, allowFailure, cancellationToken);
    }

    private Task<Reply> GetAsync(string method, IEnumerable<(string, string)> query, string accessToken, CancellationToken cancellationToken)
    {
        string url = Api + method + "?access_token=" + Uri.EscapeDataString(accessToken)
            + string.Concat(query.Select(q => "&" + Uri.EscapeDataString(q.Item1) + "=" + Uri.EscapeDataString(q.Item2)));
        return SendAsync(new HttpRequestMessage(HttpMethod.Get, url), allowFailure: false, cancellationToken);
    }

    private async Task<Reply> SendAsync(HttpRequestMessage request, bool allowFailure, CancellationToken cancellationToken)
    {
        using (request)
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new SteamRejectedException($"Steam answered {(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Steam answered {(int)response.StatusCode}");

            int eresult = response.Headers.TryGetValues("x-eresult", out var values)
                && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : 1;
            if (eresult != 1 && !allowFailure)
            {
                if (RejectedResults.Contains(eresult))
                    throw new SteamRejectedException($"Steam answered eresult {eresult}");
                throw new HttpRequestException($"Steam answered eresult {eresult}");
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            }
            catch (JsonException ex)
            {
                throw new HttpRequestException("Steam answered something that is not JSON", ex);
            }

            var root = document.RootElement.TryGetProperty("response", out var inner) ? inner : default;
            return new Reply(document, root.ValueKind == JsonValueKind.Object ? root : document.RootElement, eresult);
        }
    }

    private sealed class Reply(JsonDocument document, JsonElement response, int eresult) : IDisposable
    {
        public JsonElement Response { get; } = response;

        public int EResult { get; } = eresult;

        public void Dispose() => document.Dispose();
    }
}
