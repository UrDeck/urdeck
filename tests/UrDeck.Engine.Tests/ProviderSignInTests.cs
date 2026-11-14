// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The settings of <see cref="SignInFakeProvider"/>: a refresh token its sign-in sets, and one secret it does not.</summary>
internal sealed class SignInSettings : ProviderSettings
{
    [Setting("Refresh token", Required = true)]
    [Secret(SetBySignIn = true)]
    public SecretValue? RefreshToken { get; set; }

    [Setting("Other secret")]
    [Secret]
    public SecretValue? Other { get; set; }
}

/// <summary>A provider with a sign-in whose behaviour a test scripts.</summary>
internal sealed class SignInFakeProvider : DataProvider<SignInSettings>
{
    private readonly object _gate = new();
    private readonly List<SignInSettings> _starts = [];
    private readonly List<SignInSettings> _signOuts = [];

    public IReadingSink? Sink { get; private set; }

    public Func<SignInSettings, ISignInProgress, CancellationToken, Task<SignInResult>> SignIn { get; set; } =
        (_, _, _) => Task.FromResult(SignInResult.Failed("not scripted"));

    public Func<SignInSettings, CancellationToken, Task> SignOut { get; set; } = (_, _) => Task.CompletedTask;

    public IReadOnlyList<SignInSettings> Starts
    {
        get
        {
            lock (_gate)
                return _starts.ToArray();
        }
    }

    public IReadOnlyList<SignInSettings> SignOuts
    {
        get
        {
            lock (_gate)
                return _signOuts.ToArray();
        }
    }

    public override bool OffersSignIn => true;

    public override string SignInDescription => "Shows your friends";

    public override string SignOutHint => "the app's authorized devices";

    public override IReadOnlyList<ReadingDescriptor> Describe() =>
        [new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a")];

    public override void Start(IReadingSink sink, SignInSettings settings)
    {
        Sink = sink;
        lock (_gate)
            _starts.Add(settings);
    }

    public override void SetDemand(IReadOnlyCollection<string> paths)
    {
    }

    public override Task SampleAsync(CancellationToken cancellationToken)
    {
        Sink?.Publish("a", 1);
        return Task.CompletedTask;
    }

    public override void Shutdown()
    {
    }

    public override Task<SignInResult> SignInAsync(SignInSettings settings, ISignInProgress progress, CancellationToken cancellationToken) =>
        SignIn(settings, progress, cancellationToken);

    public override Task SignOutAsync(SignInSettings settings, CancellationToken cancellationToken)
    {
        lock (_gate)
            _signOuts.Add(settings);
        return SignOut(settings, cancellationToken);
    }

    public ProviderDescriptor Descriptor(string id = "steam") =>
        new(id, "Steam", typeof(SignInFakeProvider), 1000, 250, "tests") { Factory = () => this };
}

public sealed class ProviderSignInTests : IDisposable
{
    private static readonly Dictionary<string, string> NoValues = new();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly InMemorySecretBackend _backend = new();
    private readonly Dictionary<string, string> _environment = new();
    private readonly List<string> _log = [];
    private readonly object _logGate = new();
    private readonly ConfigStore _config;
    private readonly ReadingHub _hub;
    private readonly SecretStore _store;
    private readonly ProviderSignIn _signIn;

