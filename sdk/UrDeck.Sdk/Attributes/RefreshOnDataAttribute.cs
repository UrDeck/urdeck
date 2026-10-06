// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

/// <summary>
/// The widget has no timer: it is painted once when shown and then whenever one of the readings it declared through
/// <see cref="IWidget.Subscriptions"/> changes and <see cref="IWidget.NeedsRender"/> agrees.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RefreshOnDataAttribute : Attribute
{
}
