// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// What the deck shows behind its cards, ready to paint on any Skia canvas: a compiled shader or a picture. Owns its
/// effect or scaled bitmap. <see cref="Paint"/> does not clear: the caller has drawn the theme's background colour.
/// </summary>
public sealed class DeckBackground : IDisposable
{
    private readonly BackgroundSettings _settings;
    private readonly ShaderProgram? _program;
    private readonly string? _imagePath;
    private readonly object _gate = new();
    private SKBitmap? _bitmap;
    private bool _disposed;

    private DeckBackground(BackgroundSettings settings, string key, string description, ShaderProgram? program, string? imagePath)
    {
        _settings = settings;
        Key = key;
        Describe = description;
        _program = program;
        _imagePath = imagePath;
    }

    public BackgroundKind Kind => _settings.Kind;

    /// <summary>
    /// Equal for two backgrounds that look the same: kind, shader and its text, speed, scale, parameters and palette, or
    /// the picture's path and file stamp. A change of any of them gives a different key.
    /// </summary>
    public string Key { get; }

    /// <summary>For the log: <c>shader 'plasma' (built-in)</c> or <c>image 'C:\wall.jpg'</c>.</summary>
    public string Describe { get; }

    /// <summary>True for a shader with a speed above 0 that declares <c>iTime</c>; everything else is still.</summary>
    public bool IsMoving => _program is { DeclaresTime: true } && _settings.Speed > 0;

    /// <summary>The fraction of the panel's resolution it renders at; 1 for an image.</summary>
    public double Scale => Kind == BackgroundKind.Shader ? _settings.Scale : 1;

    public double Speed => _settings.Speed;

    /// <summary>
    /// The key for what <see cref="Create"/> would build, without compiling or decoding, so a caller can tell whether a
    /// running background is still the configured one (and whether a failed one needs another try). Null for none.
    /// </summary>
    public static string? RequestKey(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory)
    {
        switch (settings.Kind)
        {
            case BackgroundKind.Shader:
                var shader = store.Load(settings.Name!, warn: false);
                string palette = string.Join(",", BackgroundPalette.Resolve(settings.Colors, theme).Select(c => c.ToString()));
                string parameters = string.Join(";", settings.Parameters
                    .OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => $"{p.Key}={p.Value.GetRawText()}"));
                string source = shader == null ? "missing" : $"{shader.Origin}:{Hash(shader.Text)}";
                return string.Create(CultureInfo.InvariantCulture,
                    $"shader|{settings.Name}|{source}|{settings.Speed}|{settings.Scale}|{palette}|{parameters}");

            case BackgroundKind.Image:
                string path = BackgroundImage.ResolvePath(settings.Path!, appDirectory);
                var file = new FileInfo(path);
                string stamp = file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "missing";
                return $"image|{path}|{stamp}";

            default:
                return null;
        }
    }

    /// <summary>
    /// Builds the configured background, or returns null for none and for every failure, after logging one warning that
    /// names the cause. An image is only checked for existence here; <see cref="PrepareAsync"/> decodes it.
    /// </summary>
    public static DeckBackground? Create(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory)
    {
        try
        {
            return settings.Kind switch
            {
                BackgroundKind.Shader => CreateShader(settings, theme, store, appDirectory),
                BackgroundKind.Image => CreateImage(settings, theme, store, appDirectory),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background: could not be created: {ex.Message}");
            return null;
        }
    }

    /// <summary>Draws the background at <paramref name="elapsedSeconds"/> of shown time, scaled by the speed and wrapped.</summary>
    public void Paint(SKCanvas canvas, int width, int height, double elapsedSeconds) =>
        PaintAtTime(canvas, width, height, BackgroundClock.Wrap(elapsedSeconds, _settings.Speed));

    /// <summary>
    /// Draws the background with <c>iTime</c> as given (a snapshot uses <see cref="BackgroundClock.SnapshotTime"/>). A shader
    /// fills <paramref name="width"/> x <paramref name="height"/> pixels; a picture is drawn from a copy scaled to that
    /// size, made here if <see cref="PrepareAsync"/> has not made it. Throws when painting fails.
    /// </summary>
    public void PaintAtTime(SKCanvas canvas, int width, int height, float time)
    {
        if (_program != null)
        {
            using var shader = _program.CreateShader(width, height, time);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(0, 0, width, height, paint);
            return;
        }

        if (!IsReadyFor(width, height) && !PrepareAsync(width, height).GetAwaiter().GetResult())
            return;

        lock (_gate)
        {
            if (_bitmap != null)
            {
                using var image = SKImage.FromBitmap(_bitmap);
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            }
        }
    }

    /// <summary>True when there is nothing to prepare for this size (a shader, or a picture already scaled to it).</summary>
    public bool IsReadyFor(int width, int height)
    {
        lock (_gate)
            return _program != null || (_bitmap != null && _bitmap.Width == width && _bitmap.Height == height);
    }

    /// <summary>
    /// For a picture: decodes and scales it to <paramref name="width"/> x <paramref name="height"/> on a thread-pool thread
    /// and keeps only the scaled copy. False when the picture cannot be shown (a warning names the path). A shader is
    /// always ready.
    /// </summary>
    public Task<bool> PrepareAsync(int width, int height)
    {
        if (_imagePath == null)
            return Task.FromResult(true);
        if (IsReadyFor(width, height))
            return Task.FromResult(true);

        return Task.Run(() =>
        {
            var scaled = BackgroundImage.Prepare(_imagePath, width, height);
            lock (_gate)
            {
                if (_disposed)
                {
                    scaled?.Dispose();
                    return false;
                }
                var old = _bitmap;
                _bitmap = scaled;
                old?.Dispose();
            }
            return scaled != null;
        });
    }

    public void Dispose()
    {
        _program?.Dispose();
        lock (_gate)
        {
            _disposed = true;
            _bitmap?.Dispose();
            _bitmap = null;
        }
    }

    private static DeckBackground? CreateShader(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory)
    {
        var shader = store.Load(settings.Name!);
        if (shader == null)
            return null;

        var palette = BackgroundPalette.Resolve(settings.Colors, theme);
        var program = ShaderProgram.Create(shader, settings, palette);
        if (program == null)
            return null;

        string key = RequestKey(settings, theme, store, appDirectory)!;
        return new DeckBackground(settings, key, $"shader '{settings.Name}' ({shader.Origin})", program, null);
    }

    private static DeckBackground? CreateImage(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory)
    {
        string path = BackgroundImage.ResolvePath(settings.Path!, appDirectory);
        if (!File.Exists(path))
        {
            UrDeckLog.Warn($"Background: the picture {path} does not exist.");
            return null;
        }

        string key = RequestKey(settings, theme, store, appDirectory)!;
        return new DeckBackground(settings, key, $"image '{path}'", null, path);
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
