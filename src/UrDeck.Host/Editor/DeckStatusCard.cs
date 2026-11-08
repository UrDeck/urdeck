// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Config;

namespace UrDeck.Host.Editor;

/// <summary>
/// The deck status in the navigation pane's footer: a dot and a line (up to date, saving, waiting, could not save, read only)
/// and a second line saying where the deck is or what went wrong.
/// </summary>
internal sealed class DeckStatusCard
{
    private readonly ISettingsHost _host;
    private readonly Ellipse _dot = new() { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
    private readonly TextBlock _line = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _text = new() { Spacing = 2 };
    private string? _writeError;

    public DeckStatusCard(ISettingsHost host)
    {
        _host = host;
        var text = _text;
        text.Children.Add(_line);
        text.Children.Add(_detail);
        var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 8, 12, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(_dot);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Root = row;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(_line, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        Refresh();
    }

    public FrameworkElement Root { get; }

    /// <summary>
    /// In the narrow (icons only) navigation pane the card is only its dot, at the icons' place; the lines are its tooltip.
    /// </summary>
    public void SetCompact(bool compact)
    {
        _text.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ((Grid)Root).Padding = compact ? new Thickness(18, 8, 8, 12) : new Thickness(12, 8, 12, 12);
    }

    /// <summary>A write failed; the card says so until the next write succeeds.</summary>
    public void WriteFailed(ConfigProblem problem)
    {
        _writeError = problem.Message;
        Refresh();
    }

    /// <summary>A write succeeded.</summary>
    public void WriteSucceeded()
    {
        _writeError = null;
        Refresh();
    }

    public void Refresh()
    {
        var document = _host.Document;
        var deck = _host.DeckState;
        string where = deck.DisplayName ?? "the deck's display";
        if (document.IsReadOnly)
            Show("#9a9a9a", "Read only", "Written by a newer UrDeck.");
        else if (_writeError != null)
            Show("#d13438", "Could not save", $"{_writeError} Changes are kept and saved again with the next one.");
        else if (!deck.Shown)
            Show("#f7a400", "Deck is waiting", $"Waiting for {where}. Changes are saved.");
        else if (document.IsWritePending)
            Show("#0f7bd8", "Saving…", $"Shown on {where}. Changes reach it as you make them.");
        else
            Show("#13a10e", "Deck is up to date", $"Shown on {where}. Changes reach it as you make them.");
    }

    private void Show(string colour, string line, string detail)
    {
        var c = System.Drawing.ColorTranslator.FromHtml(colour);
        _dot.Fill = new SolidColorBrush(ColorHelper.FromArgb(255, c.R, c.G, c.B));
        _line.Text = line;
        _detail.Text = detail;
        ToolTipService.SetToolTip(Root, $"{line}. {detail}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(Root, $"{line}. {detail}");
    }
}
