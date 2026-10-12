// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A scratch application folder for background tests, with the log redirected into it. Shaders are painted on Skia's CPU
/// backend, so none of this needs a GPU.
/// </summary>
internal sealed class BackgroundFixture : IDisposable
{
    private readonly string _logBefore = UrDeckLog.LogPath;

    public BackgroundFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "urdeck-background-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        UrDeckLog.LogPath = Path.Combine(Root, "test.log");
        Store = new ShaderStore(Path.Combine(Root, "backgrounds"));
        Themes = new ThemeStore(Path.Combine(Root, "themes"));
    }

    public string Root { get; }
    public ShaderStore Store { get; }
    public ThemeStore Themes { get; }

    public string Log => File.Exists(Path.Combine(Root, "test.log")) ? File.ReadAllText(Path.Combine(Root, "test.log")) : "";

    public LoadedTheme Theme(string name = "default-dark") => Themes.Load(name);

    public static BackgroundSettings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return BackgroundSettings.Parse(document.RootElement.Clone());
    }

    /// <summary>
    /// Writes <c>backgrounds/&lt;name&gt;/</c> with the shader and a manifest that lints clean (a title, a description, every
    /// parameter described, and a sample picture), so a test sees only the diagnostics it provokes.
    /// </summary>
    public void WriteShader(string name, string text)
    {
        var parameters = new Dictionary<string, object>();
        foreach (var uniform in ShaderDeclarations.Parse(text, name).Uniforms)
        {
            if (!uniform.Name.StartsWith('i') || uniform.Name.Length < 2 || !char.IsUpper(uniform.Name[1]))
                parameters[uniform.Name] = new { label = uniform.Name, description = "A parameter of the test shader." };
        }

        string manifest = JsonSerializer.Serialize(new
        {
            manifest = 1,
            title = name,
            description = "A background of the tests.",
            parameters,
            samples = new[] { new { type = "image", file = "sample.png" } },
        });

        WriteFolder(name, new Dictionary<string, string> { [name + ".sksl"] = text, ["manifest.json"] = manifest });
        using var picture = new SKBitmap(1, 1);
        File.WriteAllBytes(Path.Combine(Root, "backgrounds", name, "sample.png"), picture.Encode(SKEncodedImageFormat.Png, 100).ToArray());
    }

    /// <summary>Writes the given files (relative path to text) into <c>backgrounds/&lt;name&gt;/</c>, replacing the folder's files of those names.</summary>
    public void WriteFolder(string name, IReadOnlyDictionary<string, string> files)
    {
        string folder = Path.Combine(Root, "backgrounds", name);
        foreach ((string relative, string text) in files)
        {
            string path = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
    }

    public string FolderPath(string name) => Path.Combine(Root, "backgrounds", name);

    public DeckBackground? Create(string json, string themeName = "default-dark") =>
        DeckBackground.Create(Parse(json), Theme(themeName), Store, Root);

    /// <summary>Paints the background on a CPU surface, as the live layer would, and returns the pixels.</summary>
    public static SKBitmap Paint(DeckBackground background, int width, int height, double seconds = 0)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        background.Paint(canvas, width, height, seconds);
        return bitmap;
    }

    /// <summary>True when every pixel of the bitmap has the same colour.</summary>
    public static bool IsFlat(SKBitmap bitmap)
    {
        SKColor first = bitmap.GetPixel(0, 0);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) != first)
                    return false;
        return true;
    }

    public static bool Same(SKBitmap a, SKBitmap b) =>
        a.Width == b.Width && a.Height == b.Height && a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        try
        { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}
