// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Steam;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The local half of the Steam provider (provider-steam spec) over a fake registry and fake files: no Steam, no Windows.</summary>
public sealed class SteamLocalProviderTests : IDisposable
{
    private const long Id64 = LocalReadings.IdBase + 4242;

    private readonly FakeLocal _local = new();
    private readonly Sink _sink = new();
    private readonly FakeTime _time = new();
    private readonly SteamProvider _provider;

    public SteamLocalProviderTests()
    {
        _provider = new SteamProvider(_local, new FakeSteamWeb(), _time);
        _provider.Start(_sink, new SteamSettings());
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void Not_installed_makes_every_local_reading_unavailable_with_the_reason()
    {
        _local.Installed = false;

        _provider.SetDemand(["me/game", "games/recent", "downloads", "download/progress"]);

        foreach (string path in new[] { "me/game", "games/recent", "downloads", "download/progress" })
            Assert.Equal("Steam is not installed on this PC", _sink.Latest[path].Reason);
    }

    [Fact]
    public void Steam_closed_has_no_game_and_the_most_recent_accounts_name()
    {
        _local.Client = new ClientState(false, 0, 0);

        _provider.SetDemand(["client/running", "me/name", "me/game", "me/playing"]);

        Assert.False(_sink.Bool("client/running"));
        Assert.False(_sink.Bool("me/playing"));
        Assert.Equal("", _sink.Text("me/game"));
        Assert.Equal("Sample Persona", _sink.Text("me/name"));
    }

    [Fact]
    public void A_game_started_in_steam_is_published_from_the_registry_change()
    {
        _provider.SetDemand(["me/game", "me/game/art"]);
        Assert.Equal("", _sink.Text("me/game"));
        _local.Manifests.Add(Manifest(1001, "Sample Quest", 4));
        _local.Art[1001] = @"C:\Steam\appcache\librarycache\1001\header.jpg";

        _local.Client = new ClientState(true, 4242, 1001);
        _local.Raise(LocalChange.Client);

        Assert.Equal("Sample Quest", _sink.Text("me/game"));
        Assert.Equal(@"C:\Steam\appcache\librarycache\1001\header.jpg", _sink.Text("me/game/art"));
        Assert.True(_sink.Bool("me/playing"));
    }

    [Fact]
    public void A_game_this_pc_has_no_manifest_for_is_named_from_the_registry()
    {
        _local.Apps[777] = new RegistryApp("Registry Game", false);
        _local.Client = new ClientState(true, 4242, 777);

        _provider.SetDemand(["me/game"]);

        Assert.Equal("Registry Game", _sink.Text("me/game"));
    }

    [Fact]
    public void A_game_nothing_knows_gets_a_generic_name()
    {
        _local.Client = new ClientState(true, 4242, 555);

        _provider.SetDemand(["me/game"]);

        Assert.Equal("Steam game 555", _sink.Text("me/game"));
    }

    [Fact]
    public void Recent_games_are_the_ten_latest_of_thirty_days_with_hours_and_links()
    {
        long now = _time.GetUtcNow().ToUnixTimeSeconds();
        var apps = new System.Text.StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < 12; i++)
        {
            int id = 2000 + i;
            apps.Append(inv, $"\"{id}\" {{ \"LastPlayed\" \"{now - (i * 3600)}\" \"Playtime\" \"{120 + i}\" \"Playtime2wks\" \"90\" }}\n");
            _local.Manifests.Add(Manifest(id, $"Game {i}", 4));
        }

        apps.Append(inv, $"\"3000\" {{ \"LastPlayed\" \"{now - (40 * 86400)}\" \"Playtime\" \"600\" }}\n");
        _local.Manifests.Add(Manifest(3000, "Old Game", 4));
        _local.LocalConfig = "\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { " + apps + " } } } } }";

        _provider.SetDemand(["games/recent"]);

        var rows = _sink.List("games/recent");
        Assert.Equal(10, rows.Count);
        Assert.Equal("Game 0", rows[0].Get("name")!.Value.Text);
        Assert.Equal(1.5, rows[0].Get("hours2wks")!.Value.Number);
        Assert.Equal(2.0, rows[0].Get("hours")!.Value.Number);
        Assert.Equal("steam://nav/games/details/2000", rows[0].Get("link")!.Value.Text);
        Assert.DoesNotContain(rows.Rows, r => r.Key == "3000");
    }

