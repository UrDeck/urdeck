// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.ComponentModel;
using System.Diagnostics;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Launch;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class LauncherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-launcher-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly FakeStarter _starter = new();
    private readonly FakeTime _time = new();
    private readonly Launcher _launcher;

    public LauncherTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
        _launcher = new Launcher(_starter, _time);
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    /// <summary>Records what would be started; refuses when told to, the way Windows does for a missing file.</summary>
    private sealed class FakeStarter : IProcessStarter
    {
        public List<ProcessStartInfo> Started { get; } = [];

        public Exception? Refusal { get; set; }

        public void Start(ProcessStartInfo info)
        {
            if (Refusal != null)
                throw Refusal;
            Started.Add(info);
        }
    }

    [Fact]
    public void AnExecutable_IsHandedToTheShell_WithItsFolderAsWorkingDirectory()
    {
        bool started = _launcher.Launch(@"C:\Program Files (x86)\Steam\steam.exe", "-silent");

        Assert.True(started);
        var info = Assert.Single(_starter.Started);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steam.exe", info.FileName);
        Assert.Equal("-silent", info.Arguments);
        Assert.Equal(@"C:\Program Files (x86)\Steam", info.WorkingDirectory);
        Assert.True(info.UseShellExecute);
        // The default action, never "runas": nothing is started elevated.
        Assert.Equal("", info.Verb);
    }

    [Theory]
    [InlineData(@"C:\Users\someone\Documents")]
    [InlineData(@"C:\Links\Game.lnk")]
    [InlineData("notepad")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App")]
    [InlineData("https://example.com/page")]
    [InlineData("steam://open/main")]
    public void OtherTargets_AreHandedOverAsTheyAre_WithNoWorkingDirectory(string target)
    {
        Assert.True(_launcher.Launch(target));

        var info = Assert.Single(_starter.Started);
        Assert.Equal(target, info.FileName);
        Assert.Equal("", info.Arguments);
        Assert.Equal("", info.WorkingDirectory);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void AnEnvironmentVariable_IsExpandedBeforeTheShellSeesIt()
    {
        Assert.True(_launcher.Launch(@"%WINDIR%\explorer.exe"));

        string started = Assert.Single(_starter.Started).FileName;
        Assert.DoesNotContain("%", started);
        Assert.EndsWith(@"\explorer.exe", started);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"tools\run.exe")]
    [InlineData("https://")]
    public void AnInvalidTarget_StartsNothing_ReportsFailure_AndIsLogged(string target)
    {
        Assert.False(_launcher.Launch(target));

        Assert.Empty(_starter.Started);
        Assert.Contains("Launch refused", Log);
    }

    [Fact]
    public void ATargetWindowsRefuses_ReportsFailure_AndTheLogNamesTheTargetAndTheReason()
    {
        _starter.Refusal = new Win32Exception(2, "The system cannot find the file specified.");

        bool started = _launcher.Launch(@"C:\Nowhere\missing.exe");

        Assert.False(started);
        Assert.Contains(@"C:\Nowhere\missing.exe", Log);
        Assert.Contains("cannot find the file", Log);
    }

    [Fact]
    public void ARefusedLaunch_DoesNotArmTheRepeatGuard()
    {
        _starter.Refusal = new Win32Exception(2, "missing");
        Assert.False(_launcher.Launch("notepad"));

        _starter.Refusal = null;
        Assert.True(_launcher.Launch("notepad"));

        Assert.Single(_starter.Started);
    }

    [Fact]
    public void ADoubleTap_StartsOnce_AndBothRequestsReportSuccess()
    {
        Assert.True(_launcher.Launch("notepad", "a.txt"));
        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(_launcher.Launch("notepad", "a.txt"));

        Assert.Single(_starter.Started);
    }

    [Fact]
    public void AfterASecond_TheSameTargetStartsAgain()
    {
        _launcher.Launch("notepad");
        _time.Advance(Launcher.RepeatGuard);
        _launcher.Launch("notepad");

        Assert.Equal(2, _starter.Started.Count);
    }

    [Fact]
    public void ADifferentTargetOrDifferentArguments_StartAtOnce()
    {
        _launcher.Launch("notepad", "a.txt");
        _launcher.Launch("notepad", "b.txt");
        _launcher.Launch("calc");

        Assert.Equal(3, _starter.Started.Count);
    }

    [Fact]
    public void ArgumentsGivenWithAnAddress_AreIgnoredWithAWarning()
    {
        Assert.True(_launcher.Launch("https://example.com", "--incognito"));

        Assert.Equal("", Assert.Single(_starter.Started).Arguments);
        Assert.Contains("arguments apply to file targets only", Log);
    }

    [Fact]
    public void TheLog_ShowsTheTarget_AndNeverTheArguments()
    {
        _launcher.Launch("notepad", "secret-token.txt");

        Assert.Contains("Launched 'notepad'", Log);
        Assert.DoesNotContain("secret-token", Log);
    }

    [Fact]
    public void TheBeforeLaunchCallback_RunsJustBeforeEachStartedLaunch_AndIsEmptyByDefault()
    {
        Assert.Null(_launcher.BeforeLaunch);
        var order = new List<string>();
        _launcher.BeforeLaunch = () => order.Add($"before:{_starter.Started.Count}");

        _launcher.Launch("notepad");
        _launcher.Launch("notepad"); // swallowed by the repeat guard
        _launcher.Launch(@"tools\run.exe"); // invalid

        Assert.Equal(["before:0"], order);
    }

    [Fact]
    public void ACallbackThatThrows_FailsTheLaunch_AndNothingReachesTheWidget()
    {
        _launcher.BeforeLaunch = () => throw new InvalidOperationException("no foreground");

        Assert.False(_launcher.Launch("notepad"));

        Assert.Empty(_starter.Started);
        Assert.Contains("no foreground", Log);
    }
}
