// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Sdk;

/// <summary>
/// Non-generic widget contract the host talks to. Widget authors normally derive from
/// <see cref="Widget{TConfig}"/> rather than implementing this directly.
/// </summary>
public interface IWidget
{
    string Name { get; }
    string Description { get; }
    string Category { get; }
    System.Drawing.Size[] SupportedSizes { get; }
    Type ConfigType { get; }
    WidgetConfig Config { get; }

    /// <summary>Applies a configuration; <paramref name="config"/> must be an instance of <see cref="ConfigType"/>.</summary>
    void Configure(WidgetConfig config);

    /// <summary>
    /// Receives the services the host offers the widget. Called once after the widget is created and before the first
    /// <see cref="Configure"/>. The default does nothing. Do not keep the host beyond the widget's own lifetime.
    /// </summary>
    void Attach(IWidgetHost host) { }

    /// <summary>
    /// The ids of the readings the widget uses (<c>provider:path</c>), computed from its configuration. The host reads
    /// it after each <see cref="Configure"/>, subscribes while the widget is shown and repaints the widget (through
    /// <see cref="NeedsRender"/>, without <see cref="UpdateAsync"/>) when one of them changes. Must be cheap, free of
    /// side effects and return the same ids until the configuration changes. Empty by default.
    /// </summary>
    IReadOnlyCollection<string> Subscriptions => [];

    /// <summary>
    /// Refreshes widget data (network, sensors, ...). Called off the render path on each refresh
    /// before the widget is redrawn. Must not touch the canvas.
    /// </summary>
    Task UpdateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether anything visible changed since the last paint. The host asks after each refresh (on the UI
    /// thread) and skips the repaint when this returns <c>false</c>; it always paints a widget when it is first
    /// placed. The default repaints on every refresh. Must be cheap.
    /// </summary>
    bool NeedsRender(DateTime now) => true;

    /// <summary>
    /// Whether the widget is in the middle of an animation. The host reads it after each paint; while it returns
    /// <c>true</c> the host repaints the widget at its own frame rate (without calling <see cref="UpdateAsync"/>), and
    /// stops after the paint that returns <c>false</c>. Draw from <see cref="WidgetRenderContext.Time"/>, never a frame
    /// count, and draw the resting state on the first paint and in the paint after which this first returns <c>false</c>.
    /// Must be cheap and free of side effects.
    /// </summary>
    bool IsAnimating => false;

    /// <summary>
    /// The time between frames the host should use while <see cref="IsAnimating"/> is true. Zero (the default) is the host's
    /// own rate. A slow animation looks the same at a lower rate and costs proportionally less.
    /// </summary>
    TimeSpan AnimationFrameInterval => TimeSpan.Zero;

    /// <summary>
    /// When the widget is not animating but will want to start again (an animation that plays now and then), the time of
    /// the next start; the host paints the widget then, without a refresh. Null (the default) means never. Read after each
    /// paint; must be cheap and free of side effects.
    /// </summary>
    DateTime? NextAnimationAt => null;

    /// <summary>
    /// Draws the widget. Called on the UI thread; must be synchronous and fast. The canvas is only
    /// valid for the duration of the call.
    /// </summary>
    void Render(WidgetRenderContext context);
}

public interface IWidget<TConfig> : IWidget where TConfig : WidgetConfig, new()
{
    new TConfig Config { get; }
    TConfig DefaultConfig { get; }
}