    [Fact]
    public void A_download_and_an_update_are_listed_with_progress_and_the_first_one_feeds_the_gauge()
    {
        _local.Manifests.Add(Manifest(4001, "Fresh Game", 1026, bytesToDownload: 1000, bytesDownloaded: 250));
        _local.Manifests.Add(Manifest(4002, "Updating Game", 1026, bytesToDownload: 200, bytesDownloaded: 100, lastUpdated: 1700000000));
        _local.Manifests.Add(Manifest(4003, "Waiting Game", 6));
        _local.Manifests.Add(Manifest(4004, "Quiet Game", 4));
        // Steam keeps flag 6 for an update it has scheduled for later: available, not downloading.
        _local.Manifests.Add(Manifest(4005, "Scheduled Game", 6, scheduled: _time.GetUtcNow().ToUnixTimeSeconds() + 86400));

        _provider.SetDemand(["downloads", "download/progress", "download/name"]);

        var rows = _sink.List("downloads");
        Assert.Equal(["4001", "4002", "4003"], rows.Rows.Select(r => r.Key).ToArray());
        Assert.Equal("Downloading", rows[0].Get("state")!.Value.Text);
        Assert.Equal("Updating", rows[1].Get("state")!.Value.Text);
        Assert.Equal("Queued", rows[2].Get("state")!.Value.Text);
        Assert.Equal(25, rows[0].Get("progress")!.Value.Number);
        Assert.Equal("Fresh Game", _sink.Text("download/name"));
        Assert.Equal(25, _sink.Number("download/progress"));
    }

    [Fact]
    public void Nothing_downloading_is_an_empty_list_and_the_gauge_says_so()
    {
        _local.Manifests.Add(Manifest(4004, "Quiet Game", 4));

        _provider.SetDemand(["downloads", "download/progress", "download/name"]);

        Assert.Equal(0, _sink.List("downloads").Count);
        Assert.Equal("Nothing downloading", _sink.Latest["download/progress"].Reason);
        Assert.Equal("Nothing downloading", _sink.Latest["download/name"].Reason);
    }

    [Fact]
    public void A_running_download_is_read_every_five_seconds_and_the_timer_stops_when_it_ends()
    {
        _local.Manifests.Add(Manifest(4001, "Fresh Game", 1026, bytesToDownload: 1000, bytesDownloaded: 250));
        _provider.SetDemand(["download/progress"]);
        Assert.Equal(25, _sink.Number("download/progress"));

        _local.Manifests[0] = Manifest(4001, "Fresh Game", 1026, bytesToDownload: 1000, bytesDownloaded: 500);
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(50, _sink.Number("download/progress"));

        _local.Manifests[0] = Manifest(4001, "Fresh Game", 4);
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal("Nothing downloading", _sink.Latest["download/progress"].Reason);
        Assert.Equal(0, _time.ActiveTimers);
    }

    [Fact]
    public void Manifest_changes_are_debounced_two_seconds()
    {
        _provider.SetDemand(["downloads"]);
        int reads = _local.ManifestReads;

        _local.Raise(LocalChange.Files);
        _time.Advance(TimeSpan.FromSeconds(1));
        _local.Raise(LocalChange.Files);
        _time.Advance(TimeSpan.FromSeconds(1.5));
        Assert.Equal(reads, _local.ManifestReads);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(_local.ManifestReads > reads);
    }

