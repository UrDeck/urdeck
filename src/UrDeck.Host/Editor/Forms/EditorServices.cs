// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using UrDeck.Engine.Data;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Themes;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;

namespace UrDeck.Host.Editor.Forms;

/// <summary>
/// The readings a form shows, as the editor's preview shows them (live, or the providers' samples), and the actions and suggestions
/// of the host's providers.
/// </summary>
internal sealed class PreviewEditorReadings(IReadingSource preview, UrDeck.Engine.Deck.IDeckReadings subscriptions, ReadingHub hub, DispatcherQueue queue) : IEditorReadings
{
    public bool RegionUsesFahrenheit => preview.RegionUsesFahrenheit;

    public ReadingDescriptor? Describe(string id)
    {
        if (preview.Describe(id) is { } found)
            return found;
        // The provider's own list, when it has not been asked for the reading yet (nothing subscribes to it before the form shows it).
        if (!ReadingId.TryParse(id, out string provider, out string path, out _))
            return null;
        return hub.GetCatalog(provider)?.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    public string ValueText(string id)
    {
        var text = ReadingFormatter.Format(preview.Read(id), Describe(id), null, preview);
        return text.Unit is { Length: > 0 } unit ? $"{text.Value} {unit}" : text.Value;
    }

    public IDisposable Watch(string id, Action changed)
    {
        void OnChanged(IReadOnlyCollection<string> ids)
        {
            if (ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                queue.TryEnqueue(() => changed());
        }

        subscriptions.Subscribe([id]);
        subscriptions.ReadingsChanged += OnChanged;
        return new Disposer(() =>
        {
            subscriptions.ReadingsChanged -= OnChanged;
            subscriptions.Unsubscribe([id]);
        });
    }

    public IReadOnlyList<ActionDescriptor> ActionsFor(string id) => hub.ActionsFor(id);

    public Task<SuggestionResult> SuggestAsync(string providerId, string kind, string text, CancellationToken cancellationToken) =>
        hub.SuggestAsync(providerId, kind, text, cancellationToken);

    public bool ProviderKnown(string providerId) => hub.GetCatalog(providerId) != null;
}

/// <summary>The glyphs of the theme the preview shows, drawn small on first use (editor-forms D8).</summary>
internal sealed class ThemeIconCatalog(Func<LoadedTheme> theme) : IIconCatalog
{
    private const int Pixels = 64;

    private LoadedTheme? _for;
    private IReadOnlyList<IconChoice>? _glyphs;

    public IReadOnlyList<IconChoice> Glyphs()
    {
        var loaded = theme();
        if (_glyphs != null && ReferenceEquals(_for, loaded))
            return _glyphs;

        using var resolved = ThemeResolver.Resolve(loaded, 200f);
        var list = new List<IconChoice>();
        foreach (string key in IconSets.KeysOf(resolved.Icons.Set))
        {
            using var bitmap = new SKBitmap(new SKImageInfo(Pixels, Pixels, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            if (!ThemedIcon.Draw(canvas, resolved, new SKRect(6, 6, Pixels - 6, Pixels - 6), key))
                continue;
            var image = new WriteableBitmap(Pixels, Pixels);
            using (var pixels = image.PixelBuffer.AsStream())
                pixels.Write(bitmap.GetPixelSpan());
            image.Invalidate();
            list.Add(new IconChoice(key, image));
        }

        _for = loaded;
        return _glyphs = list;
    }
}

/// <summary>No glyphs: a form with no icon setting (a provider's) has no use for the theme's.</summary>
internal sealed class NoIconCatalog : IIconCatalog
{
    public IReadOnlyList<IconChoice> Glyphs() => [];
}
