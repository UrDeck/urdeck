// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary>
/// The output of <c>shader validate</c> in every format, recorded byte for byte before the report model was shared with
/// <c>theme validate</c>. Rewrite the files with <c>URDECK_UPDATE_SNAPSHOTS=1</c> only for a change that is meant to alter
/// the output of <c>shader</c>.
/// </summary>
public sealed class ShaderSnapshotTests : IDisposable
{
    private const string Layered = "layout(color) uniform half4 tint;\nhalf4 main(float2 p) { return half4(tint.rgb, 1); }";

    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    public static TheoryData<string> Formats => new() { "text", "markdown", "json", "sarif" };

    private static string SnapshotFolder()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "urdeck.slnx")))
            directory = Path.GetDirectoryName(directory);
        return Path.Combine(directory ?? throw new InvalidOperationException("urdeck.slnx was not found above the tests."),
            "tests", "UrDeck.Cli.Tests", "Snapshots");
    }

    private static void Check(string name, string actual)
    {
        string path = Path.Combine(SnapshotFolder(), name);
        if (Environment.GetEnvironmentVariable("URDECK_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"the snapshot {name} is missing; record it with URDECK_UPDATE_SNAPSHOTS=1.");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    /// <summary>The tool's version is the only thing in the output that changes between builds.</summary>
    private static string Stable(string output) => output.Replace(ToolInfo.Version, "<version>", StringComparison.Ordinal);

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string directory in Directory.EnumerateDirectories(from))
            CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void The_built_in_backgrounds(string format)
    {
        CopyDirectory(Workspace.BuiltIns(), _work.At("builtin"));

        var run = Workspace.Invoke("shader", "validate", "--all", "builtin", "--format", format);

        Assert.Equal(0, run.Code);
        Check($"shader-builtin.{format}", Stable(run.Out));
    }

    /// <summary>One error (no sample file), one warning (no description) and one remark (a colour the manifest calls a number).</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void A_folder_with_a_finding_of_each_severity(string format)
    {
        string folder = _work.Background("mixed", Layered, description: false, sample: false);
        string manifest = Path.Combine(folder, "manifest.json");
        var edited = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(manifest))!;
        edited["parameters"] = JsonSerializer.SerializeToElement(new
        {
            tint = new { kind = "number", label = "tint", description = "A colour the manifest calls a number." },
        });
        File.WriteAllText(manifest, JsonSerializer.Serialize(edited));

        var run = Workspace.Invoke("shader", "validate", "mixed", "--format", format);

        Assert.Equal(1, run.Code);
        if (format == "text")
        {
            Assert.Contains(": error ", run.Out);
            Assert.Contains(": warning ", run.Out);
            Assert.Contains(": info ", run.Out);
        }

        Check($"shader-mixed.{format}", Stable(run.Out));
    }
}
