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
    /// (the compiler's message for a shader that does not compile); nothing is logged by the render itself. A picture that is
    /// still the theme's background colour everywhere comes with a <see cref="ShaderCodes.BlankRender"/> warning.
    /// </summary>
    public static BackgroundRenderResult Render(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, int width, int height,
        float time = BackgroundClock.SnapshotTime, string? pictureFolder = null) =>
        RenderCore(width, height, theme, time, () =>
        {
            // The tool is no host: relative picture paths start at its own folder unless the caller says where.
            var background = DeckBackground.TryCreate(settings, theme, store, pictureFolder ?? AppContext.BaseDirectory, out string? failure, out var diagnostics);
            return (background, failure, diagnostics);
        });

    /// <summary>
    /// As the overload that takes a store, for a shader read from a folder with <see cref="ShaderStore.TryRead"/>: that
    /// package is rendered, never a built-in of the same name. <paramref name="settings"/> must be a shader background
    /// named like the package.
    /// </summary>
    public static BackgroundRenderResult Render(BackgroundPackage package, BackgroundSettings settings, LoadedTheme theme, int width, int height,
        float time = BackgroundClock.SnapshotTime) =>
        RenderCore(width, height, theme, time, () =>
        {
            var background = DeckBackground.TryCreate(package, settings, theme, out string? failure, out var diagnostics);
            return (background, failure, diagnostics);
        });

    private static BackgroundRenderResult RenderCore(int width, int height, LoadedTheme theme, float time,
        Func<(DeckBackground? Background, string? Failure, IReadOnlyList<ShaderDiagnostic> Diagnostics)> create)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        try
        {
            var (background, failure, diagnostics) = create();
            using var _ = background;
            if (background == null)
                return new BackgroundRenderResult(null, failure ?? "there is no background to render.", diagnostics);

            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            using var resolved = ThemeResolver.Resolve(theme, width / 4f);
            canvas.Clear(resolved.Background);
            background.PrepareAsync(width, height).GetAwaiter().GetResult();
            background.PaintAtTime(canvas, width, height, time);

            if (IsUnchanged(bitmap, resolved.Background))
            {
                diagnostics =
                [
                    .. diagnostics,
                    new ShaderDiagnostic(ShaderCodes.BlankRender, ShaderSeverity.Warning,
                        $"the shader drew nothing at time {time:0.###}: every pixel is still the theme's background colour.", ""),
                ];
            }

            return new BackgroundRenderResult(bitmap, null, diagnostics);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new BackgroundRenderResult(null, $"the background could not be rendered: {ex.Message}", []);
        }
    }

    /// <summary>True when every pixel of the bitmap is the colour it was cleared to.</summary>
    private static bool IsUnchanged(SKBitmap bitmap, SKColor cleared)
    {
        using var reference = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(reference))
            canvas.Clear(cleared);

        var expected = reference.GetPixelSpan();
        var pixels = bitmap.GetPixelSpan();
        for (int i = 0; i + 4 <= pixels.Length; i += 4)
            if (!pixels.Slice(i, 4).SequenceEqual(expected))
                return false;
        return true;
    }
}
