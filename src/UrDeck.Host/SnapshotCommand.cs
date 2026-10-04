// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// <c>--snapshot out.png [--size WxH] [--theme name]</c>: renders the active page off-screen with the same layout and
/// widget code as the live window, writes a PNG and exits. Defaults to the target monitor's resolution. It uses the
/// engine alone and runs before the XAML application starts.
/// </summary>
internal static class SnapshotCommand
{
    public static int Run(string[] args)
    {
        int index = Array.IndexOf(args, "--snapshot");
        try
        {
            using var host = new HostContext();
            string output = index + 1 < args.Length ? args[index + 1] : "urdeck.snapshot.png";
            int width = host.Target.Bounds.Width;
            int height = host.Target.Bounds.Height;

            int sizeIndex = Array.IndexOf(args, "--size");
            if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
            {
                string[] parts = args[sizeIndex + 1].Split('x', 'X');
                width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                height = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }

            int themeIndex = Array.IndexOf(args, "--theme");
            string themeName = themeIndex >= 0 && themeIndex + 1 < args.Length ? args[themeIndex + 1] : host.Config.Config.Theme;
            var theme = host.Themes.Load(themeName);

            var page = host.Config.Config.CurrentPage;
            var widgets = new List<(WidgetConfig, IWidget)>();
            foreach (var config in page?.Widgets ?? new List<WidgetConfig>())
            {
                var widget = host.Plugins.CreateWidget(config);
                if (widget == null)
                    UrDeckLog.Warn($"Snapshot: '{config.WidgetTypeId}' not registered");
                else
                    widgets.Add((config, widget));
            }

            using var bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, DateTime.Now);
            using var file = File.Create(output);
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
            UrDeckLog.Info($"Snapshot {width}x{height} with {widgets.Count} widget(s) written to {Path.GetFullPath(output)}");
            return 0;
        }
        catch (Exception ex)
        {
            UrDeckLog.Error("Snapshot failed", ex);
            return 1;
        }
    }
}
