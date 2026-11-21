// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Steam;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The connected half of the Steam provider against the real Steam, with a Web API key from the environment.
/// Opt-in only (see <see cref="SteamFactAttribute"/>); it only reads.
/// </summary>
public sealed class SteamEndToEndTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "urdeck-steam-e2e-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_cache))
            Directory.Delete(_cache, recursive: true);
    }

    [SteamFact]
    public async Task A_sample_with_a_real_key_gives_my_state_and_the_friends()
    {
        var sink = new SteamLocalProviderTests.Sink { CacheFolder = _cache };
        using var provider = new SteamProvider();
        provider.Start(sink, new SteamSettings { ApiKey = new SecretValue(Environment.GetEnvironmentVariable(SteamFactAttribute.KeyVariable)!), SteamId = Environment.GetEnvironmentVariable(SteamFactAttribute.SteamIdVariable) });
        provider.SetDemand(["me/state", "friends/online", "friends/playing"]);

        await provider.SampleAsync(CancellationToken.None);

        Assert.Empty(sink.Interruptions);
        Assert.NotNull(sink.Latest["me/state"].Value);
        var online = sink.List("friends/online");
        var playing = sink.List("friends/playing");
        Assert.True(playing.Count <= online.Count);
        foreach (var row in online.Rows)
        {
            Assert.NotEqual("", row.Get("name")!.Value.Text);
            Assert.StartsWith("steam://url/SteamIDPage/", row.Get("link")!.Value.Text, StringComparison.Ordinal);
        }

        if (online.Count > 0)
            Assert.Contains(online.Rows, r => r.Get("avatar")!.Value.Text.Length > 0);

        // A second sample works as well.
        await provider.SampleAsync(CancellationToken.None);
        Assert.Empty(sink.Interruptions);
    }
}
