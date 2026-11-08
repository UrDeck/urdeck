// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Layout;

namespace UrDeck.Host.Editor;

/// <summary>The pane beside the preview: the palette, a widget's properties, a dock slot's, or the page's (D6, D7).</summary>
internal sealed partial class PagesSection
{
    private void BuildPane()
    {
        var config = Config;
        string key = _paletteOpen ? $"palette|{_paletteAt}|{_paletteMessage}|{_paletteDock}"
            : _selection is { } s ? $"widget|{s.Ref}|{s.Key}|{SizeOf(s.Ref)}|{_fit?.W}x{_fit?.H}|{Editable}|{config.Dock.Count}|{_arrangeMessage}"
            : $"page|{_pageId}|{config.Pages.Count}|{config.StartPage}|{config.Pager.Indicator}|{config.Dock.Count}|{PageEditing.PageIndex(config, _pageId)}|{Editable}";
        if (key == _paneKey)
        {
            // Only the name can change under the same pane; never overwrite what the user is typing.
            if (_nameBox != null && _nameBox.FocusState == FocusState.Unfocused && PageEditing.PageIndex(config, _pageId) is int i and >= 0)
                _nameBox.Text = config.Pages[i].Name;
            return;
        }

        _paneKey = key;
        _nameBox = null;
        _form?.Dispose();
        _form = null;
        var panel = new StackPanel { Spacing = 12 };
        if (_paletteOpen)
            BuildPalette(panel);
        else if (_selection is { } selection && PageEditing.Widget(config, selection.Ref) is { } widget)
            BuildWidgetPane(panel, selection.Ref, widget);
        else
            BuildPagePane(panel);
        _paneHost.Child = panel;
    }

