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
    private readonly BackgroundKind _kind;
    private readonly ThemeBackgroundStaticDefinition? _still;
    private readonly string _key;
    private readonly object _gate = new();
    private SKColor? _scrim;
    private string _scrimKey = "";
    private SKBitmap? _bitmap;
    private bool _disposed;

    private DeckBackground(BackgroundSettings settings, string key, string description, ShaderProgram? program, string? imagePath,
        BackgroundKind? kind = null, ThemeBackgroundStaticDefinition? still = null)
    {
        _settings = settings;
        _kind = kind ?? settings.Kind;
        _still = still;
        _key = key;
        Describe = description;
        _program = program;
        _imagePath = imagePath;
    }

    public BackgroundKind Kind => _kind;

    /// <summary>Where the choice came from; <see cref="BackgroundOrigin.Configuration"/> unless a plan built it for a theme.</summary>
    public BackgroundOrigin Origin { get; private set; } = BackgroundOrigin.Configuration;

    /// <summary>The position in the plan's candidates that built this background, so a failure can go on from the next one.</summary>
    public int CandidateIndex { get; private set; }

    /// <summary>
    /// Equal for two backgrounds that look the same: kind, shader and its text, speed, scale, parameters and palette, or
    /// the picture's path and file stamp, and the scrim over it. A change of any of them gives a different key.
    /// </summary>
    public string Key => _key + _scrimKey;

    /// <summary>For the log: <c>shader 'plasma' (built-in)</c> or <c>image 'C:\wall.jpg'</c>.</summary>
    public string Describe { get; }

    /// <summary>True for a shader with a speed above 0 that declares <c>iTime</c>; everything else is still.</summary>
    public bool IsMoving => _program is { DeclaresTime: true } && _settings.Speed > 0;

    /// <summary>The fraction of the panel's resolution it renders at; 1 for an image.</summary>
    public double Scale => Kind == BackgroundKind.Shader ? _settings.Scale : 1;

    /// <summary>The theme's scrim drawn over this background, or null.</summary>
    public SKColor? Scrim => _scrim;

    /// <summary>Records where a plan found this background and the scrim of the theme that is drawn over it.</summary>
    internal void Adopt(BackgroundOrigin origin, int candidateIndex, LoadedTheme theme)
    {
        Origin = origin;
        CandidateIndex = candidateIndex;
        _scrim = ScrimColor(theme);
        _scrimKey = ScrimKey(theme);
    }

    /// <summary>The scrim's colour with its alpha multiplied by <c>background.scrim.alpha</c>, or null when the theme has no usable scrim.</summary>
    internal static SKColor? ScrimColor(LoadedTheme theme)
    {
        if (theme.Definition.Background?.Scrim is not { Color: { } text, Alpha: { } alpha })
            return null;
        if (!BackgroundSettings.TryParseColor(text, out var color))
            return null;

        byte combined = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * color.Alpha);
        return combined == 0 ? null : color.WithAlpha(combined);
    }

    internal static string ScrimKey(LoadedTheme theme) =>
        ScrimColor(theme) is { } scrim ? $"|scrim={scrim}" : "";

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
                return ShaderKey(settings, theme, store.Load(settings.Name!, warn: false));

            case BackgroundKind.Image:
                string path = BackgroundImage.ResolvePath(settings.Path!, appDirectory);
                var file = new FileInfo(path);
                string stamp = file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "missing";
                return $"image|{path}|{stamp}";

            default:
                return null;
        }
    }

    private static string ShaderKey(BackgroundSettings settings, LoadedTheme theme, BackgroundPackage? shader)
    {
        string palette = string.Join(",", BackgroundPalette.Resolve(settings.Colors, theme).Select(c => c.ToString()));
        string parameters = string.Join(";", settings.Parameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value.GetRawText()}"));
        string source = shader == null ? "missing" : $"{shader.Origin}:{Hash(shader.EntryText)}:{Hash(shader.ManifestText)}";
        return string.Create(CultureInfo.InvariantCulture,
            $"shader|{settings.Name}|{source}|{settings.Speed}|{settings.Scale}|{palette}|{parameters}");
    }

    /// <summary>
    /// Builds the configured background, or returns null for none and for every failure, after logging one warning that
    /// names the cause. A shader's lint diagnostics are logged once each, as <c>Background 'name': SHADERnnn message
    /// (file)</c>. An image is only checked for existence here; <see cref="PrepareAsync"/> decodes it.
    /// </summary>
    public static DeckBackground? Create(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory)
    {
        try
        {
            var background = TryCreate(settings, theme, store, appDirectory, out string? failure, out var diagnostics);
            string who = settings.Kind == BackgroundKind.Shader ? $"Background '{settings.Name}': " : "Background: ";
            foreach (var diagnostic in diagnostics)
                UrDeckLog.Warn(who + diagnostic);
            if (failure != null && !diagnostics.Any(d => d.ToString() == failure))
                UrDeckLog.Warn(who + failure);
            return background;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background: could not be created: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// As <see cref="Create"/>, but logs nothing for the shader: <paramref name="failure"/> holds the reason when there is
    /// none (null for no background configured) and <paramref name="diagnostics"/> the lint's findings, which can be
    /// there with a background too.
    /// </summary>
    internal static DeckBackground? TryCreate(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory,
        out string? failure, out IReadOnlyList<ShaderDiagnostic> diagnostics)
    {
        failure = null;
        diagnostics = [];
        switch (settings.Kind)
        {
            case BackgroundKind.Shader:
                return CreateShader(settings, theme, store, appDirectory, out failure, out diagnostics);
            case BackgroundKind.Image:
                return CreateImage(settings, theme, store, appDirectory, out failure);
            default:
                return null;
        }
    }

    /// <summary>As the by-name <c>TryCreate</c> for a shader, from a package that was read from a folder rather than found by name.</summary>
    internal static DeckBackground? TryCreate(BackgroundPackage package, BackgroundSettings settings, LoadedTheme theme,
        out string? failure, out IReadOnlyList<ShaderDiagnostic> diagnostics) =>
        CreateShader(package, settings, theme, out failure, out diagnostics);

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
        if (!PaintContent(canvas, width, height, time))
            return;

        // The theme's veil goes last, over whatever was drawn, so it works for a shader, a picture and a still alike.
        if (_scrim is { } scrim)
        {
            using var paint = new SKPaint { Color = scrim };
            canvas.DrawRect(0, 0, width, height, paint);
        }
    }

    private bool PaintContent(SKCanvas canvas, int width, int height, float time)
    {
        if (_program != null)
        {
            using var shader = _program.CreateShader(width, height, time);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(0, 0, width, height, paint);
            return true;
        }

        if (_still != null && _imagePath == null)
        {
            StillPainter.Paint(canvas, width, height, _still);
            return true;
        }

        if (!IsReadyFor(width, height) && !PrepareAsync(width, height).GetAwaiter().GetResult())
            return false;

        lock (_gate)
        {
            if (_bitmap != null)
            {
                using var image = SKImage.FromBitmap(_bitmap);
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            }
        }

        return true;
    }

    /// <summary>True when there is nothing to prepare for this size (a shader, or a picture already scaled to it).</summary>
    public bool IsReadyFor(int width, int height)
    {
        lock (_gate)
            return _program != null || (_imagePath == null && _still != null) || (_bitmap != null && _bitmap.Width == width && _bitmap.Height == height);
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

    private static DeckBackground? CreateShader(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory,
        out string? failure, out IReadOnlyList<ShaderDiagnostic> diagnostics)
    {
        diagnostics = [];
        var load = store.TryLoad(settings.Name!);
        if (load.Package == null)
        {
            failure = load.Failure;
            return null;
        }

        return CreateShader(load.Package, settings, theme, out failure, out diagnostics);
    }

    private static DeckBackground? CreateShader(BackgroundPackage package, BackgroundSettings settings, LoadedTheme theme,
        out string? failure, out IReadOnlyList<ShaderDiagnostic> diagnostics)
    {
        diagnostics = ShaderLint.Check(package.Source, package.Name);
        var stop = diagnostics.FirstOrDefault(d => d.Severity == ShaderSeverity.Error && ShaderCodes.StopsLoading(d.Code));
        if (stop != null)
        {
            failure = stop.ToString();
            return null;
        }

        var palette = BackgroundPalette.Resolve(settings.Colors, theme);
        var program = ShaderProgram.Create(package, settings, palette, out failure);
        if (program == null)
            return null;

        return new DeckBackground(settings, ShaderKey(settings, theme, package), $"shader '{settings.Name}' ({package.Origin})", program, null);
    }

    private static DeckBackground? CreateImage(BackgroundSettings settings, LoadedTheme theme, ShaderStore store, string appDirectory, out string? failure)
    {
        string path = BackgroundImage.ResolvePath(settings.Path!, appDirectory);
        if (!File.Exists(path))
        {
            failure = $"the picture {path} does not exist.";
            return null;
        }

        failure = null;
        string key = RequestKey(settings, theme, store, appDirectory)!;
        return new DeckBackground(settings, key, $"image '{path}'", null, path);
    }

    /// <summary>
    /// Builds a theme's still background: a picture inside the theme's folder, a gradient or a colour (in that order, when
    /// the theme gives more than one). <paramref name="failure"/> says why there is none; nothing is logged here.
    /// </summary>
    internal static DeckBackground? TryCreateStill(ThemeBackgroundStaticDefinition still, LoadedTheme theme, out string? failure)
    {
        failure = null;
        if (!string.IsNullOrWhiteSpace(still.Image))
        {
            if (theme.Folder == null)
            {
                failure = $"the theme {theme.Name} is built in and has no folder to read the picture '{still.Image}' from.";
                return null;
            }

            string path = ThemeStore.ResolveIn(theme.Folder, still.Image);
            if (!ThemeStore.IsInside(theme.Folder, path))
            {
                failure = $"the picture '{still.Image}' of theme {theme.Name} leaves the theme folder and is ignored.";
                return null;
            }

            if (!File.Exists(path))
            {
                failure = $"the picture {path} does not exist.";
                return null;
            }

            return new DeckBackground(BackgroundSettings.None, StillRequestKey(still, theme), $"image '{path}'", null, path,
                BackgroundKind.Image, still);
        }

        if (still.Gradient is { Colors: { Count: >= 2 } colors })
        {
            if (!colors.All(c => BackgroundSettings.TryParseColor(c, out _)))
            {
                failure = "the theme's still gradient has a colour that is not #RRGGBB or #AARRGGBB.";
                return null;
            }

            return new DeckBackground(BackgroundSettings.None, StillRequestKey(still, theme), "gradient", null, null, BackgroundKind.Gradient, still);
        }

        if (BackgroundSettings.TryParseColor(still.Color, out _))
            return new DeckBackground(BackgroundSettings.None, StillRequestKey(still, theme), $"colour {still.Color}", null, null, BackgroundKind.Color, still);

        failure = "the theme's still background has no usable picture, gradient or colour.";
        return null;
    }

    /// <summary>True when the theme's <c>background.static</c> names something to draw.</summary>
    internal static bool HasStill(ThemeBackgroundStaticDefinition? still) =>
        still != null && (!string.IsNullOrWhiteSpace(still.Image) || still.Gradient?.Colors is { Count: >= 2 } || still.Color != null);

    /// <summary>The key of a still, without building it: a picture's file stamp is part of it.</summary>
    internal static string StillRequestKey(ThemeBackgroundStaticDefinition still, LoadedTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(still.Image))
        {
            string path = theme.Folder == null ? still.Image : ThemeStore.ResolveIn(theme.Folder, still.Image);
            var file = new FileInfo(path);
            string stamp = file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "missing";
            return $"still|image|{path}|{stamp}";
        }

        return still.Gradient is { Colors: { Count: >= 2 } } gradient
            ? "still|gradient|" + StillPainter.Describe(gradient)
            : $"still|color|{still.Color}";
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
