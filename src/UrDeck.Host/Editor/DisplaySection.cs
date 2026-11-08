// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Display;

namespace UrDeck.Host.Editor;

/// <summary>
/// The Display section: the monitor picker. One row per connected display (its name, orientation, Primary, its position
/// and its number; never a resolution), a row for a remembered display that is not connected, Identify and Choose
/// automatically. It refreshes when the host says the displays changed, after they stop changing.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The markers are disposed in Close.")]
internal sealed class DisplaySection
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan IdentifyTime = TimeSpan.FromSeconds(2);

    private readonly ISettingsHost _host;
    private readonly StackPanel _rows = new() { Spacing = 8 };
    private readonly Button _identify = new() { Content = "Identify" };
    private readonly Button _automatic = new() { Content = "Choose automatically" };
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly DispatcherQueueTimer _identifyTimer;
    private readonly IdentifyMarkers _markers = new();
    private bool _closed;

    public DisplaySection(ISettingsHost host, DispatcherQueue queue)
    {
        _host = host;
        _refreshTimer = queue.CreateTimer();
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Interval = RefreshDelay;
        _refreshTimer.Tick += OnRefreshTick;
        _identifyTimer = queue.CreateTimer();
        _identifyTimer.IsRepeating = false;
        _identifyTimer.Interval = IdentifyTime;
        _identifyTimer.Tick += OnIdentifyTick;

        _identify.Click += (_, _) =>
        {
            _markers.Show(List.Rows);
            _identifyTimer.Stop();
            _identifyTimer.Start();
        };
        _automatic.Click += (_, _) => _host.ChooseAutomatically();

        var root = new StackPanel { Spacing = 16, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(Sections.Header("Display", "Choose the display the deck is shown on."));
        root.Children.Add(_rows);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_identify);
        buttons.Children.Add(_automatic);
        root.Children.Add(buttons);
        Root = new ScrollViewer { Content = root, Padding = new Thickness(32, 24, 32, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        _host.Changed += OnHostChanged;
        Refresh();
    }

    public UIElement Root { get; }

    /// <summary>The displays as the picker shows them now.</summary>
    public DisplayList List { get; private set; } = new([], false);

    private void OnRefreshTick(DispatcherQueueTimer sender, object args) => Refresh();

    private void OnIdentifyTick(DispatcherQueueTimer sender, object args) => _markers.Hide();

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

        List = _host.GetDisplayList();
        _rows.Children.Clear();
        foreach (var row in List.Rows)
            _rows.Children.Add(BuildRow(row));
        if (List.Rows.Count == 0)
            _rows.Children.Add(new TextBlock { Text = "No display is connected.", Opacity = 0.7 });
        _automatic.IsEnabled = List.CanChooseAutomatically;
        _identify.IsEnabled = List.Rows.Any(r => r.Display != null);
    }

    private Button BuildRow(DisplayRow row)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = row.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

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
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{row.Name}, {Describe(row)}");
        if (row.Display is { } display)
            button.Click += (_, _) => _host.ChooseDisplay(display);
        return button;
    }

    /// <summary>Orientation, Primary and the position; never a resolution or a scaling.</summary>
    private static string Describe(DisplayRow row)
    {
        if (row.IsWaiting)
            return "Not connected. The deck is waiting for this display.";

        var parts = new List<string> { row.Orientation == DisplayOrientation.Portrait ? "Portrait" : "Landscape" };
        if (row.IsPrimary)
            parts.Add("Primary");
        if (row.RelativePosition != null)
            parts.Add(row.RelativePosition);
        return string.Join(" · ", parts);
    }

    public void Close()
    {
        _closed = true;
        _host.Changed -= OnHostChanged;
        _refreshTimer.Stop();
        _identifyTimer.Stop();
        // A timer holds its handler from native code: an attached one would keep the section and the window alive.
        _refreshTimer.Tick -= OnRefreshTick;
        _identifyTimer.Tick -= OnIdentifyTick;
        _markers.Dispose();
        _rows.Children.Clear();
    }
}

/// <summary>Bits every section uses.</summary>
internal static class Sections
{
    /// <summary>A section's title and its one line of explanation.</summary>
    public static StackPanel Header(string title, string? line = null)
    {
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeights.SemiBold });
        if (line != null)
            header.Children.Add(new TextBlock { Text = line, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        return header;
    }

    /// <summary>A small grey caption.</summary>
    public static TextBlock Caption(string text) => new() { Text = text, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };

    /// <summary>A label above a group of controls.</summary>
    public static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
}