    private void BuildPalette(StackPanel panel)
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = _paletteDock ? "Add to the dock" : "Add a widget", FontSize = 20, FontWeight = FontWeights.SemiBold });
        var close = new Button { Content = new SymbolIcon(Symbol.Cancel) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, "Close");
        close.Click += (_, _) => CloseTransient();
        Grid.SetColumn(close, 1);
        head.Children.Add(close);
        panel.Children.Add(head);
        if (_paletteMessage != null)
            panel.Children.Add(new InfoBar { Severity = InfoBarSeverity.Warning, IsOpen = true, IsClosable = false, Message = _paletteMessage });

        if (_paletteDock)
            panel.Children.Add(Sections.Caption("A dock slot is 1x1 and the same on every page, so only widgets with a 1x1 size are listed."));
        foreach (var type in _host.WidgetTypes)
        {
            var d = type.Descriptor;
            if (_paletteDock && !d.SupportedSizes.Contains(new System.Drawing.Size(1, 1)))
                continue;
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = d.Name, FontWeight = FontWeights.SemiBold });
            text.Children.Add(Sections.Caption(d.Description));
            text.Children.Add(Sections.Caption(string.Join(" · ", d.SupportedSizes.Select(z => $"{z.Width}x{z.Height}"))));
            if (!type.FirstParty)
                text.Children.Add(Sections.Caption($"Plugin: {type.PluginName} {type.PluginVersion}"));
            var row = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            string id = d.Id;
            row.Click += (_, _) => AddWidget(id);
            panel.Children.Add(row);
        }

        panel.Children.Add(new Button { Content = "Web page · coming later", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch });
    }

    private void BuildWidgetPane(StackPanel panel, WidgetRef at, Sdk.WidgetConfig widget)
    {
        var descriptor = _host.DescribeWidget(widget.WidgetTypeId);
        panel.Children.Add(new TextBlock { Text = descriptor?.Name ?? "Not installed", FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = widget.WidgetTypeId, FontFamily = new FontFamily("Consolas"), FontSize = 12, Opacity = 0.7 });
        if (descriptor != null && at.InDock)
            panel.Children.Add(Sections.Caption("In the dock: the same on every page, drawn smaller, always 1x1."));
        else if (descriptor != null)
        {
            panel.Children.Add(Sections.Label("Size"));
            // Wraps into as many rows as the sizes need: a widget may declare one size or fifty.
            var sizes = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 66, ItemHeight = 84 };
            var ordered = descriptor.SupportedSizes.OrderBy(z => z.Width * z.Height).ThenBy(z => z.Width).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var (w, h) = (ordered[i].Width, ordered[i].Height);
                sizes.Children.Add(SizeChip(at, w, h, widget.Width == w && widget.Height == h, i + 1, ordered.Count));
            }

            panel.Children.Add(sizes);
            if (_fit is { } fit && fit.Widget == at)
            {
                var bar = new InfoBar { Severity = InfoBarSeverity.Warning, IsOpen = true, IsClosable = false, Title = fit.Problem.Title, Message = fit.Problem.Message };
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (fit.Problem.FirstFree != null)
                {
                    var move = new Button { Content = "Move it to the first free space" };
                    move.Click += (_, _) => MoveToFirstFree();
                    buttons.Children.Add(move);
                }

                var keep = new Button { Content = "Keep the size" };
                keep.Click += (_, _) =>
                {
                    _fit = null;
                    _paneKey = null;
                    BuildPane();
                };
                buttons.Children.Add(keep);
                bar.Content = buttons;
                panel.Children.Add(bar);
            }
        }

        if (descriptor != null)
            BuildArrangeEntries(panel, at, widget);

        if (descriptor != null)
        {
            panel.Children.Add(Sections.Label("Settings"));
            if (descriptor.Settings.Settings.Count == 0)
            {
                panel.Children.Add(Sections.Caption("This widget has no settings."));
            }
            else
            {
                // The dock draws a slot as 1x1 whatever its configuration says (settings for other sizes are dimmed accordingly).
                string sizeKey = at.InDock ? "1x1" : $"{widget.Width}x{widget.Height}";
                var context = new Forms.FormContext(_host.Document, sizeKey, _readings, _icons, _owner, _chooser, _setUp);
                _form = new Forms.FormBuilder(context).Build(descriptor.Settings, ObjectPath(at));
                panel.Children.Add(_form.Root);
            }
        }

        var remove = new Button
        {
            Content = "Remove",
            IsEnabled = Editable,
            Margin = new Thickness(0, 16, 0, 0),
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        remove.Click += (_, _) => RemoveSelected();
        panel.Children.Add(remove);
    }

    /// <summary>The menu equivalents of the drags (D4): to the dock and back, and along the dock.</summary>
    private void BuildArrangeEntries(StackPanel panel, WidgetRef at, Sdk.WidgetConfig widget)
    {
        var config = Config;
        panel.Children.Add(Sections.Label("Arrange"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (at.InDock)
        {
            var toPage = new Button { Content = "Move to the page", IsEnabled = Editable && _pageId != null };
            toPage.Click += (_, _) => ArrangeResult(_editing.MoveToPage(at.Index, _pageId!, CellGrid));
            var earlier = new Button { Content = "Move earlier", IsEnabled = Editable && at.Index > 0 };
            earlier.Click += (_, _) => ArrangeResult(_editing.MoveInDock(at.Index, at.Index - 1));
            var later = new Button { Content = "Move later", IsEnabled = Editable && at.Index < config.Dock.Count - 1 };
            later.Click += (_, _) => ArrangeResult(_editing.MoveInDock(at.Index, at.Index + 1));
            row.Children.Add(toPage);
            row.Children.Add(earlier);
            row.Children.Add(later);
        }
        else
        {
            bool supports = _editing.SupportsDock(widget);
            bool room = config.Dock.Count < DockLayout.MaxSlots;
            var toDock = new Button { Content = "Move to the dock", IsEnabled = Editable && supports && room };
            toDock.Click += (_, _) => ArrangeResult(_editing.MoveToDock(at, config.Dock.Count));
            row.Children.Add(toDock);
            var others = config.Pages.Where(p => p.Id is not null && p.Id != at.PageId).ToList();
            var toPage = new DropDownButton { Content = "Move to page", IsEnabled = Editable && others.Count > 0 };
            var menu = new MenuFlyout();
            foreach (var page in others)
            {
                string id = page.Id!;
                var item = new MenuFlyoutItem { Text = page.Name };
                item.Click += (_, _) => MoveToOtherPage(at, id);
                menu.Items.Add(item);
            }

            toPage.Flyout = menu;
            row.Children.Add(toPage);
        }

        panel.Children.Add(row);
        if (!at.InDock && !_editing.SupportsDock(widget))
            panel.Children.Add(Sections.Caption("This widget has no 1x1 size, so it cannot go in the dock."));
        else if (!at.InDock && config.Dock.Count >= DockLayout.MaxSlots)
            panel.Children.Add(Sections.Caption("The dock is full."));
        if (_arrangeMessage != null)
            panel.Children.Add(new InfoBar { Severity = InfoBarSeverity.Warning, IsOpen = true, IsClosable = false, Message = _arrangeMessage });
        panel.Children.Add(Sections.Caption("You can also drag a card, or hold Ctrl and press an arrow key."));
    }

    // The editor follows the widget to its new page and selects it there.
    private void MoveToOtherPage(WidgetRef at, string pageId)
    {
        var outcome = _editing.MoveWidgetToPage(at, pageId, CellGrid);
        if (outcome.Applied && outcome.Widget != null)
        {
            _arrangeMessage = null;
            SelectPage(pageId);
            _selection = WidgetSelection.Of(Config, outcome.Widget);
            Refresh();
        }
        else
        {
            ArrangeResult(outcome);
        }
    }

    private void ArrangeResult(EditOutcome outcome)
    {
        if (outcome.Applied && outcome.Widget != null)
        {
            _selection = WidgetSelection.Of(Config, outcome.Widget);
            _arrangeMessage = null;
            Refresh();
        }
        else
        {
            _arrangeMessage = outcome.Message ?? "That does not fit there.";
            _paneKey = null;
            BuildPane();
        }
    }

    /// <summary>The widget's object in the configuration document (<c>/pages/0/widgets/3</c>, <c>/dock/1</c>).</summary>
    private string ObjectPath(WidgetRef widget) =>
        widget.InDock ? $"/dock/{widget.Index}" : $"/pages/{PageEditing.PageIndex(Config, widget.PageId)}/widgets/{widget.Index}";

    private string SizeOf(WidgetRef widget) => PageEditing.Widget(Config, widget) is { } w ? $"{w.Width}x{w.Height}" : "";

    /// <summary>
    /// One size of the selector: the size's shape (a cell is 8 DIP, the shape at most 4 cells wide) above its name. The current
    /// size is filled with the accent and announced as selected; the row is a group of radio buttons for a screen reader.
    /// </summary>
    private Button SizeChip(WidgetRef at, int w, int h, bool current, int position, int count)
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        const double cell = 7;
        var shape = new Rectangle
        {
            Width = w * cell,
            Height = h * cell,
            RadiusX = 2,
            RadiusY = 2,
            Fill = current ? accent : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        var content = new StackPanel { Spacing = 6, Width = 44 };
        content.Children.Add(new Grid { Height = 4 * cell, Children = { shape } });
        content.Children.Add(new TextBlock { Text = $"{w}x{h}", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12, FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal });
        var chip = new Button
        {
            Content = content,
            Padding = new Thickness(6, 8, 6, 6),
            Margin = new Thickness(0, 0, 6, 6),
            IsEnabled = Editable,
            BorderThickness = new Thickness(current ? 2 : 1),
        };
        if (current)
        {
            chip.BorderBrush = accent;
            chip.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chip, current ? $"Size {w}x{h}, current" : $"Size {w}x{h}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetPositionInSet(chip, position);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetSizeOfSet(chip, count);
        chip.Click += (_, _) =>
        {
            if (!current)
                Resize(at, w, h);
        };
        return chip;
    }

    private void BuildPagePane(StackPanel panel)
    {
        var config = Config;
        int index = PageEditing.PageIndex(config, _pageId);
        if (index < 0 || _pageId is not { } id)
            return;
        var page = config.Pages[index];
        panel.Children.Add(new TextBlock { Text = "Page", FontSize = 20, FontWeight = FontWeights.SemiBold });

        _nameBox = new TextBox { Header = "Name", Text = page.Name, IsEnabled = Editable };
        _nameBox.TextChanged += (_, _) =>
        {
            if (_nameBox.FocusState != FocusState.Unfocused && _nameBox.Text.Length > 0)
                _editing.RenamePage(id, _nameBox.Text);
        };
        panel.Children.Add(_nameBox);

        panel.Children.Add(Sections.Label("Start page"));
        bool isStart = StartPageResolver.IsSet(config) ? config.StartPage == id : index == 0;
        if (isStart)
            panel.Children.Add(new TextBlock
            {
                Text = StartPageResolver.IsSet(config) ? "⌂ This is the start page" : "⌂ The deck starts on this page because no start page is chosen.",
                TextWrapping = TextWrapping.Wrap,
            });
        else
        {
            var make = new Button { Content = "Make this the start page", IsEnabled = Editable };
            make.Click += (_, _) => _editing.SetStartPage(id);
            panel.Children.Add(make);
        }

        panel.Children.Add(Sections.Label("Order"));
        var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var earlier = new Button { Content = "Move left", IsEnabled = Editable && index > 0 };
        earlier.Click += (_, _) => _editing.MovePage(id, -1);
        var later = new Button { Content = "Move right", IsEnabled = Editable && index < config.Pages.Count - 1 };
        later.Click += (_, _) => _editing.MovePage(id, 1);
        order.Children.Add(earlier);
        order.Children.Add(later);
        panel.Children.Add(order);

        var dots = new RadioButtons { Header = "Page dots on the deck", IsEnabled = Editable };
        foreach (string item in new[] { "Automatic", "Always", "Show then fade", "Off" })
            dots.Items.Add(item);
        dots.SelectedIndex = config.Pager.GetIndicatorMode() switch { IndicatorMode.Always => 1, IndicatorMode.Fade => 2, IndicatorMode.Off => 3, _ => 0 };
        dots.SelectionChanged += (_, _) => _editing.SetIndicator(dots.SelectedIndex switch
        {
            1 => IndicatorMode.Always,
            2 => IndicatorMode.Fade,
            3 => IndicatorMode.Off,
            _ => IndicatorMode.Auto,
        });
        panel.Children.Add(dots);
        panel.Children.Add(Sections.Caption("Applies to every page."));

        panel.Children.Add(Sections.Label("Dock"));
        panel.Children.Add(new TextBlock { Text = $"{Math.Min(config.Dock.Count, DockLayout.MaxSlots)} of {DockLayout.MaxSlots} slots" });
        var addToDock = new Button { Content = "Add to the dock…", IsEnabled = Editable && config.Dock.Count < DockLayout.MaxSlots };
        addToDock.Click += (_, _) => OpenPalette(null, dock: true);
        panel.Children.Add(addToDock);

        var duplicate = new Button { Content = "Duplicate this page", IsEnabled = Editable, Margin = new Thickness(0, 8, 0, 0) };
        duplicate.Click += (_, _) =>
        {
            if (_editing.DuplicatePage(id).PageId is { } copy)
                SelectPage(copy);
        };
        panel.Children.Add(duplicate);

        var delete = new Button { Content = "Delete this page", IsEnabled = Editable && config.Pages.Count > 1, Margin = new Thickness(0, 16, 0, 0) };
        delete.Click += (_, _) => _ = DeletePageAsync(id);
        panel.Children.Add(delete);
    }
}
