// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// <c>--snapshot out.png [--size WxH] [--theme name] [--page N]</c>: renders a page (default: the one at
/// <c>activePage</c>) off-screen with the same layout and
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

            int? requested = null;
            int pageArg = Array.IndexOf(args, "--page");
            if (pageArg >= 0 && pageArg + 1 < args.Length)
                requested = int.Parse(args[pageArg + 1], CultureInfo.InvariantCulture);

            var config = host.Config.Config;
            if (!PageSelection.TryChoose(config.Pages.Count, config.ActivePage, requested, out int pageIndex, out string? pageError))
            {
                UrDeckLog.Error($"Snapshot failed: {pageError}");
                return 1;
            }

            var page = config.Pages.Count == 0 ? null : config.Pages[pageIndex];
            var widgets = new List<(WidgetConfig, IWidget)>();
            foreach (var widgetConfig in page?.Widgets ?? new List<WidgetConfig>())
            {
                var widget = host.Plugins.CreateWidget(widgetConfig);
                if (widget == null)
                    UrDeckLog.Warn($"Snapshot: '{widgetConfig.WidgetTypeId}' not registered");
                else
                    widgets.Add((widgetConfig, widget));
            }

            // The dock's slots, decided as the live window decides them; an empty slot has no widget.
            var dock = new List<(WidgetConfig, IWidget?)>();
            foreach (var entry in DockLayout.Select(config.Dock, host.Plugins.GetDescriptor))
                dock.Add((entry.Config, entry.Shown ? host.Plugins.CreateWidget(entry.Config) : null));

            // Subscribe the way the live window does, and let the providers report once so the card shows values.
            var readings = host.Plugins.Readings;
            readings.ApplySettings(host.Config.Config.Providers);
            string[] ids = widgets.Select(w => w.Item2).Concat(dock.Select(d => d.Item2).OfType<IWidget>())
                .SelectMany(w => w.Subscriptions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            readings.Subscribe(ids);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            while (readings.AnyPending(ids) && DateTime.UtcNow < deadline)
                Thread.Sleep(25);

            var chrome = new PageRenderer.Chrome(config.Pager.GetIndicatorMode(), pageIndex, config.Pages.Count);
            var bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, DateTime.Now, chrome, dock);

            // Icons are asked for while painting. If that paint asked for any, wait for them (a few seconds at most) and
            // paint again, so the image shows icons and not placeholders. A page without them is painted once.
            var icons = host.Plugins.Icons;
            if (icons.AnyPending)
            {
                var iconDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (icons.AnyPending && DateTime.UtcNow < iconDeadline)
                    Thread.Sleep(25);
                bitmap.Dispose();
                bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, DateTime.Now, chrome, dock);
            }

            using var image = bitmap;
            using var file = File.Create(output);
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
            UrDeckLog.Info($"Snapshot {width}x{height} with {widgets.Count} widget(s) and {dock.Count(d => d.Item2 != null)} in the dock written to {Path.GetFullPath(output)}");
            return 0;
        }
        catch (Exception ex)
        {
            UrDeckLog.Error("Snapshot failed", ex);
            return 1;
        }
    }
}
