// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SdkContractTests
{
    [Fact]
    public void SdkAssemblyVersion_IsFrozen()
    {
        // Plugins bind to the host's UrDeck.Sdk by assembly version; a bump makes already-built plugins load a second
        // SDK copy and silently fail. Only change this together with a deliberate, announced breaking SDK change.
        var version = typeof(Widget<>).Assembly.GetName().Version;
        Assert.Equal(new Version(0, 2, 0, 0), version);
    }

    [Fact]
    public void NeedsRender_DefaultsToTrue()
    {
        // A widget that does not override it is repainted on every refresh.
        IWidget widget = new PlainWidget();

        Assert.True(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void IsAnimating_DefaultsToFalse()
    {
        IWidget widget = new PlainWidget();

        Assert.False(widget.IsAnimating);
    }

    private sealed class PlainWidget : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }
}
