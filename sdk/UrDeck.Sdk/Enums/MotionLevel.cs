// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

/// <summary>
/// How much the deck may move, as the user and the theme have set it (<see cref="WidgetRenderContext.Motion"/>). A widget
/// or component that moves its own content stops that motion below <see cref="Full"/>.
/// </summary>
public enum MotionLevel
{
    Off = 0,
    Reduced = 1,
    Full = 2,
}
