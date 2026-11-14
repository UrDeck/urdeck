// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Steam;

/// <summary>
/// The readings Steam's registry values and files give (the local half of the provider): what to publish, with which state and
/// reason. Plain code over <see cref="ISteamLocal"/>. A file that cannot be read or understood leaves the readings that depend on
/// it unpublished, so they keep their last values, and is logged once per file and cause (never with a name, a game or an id).
/// </summary>
internal sealed class LocalReadings(ISteamLocal local, TimeProvider time, Action<string> log)
{
    internal const string ClientRunning = "client/running";
    internal const string MeName = "me/name";
    internal const string MePlaying = "me/playing";
    internal const string MeGame = "me/game";
    internal const string MeGameArt = "me/game/art";
    internal const string GamesRecent = "games/recent";
    internal const string Downloads = "downloads";
    internal const string DownloadProgress = "download/progress";
    internal const string DownloadName = "download/name";

    internal const string NotInstalled = "Steam is not installed on this PC";
    internal const string NothingDownloading = "Nothing downloading";

    /// <summary>SteamID64 of account 0: a 32-bit account id plus this is the SteamID64.</summary>
    internal const long IdBase = 76561197960265728;

    internal static readonly string[] ClientPaths = [ClientRunning, MeName, MePlaying, MeGame, MeGameArt];
    internal static readonly string[] DownloadPaths = [Downloads, DownloadProgress, DownloadName];

    private const int RecentCount = 10;
    private const int RecentDays = 30;

    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    public static ReadingDescriptor[] Describe() =>
    [
        new(ClientRunning, ReadingKind.OnOff, "Steam running", "Whether the Steam client is running"),
        new(MeName, ReadingKind.Text, "My name", "The persona name of the account signed in to Steam on this PC"),
        new(MePlaying, ReadingKind.OnOff, "Playing", "Whether a game is being played"),
        new(MeGame, ReadingKind.Text, "My game", "The game being played (empty when none)"),
        new(MeGameArt, ReadingKind.Text, "My game art", "Path of the game's art in Steam's library cache (empty when there is none)"),
        new(GamesRecent, ReadingKind.List, "Recent games", "The games played most recently, with hours")
        {
            Unit = "games",
            EmptyText = "No games played recently",
            Fields =
            [
                new ReadingField("name", ReadingKind.Text, "Game") { Role = FieldRole.Title },
                new ReadingField("lastPlayed", ReadingKind.Date, "Last played") { Role = FieldRole.Detail },
                new ReadingField("hours2wks", ReadingKind.Number, "Last two weeks") { Role = FieldRole.Status, Unit = "h", Decimals = 1 },
                new ReadingField("hours", ReadingKind.Number, "Total") { Unit = "h", Decimals = 0 },
                new ReadingField("art", ReadingKind.Text, "Art") { Role = FieldRole.Image },
                new ReadingField("link", ReadingKind.Text, "Link") { Role = FieldRole.Link },
            ],
        },
        new(Downloads, ReadingKind.List, "Downloads", "Games downloading or updating")
        {
            Unit = "downloads",
            EmptyText = NothingDownloading,
            Fields =
            [
                new ReadingField("name", ReadingKind.Text, "Game") { Role = FieldRole.Title },
                new ReadingField("state", ReadingKind.Text, "State") { Role = FieldRole.Detail },
                new ReadingField("progress", ReadingKind.Percent, "Progress") { Role = FieldRole.Status, Decimals = 0, Min = 0, Max = 100 },
                new ReadingField("art", ReadingKind.Text, "Art") { Role = FieldRole.Image },
            ],
        },
        new(DownloadProgress, ReadingKind.Percent, "Download progress", "Progress of the first active download")
        {
            Decimals = 0,
            Min = 0,
            Max = 100,
        },
        new(DownloadName, ReadingKind.Text, "Download", "Name of the first active download"),
    ];

