// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>A rendered background, or the reason there is none. The caller disposes <see cref="Bitmap"/>.</summary>
public sealed record BackgroundRenderResult(SKBitmap? Bitmap, string? Failure, IReadOnlyList<ShaderDiagnostic> Diagnostics) : IDisposable
{
    public void Dispose() => Bitmap?.Dispose();
}

/// <summary>
/// Renders a background to a bitmap without a window or a GPU, for tools that make sample pictures. The picture is the
/// theme's background colour with the background over it, drawn at one fixed time.
/// </summary>
public static class BackgroundRender
{
    /// <summary>
    /// <paramref name="time"/> is <c>iTime</c> in seconds, <see cref="BackgroundClock.SnapshotTime"/> by default, so the same
    /// input gives the same picture on one machine. A background that cannot be built returns no bitmap and the reason
    /// (the compiler's message for a shader that does not compile); nothing is logged.
    /// </summary>
    public static BackgroundRenderResult Render(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, int width, int height,
        float time = BackgroundClock.SnapshotTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        try
        {
            using var background = DeckBackground.TryCreate(settings, theme, store, AppContext.BaseDirectory, out string? failure, out var diagnostics);
            if (background == null)
                return new BackgroundRenderResult(null, failure ?? "there is no background to render.", diagnostics);

            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            using var resolved = ThemeResolver.Resolve(theme, width / 4f);
            canvas.Clear(resolved.Background);
            background.PrepareAsync(width, height).GetAwaiter().GetResult();
            background.PaintAtTime(canvas, width, height, time);
            return new BackgroundRenderResult(bitmap, null, diagnostics);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new BackgroundRenderResult(null, $"the background could not be rendered: {ex.Message}", []);
        }
    }
}
