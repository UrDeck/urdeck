// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Providers.Steam;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>Valve's text KeyValues reader (steam-provider spec, "Steam Files And Faults"): strict, never guessing.</summary>
public sealed class KeyValuesTests
{
    private const string Manifest = """
        "AppState"
        {
        	"appid"		"1001"
        	"name"		"Sample Quest"
        	"StateFlags"		"4"
        	"installdir"		"C:\\Games\\Sample \"Quest\""
        	// a comment
        	"UserConfig"
        	{
        		"language"		"english"
        	}
        }
        """;

    [Fact]
    public void Reads_nested_blocks_texts_escapes_and_comments()
    {
        var kv = KeyValues.Parse(Manifest);

        Assert.Equal("1001", kv.GetText("AppState/appid"));
        Assert.Equal("Sample Quest", kv.GetText("AppState/name"));
        Assert.Equal(4, kv.GetLong("AppState/StateFlags"));
        Assert.Equal("C:\\Games\\Sample \"Quest\"", kv.GetText("AppState/installdir"));
        Assert.Equal("english", kv.GetText("AppState/UserConfig/language"));
        Assert.True(kv.Find("AppState/UserConfig")!.IsBlock);
        Assert.Null(kv.GetText("AppState/UserConfig"));
    }

    [Fact]
    public void Keys_ignore_case_and_the_last_duplicate_wins()
    {
        var kv = KeyValues.Parse("\"A\" { \"Key\" \"1\" \"key\" \"2\" }");

        Assert.Equal("2", kv.GetText("a/KEY"));
    }

    [Fact]
    public void Missing_or_non_numeric_values_give_the_fallback()
    {
        var kv = KeyValues.Parse("\"A\" { \"n\" \"x\" }");

        Assert.Equal(7, kv.GetLong("A/n", 7));
        Assert.Equal(-1, kv.GetLong("A/none", -1));
    }

    [Fact]
    public void An_empty_document_is_an_empty_block()
    {
        Assert.Empty(KeyValues.Parse("  \n// nothing\n").Children);
    }

    [Theory]
    [InlineData("\"A\" {")]
    [InlineData("\"A\" { \"k\" \"v\"")]
    [InlineData("\"A\" { \"k\" \"v")]
    [InlineData("\"A\" { \"k\" }")]
    [InlineData("\"A\" \"x\" }")]
    [InlineData("A { }")]
    [InlineData("\"A\"")]
    [InlineData("{ }")]
    public void Rejects_what_it_does_not_understand(string text)
    {
        Assert.Throws<FormatException>(() => KeyValues.Parse(text));
        Assert.False(KeyValues.TryParse(text, out _));
    }

    [Fact]
    public void Every_truncation_of_a_manifest_is_rejected_or_a_prefix_of_complete_entries()
    {
        // Steam writes these files while they are read: a cut-off file must not read as a smaller valid one with a wrong value.
        for (int length = 0; length < Manifest.Length; length++)
        {
            string cut = Manifest[..length];
            if (!KeyValues.TryParse(cut, out var kv))
                continue;

            // Only a cut before the block opens (nothing, or the key alone is rejected above) can parse: it holds nothing.
            Assert.Null(kv.Find("AppState/name"));
        }
    }

    [Fact]
    public void Garbled_bytes_throw_a_format_exception_and_nothing_else()
    {
        var random = new Random(1234);
        for (int i = 0; i < 300; i++)
        {
            char[] chars = new char[random.Next(1, 80)];
            for (int j = 0; j < chars.Length; j++)
                chars[j] = "\"{}/\\ \n\tab1"[random.Next(11)];
            _ = KeyValues.TryParse(new string(chars), out _); // must not throw anything but be false
        }
    }

    [Fact]
    public void Nesting_beyond_the_limit_is_rejected()
    {
        string deep = string.Concat(Enumerable.Repeat("\"a\" {", 100)) + string.Concat(Enumerable.Repeat("}", 100));

        Assert.False(KeyValues.TryParse(deep, out _));
    }
}
