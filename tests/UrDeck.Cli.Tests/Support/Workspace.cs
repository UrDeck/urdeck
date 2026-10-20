// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;

namespace UrDeck.Cli.Tests.Support;

/// <summary>What one run of the tool printed and returned.</summary>
internal sealed record Run(int Code, string Out, string Error);

/// <summary>
/// A scratch directory that is the working directory while the test runs (the tool prints paths relative to it). The tests
/// of this project do not run in parallel because of that.
/// </summary>
internal sealed class Workspace : IDisposable
{
    public const string Red = "half4 main(float2 p) { return half4(1, 0, 0, 1); }";
    public const string Blank = "half4 main(float2 p) { return half4(0); }";
    public const string Level = "uniform float level;   // default: 0\nhalf4 main(float2 p) { return half4(level, 0, 0, 1); }";
    public const string Palette = "uniform float4 iColor0;\nhalf4 main(float2 p) { return half4(iColor0.rgb, 1); }";

    private readonly string _before = Directory.GetCurrentDirectory();

    public Workspace()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "urdeck-cli-tests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
        Directory.SetCurrentDirectory(Root);
    }

    public string Root { get; }

    public string At(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Runs the tool in this process, with the arguments as the shell would split them.</summary>
    public static Run Invoke(params string[] args)
    {
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        int code = CliApp.Run(args, output, error);
        return new Run(code, output.ToString(), error.ToString());
    }

    /// <summary>
    /// A background folder that lints clean (a title, a description, every parameter described, a sample file), under
    /// <paramref name="parent"/> (the workspace itself by default).
    /// </summary>
    public string Background(string name, string shader = Red, string parent = "", bool description = true, bool sample = true)
    {
        string folder = Path.Combine(Root, parent, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".sksl"), shader);

        var parameters = new Dictionary<string, object>();
        foreach (string uniform in System.Text.RegularExpressions.Regex.Matches(shader, @"uniform\s+\w+\s+(\w+)\s*;").Select(m => m.Groups[1].Value))
        {
            if (!(uniform.StartsWith('i') && uniform.Length > 1 && char.IsUpper(uniform[1])))
                parameters[uniform] = new { label = uniform, description = "A parameter of the test shader." };
        }

        var manifest = new Dictionary<string, object?>
        {
            ["manifest"] = 1,
            ["title"] = name,
            ["parameters"] = parameters,
            ["samples"] = new[] { new { type = "image", file = "sample.png" } },
        };
        if (description)
            manifest["description"] = "A background of the tests.";
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest));

        if (sample)
        {
            using var picture = new SKBitmap(1, 1);
            File.WriteAllBytes(Path.Combine(folder, "sample.png"), picture.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        }

        return folder;
    }

    /// <summary>
    /// A copy of a theme folder of the engine's lint fixtures (<c>tests/UrDeck.Engine.Tests/Fixtures/lint/&lt;case&gt;/&lt;side&gt;</c>) in this
    /// workspace, named <paramref name="name"/> (the theme's name is its folder's), under <paramref name="parent"/>.
    /// </summary>
    public string Theme(string fixture, string name, string side = "fail", string parent = "")
    {
        string? directory = AppContext.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "urdeck.slnx")))
            directory = Path.GetDirectoryName(directory);
        string from = Path.Combine(directory ?? throw new InvalidOperationException("urdeck.slnx was not found above the tests."),
            "tests", "UrDeck.Engine.Tests", "Fixtures", "lint", fixture, side);
        string to = Path.Combine(Root, parent, name);
        Directory.CreateDirectory(to);
        foreach (string file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        return to;
    }

    public static SKBitmap Decode(string path) => SKBitmap.Decode(path);

    /// <summary>The repository's <c>src/UrDeck.Engine/Backgrounds/Builtin</c> directory, found from the test's own location.</summary>
    public static string BuiltIns()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "urdeck.slnx")))
            directory = Path.GetDirectoryName(directory);
        return Path.Combine(directory ?? throw new InvalidOperationException("urdeck.slnx was not found above the tests."),
            "src", "UrDeck.Engine", "Backgrounds", "Builtin");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_before);
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
