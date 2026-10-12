// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Startup;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class AutostartStateTests
{
    private const string Running = @"C:\Users\me\AppData\Local\Programs\UrDeck\UrDeck.Host.exe";

    [Fact]
    public void NoValueIsOff() =>
        Assert.Equal(AutostartState.Off, AutostartStates.From(null, null, Running));

    [Fact]
    public void ThisCopyIsOn() =>
        Assert.Equal(AutostartState.On, AutostartStates.From($"\"{Running}\" --autostart", null, Running));

    [Fact]
    public void ThisCopyInAnotherCaseIsOn() =>
        Assert.Equal(AutostartState.On, AutostartStates.From($"\"{Running.ToUpperInvariant()}\" --autostart", null, Running));

    [Fact]
    public void AnotherFolderIsAnotherCopy() =>
        Assert.Equal(AutostartState.AnotherCopy, AutostartStates.From(@"""D:\old\UrDeck.Host.exe"" --autostart", null, Running));

    [Fact]
    public void DataWithoutQuotesIsRead() =>
        Assert.Equal(AutostartState.On, AutostartStates.From(Running, null, Running));

    [Fact]
    public void DataWithoutQuotesAndWithArgumentsIsRead() =>
        Assert.Equal(AutostartState.On, AutostartStates.From(Running + " --autostart", null, Running));

    [Fact]
    public void DataWithArgumentsIgnoresThem() =>
        Assert.Equal(AutostartState.On, AutostartStates.From($"\"{Running}\" --other --flags x", null, Running));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void UnreadableDataIsOff(string data) =>
        Assert.Equal(AutostartState.Off, AutostartStates.From(data, null, Running));

    [Fact]
    public void MalformedQuoteStillReadsThePath() =>
        Assert.Equal(AutostartState.On, AutostartStates.From($"\"{Running}", null, Running));

    [Theory]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, AutostartState.On)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, AutostartState.SwitchedOffInWindows)]
    [InlineData(new byte[0], AutostartState.On)]
    [InlineData(null, AutostartState.On)]
    public void ApprovalBytesDecideEnabled(byte[]? approval, AutostartState expected) =>
        Assert.Equal(expected, AutostartStates.From($"\"{Running}\" --autostart", approval, Running));

    [Fact]
    public void ApprovalOfAnotherCopyDoesNotMatterForTheState() =>
        Assert.Equal(AutostartState.AnotherCopy,
            AutostartStates.From(@"""D:\old\UrDeck.Host.exe""", [0x03], Running));

    [Fact]
    public void NoRunValueIsOffWhateverTheApproval() =>
        Assert.Equal(AutostartState.Off, AutostartStates.From(null, [0x03], Running));
}

public sealed class InstanceNamesTests
{
    [Fact]
    public void CaseDoesNotMatter() =>
        Assert.Equal(InstanceNames.Key(@"C:\Apps\UrDeck"), InstanceNames.Key(@"c:\apps\urdeck"));

    [WindowsFact]
    public void TrailingSeparatorDoesNotMatter() =>
        Assert.Equal(InstanceNames.Key(@"C:\Apps\UrDeck"), InstanceNames.Key(@"C:\Apps\UrDeck\"));

    [Fact]
    public void DifferentFoldersDiffer() =>
        Assert.NotEqual(InstanceNames.Key(@"C:\Apps\UrDeck"), InstanceNames.Key(@"C:\Dev\UrDeck"));

    [Fact]
    public void NamesAreSessionLocalAndShareTheKey()
    {
        string key = InstanceNames.Key(@"C:\Apps\UrDeck");
        Assert.Equal(16, key.Length);
        Assert.Equal($@"Local\UrDeck.{key}.instance", InstanceNames.Mutex(@"C:\Apps\UrDeck"));
        Assert.Equal($@"Local\UrDeck.{key}.quit", InstanceNames.QuitEvent(@"C:\Apps\UrDeck"));
    }
}