    /// <summary>The client's readings: whether Steam runs, my name and the game being played.</summary>
    public IReadOnlyList<ReadingUpdate> ReadClient()
    {
        if (!local.Installed)
            return Unavailable(ClientPaths, NotInstalled);

        ClientState client;
        try
        {
            client = local.ReadClient();
        }
        catch (Exception ex) when (IsFault(ex))
        {
            Fault("registry", ex);
            return [];
        }

        Recovered("registry");

        var updates = new List<ReadingUpdate>
        {
            ReadingUpdate.Of(ClientRunning, client.Running),
        };

        long? account = Account(client);
        if (account is { } id)
        {
            string? name = PersonaName(id);
            if (name != null)
                updates.Add(ReadingUpdate.Of(MeName, name));
        }
        else
        {
            updates.Add(ReadingUpdate.Missing(MeName, "No Steam account has signed in on this PC"));
        }

        uint appId = client.Running ? client.RunningAppId : 0;
        bool playing = appId != 0;
        updates.Add(ReadingUpdate.Of(MePlaying, playing));
        updates.Add(ReadingUpdate.Of(MeGame, playing ? GameName(appId) ?? $"Steam game {appId.ToString(CultureInfo.InvariantCulture)}" : ""));
        updates.Add(ReadingUpdate.Of(MeGameArt, playing ? local.FindArt(appId) : ""));
        return updates;
    }

    /// <summary>The recently played games, from the account's <c>localconfig.vdf</c>.</summary>
    public IReadOnlyList<ReadingUpdate> ReadRecent()
    {
        if (!local.Installed)
            return Unavailable([GamesRecent], NotInstalled);

        var clientState = SafeClient();
        if (Account(clientState) is not { } account)
            return [ReadingUpdate.Missing(GamesRecent, "No Steam account has signed in on this PC")];

        KeyValues config;
        try
        {
            if (!KeyValues.TryParse(local.ReadLocalConfig(AccountId(account)), out config))
            {
                Fault("localconfig", "it does not parse");
                return [];
            }

            Recovered("localconfig");
        }
        catch (Exception ex) when (IsFault(ex))
        {
            Fault("localconfig", ex);
            return [];
        }

        var apps = config.Find("UserLocalConfigStore/Software/Valve/Steam/apps");
        long cutoff = time.GetUtcNow().AddDays(-RecentDays).ToUnixTimeSeconds();
        var candidates = new List<(uint AppId, long LastPlayed, long Total, long TwoWeeks)>();
        foreach (var (key, app) in apps?.Children ?? [])
        {
            if (!uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out uint appId))
                continue;
            long last = app.GetLong("LastPlayed");
            if (last < cutoff)
                continue;
            candidates.Add((appId, last, app.GetLong("Playtime"), app.GetLong("Playtime2wks")));
        }

        var names = Names();
        var rows = new List<ReadingRow>();
        foreach (var c in candidates.OrderByDescending(c => c.LastPlayed).ThenBy(c => c.AppId))
        {
            if (rows.Count == RecentCount)
                break;
            if (!names.TryGetValue(c.AppId, out string? name))
                continue; // a game this PC knows no name of (not installed): the connected half's cache may fill it later
            rows.Add(new ReadingRow(c.AppId.ToString(CultureInfo.InvariantCulture),
            [
                KeyValuePair.Create("name", (ReadingValue)name),
                KeyValuePair.Create("lastPlayed", ReadingValue.FromTime(DateTimeOffset.FromUnixTimeSeconds(c.LastPlayed).ToLocalTime())),
                KeyValuePair.Create("hours2wks", (ReadingValue)Math.Round(c.TwoWeeks / 60.0, 1)),
                KeyValuePair.Create("hours", (ReadingValue)Math.Round(c.Total / 60.0, 1)),
                KeyValuePair.Create("art", (ReadingValue)local.FindArt(c.AppId)),
                KeyValuePair.Create("link", (ReadingValue)("steam://nav/games/details/" + c.AppId.ToString(CultureInfo.InvariantCulture))),
            ]));
        }

