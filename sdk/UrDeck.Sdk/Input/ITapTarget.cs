// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Input;

/// <summary>
/// Implemented by a widget, beside <see cref="IWidget"/>, that reacts to taps. Implementing it is the declaration: the
/// host calls nothing for input on a widget that does not. Points are in the widget's own pixels, the space of
/// <see cref="WidgetRenderContext.PixelSize"/>, with <c>(0, 0)</c> at the top-left corner of the card.
/// <para>
/// The host shows the press: while the pointer is down the whole card shrinks and dims as the theme says. A widget draws
/// no pressed state and is not repainted for a press or a release.
/// </para>
/// <para>
/// Other kinds of input (scrolling, a long press) are further interfaces beside this one; a widget that implements only
/// this one is never sent them.
/// </para>
/// </summary>
public interface ITapTarget
{
    /// <summary>
    /// Whether a tap at <paramref name="point"/> would do something. The host asks when the pointer goes down, to decide
    /// whether to show the press, and again before it delivers the tap. Must be cheap and free of side effects. The default
    /// accepts every point.
    /// </summary>
    bool CanTap(SKPoint point) => true;

    /// <summary>
    /// Performs the tap's action. Called on the UI thread when the tap is recognised (a short press and release that did
    /// not become a swipe); must return quickly. It may call the host's launch service. Afterwards the host asks
    /// <see cref="IWidget.NeedsRender"/> and repaints the widget when it returns <c>true</c>.
    /// </summary>
    void OnTap(SKPoint point);
}
