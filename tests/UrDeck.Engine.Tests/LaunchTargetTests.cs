// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Launch;
using Xunit;

namespace UrDeck.Engine.Tests;

public class LaunchTargetTests
{
    [Fact]
    public void AWebAddress_IsWeb_NamedByItsHost()
    {
        var target = LaunchTarget.Parse("https://homeassistant.example.com");

        Assert.Equal(LaunchTargetKind.Web, target.Kind);
        Assert.Equal("https://homeassistant.example.com", target.Text);
        Assert.Equal("homeassistant.example.com", target.DisplayName);
        Assert.True(target.IsValid);
    }

    [Theory]
    [InlineData("http://www.example.com/path?x=1", "example.com")]
    [InlineData("HTTPS://WWW.Example.com", "example.com")]
    [InlineData("http://192.168.1.20:8123/lovelace", "192.168.1.20")]
    public void TheDisplayNameOfAWebAddress_IsItsHostWithoutWww(string text, string expected)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.Web, target.Kind);
        Assert.Equal(expected, target.DisplayName);
    }

    [Fact]
    public void APackagedApplication_IsAShellItem()
    {
        var target = LaunchTarget.Parse(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App");

        Assert.Equal(LaunchTargetKind.Shell, target.Kind);
        Assert.Equal(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", target.Text);
        Assert.Equal(target.Text, target.DisplayName);
    }

    [Fact]
    public void ADriveLetter_IsNotAScheme()
    {
        var target = LaunchTarget.Parse(@"C:\Program Files (x86)\Steam\steam.exe");

        Assert.Equal(LaunchTargetKind.File, target.Kind);
        Assert.Equal("steam.exe", target.DisplayName);
    }

    [Theory]
    [InlineData(@"C:\Tools")]
    [InlineData(@"C:\Tools\")]
    [InlineData("D:/Tools")]
    [InlineData(@"\\server\share\Tools")]
    public void ARootedPath_IsAFileTarget_NamedByItsLastPart(string text)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.File, target.Kind);
        Assert.Equal("Tools", target.DisplayName);
    }

    [Fact]
    public void AnEnvironmentVariable_IsExpanded()
    {
        Environment.SetEnvironmentVariable("URDECK_TEST_HOME", @"C:\Users\someone");
        try
        {
            var target = LaunchTarget.Parse(@"%URDECK_TEST_HOME%\Documents");

            Assert.Equal(LaunchTargetKind.File, target.Kind);
            Assert.Equal(@"C:\Users\someone\Documents", target.Text);
            Assert.Equal("Documents", target.DisplayName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("URDECK_TEST_HOME", null);
        }
    }

    [Fact]
    public void AnUnknownEnvironmentVariable_LeavesARelativePath_WhichIsInvalid() =>
        Assert.Equal(LaunchTargetKind.Invalid, LaunchTarget.Parse(@"%URDECK_NO_SUCH_VARIABLE%\Documents").Kind);

    [Theory]
    [InlineData(@"tools\run.exe")]
    [InlineData("tools/run.exe")]
    [InlineData(@".\run.exe")]
    [InlineData(@"..\run.exe")]
    [InlineData(@"\run.exe")]
    [InlineData("C:run.exe")]
    public void ARelativePath_IsInvalid(string text)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.Invalid, target.Kind);
        Assert.False(target.IsValid);
        Assert.Equal(text, target.DisplayName);
    }

    [Theory]
    [InlineData("steam://open/main")]
    [InlineData("ms-settings:")]
    [InlineData("ms-settings:display")]
    [InlineData("mailto:someone@example.com")]
    public void AnyOtherScheme_IsAnAddressForItsRegisteredApplication(string text)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.Uri, target.Kind);
        Assert.Equal(text, target.Text);
        Assert.Equal(text, target.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AnEmptyOrWhitespaceTarget_IsInvalid_WithNoName(string? text)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.Invalid, target.Kind);
        Assert.Equal("", target.Text);
        Assert.Equal("", target.DisplayName);
    }

    [Fact]
    public void SurroundingSpace_IsRemoved() =>
        Assert.Equal(new LaunchTarget(LaunchTargetKind.File, "notepad", "notepad"), LaunchTarget.Parse("  notepad "));

    [Theory]
    [InlineData("notepad")]
    [InlineData("notepad.exe")]
    [InlineData("wt")]
    public void ABareName_IsAFileTarget_WindowsResolves(string text)
    {
        var target = LaunchTarget.Parse(text);

        Assert.Equal(LaunchTargetKind.File, target.Kind);
        Assert.Equal(text, target.Text);
        Assert.Equal(text, target.DisplayName);
    }

    [Theory]
    [InlineData("https://")]
    [InlineData("https:example.com")]
    [InlineData("https:/example.com")]
    [InlineData("http://exa mple.com")]
    [InlineData("https:// example.com")]
    public void AMalformedWebAddress_IsInvalid(string text) =>
        Assert.Equal(LaunchTargetKind.Invalid, LaunchTarget.Parse(text).Kind);

    [Fact]
    public void AOneLetterScheme_IsAPath_NotAnAddress()
    {
        // "x:" alone could be read as a scheme; one letter never is, so this is a (drive-relative, invalid) path.
        Assert.Equal(LaunchTargetKind.Invalid, LaunchTarget.Parse("x:thing").Kind);
        Assert.Equal(LaunchTargetKind.File, LaunchTarget.Parse(@"x:\thing").Kind);
        Assert.Equal(LaunchTargetKind.Uri, LaunchTarget.Parse("xy:thing").Kind);
    }
}
