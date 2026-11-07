// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Plugin;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>settings-metadata, "Positions": what Stats declares matches what it draws at every size it supports.</summary>
public class StatsPositionsTests
{
    private static string Role(StatsWidget.PositionKind kind) => kind switch
    {
        StatsWidget.PositionKind.Gauge => "gauge",
        StatsWidget.PositionKind.Text => "text",
        _ => "reading",
    };

    [Fact]
    public void The_declared_positions_match_what_Compose_builds_at_each_declared_size()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(StatsWidget), out string? reason)!;
        Assert.True(descriptor != null, reason);
        var slots = descriptor.Settings.Settings.Single(s => s.Key == "slots");

        Assert.NotEmpty(descriptor.SupportedSizes);
        foreach (var size in descriptor.SupportedSizes)
        {
            var widget = new StatsWidget();
            widget.Configure(new StatsConfig
            {
                Width = size.Width,
                Height = size.Height,
                Slots = [.. Enumerable.Range(0, 9).Select(i => new StatsSlot { Reading = "system:cpu/core/" + (i + 1) + "/load" })],
            });

            var declared = slots.Positions!.RolesAt($"{size.Width}x{size.Height}");
            Assert.True(declared != null, $"no positions declared for {size.Width}x{size.Height}");
            Assert.Equal(declared, widget.Compose().Select(p => Role(p.Kind)));
        }
    }

    [Fact]
    public void Stats_supports_two_by_one()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(StatsWidget), out _)!;

        Assert.Equal(["1x1", "2x1", "2x2", "4x2", "4x4"], descriptor.SupportedSizes.Select(s => $"{s.Width}x{s.Height}"));
    }

    [Fact]
    public void The_gauge_style_is_disabled_in_the_text_role()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(StatsWidget), out _)!;
        var item = descriptor.Settings.Settings.Single(s => s.Key == "slots").Item!;

        Assert.Equal(["text"], item.Settings.Single(s => s.Key == "style").DisabledInRoles);
        Assert.Equal(["Range", "Levels"], item.Settings.Select(s => s.Group).Where(g => g != null).Distinct());
    }
}
