// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace UrDeck.Host.Editor;

/// <summary>
/// The handle at the left edge of the properties pane: dragging it makes the pane wider or narrower (the preview takes the
/// rest), and with focus the left and right arrow keys do the same in steps. The width lasts while the window is open.
/// </summary>
internal sealed class PaneSplitter : ContentControl
{
    /// <summary>Wide enough for five size chips in a row.</summary>
    public const double DefaultWidth = 380;

    private const double PaneMin = 300;
    private const double PaneMax = 720;
    private const double Step = 24;

    private readonly FrameworkElement _pane;
    private readonly Rectangle _grip;
    private double? _startX;
    private double _startWidth;

    public PaneSplitter(FrameworkElement pane)
    {
        _pane = pane;
        Width = 8;
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _grip = new Rectangle { Width = 2, Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] };
        Content = _grip;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        AutomationProperties.SetName(this, "Resize the properties pane");
        ToolTipService.SetToolTip(this, "Drag to resize the pane");

        PointerEntered += (_, _) => _grip.Fill = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        PointerExited += (_, _) =>
        {
            if (_startX == null)
                _grip.Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
        };
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => _startX = null;
        KeyDown += OnKey;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _startX = e.GetCurrentPoint(null).Position.X;
        _startWidth = _pane.ActualWidth;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_startX is not { } start)
            return;
        // The pane is at the right: dragging the handle to the left makes it wider.
        SetWidth(_startWidth + (start - e.GetCurrentPoint(null).Position.X));
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        _startX = null;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                SetWidth(_pane.ActualWidth + Step);
                break;
            case Windows.System.VirtualKey.Right:
                SetWidth(_pane.ActualWidth - Step);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void SetWidth(double width) => _pane.Width = Math.Clamp(width, PaneMin, PaneMax);
}
