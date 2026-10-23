// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;

namespace UrDeck.Preview;

/// <summary>
/// One page of a configuration with its widgets made, as the deck's snapshot makes them: each widget of a type the preview
/// knows, attached to the preview's host and configured, and, for the page as the deck shows it, the dock's slots and the
/// page indicator. A widget of an unknown type leaves its cell empty.
/// </summary>
internal sealed class PreviewPage : IDisposable
{
    private PreviewPage()
    {
    }

    public List<(WidgetConfig Config, IWidget Widget)> Widgets { get; } = [];

    /// <summary>The dock's slots, an empty one without a widget; null for the page alone.</summary>
    public List<(WidgetConfig Config, IWidget? Widget)>? Dock { get; private set; }

    /// <summary>The page indicator; null for the page alone.</summary>
    public PageRenderer.Chrome? Chrome { get; private set; }

    /// <summary>Every reading the page's widgets and the dock's subscribe to.</summary>
    public IReadOnlyList<string> Subscriptions =>
        Widgets.Select(w => w.Widget).Concat(Dock?.Select(d => d.Widget).OfType<IWidget>() ?? [])
            .SelectMany(w => w.Subscriptions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static PreviewPage Build(UrDeckConfig config, int pageIndex, bool withDock, IWidgetHost host)
    {
        var page = new PreviewPage();
        foreach (var widgetConfig in config.Pages[pageIndex].Widgets)
        {
            if (PreviewWidgets.Create(widgetConfig, host) is { } widget)
                page.Widgets.Add((widget.Config, widget));
        }

        if (withDock)
        {
            page.Dock = [];
            foreach (var entry in DockLayout.Select(config.Dock, PreviewWidgets.Get))
                page.Dock.Add((entry.Config, entry.Shown ? PreviewWidgets.Create(entry.Config, host) : null));
            page.Chrome = new PageRenderer.Chrome(config.Pager.GetIndicatorMode(), pageIndex, config.Pages.Count);
        }

        return page;
    }

    public void Dispose()
    {
        foreach (var widget in Widgets.Select(w => w.Widget).Concat(Dock?.Select(d => d.Widget).OfType<IWidget>() ?? []))
            (widget as IDisposable)?.Dispose();
    }
}
