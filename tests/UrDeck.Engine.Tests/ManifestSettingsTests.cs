// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ManifestSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "urdeck-manifest-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    [Fact]
    public void Every_builtin_background_gives_settings_with_a_default_inside_the_range()
    {
        var store = new ShaderStore(_folder);
        int total = 0;
        foreach (string name in ShaderStore.BuiltInNames)
        {
            var settings = ManifestSettings.From(store, name);
            foreach (var setting in settings)
            {
                total++;
                Assert.True(setting.Kind is SettingKinds.Range or SettingKinds.Number, $"{name}.{setting.Key}");
                Assert.False(string.IsNullOrWhiteSpace(setting.Label), $"{name}.{setting.Key}");
                double value = Assert.IsType<double>(setting.Default);
                if (setting.Number is { Min: { } min, Max: { } max })
                    Assert.InRange(value, min, max);
                if (setting.Kind == SettingKinds.Range)
                    Assert.True(setting.Number!.Step > 0, $"{name}.{setting.Key} has a step");
            }

            Assert.Equal(settings.Count, settings.Select(s => s.Key).Distinct().Count());
        }

        Assert.True(total > 0);
    }

    [Fact]
    public void A_hidden_parameter_and_the_hosts_inputs_are_left_out_and_unknown_names_give_nothing()
    {
        var store = new ShaderStore(_folder);
        foreach (string name in ShaderStore.BuiltInNames)
            Assert.DoesNotContain(ManifestSettings.From(store, name), s => HostKeys.IsHostKey(s.Key));

        Assert.Empty(ManifestSettings.From(store, "no-such-background"));
    }

    [Fact]
    public void A_builtin_has_a_sample_picture_and_can_be_drawn_as_one_frame()
    {
        var store = new ShaderStore(_folder);
        var package = store.TryLoad("nebula").Package!;

        byte[]? sample = BackgroundTiles.SamplePicture(package);
        Assert.NotNull(sample);
        using (var decoded = SkiaSharp.SKBitmap.Decode(sample))
            Assert.NotNull(decoded);

        var theme = ThemeStore.LoadBuiltIn("default-dark")!;
        byte[]? still = BackgroundTiles.RenderStill(package, theme, 120, 80);
        Assert.NotNull(still);
        using var drawn = SkiaSharp.SKBitmap.Decode(still);
        Assert.Equal(120, drawn.Width);
    }
}
