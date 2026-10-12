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

    public void WriteShader(string name, string text)
    {
        string folder = Path.Combine(Root, "backgrounds");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".sksl"), text);
    }

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
