// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// <c>--snapshot out.png [--size WxH] [--theme name] [--page N] [--frames N [--fps F] [--start time]]</c>: renders a page (default: the one at
/// <c>activePage</c>) off-screen with the same layout and
/// widget code as the live window, and the configured <c>background</c> (a shader at a fixed time, in the palette of the
/// theme in effect; a picture covering the page), writes a PNG and exits. Defaults to the target monitor's resolution, to the primary
/// monitor's when the target is not connected. It uses the engine alone, runs before the XAML application starts and
/// never writes the configuration. With <c>--frames N</c> the output is a folder and N pictures <c>frame-0000.png</c> ... are
/// written, one every 1/<c>--fps</c> second (default 30) of the time the widgets are given, from <c>--start</c> (a local
/// date and time, default now): a widget draws from that time, so an animation (the split-flap clock, the weather icons)
/// moves from frame to frame. The background stays at its snapshot time.
/// </summary>
internal static class SnapshotCommand
{
    private static DeckBackground? BuildBackground(HostContext host, LoadedTheme theme, int width, int height)
    {
        var settings = BackgroundSettings.Parse(host.Config.Config.Background);
        // The configuration's motion alone decides; Windows' setting is not read, so a snapshot is the same on every machine.
        var motion = MotionPolicy.ForSnapshot(host.Config.Config.Motion, theme.Definition.Motion?.Level);
        var plan = BackgroundPlan.Resolve(settings, theme, motion.User);

        // As the live layer does: a picture that cannot be decoded goes on to the plan's next candidate.
        int first = 0;
        while (plan.Build(host.Shaders, host.AppDirectory, first) is { } background)
        {
            if (background.PrepareAsync(width, height).GetAwaiter().GetResult())
                return background;

            first = background.CandidateIndex + 1;
            background.Dispose();
        }

        return null;
    }

    public static int Run(string[] args)
    {
        int index = Array.IndexOf(args, "--snapshot");
        try
        {
            using var host = new HostContext(saveConfiguration: false);
            string output = index + 1 < args.Length ? args[index + 1] : "urdeck.snapshot.png";
            int width, height;

            int sizeIndex = Array.IndexOf(args, "--size");
            if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
            {
                string[] parts = args[sizeIndex + 1].Split('x', 'X');
                width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                height = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (host.Target is { } target)
            {
                (width, height) = (target.Bounds.Width, target.Bounds.Height);
                UrDeckLog.Info($"Snapshot size from the target monitor: {target}");
            }
            else if (host.Displays.FirstOrDefault(d => d.IsPrimary) is { } primary)
            {
                (width, height) = (primary.Bounds.Width, primary.Bounds.Height);
                UrDeckLog.Info($"Snapshot: the target monitor is not connected; using the size of the primary monitor, {primary}");
            }
            else
            {
                (width, height) = (1100, 3840);
                UrDeckLog.Info("Snapshot: no monitor found; using 1100x3840");
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

            // The background, built as the live window builds it; a picture is decoded now and the render waits for it.
            using var background = BuildBackground(host, theme, width, height);

            int frames = 0;
            int framesIndex = Array.IndexOf(args, "--frames");
            if (framesIndex >= 0 && framesIndex + 1 < args.Length)
                frames = int.Parse(args[framesIndex + 1], CultureInfo.InvariantCulture);
            double fps = 30;
            int fpsIndex = Array.IndexOf(args, "--fps");
            if (fpsIndex >= 0 && fpsIndex + 1 < args.Length)
                fps = double.Parse(args[fpsIndex + 1], CultureInfo.InvariantCulture);
            if (frames < 0 || fps <= 0)
            {
                UrDeckLog.Error("Snapshot failed: --frames must not be negative and --fps must be above zero");
                return 1;
            }

            var start = DateTime.Now;
            int startIndex = Array.IndexOf(args, "--start");
            if (startIndex >= 0 && startIndex + 1 < args.Length)
                start = DateTime.Parse(args[startIndex + 1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

            var chrome = new PageRenderer.Chrome(config.Pager.GetIndicatorMode(), pageIndex, config.Pages.Count);
            var bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, start, chrome, dock, background);

            // Icons are asked for while painting. If that paint asked for any, wait for them (a few seconds at most) and
            // paint again, so the image shows icons and not placeholders. A page without them is painted once.
            var icons = host.Plugins.Icons;
            if (icons.AnyPending)
            {
                var iconDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (icons.AnyPending && DateTime.UtcNow < iconDeadline)
                    Thread.Sleep(25);
                bitmap.Dispose();
                bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, start, chrome, dock, background);
            }

            if (frames > 0)
            {
                bitmap.Dispose();
                Directory.CreateDirectory(output);
                for (int i = 0; i < frames; i++)
                {
                    using var frame = PageRenderer.RenderToBitmap(width, height, widgets, theme, start + TimeSpan.FromSeconds(i / fps), chrome, dock, background);
                    using var frameFile = File.Create(Path.Combine(output, $"frame-{i:D4}.png"));
                    frame.Encode(frameFile, SKEncodedImageFormat.Png, 100);
                }

                UrDeckLog.Info($"Snapshot {width}x{height}: {frames} frames at {fps.ToString(CultureInfo.InvariantCulture)} per second from {start:HH:mm:ss.fff} written to {Path.GetFullPath(output)}");
                return 0;
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
