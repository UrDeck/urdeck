// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;

namespace UrDeck.Engine.Plugin;

/// <summary>
/// The host services of one widget. Every widget gets its own, so that what a widget asks for (an icon) can be tied to
/// it: repainted when it arrives and released when the widget goes. It holds no reference to the widget, so it never
/// keeps a plugin alive; whoever shows the widget subscribes to <see cref="RepaintRequested"/> and disposes this object
/// with the view.
/// </summary>
public sealed class WidgetServices : IWidgetHost, IIconSource, IDisposable
{
    private readonly IconService? _icons;
    private volatile bool _disposed;

    internal WidgetServices(IReadingSource readings, ILauncher launcher, IconService? icons)
    {
        Readings = readings;
        Launcher = launcher;
        _icons = icons;
    }

    public IReadingSource Readings { get; }

    public ILauncher Launcher { get; }

    public IIconSource Icons => this;

    public void Log(string message) => UrDeckLog.Info(message);

    /// <summary>
    /// Raised when something the widget asked for changed (an icon became ready or was given up on) and the widget
    /// should be painted again. Raised on a worker thread: marshal to the UI thread and repaint, without asking
    /// <c>NeedsRender</c>.
    /// </summary>
    public event Action? RepaintRequested;

    IconResult IIconSource.GetIcon(string source, int pixelSize) =>
        _disposed || _icons == null ? IconResult.None : _icons.Get(this, source, pixelSize);

    internal void RequestRepaint()
    {
        if (!_disposed)
            RepaintRequested?.Invoke();
    }

    /// <summary>Releases what the widget holds (its icons) and detaches every listener.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        RepaintRequested = null;
        _icons?.Release(this);
    }
}
