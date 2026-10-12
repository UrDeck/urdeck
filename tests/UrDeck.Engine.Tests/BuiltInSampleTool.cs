// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// Not a test: the helper that renders the built-in backgrounds' <c>sample.png</c> files (270 x 480, the default theme, the
/// snapshot time) into the source tree. It only runs when asked for explicitly; see "Regenerating the built-in samples"
/// in docs/backgrounds.md. A later change replaces it with <c>urdeck-cli shader render</c>.
/// </summary>
public sealed class BuiltInSampleTool : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact(Explicit = true)]
    public void Render_the_built_in_samples()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        foreach (string name in ShaderStore.BuiltInNames)
        {
            var settings = BackgroundFixture.Parse($$"""{ "type": "shader", "name": "{{name}}", "scale": 1 }""");
            using var result = BackgroundRender.Render(settings, _fixture.Theme(), _fixture.Store, 270, 480);
            Assert.True(result.Bitmap != null, result.Failure);

            using var image = SKImage.FromBitmap(result.Bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string path = Path.Combine(root, "src", "UrDeck.Engine", "Backgrounds", "Builtin", name, "sample.png");
            File.WriteAllBytes(path, data.ToArray());
        }
    }
}