    public ProviderSignInTests()
    {
        UrDeckLog.Listener = (_, message) =>
        {
            lock (_logGate)
                _log.Add(message);
        };
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "urdeck-config.json"), "{}");
        _config = new ConfigStore(Path.Combine(_dir, "urdeck-config.json"));
        Provider = new SignInFakeProvider();
        _registry.TryRegister(Provider.Descriptor(), out _);
        _store = new SecretStore(_backend, name => _environment.GetValueOrDefault(name));
        _hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false, secrets: _store);
        _signIn = new ProviderSignIn(_config, _registry, _store, _hub, _time);
    }

    private SignInFakeProvider Provider { get; }

    public void Dispose()
    {
        _hub.Dispose();
        _config.Dispose();
        UrDeckLog.Listener = null;
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string[] Lines()
    {
        lock (_logGate)
            return _log.ToArray();
    }

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private static SignInResult Approved(string account = "example_gamer", string token = "refresh-1") =>
        SignInResult.Success(account, new Dictionary<string, SecretValue> { ["refreshToken"] = new(token) });

    private SignInChallenge Qr(string text = "https://s.team/q/1") =>
        SignInChallenge.Qr(text, "Scan with the app", _time.GetUtcNow().AddMinutes(5));

    [Fact]
    public async Task Approval_StoresTheSecretWithItsAccount_AndRestartsTheProvider()
    {
        Provider.SignIn = (_, _, _) => Task.FromResult(Approved());
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => _hub.Read("steam:a").State == ReadingState.Unavailable));
        Assert.Equal("Connect Steam in Settings", _hub.Read("steam:a").Reason);

        var session = _signIn.Start("steam", NoValues);
        var final = await session.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SignInPhase.Approved, final.Phase);
        Assert.Equal("example_gamer", final.Account);
        var stored = _backend.Read("steam", "refreshToken")!;
        Assert.Equal("refresh-1", stored.Value);
        Assert.Equal("example_gamer", stored.Context);
        Assert.True(Wait(() => Provider.Starts.Count == 1));
        Assert.Equal("refresh-1", Provider.Starts[0].RefreshToken!.Reveal());
        Assert.DoesNotContain("refresh-1", File.ReadAllText(Path.Combine(_dir, "urdeck-config.json")));

        var connection = _signIn.GetConnection("steam");
        Assert.True(connection.Offered);
        Assert.True(connection.Connected);
        Assert.Equal("example_gamer", connection.Account);
        Assert.Equal("Shows your friends", connection.Description);
    }

    [Fact]
    public async Task TheLog_HasOneLinePerStep_AndNeverTheChallengeTheCodeTheSecretOrTheAccount()
    {
        Provider.SignIn = (_, progress, _) =>
        {
            progress.Show(Qr("https://s.team/q/SECRETCHALLENGE"));
            progress.Show(SignInChallenge.CodeAndAddress("WDJB-MJHT", "https://example.org/device", "Type it", _time.GetUtcNow().AddMinutes(5)));
            progress.Status("Scanned");
            return Task.FromResult(Approved("example_gamer", "refresh-SECRET"));
        };

        await _signIn.Start("steam", NoValues).Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        string[] lines = Lines().Where(l => l.StartsWith("Sign-in", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["Sign-in steam: started", "Sign-in steam: approved"], lines);
        Assert.DoesNotContain(Lines(), l => l.Contains("SECRETCHALLENGE") || l.Contains("WDJB") || l.Contains("refresh-SECRET") || l.Contains("example_gamer"));
    }

    [Fact]
    public async Task Declined_StoresNothing_AndShowsTheReason()
    {
        Provider.SignIn = (_, _, _) => Task.FromResult(SignInResult.Failed("You declined on the phone."));

        var final = await _signIn.Start("steam", NoValues).Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SignInPhase.Failed, final.Phase);
        Assert.Equal("You declined on the phone.", final.Message);
        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.False(_signIn.GetConnection("steam").Connected);
    }

    [Fact]
    public async Task ARotatedChallenge_ReplacesTheShownOne_AndStatusIsKept()
    {
        var gate = new TaskCompletionSource<SignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progressSeen = new TaskCompletionSource<ISignInProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignIn = (_, progress, _) =>
        {
            progress.Show(Qr("first"));
            progressSeen.SetResult(progress);
            return gate.Task;
        };

        var session = _signIn.Start("steam", NoValues);
        ISignInProgress progress = await progressSeen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("first", session.State.Challenge!.QrText);

        progress.Status("Scanned; approve on your phone");
        progress.Show(Qr("second"));

        Assert.Equal("second", session.State.Challenge!.QrText);
        Assert.Null(session.State.Status); // a new challenge clears the old status
        progress.Status("Scanned; approve on your phone");
        Assert.Equal("Scanned; approve on your phone", session.State.Status);
        gate.SetResult(Approved());
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AChallengeThatExpired_EndsTheSignInThirtySecondsLater_WhenTheProviderIsSilent()
    {
        var token = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignIn = async (_, progress, ct) =>
        {
            progress.Show(SignInChallenge.Qr("q", "scan", _time.GetUtcNow().AddMinutes(1)));
            token.SetResult(ct);
            await Task.Delay(Timeout.Infinite, ct);
            return Approved();
        };

        var session = _signIn.Start("steam", NoValues);
        var ct = await token.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(89));
        Assert.Equal(SignInPhase.Running, session.State.Phase);

        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(SignInPhase.Failed, session.State.Phase);
        Assert.Equal("The code expired.", session.State.Message);
        Assert.True(ct.IsCancellationRequested);
        Assert.Null(_backend.Read("steam", "refreshToken"));
    }

    [Fact]
    public async Task Cancel_StopsTheSignIn_AndALateApprovalIsDropped()
    {
        var gate = new TaskCompletionSource<SignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignIn = (_, _, _) =>
        {
            started.SetResult();
            return gate.Task; // ignores its token: a hung provider
        };

        var session = _signIn.Start("steam", NoValues);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        session.Cancel();
        Assert.Equal(SignInPhase.Cancelled, session.State.Phase);

        gate.SetResult(Approved());
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(SignInPhase.Cancelled, session.State.Phase);
        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.Contains("Sign-in steam: cancelled", Lines());
    }

    [Fact]
    public async Task ClosingTheWindow_CancelsEverySignIn()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignIn = async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return Approved();
        };

        var session = _signIn.Start("steam", NoValues);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        _signIn.CancelAll();

        Assert.Equal(SignInPhase.Cancelled, session.State.Phase);
        Assert.Null(_backend.Read("steam", "refreshToken"));
    }

    [Fact]
    public async Task StartingASecondSignIn_CancelsTheFirst()
    {
        var gate = new TaskCompletionSource<SignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignIn = (_, _, _) => gate.Task;

        var first = _signIn.Start("steam", NoValues);
        var second = _signIn.Start("steam", NoValues);

        Assert.Equal(SignInPhase.Cancelled, first.State.Phase);
        Assert.Equal(SignInPhase.Running, second.State.Phase);
        _signIn.CancelAll();
    }

    [Fact]
    public async Task AnUnexpectedSecret_IsRefused_AndNothingIsStored()
    {
        Provider.SignIn = (_, _, _) => Task.FromResult(SignInResult.Success(
            "example_gamer",
            new Dictionary<string, SecretValue> { ["refreshToken"] = new("r"), ["other"] = new("sneaky") }));

        var final = await _signIn.Start("steam", NoValues).Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SignInPhase.Failed, final.Phase);
        Assert.Equal("The provider returned an unexpected secret.", final.Message);
        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.Null(_backend.Read("steam", "other"));
        Assert.Contains(Lines(), l => l.Contains("Sign-in steam") && l.Contains("does not declare"));
    }

    [Fact]
    public async Task AProviderThatThrows_FailsTheSignIn()
    {
        Provider.SignIn = (_, _, _) => throw new InvalidOperationException("The service said no.");

        var final = await _signIn.Start("steam", NoValues).Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SignInPhase.Failed, final.Phase);
        Assert.Equal("The service said no.", final.Message);
    }

    [Fact]
    public async Task ARenewedSecret_IsStoredWithoutRestartingTheProvider_AndKeepsTheAccount()
    {
        _backend.Write("steam", "refreshToken", "old", "example_gamer");
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        bool kept = Provider.Sink!.StoreSecret("refreshToken", new SecretValue("rotated"));

        Assert.True(kept);
        var stored = _backend.Read("steam", "refreshToken")!;
        Assert.Equal("rotated", stored.Value);
        Assert.Equal("example_gamer", stored.Context);
        _hub.SecretsChanged();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Single(Provider.Starts);
        Assert.Contains("Secret steam/refreshToken: renewed", Lines());
    }

    [Fact]
    public async Task ARenewalOfASecretThatComesFromTheEnvironment_WritesNothing_AndSaysSoOnce()
    {
        _environment[SecretStore.EnvironmentName("steam", "refreshToken")] = "from-env";
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Assert.False(Provider.Sink!.StoreSecret("refreshToken", new SecretValue("rotated")));
        Assert.False(Provider.Sink!.StoreSecret("refreshToken", new SecretValue("rotated again")));

        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.Single(Lines(), l => l.Contains("kept for this run only"));
    }

    [Theory]
    [InlineData("other")]
    [InlineData("homeassistant/token")]
    [InlineData("nope")]
    public async Task ARenewalOfAnotherSecret_IsRefused_AndNamesTheProvider(string setting)
    {
        _backend.Write("steam", "refreshToken", "old", null);
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Assert.False(Provider.Sink!.StoreSecret(setting, new SecretValue("x")));

        Assert.Null(_backend.Read("steam", setting));
        Assert.Equal("old", _backend.Read("steam", "refreshToken")!.Value);
        Assert.Contains(Lines(), l => l.Contains("Provider 'steam'") && l.Contains("nothing stored"));
    }

    [Fact]
    public async Task ARenewedValueThatIsTooLarge_IsRefused()
    {
        _backend.Write("steam", "refreshToken", "old", null);
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Assert.False(Provider.Sink!.StoreSecret("refreshToken", new SecretValue(new string('x', SecretStore.MaxSecretBytes + 1))));

        Assert.Equal("old", _backend.Read("steam", "refreshToken")!.Value);
    }

    [Fact]
    public async Task Disconnect_TellsTheService_ThenDeletesTheSecrets_AndRestartsTheProvider()
    {
        _backend.Write("steam", "refreshToken", "tok", "example_gamer");
        _hub.Subscribe(["steam:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        var result = await _signIn.DisconnectAsync("steam", TestContext.Current.CancellationToken);

        Assert.True(result.ServiceTold);
        Assert.True(result.Deleted);
        Assert.Equal("", result.Hint);
        Assert.Equal("tok", Provider.SignOuts.Single().RefreshToken!.Reveal());
        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.True(Wait(() => _hub.Read("steam:a").Reason == "Connect Steam in Settings"));
        Assert.Contains("Sign-out steam: service told", Lines());
    }

    [Fact]
    public async Task Disconnect_Offline_StillDeletesTheSecrets_AndGivesTheHint()
    {
        _backend.Write("steam", "refreshToken", "tok", "example_gamer");
        Provider.SignOut = (_, _) => throw new HttpRequestException("no network");

        var result = await _signIn.DisconnectAsync("steam", TestContext.Current.CancellationToken);

        Assert.False(result.ServiceTold);
        Assert.True(result.Deleted);
        Assert.Equal("the app's authorized devices", result.Hint);
        Assert.Null(_backend.Read("steam", "refreshToken"));
        Assert.Contains("Sign-out steam: service not told", Lines());
    }

    [Fact]
    public async Task Disconnect_OfAProviderThatNeverReturns_GivesUpAfterTenSeconds_AndDeletesTheSecrets()
    {
        _backend.Write("steam", "refreshToken", "tok", null);
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Provider.SignOut = (_, _) =>
        {
            asked.SetResult();
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task; // hung
        };

        var disconnect = Task.Run(() => _signIn.DisconnectAsync("steam"));
        await asked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(Wait(() => _time.ActiveTimers > 0));
        _time.Advance(TimeSpan.FromSeconds(11));

        var result = await disconnect.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(result.ServiceTold);
        Assert.Null(_backend.Read("steam", "refreshToken"));
    }

    [Fact]
    public async Task FromTheEnvironment_ThereIsNoAccount_AndTheVariableIsNamed()
    {
        _environment[SecretStore.EnvironmentName("steam", "refreshToken")] = "from-env";

        var connection = _signIn.GetConnection("steam");

        Assert.True(connection.Connected);
        Assert.Null(connection.Account);
        Assert.Equal("URDECK_SECRET_STEAM_REFRESHTOKEN", connection.EnvironmentName);
    }

    [Fact]
    public async Task TheForm_OffersSignIn_AndAProviderWithoutOneDoesNot()
    {
        var registry = new ProviderRegistry();
        registry.TryRegister(Provider.Descriptor(), out _);
        var typed = new TypedFakeProvider();
        registry.TryRegister(typed.Descriptor(), out _);
        var editor = new ProviderSettingsEditor(_config, registry, _store, _hub);

        var forms = editor.GetForms().ToDictionary(f => f.ProviderId);

        Assert.True(forms["steam"].OffersSignIn);
        Assert.True(forms["steam"].Fields.Single(f => f.Setting.Key == "refreshToken").Setting.SetBySignIn);
        Assert.False(forms["typed"].OffersSignIn);
    }

    [Fact]
    public async Task ASampleThatReplacesAProvider_OffersNoSignIn()
    {
        var descriptor = Provider.Descriptor() with { SettingsTypeOverride = typeof(ProviderSettings), Factory = () => new FakeProvider(_time) };

        Assert.False(descriptor.OffersSignIn);
    }

    [Fact]
    public async Task ARequiredSecretSetBySignIn_ReportsConnectInSettings_NotASettingToFill()
    {
        _hub.Subscribe(["steam:a"]);

        Assert.True(Wait(() => _hub.Read("steam:a").State == ReadingState.Unavailable));
        Assert.Equal("Connect Steam in Settings", _hub.Read("steam:a").Reason);
    }
}
