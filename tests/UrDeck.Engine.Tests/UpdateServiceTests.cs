// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Updates;
using Xunit;

namespace UrDeck.Engine.Tests;

public class UpdateServiceTests : IDisposable
{
    private readonly FakeBackend _backend = new();
    private readonly List<string> _log = new();
    private readonly UpdateService _service;
    private bool _enabled = true;

    public UpdateServiceTests()
    {
        UrDeckLog.Listener = (level, message) => _log.Add($"{level} {message}");
        _service = new UpdateService(_backend, () => _enabled);
    }

    public void Dispose()
    {
        _service.Dispose();
        UrDeckLog.Listener = null;
    }

    [Fact]
    public async Task NothingNew_StaysNone()
    {
        await _service.CheckNowAsync();

        Assert.Equal(1, _backend.Checks);
        Assert.Equal(0, _backend.Downloads);
        Assert.Equal(UpdateState.None, _service.State);
        Assert.Null(_service.ReadyVersion);
    }

    [Fact]
    public async Task NewerVersion_IsDownloadedAndBecomesReady()
    {
        _backend.Available = "0.2.0";

        await _service.CheckNowAsync();

        Assert.Equal(1, _backend.Downloads);
        Assert.Equal(UpdateState.Ready, _service.State);
        Assert.Equal("0.2.0", _service.ReadyVersion);
        Assert.Contains(_log, l => l.Contains("0.2.0 is available", StringComparison.Ordinal));
        Assert.Contains(_log, l => l.Contains("0.2.0 downloaded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Off_MakesNoRequest()
    {
        _enabled = false;
        _backend.Available = "0.2.0";

        await _service.CheckNowAsync();

        Assert.Equal(0, _backend.Checks);
        Assert.Equal(UpdateState.None, _service.State);
    }

    [Fact]
    public async Task AFailure_IsAWarning_AndTheNextTickTriesAgain()
    {
        _backend.Fail = true;
        await _service.CheckNowAsync();

        Assert.Equal(UpdateState.None, _service.State);
        Assert.Contains(_log, l => l.StartsWith("WARN ", StringComparison.Ordinal));

        _backend.Fail = false;
        _backend.Available = "0.2.0";
        await _service.CheckNowAsync();

        Assert.Equal(2, _backend.Checks);
        Assert.Equal(UpdateState.Ready, _service.State);
    }

    [Fact]
    public async Task AReadyUpdate_IsReportedOnce()
    {
        var reported = new List<string>();
        _service.UpdateReady += reported.Add;
        _backend.Available = "0.2.0";

        await _service.CheckNowAsync();
        await _service.CheckNowAsync();
        await _service.CheckNowAsync();

        Assert.Equal(new[] { "0.2.0" }, reported);
        Assert.Equal(1, _backend.Checks);
        Assert.Equal(1, _backend.Downloads);
    }

    [Fact]
    public async Task Apply_NeedsAReadyUpdate_AndUpdatesOn()
    {
        _service.ApplyAndRestart();
        _service.ApplyOnQuit();
        Assert.Equal(0, _backend.Restarts + _backend.AfterExits);

        _backend.Available = "0.2.0";
        await _service.CheckNowAsync();

        _service.ApplyOnQuit();
        Assert.Equal(1, _backend.AfterExits);

        _enabled = false;
        _service.ApplyOnQuit();
        _service.ApplyAndRestart();
        Assert.Equal(1, _backend.AfterExits);
        Assert.Equal(0, _backend.Restarts);

        _enabled = true;
        _service.ApplyAndRestart();
        Assert.Equal(1, _backend.Restarts);
    }

    [Fact]
    public void TheScheduleIsOneMinuteThenADay()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), UpdateService.FirstCheckDelay);
        Assert.Equal(TimeSpan.FromHours(24), UpdateService.CheckInterval);
    }

    private sealed class FakeBackend : IUpdateBackend
    {
        public string? Available { get; set; }

        public bool Fail { get; set; }

        public int Checks { get; private set; }

        public int Downloads { get; private set; }

        public int Restarts { get; private set; }

        public int AfterExits { get; private set; }

        public Task<string?> CheckAsync(CancellationToken cancellationToken)
        {
            Checks++;
            if (Fail)
                throw new HttpRequestException("offline");
            return Task.FromResult(Available);
        }

        public Task DownloadAsync(CancellationToken cancellationToken)
        {
            Downloads++;
            return Task.CompletedTask;
        }

        public void ApplyAndRestart() => Restarts++;

        public void ApplyAfterExit() => AfterExits++;
    }
}
