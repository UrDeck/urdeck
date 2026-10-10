// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// The widgets of one page: a canvas of <see cref="WidgetView"/>s, owning its reading-to-view map. Disposing it releases
/// every view (readings unsubscribed, frame-clock entries removed), so a page that is not shown costs nothing.
/// </summary>
internal sealed class PageHost : Canvas, IDisposable
{
    private readonly List<WidgetView> _views = new();
    private readonly Dictionary<string, List<WidgetView>> _viewsByReading = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, WidgetView> _viewsByIndex = new();
    private readonly IReadOnlyList<WidgetLayoutItem> _layout;

    public PageHost(
        PageConfig page,
        IReadOnlyList<WidgetLayoutItem> layout,
        WidgetPluginLoader plugins,
        Theme theme,
        PressStyle press,
        FrameClock frameClock)
    {
        _layout = layout;
        for (int i = 0; i < page.Widgets.Count; i++)
        {
            var config = page.Widgets[i];
            if (!config.IsVisible)
                continue;

            var item = layout[i];
            if (!item.Placed)
                continue;

            var descriptor = plugins.GetDescriptor(config.WidgetTypeId);
            if (descriptor == null)
            {
                UrDeckLog.Warn($"Widget type '{config.WidgetTypeId}' is not registered; leaving its cell empty. " +
                              $"Registered: [{string.Join(", ", plugins.GetRegisteredWidgetTypes())}]");
                continue;
            }

            try
            {
                var widget = plugins.CreateWidget(config, out var services)!;
                var view = new WidgetView(widget, services, descriptor, theme, press, frameClock, plugins.Readings)
                {
                    Width = item.Size.Width,
                    Height = item.Size.Height,
                };
                SetLeft(view, item.Position.X);
                SetTop(view, item.Position.Y);
                Children.Add(view);
                _views.Add(view);
                _viewsByIndex[i] = view;
                foreach (string reading in view.Subscriptions)
                {
                    if (!_viewsByReading.TryGetValue(reading, out var users))
                        _viewsByReading[reading] = users = [];
                    users.Add(view);
                }

                UrDeckLog.Info($"Placed '{config.WidgetTypeId}' at grid ({config.Col},{config.Row}) {config.Width}x{config.Height} " +
                              $"-> {item.Position.X},{item.Position.Y} {item.Size.Width}x{item.Size.Height} DIPs");
            }
            catch (Exception ex)
            {
                UrDeckLog.Error($"Failed to create widget '{config.WidgetTypeId}'", ex);
            }
        }
    }

    /// <summary>The views that declared <paramref name="reading"/>.</summary>
    public IReadOnlyList<WidgetView> ViewsFor(string reading) =>
        _viewsByReading.TryGetValue(reading, out var users) ? users : [];

    /// <summary>
    /// The view whose card contains the point (in the page's DIPs), and the point relative to that card; null in a gap,
    /// on an empty cell and on a widget that could not be created. The decision is the engine's.
    /// </summary>
    public WidgetView? FindView(double x, double y, out System.Drawing.PointF local)
    {
        local = default;
        if (WidgetHitTest.Find(_layout, x, y) is not { } hit || !_viewsByIndex.TryGetValue(hit.Index, out var view))
            return null;
        local = new System.Drawing.PointF((float)hit.X, (float)hit.Y);
        return view;
    }

    public void Dispose()
    {
        foreach (var view in _views)
            view.Dispose();
        _views.Clear();
        _viewsByIndex.Clear();
        _viewsByReading.Clear();
        Children.Clear();
    }
}
