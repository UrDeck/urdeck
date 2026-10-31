// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Startup;

namespace UrDeck.Host;

/// <summary>
/// The settings window: an ordinary window that takes focus and has a taskbar button while it is open. It has one page,
/// the monitor picker and the providers' settings. It talks to the host only through <see cref="ISettingsHost"/>, and the application drops every
/// reference to it when it closes (a new one is made for each opening), so nothing of it stays behind.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The markers are disposed when the window closes.")]
internal sealed class SettingsWindow : Window
{
    private const double WidthDip = 560;
    private const double HeightDip = 720;
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan IdentifyTime = TimeSpan.FromSeconds(2);

    private readonly ISettingsHost _host;
    private readonly StackPanel _rows = new() { Spacing = 8 };
    private readonly ProvidersSection _providers;
    private readonly Button _identify = new() { Content = "Identify" };
    private readonly Button _automatic = new() { Content = "Choose automatically" };
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly DispatcherQueueTimer _identifyTimer;
    private readonly IdentifyMarkers _markers = new();
    private DisplayList _list = new([], false);
    private bool _closed;

    public SettingsWindow(ISettingsHost host, string iconPath)
    {
        _host = host;
        _providers = new ProvidersSection(host);
        Title = "UrDeck Settings";

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Interval = RefreshDelay;
        _refreshTimer.Tick += (_, _) => Refresh();
        _identifyTimer = DispatcherQueue.CreateTimer();
        _identifyTimer.IsRepeating = false;
        _identifyTimer.Interval = IdentifyTime;
        _identifyTimer.Tick += (_, _) => _markers.Hide();

        _identify.Click += (_, _) =>
        {
            _markers.Show(_list.Rows);
            _identifyTimer.Stop();
            _identifyTimer.Start();
        };
        _automatic.Click += (_, _) => _host.ChooseAutomatically();

        Content = BuildContent();

        _host.Changed += OnHostChanged;
        Closed += OnClosed;
        Refresh();
    }

    /// <summary>Places the window on a display away from the deck and shows it with focus.</summary>
    public void Open()
    {
        var display = SettingsWindowPlacement.Choose(_list, MonitorPlacement.GetCursor());
        if (display != null)
        {
            // Sized in device-independent pixels of the display it opens on; the window is placed in physical pixels.
            int width = (int)Math.Round(WidthDip * display.Scaling);
            int height = Math.Min((int)Math.Round(HeightDip * display.Scaling), display.Bounds.Height);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                display.Bounds.X + ((display.Bounds.Width - width) / 2),
                display.Bounds.Y + ((display.Bounds.Height - height) / 2),
                width,
                height));
            UrDeckLog.Info($"Settings window opened on {display.Name}");
        }

        Show();
    }

    /// <summary>The window is already open: restore it if it was minimised, bring it to the front and give it focus.</summary>
    public void BringForward()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Show();
    }

    /// <summary>Activates the window and asks Windows for the foreground too: activation alone can leave it behind other windows.</summary>
    private void Show()
    {
        Activate();
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (!SetForegroundWindow(hwnd))
            UrDeckLog.Info("Settings window: Windows did not give it the foreground; it is active but may be behind another window");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    private Grid BuildContent()
    {
        bool dark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
        var root = new Grid
        {
            Padding = new Thickness(24),
            RowSpacing = 16,
            Background = new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 32, 32, 32) : ColorHelper.FromArgb(255, 243, 243, 243)),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
        };

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = "Display", FontSize = 24, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = "Choose the display the deck is shown on.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(header);

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(_rows);
        content.Children.Add(_providers.Root);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_identify);
        buttons.Children.Add(_automatic);
        buttons.Children.Add(new TextBlock
        {
            Text = $"UrDeck {AppVersion.WithoutMetadata(Program.Version)}",
            Opacity = 0.5,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) =>
        {
            e.Handled = true;
            Close();
        };
        root.KeyboardAccelerators.Add(escape);
        return root;
    }

    private void OnHostChanged()
    {
        // Displays change in bursts and settle late; look once after they stop.
        if (_closed)
            return;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private void Refresh()
    {
        if (_closed)
            return;

        _list = _host.GetDisplayList();
        _rows.Children.Clear();
        foreach (var row in _list.Rows)
            _rows.Children.Add(BuildRow(row));
        if (_list.Rows.Count == 0)
            _rows.Children.Add(new TextBlock { Text = "No display is connected.", Opacity = 0.7 });
        _automatic.IsEnabled = _list.CanChooseAutomatically;
        _identify.IsEnabled = _list.Rows.Any(r => r.Display != null);
    }

    private Button BuildRow(DisplayRow row)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var number = new TextBlock
        {
            Text = row.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(number);

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = row.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = Describe(row), Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (row.IsDeck)
        {
            var mark = new TextBlock { Text = "Deck is here", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(mark, 2);
            grid.Children.Add(mark);
        }

        var button = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 12, 16, 12),
            IsEnabled = !row.IsWaiting,
        };
        if (row.Display is { } display)
            button.Click += (_, _) => _host.ChooseDisplay(display);
        return button;
    }

    private static string Describe(DisplayRow row)
    {
        if (row.IsWaiting)
            return "Not connected. The deck is waiting for this display.";

        var parts = new List<string>
        {
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{row.Width} × {row.Height}"),
            row.Orientation == DisplayOrientation.Portrait ? "Portrait" : "Landscape",
        };
        if (row.IsPrimary)
            parts.Add("Primary");
        if (row.RelativePosition != null)
            parts.Add(row.RelativePosition);
        return string.Join(" · ", parts);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _host.Changed -= OnHostChanged;
        _refreshTimer.Stop();
        _identifyTimer.Stop();
        _markers.Dispose();
        _rows.Children.Clear();
        _providers.Close();
        UrDeckLog.Info("Settings window closed");
    }
}