        return [ReadingUpdate.Of(GamesRecent, ReadingValue.FromList(new ReadingList(rows)))];
    }

    /// <summary>The downloads, from the manifests of every library. <paramref name="active"/> says whether one is running.</summary>
    public IReadOnlyList<ReadingUpdate> ReadDownloads(out bool active)
    {
        active = false;
        if (!local.Installed)
            return Unavailable(DownloadPaths, NotInstalled);

        var rows = new List<(string Key, string Name, string State, double Progress, uint AppId, int Order)>();
        foreach (string library in Libraries() ?? [])
        {
            IReadOnlyList<ManifestFile> files;
            try
            {
                files = local.ReadManifests(library);
            }
            catch (Exception ex) when (IsFault(ex))
            {
                Fault("manifests", ex);
                return [];
            }

            foreach (var file in files)
            {
                if (file.Text is null || !KeyValues.TryParse(file.Text, out var manifest))
                {
                    // A manifest cut off while Steam writes it: the whole answer waits for the next change rather than dropping a row.
                    Fault("manifest", file.Fault ?? "it does not parse");
                    return [];
                }

                Recovered("manifest");
                var row = Download(manifest, time.GetUtcNow().ToUnixTimeSeconds());
                if (row is { } r)
                    rows.Add(r);
            }
        }

        rows.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.AppId.CompareTo(b.AppId));
        active = rows.Any(r => r.Order == 0);

        var list = new ReadingList(rows.Select(r => new ReadingRow(r.Key,
        [
            KeyValuePair.Create("name", (ReadingValue)r.Name),
            KeyValuePair.Create("state", (ReadingValue)r.State),
            KeyValuePair.Create("progress", (ReadingValue)Math.Round(r.Progress, 0)),
            KeyValuePair.Create("art", (ReadingValue)local.FindArt(r.AppId)),
        ])));

        var updates = new List<ReadingUpdate> { ReadingUpdate.Of(Downloads, ReadingValue.FromList(list)) };
        var first = rows.FirstOrDefault(r => r.Order == 0);
        if (first.Key is null)
        {
            updates.Add(ReadingUpdate.Missing(DownloadProgress, NothingDownloading));
            updates.Add(ReadingUpdate.Missing(DownloadName, NothingDownloading));
        }
        else
        {
            updates.Add(ReadingUpdate.Of(DownloadProgress, Math.Round(first.Progress, 0)));
            updates.Add(ReadingUpdate.Of(DownloadName, first.Name));
        }

        return updates;
    }

    /// <summary>The unavailable updates for <paramref name="paths"/>.</summary>
    internal static List<ReadingUpdate> Unavailable(IEnumerable<string> paths, string reason) =>
        [.. paths.Select(p => ReadingUpdate.Missing(p, reason))];

    /// <summary>The SteamID64 of the account this PC's Steam is signed in to, or of the most recent one.</summary>
    internal long? Account(ClientState client)
    {
        if (client.Running && client.ActiveUser != 0)
            return IdBase + client.ActiveUser;
        var users = LoginUsers();
        foreach (var (id, user) in users?.Find("users")?.Children ?? [])
        {
            if (user.GetText("MostRecent") == "1" && long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long steamId))
                return steamId;
        }

        return null;
    }

    /// <summary>The account this PC's Steam is signed in to (or most recently was), or null when it is not installed or unknown.</summary>
    internal long? CurrentAccount() => local.Installed ? Account(SafeClient()) : null;

    internal static uint AccountId(long steamId) => (uint)(steamId - IdBase);

    private static (string Key, string Name, string State, double Progress, uint AppId, int Order)? Download(KeyValues manifest, long now)
    {
        var app = manifest.Find("AppState");
        if (app is null || !uint.TryParse(app.GetText("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out uint appId))
            return null;

        long flags = app.GetLong("StateFlags");
        const long UpdateRequired = 2, FullyInstalled = 4, UpdateRunning = 256, UpdatePaused = 512, UpdateStarted = 1024;
        if (flags == FullyInstalled || (flags & (UpdateRequired | UpdateRunning | UpdatePaused | UpdateStarted)) == 0)
            return null;

        bool paused = (flags & UpdatePaused) != 0;
        bool running = (flags & (UpdateRunning | UpdateStarted)) != 0;
        bool update = app.GetLong("LastUpdated") != 0;

        // An update Steam has only scheduled (its time is still ahead) is available, not queued: it is not shown as a download.
        long scheduled = app.GetLong("ScheduledAutoUpdate");
        if (!paused && !running && scheduled > now)
            return null;
        (string state, int order) = paused ? ("Paused", 1) : running ? (update ? "Updating" : "Downloading", 0) : ("Queued", 2);

        long total = app.GetLong("BytesToDownload");
        long done = app.GetLong("BytesDownloaded");
        double progress = total > 0 ? Math.Clamp(done * 100.0 / total, 0, 100) : 0;
        return (appId.ToString(CultureInfo.InvariantCulture), app.GetText("name") ?? $"Steam game {appId.ToString(CultureInfo.InvariantCulture)}", state, progress, appId, order);
    }

    private ClientState SafeClient()
    {
        try
        {
            return local.ReadClient();
        }
        catch (Exception ex) when (IsFault(ex))
        {
            return default;
        }
    }

    private KeyValues? LoginUsers()
    {
        try
        {
            if (KeyValues.TryParse(local.ReadLoginUsers(), out var users))
            {
                Recovered("loginusers");
                return users;
            }

            Fault("loginusers", "it does not parse");
        }
        catch (Exception ex) when (IsFault(ex))
        {
            Fault("loginusers", ex);
        }

        return null;
    }

    private string? PersonaName(long steamId) =>
        LoginUsers()?.GetText("users/" + steamId.ToString(CultureInfo.InvariantCulture) + "/PersonaName");

    /// <summary>The library folders: Steam's own and those in <c>libraryfolders.vdf</c>; null when the file cannot be read.</summary>
    private List<string>? Libraries()
    {
        var libraries = new List<string> { local.SteamPath };
        try
        {
            if (!KeyValues.TryParse(local.ReadLibraryFolders(), out var folders))
            {
                Fault("libraryfolders", "it does not parse");
                return libraries;
            }

            Recovered("libraryfolders");
            foreach (var (_, entry) in folders.Find("libraryfolders")?.Children ?? [])
            {
                string? path = entry.GetText("path");
                if (!string.IsNullOrEmpty(path) && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                    libraries.Add(path);
            }
        }
        catch (Exception ex) when (IsFault(ex))
        {
            Fault("libraryfolders", ex);
        }

        return libraries;
    }

    /// <summary>The name of an installed game from its manifest, else from the registry; null when this PC does not know it.</summary>
    internal string? GameName(uint appId)
    {
        foreach (string library in Libraries() ?? [])
        {
            try
            {
                string? text = local.ReadManifest(library, appId);
                if (text != null && KeyValues.TryParse(text, out var manifest) && manifest.GetText("AppState/name") is { Length: > 0 } name)
                    return name;
            }
            catch (Exception ex) when (IsFault(ex))
            {
                Fault("manifest", ex);
            }
        }

        try
        {
            return local.ReadApp(appId)?.Name is { Length: > 0 } registered ? registered : null;
        }
        catch (Exception ex) when (IsFault(ex))
        {
            return null;
        }
    }

    /// <summary>Every game name this PC knows: the manifests, then the registry's, by app id.</summary>
    private Dictionary<uint, string> Names()
    {
        var names = new Dictionary<uint, string>();
        foreach (string library in Libraries() ?? [])
        {
            IReadOnlyList<ManifestFile> files;
            try
            {
                files = local.ReadManifests(library);
            }
            catch (Exception ex) when (IsFault(ex))
            {
                Fault("manifests", ex);
                continue;
            }

            foreach (var file in files)
            {
                if (file.Text != null && KeyValues.TryParse(file.Text, out var manifest)
                    && uint.TryParse(manifest.GetText("AppState/appid"), NumberStyles.None, CultureInfo.InvariantCulture, out uint id)
                    && manifest.GetText("AppState/name") is { Length: > 0 } name)
                {
                    names[id] = name;
                }
            }
        }

        return names;
    }

    private static bool IsFault(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException;

    private void Fault(string what, Exception ex) => Fault(what, ex.GetType().Name);

    private void Fault(string what, string cause)
    {
        if (_logged.Add(what + "|" + cause))
            log($"Steam: {what} could not be read ({cause}); the readings keep their last values");
    }

    private void Recovered(string what) => _logged.RemoveWhere(k => k.StartsWith(what + "|", StringComparison.Ordinal));
}
