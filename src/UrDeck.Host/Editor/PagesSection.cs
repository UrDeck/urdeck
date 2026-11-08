// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Layout;
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;

namespace UrDeck.Host.Editor;

/// <summary>
/// The Pages section: the page tabs, the preview of the selected page (the deck's own rendering, see
/// <see cref="PreviewPresenter"/>) with its zoom, grid and readings switches, the selection, and the properties pane or the
/// palette beside it. Every change is one transaction of the document through <see cref="PageEditing"/>; after each change
/// what is shown is read again from the document, and the selection is found again by its key.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The preview is disposed in Close, when the window closes.")]
internal sealed partial class PagesSection
{
    private const double FitWidthCellMax = 120;
    private const double StageMargin = 24;
    private const double BezelPadding = 12;

    private readonly ISettingsHost _host;
    private readonly Window _owner;
    private readonly PageEditing _editing;
    private readonly DispatcherQueue _queue;
    private readonly PreviewPresenter _preview;
    private readonly Grid _root = new();
    private readonly InfoBar _outsideBar;
    private readonly InfoBar _invalidBar;
    private readonly InfoBar _newerBar;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly Button _addWidget = new() { Content = "Add widget" };
    private readonly Button _undo = new() { Content = new SymbolIcon(Symbol.Undo) };
    private readonly Button _redo = new() { Content = new SymbolIcon(Symbol.Redo) };
    private readonly Grid _stage = new();
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ZoomMode = ZoomMode.Disabled };
    private readonly Border _bezel = new() { Padding = new Thickness(BezelPadding), CornerRadius = new CornerRadius(18), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(StageMargin), Background = new SolidColorBrush(ColorHelper.FromArgb(255, 18, 18, 20)) };
    private readonly Grid _page = new();
    private readonly Canvas _overlay = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly ContentControl _focusTarget = new() { IsTabStop = true, UseSystemFocusVisuals = true, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly ToggleButton _gridToggle = new() { Padding = new Thickness(12, 5, 12, 5) };
    private readonly Border _paneHost = new() { Width = PaneSplitter.DefaultWidth, Padding = new Thickness(20, 16, 20, 16) };

    private string? _pageId;
    private WidgetSelection? _selection;
    private bool _wholePage;
    private bool _paletteOpen;
    private Cell? _paletteAt;
    private string? _paletteMessage;
    private (WidgetRef Widget, int W, int H, FitProblem Problem)? _fit;
    private string? _paneKey;
    private readonly Forms.IEditorReadings _readings;
    private readonly Forms.IIconCatalog _icons;
    private readonly Forms.IReadingChooser _chooser;
    private readonly Action<string> _setUp;
    private Forms.FormView? _form;
    private TextBox? _nameBox;
    private double _factor = 1;
    private bool _active;

    public PagesSection(ISettingsHost host, PageEditing editing, DispatcherQueue queue, Window owner, Forms.ReadingCatalog catalog, Action<string> setUp)
    {
        _host = host;
        _owner = owner;
        _editing = editing;
        _queue = queue;
        _pageId = host.DeckPageId ?? host.Document.Typed.Pages.FirstOrDefault()?.Id;
        _preview = new PreviewPresenter(host, queue, _pageId);
        _preview.LayoutChanged += OnLayoutChanged;
        _preview.Reloaded += () => _queue.TryEnqueue(DispatcherQueuePriority.Low, FollowPage);
        _readings = new Forms.PreviewEditorReadings(_preview.Readings, _preview.Readings, host.Readings, queue);
        _icons = new Forms.ThemeIconCatalog(() => _preview.Session.Theme);
        _setUp = setUp;
        var typed = new Forms.ReadingChooserDialog(() => _root.XamlRoot, _readings);
        _chooser = new Forms.ReadingPickerDialog(() => _root.XamlRoot, _readings, catalog, queue, typed, setUp);

        _outsideBar = new InfoBar { Severity = InfoBarSeverity.Informational, IsClosable = true, Message = $"{FileName} changed outside the editor. The editor took the new file." };
        var undoReload = new Button { Content = "Undo the reload" };
        undoReload.Click += (_, _) =>
        {
            if (_host.Document.History.UndoLabel == "Changed outside the editor")
                _host.Document.History.Undo();
            _outsideBar.IsOpen = false;
        };
        _outsideBar.ActionButton = undoReload;
        _invalidBar = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        _newerBar = new InfoBar
        {
            Severity = InfoBarSeverity.Warning,
            IsClosable = false,
            Message = "This configuration was written by a newer version of UrDeck. It is shown but cannot be changed here.",
        };

        BuildLayout();

        host.Document.Changed += OnDocumentChanged;
        host.Document.Problem += OnProblem;
        host.Document.History.StateChanged += UpdateUndo;
        host.DeckPageChanged += OnDeckPageChanged;
        Refresh();
    }

    public UIElement Root => _root;

    /// <summary>The readings the forms show, which the Readings section lists with.</summary>
    public Forms.IEditorReadings Readings => _readings;

    private string FileName => System.IO.Path.GetFileName(_host.ConfigPath);

    private UrDeckConfig Config => _host.Document.Typed;

    private bool Editable => !_host.Document.IsReadOnly;

    /// <summary>The page shown (its id).</summary>
    public string? PageId => _pageId;

    /// <summary>Whether the section is shown and the window not minimised: only then the preview draws.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        _preview.SetShown(active);
        if (active)
            FollowPage();
    }

    // ---------------- layout ----------------

    private void BuildLayout()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.Padding = new Thickness(24, 16, 0, 0);

        // Title, Undo and Redo.
        var header = new Grid { Margin = new Thickness(0, 0, 24, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "Pages", FontSize = 28, FontWeight = FontWeights.SemiBold });
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _undo.Click += (_, _) => _host.Document.History.Undo();
        _redo.Click += (_, _) => _host.Document.History.Redo();
        AutomationProperties.SetName(_undo, "Undo");
        AutomationProperties.SetName(_redo, "Redo");
        history.Children.Add(_undo);
        history.Children.Add(_redo);
        Grid.SetColumn(history, 1);
        header.Children.Add(history);
        _root.Children.Add(header);

        var bars = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 24, 0) };
        bars.Children.Add(_outsideBar);
        bars.Children.Add(_invalidBar);
        bars.Children.Add(_newerBar);
        Grid.SetRow(bars, 1);
        _root.Children.Add(bars);

        // The tabs and Add widget.
        var tabsRow = new Grid { Margin = new Thickness(0, 8, 24, 8) };
        tabsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tabsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tabsRow.Children.Add(new ScrollViewer
        {
            Content = _tabs,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
        });
        _addWidget.Click += (_, _) => OpenPalette(null);
        _addWidget.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        Grid.SetColumn(_addWidget, 1);
        tabsRow.Children.Add(_addWidget);
        Grid.SetRow(tabsRow, 2);
        _root.Children.Add(tabsRow);

        // The stage (the preview and its switches) and the pane.
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(body, 3);
        _root.Children.Add(body);

        _stage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _stage.Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"];
        _stage.CornerRadius = new CornerRadius(8, 0, 0, 0);
        body.Children.Add(_stage);

        _page.Children.Add(_preview.Canvas);
        _focusTarget.Content = _overlay;
        _page.Children.Add(_focusTarget);
        _bezel.Child = _page;
        _scroll.Content = _bezel;
        _stage.Children.Add(_scroll);
        _stage.SizeChanged += (_, _) => Resize();

        _overlay.PointerPressed += OnPreviewPressed;
        _focusTarget.KeyDown += OnPreviewKey;
        AutomationProperties.SetName(_focusTarget, "Page preview");

        // Zoom and Grid at the left, Readings at the right, as compact segmented controls.
        var switches = new Grid { Padding = new Thickness(12, 8, 12, 8), ColumnSpacing = 12, RowSpacing = 8 };
        switches.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        switches.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        switches.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        switches.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        switches.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        switches.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _wholePage = true;
        var zoom = Segmented("Zoom", ["Fit width", "Whole page"], 1, index =>
        {
            _wholePage = index == 1;
            Resize();
        });
        switches.Children.Add(zoom);
        _gridToggle.Content = "# Grid";
        _gridToggle.IsChecked = true;
        AutomationProperties.SetName(_gridToggle, "Show the cells");
        _gridToggle.Click += (_, _) => BuildOverlay();
        Grid.SetColumn(_gridToggle, 1);
        switches.Children.Add(_gridToggle);
        var readings = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        readings.Children.Add(new TextBlock { Text = "Readings", Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        readings.Children.Add(Segmented("Readings", ["Live", "Sample"], 0, index =>
            _preview.Readings.Mode = index == 1 ? PreviewReadingMode.Sample : PreviewReadingMode.Live));
        Grid.SetColumn(readings, 3);
        switches.Children.Add(readings);
        // When the stage is narrow, Readings goes to a line of its own under Zoom and Grid instead of being cut off.
        switches.SizeChanged += (_, e) =>
        {
            bool narrow = e.NewSize.Width < 560;
            Grid.SetRow(readings, narrow ? 1 : 0);
            Grid.SetColumn(readings, narrow ? 0 : 3);
            Grid.SetColumnSpan(readings, narrow ? 4 : 1);
        };
        Grid.SetRow(switches, 1);
        _stage.Children.Add(switches);

        var paneScroll = new ScrollViewer { Content = _paneHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var splitter = new PaneSplitter(_paneHost);
        Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        Grid.SetColumn(paneScroll, 2);
        body.Children.Add(paneScroll);

        // Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z; Ctrl+PageUp and Ctrl+PageDown switch pages.
        AddKey(VirtualKey.Z, VirtualKeyModifiers.Control, () => _host.Document.History.Undo());
        AddKey(VirtualKey.Y, VirtualKeyModifiers.Control, () => _host.Document.History.Redo());
        AddKey(VirtualKey.Z, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => _host.Document.History.Redo());
        AddKey(VirtualKey.PageUp, VirtualKeyModifiers.Control, () => StepPage(-1));
        AddKey(VirtualKey.PageDown, VirtualKeyModifiers.Control, () => StepPage(1));
    }

    private void AddKey(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, e) =>
        {
            // A text box undoes its own typing.
            if (key is VirtualKey.Z or VirtualKey.Y && FocusManager.GetFocusedElement(_root.XamlRoot) is TextBox)
                return;
            e.Handled = true;
            action();
        };
        _root.KeyboardAccelerators.Add(accelerator);
        // Undo's tooltip names its key; WinUI would otherwise show "Ctrl+Z" over whatever the pointer rests on in the section.
        _root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    /// <summary>
    /// A segmented control: the options side by side in one rounded group, the chosen one filled. Each option is a radio
    /// button for accessibility (one of a group, arrow keys move between them).
    /// </summary>
    internal static Border Segmented(string name, IReadOnlyList<string> options, int selected, Action<int> chosen)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var buttons = new List<ToggleButton>();
        void Mark(int index)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].IsChecked = i == index;
                buttons[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            var button = new ToggleButton
            {
                Content = options[i],
                Padding = new Thickness(12, 4, 12, 4),
                BorderThickness = new Thickness(0),
                MinWidth = 0,
            };
            AutomationProperties.SetName(button, $"{name}: {options[i]}");
            AutomationProperties.SetPositionInSet(button, i + 1);
            AutomationProperties.SetSizeOfSet(button, options.Count);
            button.Click += (_, _) =>
            {
                Mark(index);
                chosen(index);
            };
            buttons.Add(button);
            row.Children.Add(button);
        }

        Mark(selected);
        return new Border
        {
            Child = row,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Sizes the page for the zoom: Fit width (cells at most 120 DIP, scrolling) or Whole page (everything visible).</summary>
    private void Resize()
    {
        var surface = _host.DeckSurface;
        if (!surface.IsUsable)
            return;
        double width = surface.Size.Width;
        double height = surface.Size.Height;
        double chromeX = 2 * (StageMargin + BezelPadding);
        double availableW = Math.Max(120, _stage.ActualWidth - chromeX - 16);
        double availableH = Math.Max(120, _stage.ActualHeight - (_stage.RowDefinitions.Count > 1 ? 64 : 0) - chromeX);
        double factor = _wholePage
            ? Math.Min(availableW / width, availableH / height)
            : Math.Min(availableW, GridLayoutManager.Columns * FitWidthCellMax) / width;
        factor = Math.Max(factor, 0.02);
        _factor = factor;
        _page.Width = Math.Round(width * factor);
        _page.Height = Math.Round(height * factor);
        _preview.Canvas.Invalidate();
        BuildOverlay();
    }

    // ---------------- reading the document ----------------

    private void OnDocumentChanged(ConfigChange change)
    {
        if (change.Origin == EditOrigin.Outside)
        {
            _outsideBar.IsOpen = !_host.Document.IsReadOnly;
            _invalidBar.IsOpen = false;
        }
        else if (change.Origin == EditOrigin.User && !change.Label.StartsWith("Undo ", StringComparison.Ordinal))
        {
            _outsideBar.IsOpen = false;
        }

        Refresh();
    }

    private void OnProblem(ConfigProblem problem)
    {
        if (problem.Line is not { } line)
            return; // a failed write: the deck status says so
        _invalidBar.Message = $"{FileName} is not valid (line {line}, column {problem.Column ?? 1}). The deck keeps the last good version.";
        _invalidBar.IsOpen = true;
    }

    private void OnDeckPageChanged(string? pageId)
    {
        // A swipe on the panel: the editor's tab follows the deck.
        if (pageId == null || pageId == _pageId)
            return;
        _pageId = pageId;
        _selection = null;
        _fit = null;
        _preview.ShowPage(pageId);
        Refresh();
    }

    /// <summary>The deck and the preview show the editor's page; after a reload both may need telling again (a page just added).</summary>
    private void FollowPage()
    {
        if (_pageId == null)
            return;
        _preview.ShowPage(_pageId);
        if (_active)
            _host.ShowPageOnDeck(_pageId);
    }

    private void OnLayoutChanged() => BuildOverlay();

    /// <summary>Shows what the document says now: the tabs, the bars, Undo, the overlays and the pane.</summary>
    private void Refresh()
    {
        var config = Config;
        if (PageEditing.PageIndex(config, _pageId) < 0)
        {
            // The page went (deleted, undone, changed outside): the deck's page, else the first.
            _pageId = PageEditing.PageIndex(config, _host.DeckPageId) >= 0 ? _host.DeckPageId : config.Pages.FirstOrDefault()?.Id;
            if (_pageId != null)
                _preview.ShowPage(_pageId);
        }

        _selection = _selection?.Resolve(config);
        if (_fit is { } fit && (_selection == null || _selection.Ref != fit.Widget))
            _fit = null;
        _newerBar.IsOpen = _host.Document.IsNewerThanHost;
        _addWidget.IsEnabled = Editable && _pageId != null;
        UpdateUndo();
        BuildTabs();
        BuildOverlay();
        BuildPane();
    }

    private void UpdateUndo()
    {
        var history = _host.Document.History;
        _undo.IsEnabled = history.CanUndo && Editable;
        _redo.IsEnabled = history.CanRedo && Editable;
        ToolTipService.SetToolTip(_undo, history.UndoLabel is { } u ? $"Undo {u} (Ctrl+Z)" : "Nothing to undo");
        ToolTipService.SetToolTip(_redo, history.RedoLabel is { } r ? $"Redo {r} (Ctrl+Y)" : "Nothing to redo");
    }

    private GridSize CellGrid =>
        _preview.Session.Layout is { } layout ? GridSize.From(layout.Chrome) : GridSize.For(_host.DeckSurface, Config, _preview.Session.Theme.Definition);

    // ---------------- tabs ----------------

    private void BuildTabs()
    {
        _tabs.Children.Clear();
        var config = Config;
        int start = StartPageResolver.Resolve(config);
        for (int i = 0; i < config.Pages.Count; i++)
        {
            var page = config.Pages[i];
            string? id = page.Id;
            bool current = id != null && id == _pageId;
            // A text tab with an underline under the current page; its menu is the context menu (right-click, Shift+F10).
            var label = new TextBlock
            {
                Text = i == start ? $"{page.Name}  ⌂" : page.Name,
                FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
                Opacity = current ? 1 : 0.75,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var underline = new Rectangle
            {
                Height = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Margin = new Thickness(0, 6, 0, 0),
                Fill = current ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"] : new SolidColorBrush(Colors.Transparent),
            };
            var content = new StackPanel();
            content.Children.Add(label);
            content.Children.Add(underline);
            var tab = new Button
            {
                Content = content,
                Padding = new Thickness(12, 6, 12, 2),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            AutomationProperties.SetName(tab, (i == start ? $"{page.Name}, start page" : page.Name) + (current ? ", selected" : ""));
            ToolTipService.SetToolTip(tab, Editable ? "Right-click to rename, duplicate, move or delete" : null);
            tab.Click += (_, _) =>
            {
                if (id != null)
                    SelectPage(id);
            };
            if (Editable && id != null)
                tab.ContextFlyout = PageMenu(i, page, config.Pages.Count);
            _tabs.Children.Add(tab);
        }

        var add = new Button { Content = new SymbolIcon(Symbol.Add), IsEnabled = Editable, Padding = new Thickness(10, 6, 10, 6), Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
        AutomationProperties.SetName(add, "Add a page");
        ToolTipService.SetToolTip(add, "Add a page");
        add.Click += (_, _) =>
        {
            var outcome = _editing.AddPage(_pageId);
            if (outcome.PageId is { } id)
                SelectPage(id);
        };
        _tabs.Children.Add(add);
    }

    private MenuFlyout PageMenu(int index, PageConfig page, int count)
    {
        string id = page.Id!;
        var menu = new MenuFlyout();
        void Item(string text, Action action, bool enabled = true)
        {
            var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Item("Rename", () =>
        {
            _selection = null;
            _paletteOpen = false;
            Refresh();
            _nameBox?.Focus(FocusState.Programmatic);
            _nameBox?.SelectAll();
        });
        Item("Duplicate", () =>
        {
            if (_editing.DuplicatePage(id).PageId is { } copy)
                SelectPage(copy);
        });
        Item("Move left", () => _editing.MovePage(id, -1), index > 0);
        Item("Move right", () => _editing.MovePage(id, 1), index < count - 1);
        menu.Items.Add(new MenuFlyoutSeparator());
        bool isStart = StartPageResolver.IsSet(Config) && Config.StartPage == id;
        Item("Make this the start page", () => _editing.SetStartPage(id), !isStart);
        menu.Items.Add(new MenuFlyoutSeparator());
        Item("Delete", () => _ = DeletePageAsync(id), count > 1);
        return menu;
    }

    private void SelectPage(string id)
    {
        if (id == _pageId)
        {
            Refresh();
            return;
        }

        _pageId = id;
        _selection = null;
        _fit = null;
        _paletteOpen = false;
        _preview.ShowPage(id);
        // The deck follows the editor while it is open; nothing is written.
        _host.ShowPageOnDeck(id);
        Refresh();
    }

    private void StepPage(int by)
    {
        var pages = Config.Pages;
        int index = PageEditing.PageIndex(Config, _pageId);
        int next = Math.Clamp(index + by, 0, pages.Count - 1);
        if (next != index && pages[next].Id is { } id)
            SelectPage(id);
    }

    private async Task DeletePageAsync(string id)
    {
        var config = Config;
        int index = PageEditing.PageIndex(config, id);
        if (index < 0)
            return;
        var page = config.Pages[index];
        string? next = _editing.NextStartPage(id);
        if (page.Widgets.Count > 0 || next != null)
        {
            string startNote = next != null
                ? $" It is the start page: {config.Pages[PageEditing.PageIndex(config, next)].Name} becomes the start page."
                : "";
            var dialog = new ContentDialog
            {
                XamlRoot = _root.XamlRoot,
                Title = $"Delete {page.Name}?",
                Content = $"Its {page.Widgets.Count} widget(s) go with it.{startNote} You can undo this.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        _editing.DeletePage(id);
    }

    // ---------------- the overlays: cells, add buttons, placeholders, the selection ----------------

    private PageSlots? CurrentPage => _preview.Session.Pager.Current;

    private void BuildOverlay()
    {
        _overlay.Children.Clear();
        _overlay.Width = _page.Width;
        _overlay.Height = _page.Height;
        if (_preview.Session.Layout is not { } layout || _pageId == null)
            return;

        // The cells, the placeholders and the selection are those of the page drawn: until the preview draws the editor's page
        // (a jump being built, or one it missed) nothing is shown, and the preview is asked again; its next layout brings them.
        if (!_preview.Shows(_pageId))
        {
            _preview.EnsurePage(_pageId);
            return;
        }

        double f = _factor;
        double cell = layout.Surface.Size.Width / (double)GridLayoutManager.Columns * f;
        var grid = GridSize.From(layout.Chrome);
        int pageIndex = PageEditing.PageIndex(Config, _pageId);
        var widgets = pageIndex >= 0 ? Config.Pages[pageIndex].Widgets : [];
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

        if (_gridToggle.IsChecked == true)
        {
            var line = new SolidColorBrush(ColorHelper.FromArgb(70, 255, 255, 255));
            for (int c = 1; c < grid.Columns; c++)
                _overlay.Children.Add(new Line { X1 = c * cell, X2 = c * cell, Y1 = 0, Y2 = grid.Rows * cell, Stroke = line, StrokeThickness = 1, IsHitTestVisible = false });
            for (int r = 1; r <= grid.Rows; r++)
                _overlay.Children.Add(new Line { X1 = 0, X2 = grid.Columns * cell, Y1 = r * cell, Y2 = r * cell, Stroke = line, StrokeThickness = 1, IsHitTestVisible = false });
            if (Editable)
            {
                foreach (var empty in PageEditing.EmptyCells(widgets, grid))
                {
                    var plus = new Button
                    {
                        Content = "+",
                        Width = Math.Max(20, cell * 0.36),
                        Height = Math.Max(20, cell * 0.36),
                        Padding = new Thickness(0),
                        FontSize = Math.Clamp(cell * 0.2, 10, 22),
                        Opacity = 0.75,
                        IsTabStop = false,
                    };
                    AutomationProperties.SetName(plus, $"Add a widget at column {empty.Col + 1}, row {empty.Row + 1}");
                    Canvas.SetLeft(plus, empty.Col * cell + (cell - plus.Width) / 2);
                    Canvas.SetTop(plus, empty.Row * cell + (cell - plus.Height) / 2);
                    var at = empty;
                    plus.Click += (_, _) => OpenPalette(at);
                    _overlay.Children.Add(plus);
                }
            }
        }

        // A widget whose type is not installed is drawn by nobody: an outlined empty card says it is there.
        if (CurrentPage is { } current)
        {
            for (int i = 0; i < current.Layout.Count && i < widgets.Count; i++)
            {
                var item = current.Layout[i];
                if (!item.Placed || _host.DescribeWidget(widgets[i].WidgetTypeId) != null)
                    continue;
                var box = new Rectangle
                {
                    Width = item.Size.Width * f,
                    Height = item.Size.Height * f,
                    Stroke = new SolidColorBrush(ColorHelper.FromArgb(160, 255, 255, 255)),
                    StrokeDashArray = [4, 3],
                    StrokeThickness = 1.5,
                    RadiusX = 6,
                    RadiusY = 6,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(box, item.Position.X * f);
                Canvas.SetTop(box, item.Position.Y * f);
                _overlay.Children.Add(box);
            }
        }

        if (_selection is { } selection && RectOf(layout, selection.Ref) is { } rect)
        {
            var outline = new Rectangle
            {
                Width = rect.Width * f + 6,
                Height = rect.Height * f + 6,
                Stroke = accent,
                StrokeThickness = 3,
                RadiusX = 8,
                RadiusY = 8,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, rect.X * f - 3);
            Canvas.SetTop(outline, rect.Y * f - 3);
            _overlay.Children.Add(outline);

            var label = new Border
            {
                Background = accent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = SelectionLabel(selection.Ref), Foreground = new SolidColorBrush(Colors.White), FontSize = 12 },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(label, rect.X * f - 3);
            Canvas.SetTop(label, Math.Max(0, rect.Y * f - 26));
            _overlay.Children.Add(label);
            AutomationProperties.SetName(_focusTarget, $"Page preview. {AccessibleName(selection.Ref)}");
        }
        else
        {
            AutomationProperties.SetName(_focusTarget, "Page preview. Nothing selected");
        }
    }

    /// <summary>A widget's rectangle in surface units: its card on the page, or its slot in the dock.</summary>
    private static System.Drawing.Rectangle? RectOf(DeckLayout layout, WidgetRef widget)
    {
        if (widget.InDock)
            return layout.Dock.FirstOrDefault(c => c.Index == widget.Index) is { } card ? new System.Drawing.Rectangle(card.Item.Position, card.Item.Size) : null;
        return layout.Page.FirstOrDefault(c => c.Index == widget.Index) is { } placed
            ? new System.Drawing.Rectangle(placed.Item.Position, placed.Item.Size)
            : null;
    }

    private System.Drawing.Rectangle? RectOf(WidgetRef widget)
    {
        if (_preview.Session.Layout is not { } layout)
            return null;
        if (RectOf(layout, widget) is { } rect)
            return rect;
        // Not drawn (its type is not installed): where the layout put it.
        if (!widget.InDock && CurrentPage is { } page && widget.Index < page.Layout.Count && page.Layout[widget.Index].Placed)
            return new System.Drawing.Rectangle(page.Layout[widget.Index].Position, page.Layout[widget.Index].Size);
        return null;
    }

    private string SelectionLabel(WidgetRef widget)
    {
        if (PageEditing.Widget(Config, widget) is not { } config)
            return "";
        string name = _editing.TypeName(config);
        return widget.InDock ? $"{name} · dock" : $"{name} · {config.Width}x{config.Height}";
    }

    private string AccessibleName(WidgetRef widget)
    {
        if (PageEditing.Widget(Config, widget) is not { } config)
            return "";
        string name = _editing.TypeName(config);
        return widget.InDock ? $"{name}, in the dock, slot {widget.Index + 1}" : $"{name}, {config.Width}x{config.Height}, row {config.Row + 1}";
    }

    // ---------------- selection by pointer and keyboard ----------------

    private void OnPreviewPressed(object sender, PointerRoutedEventArgs e)
    {
        _focusTarget.Focus(FocusState.Pointer);
        var point = e.GetCurrentPoint(_overlay).Position;
        Select(HitTest(point.X / _factor, point.Y / _factor));
        e.Handled = true;
    }

    private WidgetRef? HitTest(double x, double y)
    {
        if (_preview.Session.Layout is not { } layout || _pageId == null)
            return null;
        var dock = layout.Chrome.Dock;
        if (dock.Height > 0 && y >= dock.Top && y < dock.Bottom)
        {
            var card = layout.Dock.FirstOrDefault(c => c.Item.Position.X <= x && x < c.Item.Position.X + c.Item.Size.Width
                && c.Item.Position.Y <= y && y < c.Item.Position.Y + c.Item.Size.Height);
            return card == null ? null : WidgetRef.Dock(card.Index);
        }

        if (CurrentPage is { } page && WidgetHitTest.Find(page.Layout, x, y) is { } hit)
            return new WidgetRef(_pageId, hit.Index);
        return null;
    }

    private void Select(WidgetRef? widget)
    {
        _selection = widget == null ? null : WidgetSelection.Of(Config, widget);
        _fit = null;
        _paletteOpen = false;
        BuildOverlay();
        BuildPane();
    }

    private List<WidgetRef> SelectableWidgets()
    {
        var result = new List<WidgetRef>();
        if (_pageId == null)
            return result;
        int page = PageEditing.PageIndex(Config, _pageId);
        if (page >= 0)
        {
            for (int i = 0; i < Config.Pages[page].Widgets.Count; i++)
            {
                var widget = new WidgetRef(_pageId, i);
                if (RectOf(widget) != null)
                    result.Add(widget);
            }
        }

        for (int i = 0; i < Config.Dock.Count; i++)
        {
            if (RectOf(WidgetRef.Dock(i)) != null)
                result.Add(WidgetRef.Dock(i));
        }

        return result;
    }

    private void OnPreviewKey(object sender, KeyRoutedEventArgs e)
    {
        var all = SelectableWidgets();
        switch (e.Key)
        {
            case VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down:
                Select(Nearest(all, e.Key) ?? _selection?.Ref ?? (all.Count > 0 ? all[0] : null));
                break;
            case VirtualKey.Home:
                Select(all.Count > 0 ? all[0] : null);
                break;
            case VirtualKey.End:
                Select(all.Count > 0 ? all[^1] : null);
                break;
            case VirtualKey.Enter:
                FocusManager.TryMoveFocus(FocusNavigationDirection.Next, new FindNextElementOptions { SearchRoot = _paneHost });
                if (FirstFocusable(_paneHost) is Control control)
                    control.Focus(FocusState.Keyboard);
                break;
            case VirtualKey.Delete:
                if (_selection != null && Editable)
                    RemoveSelected();
                break;
            case VirtualKey.Escape:
                if (_selection == null && !_paletteOpen)
                    return; // the window closes
                _paletteOpen = false;
                Select(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>The widget nearest to the selected one in the direction of the key, by the centres of their cards.</summary>
    private WidgetRef? Nearest(IReadOnlyList<WidgetRef> all, VirtualKey key)
    {
        if (_selection is not { } selection || RectOf(selection.Ref) is not { } from)
            return null;
        double fx = from.X + from.Width / 2.0, fy = from.Y + from.Height / 2.0;
        WidgetRef? best = null;
        double bestScore = double.MaxValue;
        foreach (var widget in all)
        {
            if (widget == selection.Ref || RectOf(widget) is not { } to)
                continue;
            double dx = to.X + to.Width / 2.0 - fx, dy = to.Y + to.Height / 2.0 - fy;
            double along = key switch { VirtualKey.Left => -dx, VirtualKey.Right => dx, VirtualKey.Up => -dy, _ => dy };
            double across = key is VirtualKey.Left or VirtualKey.Right ? Math.Abs(dy) : Math.Abs(dx);
            if (along <= 0.5)
                continue;
            double score = along + 2 * across;
            if (score < bestScore)
            {
                bestScore = score;
                best = widget;
            }
        }

        return best;
    }

    private static DependencyObject? FirstFocusable(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Control { IsEnabled: true, IsTabStop: true })
                return child;
            if (FirstFocusable(child) is { } found)
                return found;
        }

        return null;
    }

    // ---------------- actions ----------------

    private void OpenPalette(Cell? at)
    {
        if (!Editable)
            return;
        _paletteOpen = true;
        _paletteAt = at;
        _paletteMessage = null;
        BuildPane();
    }

    private void AddWidget(string typeId)
    {
        if (_pageId == null)
            return;
        var outcome = _editing.AddWidget(_pageId, typeId, CellGrid, _paletteAt);
        if (!outcome.Applied)
        {
            _paletteMessage = outcome.Message;
            _paneKey = null;
            BuildPane();
            return;
        }

        _paletteOpen = false;
        _selection = WidgetSelection.Of(Config, outcome.Widget!);
        Refresh();
    }

    private void Resize(WidgetRef widget, int w, int h)
    {
        var outcome = _editing.Resize(widget, w, h, CellGrid);
        if (outcome.Applied)
        {
            _fit = null;
            _selection = WidgetSelection.Of(Config, widget);
            Refresh();
        }
        else if (outcome.Problem is { } problem)
        {
            _fit = (widget, w, h, problem);
            _paneKey = null;
            BuildPane();
        }
    }

    private void MoveToFirstFree()
    {
        if (_fit is not { Problem.FirstFree: { } spot } fit)
            return;
        if (_editing.MoveTo(fit.Widget, spot, fit.W, fit.H, CellGrid).Applied)
        {
            _fit = null;
            _selection = WidgetSelection.Of(Config, fit.Widget);
            Refresh();
        }
    }

    private void RemoveSelected()
    {
        if (_selection is not { } selection)
            return;
        _selection = null;
        _fit = null;
        _editing.RemoveWidget(selection.Ref);
        _focusTarget.Focus(FocusState.Programmatic);
    }

    /// <summary>Closes what the Escape key closes first: the palette. False when there was nothing to close.</summary>
    public bool CloseTransient()
    {
        if (_paletteOpen)
        {
            _paletteOpen = false;
            BuildPane();
            return true;
        }

        return false;
    }

    public void Close()
    {
        _form?.Dispose();
        _form = null;
        _host.Document.Changed -= OnDocumentChanged;
        _host.Document.Problem -= OnProblem;
        _host.Document.History.StateChanged -= UpdateUndo;
        _host.DeckPageChanged -= OnDeckPageChanged;
        _overlay.Children.Clear();
        _page.Children.Clear();
        _paneHost.Child = null;
        _preview.Dispose();
    }
}
