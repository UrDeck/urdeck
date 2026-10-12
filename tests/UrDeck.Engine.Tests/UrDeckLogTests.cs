// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.RegularExpressions;
using UrDeck.Engine.Diagnostics;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class UrDeckLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-log-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly long _capBefore = UrDeckLog.MaxBytes;
    private readonly string _log;

    public UrDeckLogTests()
    {
        Directory.CreateDirectory(_dir);
        _log = Path.Combine(_dir, "urdeck.log");
        UrDeckLog.LogPath = _log;
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        UrDeckLog.MaxBytes = _capBefore;
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException) { }
    }

    private string Previous => _log + ".1";

    private static string[] Lines(string path) => File.ReadAllLines(path);

    [Fact]
    public void LinesStartWithTheDateAndTime()
    {
        UrDeckLog.Info("hello");

        string line = Assert.Single(Lines(_log));
        Assert.Matches(new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] INFO hello$"), line);
    }

    [Fact]
    public void PassingTheCapMovesTheContentToThePreviousFileWithoutLosingLines()
    {
        UrDeckLog.MaxBytes = 1000;
        for (int i = 0; i < 20; i++)
            UrDeckLog.Info($"line {i:00} padding padding padding");

        Assert.True(File.Exists(Previous));
        string[] all = [.. Lines(Previous), .. Lines(_log)];
        Assert.Equal(20, all.Length);
        for (int i = 0; i < 20; i++)
            Assert.EndsWith($"line {i:00} padding padding padding", all[i]);
    }

    [Fact]
    public void ASecondRotationReplacesThePreviousFile()
    {
        UrDeckLog.MaxBytes = 300;
        UrDeckLog.Info("first-marker " + new string('x', 400));
        UrDeckLog.Info("second");   // rotates: the first line is now in .1
        Assert.Contains("first-marker", File.ReadAllText(Previous));

        UrDeckLog.Info("padding " + new string('y', 400));
        UrDeckLog.Info("third");    // rotates again: .1 now holds what was in the log

        Assert.DoesNotContain("first-marker", File.ReadAllText(Previous));
        Assert.Contains("second", File.ReadAllText(Previous));
    }

    [Fact]
    public void AFileOverTheCapAtStartupIsRotatedAtTheFirstWrite()
    {
        File.WriteAllText(_log, new string('z', 2000));
        UrDeckLog.MaxBytes = 1000;

        UrDeckLog.Info("after start");

        Assert.Equal(2000, new FileInfo(Previous).Length);
        Assert.Single(Lines(_log));
    }

    [Fact]
    public void ALockedFileDoesNotThrowAndIsRotatedLater()
    {
        UrDeckLog.MaxBytes = 200;
        UrDeckLog.Info("padding " + new string('x', 300));

        // Open for reading with no sharing of delete: the rename fails, the append (which needs write access) too.
        using (new FileStream(_log, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = Record.Exception(() => UrDeckLog.Info("while locked"));
            Assert.Null(ex);
        }

        UrDeckLog.Info("after unlock");

        Assert.True(File.Exists(Previous));
        Assert.Contains("after unlock", File.ReadAllText(_log));
    }

    [Fact]
    public void ARedirectedPathKeepsItsOwnSize()
    {
        string other = Path.Combine(_dir, "other.log");
        UrDeckLog.MaxBytes = 300;
        UrDeckLog.Info(new string('x', 400));
        UrDeckLog.LogPath = other;
        UrDeckLog.Info("small");
        UrDeckLog.Info("small again");

        Assert.False(File.Exists(other + ".1"));
        Assert.Equal(2, Lines(other).Length);
    }
}