    [Fact]
    public void A_manifest_cut_off_while_written_keeps_the_last_value_and_is_logged_once()
    {
        _local.Manifests.Add(Manifest(4001, "Fresh Game", 1026, bytesToDownload: 1000, bytesDownloaded: 250));
        _provider.SetDemand(["downloads"]);
        int batches = _sink.Batches.Count;

        _local.Manifests[0] = Manifest(4001, "Fresh Game", 1026)[..40];
        _local.Raise(LocalChange.Files);
        _time.Advance(SteamProvider.FileDebounce);
        _local.Raise(LocalChange.Files);
        _time.Advance(SteamProvider.FileDebounce);

        Assert.Equal(batches, _sink.Batches.Count);
        Assert.Single(_sink.Logged);

        _local.Manifests[0] = Manifest(4001, "Fresh Game", 1026, bytesToDownload: 1000, bytesDownloaded: 600);
        _local.Raise(LocalChange.Files);
        _time.Advance(SteamProvider.FileDebounce);
        Assert.Equal(60, _sink.List("downloads")[0].Get("progress")!.Value.Number);
    }

    [Fact]
    public void A_missing_login_file_keeps_the_name_and_is_logged_once_without_naming_anything()
    {
        _provider.SetDemand(["me/name"]);
        Assert.Equal("Sample Persona", _sink.Text("me/name"));
        _local.LoginUsers = null;

        _local.Raise(LocalChange.Client);
        _local.Raise(LocalChange.Client);

        Assert.Equal("Sample Persona", _sink.Text("me/name"));
        string line = Assert.Single(_sink.Logged);
        Assert.DoesNotContain("Sample", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_watched_or_read_until_something_is_subscribed_and_all_stops_when_it_is_not()
    {
        Assert.Equal(0, _local.OpenWatches);
        Assert.Equal(0, _local.Reads);

        _provider.SetDemand(["me/game"]);
        Assert.Equal(1, _local.OpenWatches);
        _provider.SetDemand(["games/recent", "downloads"]);
        Assert.Equal(1, _local.OpenWatches);
        Assert.True(_local.Watching.HasFlag(LocalChange.Files));

        _provider.SetDemand([]);
        Assert.Equal(0, _local.OpenWatches);
        Assert.Equal(0, _time.ActiveTimers);
    }

    [Fact]
    public void Only_my_game_reads_no_manifests_or_local_configuration_until_the_registry_changes()
    {
        _provider.SetDemand(["me/game"]);

        Assert.Equal(0, _local.LocalConfigReads);
        Assert.Equal(0, _local.ManifestReads);
        Assert.False(_local.Watching.HasFlag(LocalChange.Files));
    }

    [Fact]
    public void The_log_never_names_a_person_or_a_game()
    {
        _local.Manifests.Add(Manifest(4001, "Fresh Game", 1026)[..30]);
        _provider.SetDemand(["downloads", "me/name"]);
        _local.LoginUsers = "\"users\" {";
        _local.Raise(LocalChange.Client);

        foreach (string line in _sink.Logged)
        {
            Assert.DoesNotContain("Persona", line, StringComparison.Ordinal);
            Assert.DoesNotContain("Fresh", line, StringComparison.Ordinal);
            Assert.DoesNotContain("4001", line, StringComparison.Ordinal);
        }
    }

    private static string Manifest(int id, string name, int flags, long bytesToDownload = 0, long bytesDownloaded = 0, long lastUpdated = 0, long scheduled = 0) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\"{id}\"\n\t\"name\"\t\"{name}\"\n\t\"StateFlags\"\t\"{flags}\"\n\t\"LastUpdated\"\t\"{lastUpdated}\"\n" +
        $"\t\"BytesToDownload\"\t\"{bytesToDownload}\"\n\t\"BytesDownloaded\"\t\"{bytesDownloaded}\"\n\t\"ScheduledAutoUpdate\"\t\"{scheduled}\"\n}}\n";

    internal sealed class FakeLocal : ISteamLocal
    {
        private readonly List<Action<LocalChange>> _handlers = [];
        private int _reads;
        private int _opened;

        public bool Installed { get; set; } = true;

        public string SteamPath => @"C:\Steam";

        public ClientState Client { get; set; } = new(true, 4242, 0);

        public Dictionary<uint, RegistryApp> Apps { get; } = [];

        public Dictionary<uint, string> Art { get; } = [];

        public string? LoginUsers { get; set; } =
            $"\"users\" {{ \"{Id64}\" {{ \"AccountName\" \"sample\" \"PersonaName\" \"Sample Persona\" \"MostRecent\" \"1\" }} }}";

        public string LocalConfig { get; set; } = "\"UserLocalConfigStore\" { }";

        public List<string> Manifests { get; } = [];

        public int ManifestReads { get; private set; }

        public int LocalConfigReads { get; private set; }

        public int Reads => _reads;

        public int OpenWatches => _handlers.Count;

        public LocalChange Watching { get; private set; }

        public ClientState ReadClient()
        {
            _reads++;
            return Client;
        }

        public RegistryApp? ReadApp(uint appId) => Apps.TryGetValue(appId, out var app) ? app : null;

        public string ReadLoginUsers()
        {
            _reads++;
            return LoginUsers ?? throw new FileNotFoundException();
        }

        public string ReadLibraryFolders() => "\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Steam\" } }";

        public string ReadLocalConfig(uint accountId)
        {
            LocalConfigReads++;
            _reads++;
            return LocalConfig;
        }

        public string? ReadManifest(string libraryPath, uint appId)
        {
            ManifestReads++;
            _reads++;
            return Manifests.FirstOrDefault(m => m.Contains($"\"appid\"\t\"{appId}\"", StringComparison.Ordinal));
        }

        public IReadOnlyList<ManifestFile> ReadManifests(string libraryPath)
        {
            ManifestReads++;
            _reads++;
            return [.. Manifests.Select((m, i) => new ManifestFile($"file{i}", m, null))];
        }

        public string FindArt(uint appId) => Art.GetValueOrDefault(appId, "");

        public IDisposable Watch(LocalChange what, Action<LocalChange> changed)
        {
            _handlers.Add(changed);
            _opened++;
            Watching |= what;
            return new Handle(this, changed, what);
        }

        public void Raise(LocalChange change)
        {
            foreach (var handler in _handlers.ToArray())
                handler(change);
        }

        public void Dispose()
        {
        }

        private sealed class Handle(FakeLocal owner, Action<LocalChange> handler, LocalChange what) : IDisposable
        {
            public void Dispose()
            {
                owner._handlers.Remove(handler);
                owner.Watching &= ~what;
            }
        }
    }

    internal sealed class Sink : IReadingSink
    {
        private readonly List<ReadingUpdate[]> _batches = [];
        private readonly List<string> _logged = [];

        public List<ReadingUpdate[]> Batches => _batches;

        public IReadOnlyList<string> Logged => _logged;

        public Dictionary<string, ReadingUpdate> Latest
        {
            get
            {
                var latest = new Dictionary<string, ReadingUpdate>(StringComparer.Ordinal);
                foreach (var update in _batches.SelectMany(b => b))
                    latest[update.Path] = update;
                return latest;
            }
        }

        public string Text(string path) => Latest[path].Value!.Value.Text;

        public bool Bool(string path) => Latest[path].Value!.Value.Number != 0;

        public double Number(string path) => Latest[path].Value!.Value.Number;

        public ReadingList List(string path) => Latest[path].Value!.Value.List;

        public void Publish(string path, ReadingValue value) => _batches.Add([ReadingUpdate.Of(path, value)]);

        public void Unavailable(string path, string reason) => _batches.Add([ReadingUpdate.Missing(path, reason)]);

        public void PublishBatch(IReadOnlyList<ReadingUpdate> updates) => _batches.Add([.. updates]);

        public void Log(string message) => _logged.Add(message);

        public List<string> Interruptions { get; } = [];

        public string CacheFolder { get; set; } = "";

        public void Interrupted(string reason) => Interruptions.Add(reason);

        public List<(string Setting, string Value)> StoredSecrets { get; } = [];

        public bool StoreSecret(string setting, SecretValue value)
        {
            StoredSecrets.Add((setting, value.Reveal()));
            return true;
        }
    }
}
