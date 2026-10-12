// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class ManifestWriterTests
{
    private static string RoundTrip(string text) => ManifestWriter.Serialize(ManifestWriter.Parse(text)!);

    public static TheoryData<string> BuiltInManifests()
    {
        var data = new TheoryData<string>();
        foreach (string folder in Directory.GetDirectories(Workspace.BuiltIns()).Order(StringComparer.Ordinal))
            data.Add(Path.GetFileName(folder));
        return data;
    }

    [Theory]
    [MemberData(nameof(BuiltInManifests))]
    public void A_built_in_manifest_is_written_back_byte_for_byte(string name)
    {
        string text = File.ReadAllText(Path.Combine(Workspace.BuiltIns(), name, "manifest.json")).ReplaceLineEndings("\n");

        Assert.Equal(text, RoundTrip(text));
        Assert.Empty(ManifestWriter.Inspect(text).Losses);
    }

    [Fact]
    public void Unknown_keys_and_the_order_of_keys_and_items_survive()
    {
        const string text = "{\n  \"zeta\": [\n    3,\n    1,\n    2\n  ],\n  \"manifest\": 1,\n  \"alpha\": {\n    \"b\": true,\n    \"a\": null\n  }\n}\n";

        Assert.Equal(text, RoundTrip(text));
    }

    [Fact]
    public void Numbers_are_written_as_they_were_read()
    {
        const string text = "{\n  \"a\": 1.0,\n  \"b\": 1e3,\n  \"c\": 0.1234567890123456789,\n  \"d\": -0,\n  \"e\": 10\n}\n";

        Assert.Equal(text, RoundTrip(text));
    }

    [Fact]
    public void Text_stays_readable_and_escapes_survive_as_json()
    {
        const string text = """
            {
              "unit": "°",
              "name": "é <b> & \"q\" \\ \n"
            }

            """;

        Assert.Equal(text, RoundTrip(text));
    }

    [Fact]
    public void The_output_has_lf_two_spaces_and_a_final_newline()
    {
        string text = RoundTrip("{\r\n\t\"a\": { \"b\": [1, 2] }\r\n}");

        Assert.Equal("{\n  \"a\": {\n    \"b\": [\n      1,\n      2\n    ]\n  }\n}\n", text);
    }

    [Fact]
    public void A_comment_a_trailing_comma_and_a_duplicate_key_are_each_named()
    {
        Assert.Equal(["a comment"], ManifestWriter.Inspect("{ // note\n \"manifest\": 1 }").Losses);
        Assert.Equal(["a comment"], ManifestWriter.Inspect("{ \"manifest\": 1 /* x */ }").Losses);
        Assert.Equal(["a trailing comma"], ManifestWriter.Inspect("{ \"manifest\": 1, }").Losses);
        Assert.Equal(["a trailing comma"], ManifestWriter.Inspect("{ \"manifest\": 1, \"tags\": [1, 2,] }").Losses);
        Assert.Equal(["the key 'title' twice in one object"], ManifestWriter.Inspect("{ \"title\": \"a\", \"title\": \"b\" }").Losses);
    }

    [Fact]
    public void Several_losses_are_all_named()
    {
        var inspection = ManifestWriter.Inspect("{ // c\n \"a\": 1, \"a\": 2, }");

        Assert.True(inspection.IsObject);
        Assert.Equal(["a comment", "a trailing comma", "the key 'a' twice in one object"], inspection.Losses);
    }

    [Fact]
    public void The_same_key_in_different_objects_is_not_a_duplicate()
    {
        var inspection = ManifestWriter.Inspect("{ \"a\": { \"label\": 1 }, \"b\": { \"label\": 2 }, \"c\": [ { \"x\": 1 }, { \"x\": 2 } ] }");

        Assert.True(inspection.IsObject);
        Assert.Empty(inspection.Losses);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("")]
    public void Text_that_is_not_an_object_is_left_to_the_reader(string text)
    {
        var inspection = ManifestWriter.Inspect(text);

        Assert.False(inspection.IsObject);
        Assert.Empty(inspection.Losses);
    }

    [Fact]
    public void A_write_leaves_no_temporary_file_and_replaces_the_old_one()
    {
        using var work = new Workspace();
        string path = work.At("manifest.json");
        File.WriteAllText(path, "old");

        ManifestWriter.Write(path, new JsonObject { ["manifest"] = 1 });

        Assert.Equal("{\n  \"manifest\": 1\n}\n", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(work.Root));
    }
}
