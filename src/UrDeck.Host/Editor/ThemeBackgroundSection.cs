// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;

namespace UrDeck.Host.Editor;

/// <summary>
/// The Theme and background section (editor-theme-background): the themes as pictures of the user's page, the background choices
/// with the theme's suggestions first, and beside them a live preview and the options of the chosen background (Moving or Still, the
/// shader's parameters, the dimming, the picture) and the deck-wide motion level. Every choice is a document edit.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Close disposes the preview and the thumbnails when the editor closes.")]
internal sealed class ThemeBackgroundSection
{
    private const double TileWidth = 220;
    private const double TileImageHeight = 150;
    private const double TileGap = 12;

    /// <summary>How many rows of the deck's grid the preview shows in full.</summary>
    private const int PreviewRows = 4;
    private const double FrameExtra = 12; // the selection frame: 4 DIP of padding and 2 of border on each side
    private const double OptionsWidth = 360;
    private const double StackBelow = 1100;

    private enum Choice { ThemesChoice, None, Shader, Picture }

    private readonly ISettingsHost _host;
    private readonly DispatcherQueue _queue;
    private readonly Window _owner;
    private readonly Func<Forms.FormContext> _formContext;
    private readonly BackgroundEditing _editing;
    private readonly ThemeThumbnails _thumbnails;
    private readonly ThumbnailCache _tileCache;
    private readonly PreviewPresenter _preview;
    private readonly Grid _root = new();
    private readonly Button _undo = new() { Content = new SymbolIcon(Symbol.Undo) };
    private readonly Button _redo = new() { Content = new SymbolIcon(Symbol.Redo) };
    private readonly ScrollViewer _left = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ScrollViewer _right = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly VariableSizedWrapGrid _themeGrid = Wrap(TileWidth + FrameExtra + TileGap);
    private readonly VariableSizedWrapGrid _suggestedGrid = Wrap(0);
    private readonly VariableSizedWrapGrid _allGrid = Wrap(0);
    private readonly TextBlock _suggestedLabel = Sections.Label("");
    private readonly Button _chipTheme = new();
    private readonly Button _chipNone = new();
    private readonly Button _chipPicture = new();
    private readonly Dictionary<Button, Border> _chipFrames = [];
    private double _backgroundTileWidth = 104;
    private double _backgroundTileHeight = 320;
    private readonly TextBlock _chipThemeSub = Sections.Caption("");
    private readonly TextBlock _chipPictureSub = Sections.Caption("");
    private readonly Border _previewFrame = new() { CornerRadius = new CornerRadius(8), Height = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _options = new() { Spacing = 12 };
    private readonly ComboBox _motionLevel = new() { MinWidth = 220 };
    private readonly Dictionary<string, (Border Frame, Border Image)> _themeTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _backgroundTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitmapImage> _backgroundPictures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _themePictures = new(StringComparer.Ordinal);
    private readonly DispatcherQueueTimer _requestTimer;
    private IReadOnlyList<ThemeCatalogEntry> _themes = [];
    private IReadOnlyList<BackgroundCatalogEntry> _backgrounds = [];
    private PairingIndex? _pairing;
    private Forms.FormView? _form;
    private string? _optionsKey;
    private string? _backgroundsKey;
    private string? _themesKey;
    private string? _requestedKey;
    private bool _active;
    private bool _updating;
    private bool _stacked;
    private bool _closed;

    public ThemeBackgroundSection(ISettingsHost host, DispatcherQueue queue, Window owner, Func<Forms.FormContext> formContext, string? pageId)
    {
        _host = host;
        _queue = queue;
        _owner = owner;
        _formContext = formContext;
        _editing = new BackgroundEditing(host.Document);
        _thumbnails = new ThemeThumbnails(host, queue);
        _thumbnails.Ready += OnThumbnailReady;
        _tileCache = new ThumbnailCache(ThumbnailCache.FolderIn(host.DataFolder));
        _preview = new PreviewPresenter(host, queue, pageId);
        _requestTimer = queue.CreateTimer();
        _requestTimer.IsRepeating = false;
        _requestTimer.Interval = TimeSpan.FromMilliseconds(600);
        _requestTimer.Tick += OnRequestTick;

        BuildLayout();
        host.Document.Changed += OnDocumentChanged;
        host.Document.History.StateChanged += UpdateUndo;
        _root.SizeChanged += OnSizeChanged;
        UpdateUndo();
    }

    public UIElement Root => _root;

    /// <summary>The theme list with the editor's own editing object, shared with the Pages header.</summary>
    public BackgroundEditing Editing => _editing;

    /// <summary>Draws the preview and the pictures only while the section is shown.</summary>
    public void SetActive(bool active)
    {
        if (_closed || active == _active)
            return;
        _active = active;
        _preview.SetShown(active);
        if (!active)
            return;

        _themes = ThemeCatalog.List(_host.Themes);
        _backgrounds = BackgroundCatalog.List(_host.Shaders);
        _pairing = new PairingIndex(_themes, _backgrounds);
        _themesKey = null;
        _backgroundsKey = null;
        _requestedKey = null;
        if (_host.DeckPageId is { } page)
            _preview.EnsurePage(page);
        Refresh();
    }

    public void Close()
    {
        if (_closed)
            return;
        _closed = true;
        _host.Document.Changed -= OnDocumentChanged;
        _host.Document.History.StateChanged -= UpdateUndo;
        _root.SizeChanged -= OnSizeChanged;
        _requestTimer.Stop();
        _requestTimer.Tick -= OnRequestTick;
        _thumbnails.Ready -= OnThumbnailReady;
        _thumbnails.Dispose();
        _form?.Dispose();
        _form = null;
        _preview.Dispose();
        _themeTiles.Clear();
        _backgroundTiles.Clear();
    }

    // ---------------- layout ----------------

    private static VariableSizedWrapGrid Wrap(double itemWidth) => new() { Orientation = Orientation.Horizontal, ItemWidth = itemWidth, MaximumRowsOrColumns = -1 };

    private void BuildLayout()
    {
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(OptionsWidth + 32) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var left = new StackPanel { Spacing = 8, Padding = new Thickness(32, 24, 16, 24) };
        left.Children.Add(Header());
        left.Children.Add(Sections.Label("Theme"));
        left.Children.Add(Sections.Caption("Each picture is your current page in that theme."));
        left.Children.Add(_themeGrid);
        var folder = new HyperlinkButton { Content = "Your own theme folder…", Padding = new Thickness(0) };
        folder.Click += (_, _) => OpenThemesFolder();
        left.Children.Add(folder);

        left.Children.Add(Sections.Label("Background"));
        left.Children.Add(Chips());
        left.Children.Add(_suggestedLabel);
        left.Children.Add(_suggestedGrid);
        left.Children.Add(Sections.Label("All backgrounds"));
        left.Children.Add(_allGrid);
        _left.Content = left;
        _root.Children.Add(_left);

        var right = new StackPanel { Spacing = 12, Padding = new Thickness(16, 24, 32, 24), Width = OptionsWidth + 48 };
        _previewFrame.Child = _preview.Canvas;
        right.Children.Add(Sections.Label("Preview"));
        right.Children.Add(_previewFrame);
        right.Children.Add(_options);
        right.Children.Add(MotionLevelBlock());
        _right.Content = right;
        Grid.SetColumn(_right, 1);
        _root.Children.Add(_right);

        _preview.Canvas.HorizontalAlignment = HorizontalAlignment.Left;
        _preview.Canvas.VerticalAlignment = VerticalAlignment.Top;
        _previewFrame.SizeChanged += (_, _) => SizePreview();
        _previewFrame.Clip = null;
    }

    /// <summary>The title with Undo and Redo, as the Pages section has them; Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z work in this section too.</summary>
    private Grid Header()
    {
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Sections.Header("Theme and background", "The look of the deck: how the cards are drawn, and what is behind them."));
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        _undo.Click += (_, _) => _host.Document.History.Undo();
        _redo.Click += (_, _) => _host.Document.History.Redo();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_undo, "Undo");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_redo, "Redo");
        history.Children.Add(_undo);
        history.Children.Add(_redo);
        Grid.SetColumn(history, 1);
        header.Children.Add(history);

        AddKey(Windows.System.VirtualKey.Z, Windows.System.VirtualKeyModifiers.Control, () => _host.Document.History.Undo());
        AddKey(Windows.System.VirtualKey.Y, Windows.System.VirtualKeyModifiers.Control, () => _host.Document.History.Redo());
        AddKey(Windows.System.VirtualKey.Z, Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift, () => _host.Document.History.Redo());
        return header;
    }

    private void AddKey(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, e) =>
        {
            // A text box undoes its own typing.
            if (FocusManager.GetFocusedElement(_root.XamlRoot) is TextBox)
                return;
            e.Handled = true;
            action();
        };
        _root.KeyboardAccelerators.Add(accelerator);
    }

    private void UpdateUndo()
    {
        var history = _host.Document.History;
        _undo.IsEnabled = history.CanUndo && Editable;
        _redo.IsEnabled = history.CanRedo && Editable;
        ToolTipService.SetToolTip(_undo, history.UndoLabel is { } u ? $"Undo {u} (Ctrl+Z)" : "Nothing to undo");
        ToolTipService.SetToolTip(_redo, history.RedoLabel is { } r ? $"Redo {r} (Ctrl+Y)" : "Nothing to redo");
    }

    private Grid Chips()
    {
        var grid = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < 3; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Chip(_chipTheme, "Theme's choice", _chipThemeSub, 0, grid);
        Chip(_chipNone, "None", Sections.Caption("The theme colour only"), 1, grid);
        Chip(_chipPicture, "Picture…", _chipPictureSub, 2, grid);
        _chipTheme.Click += (_, _) =>
        {
            _editing.UseThemesBackground();
            Refresh();
        };
        _chipNone.Click += (_, _) =>
        {
            _editing.UseNoBackground();
            Refresh();
        };
        _chipPicture.Click += async (_, _) => await ChoosePictureAsync();
        _chipPicture.AllowDrop = true;
        _chipPicture.DragOver += OnPictureDragOver;
        _chipPicture.Drop += OnPictureDrop;
        return grid;
    }

    private void Chip(Button chip, string title, TextBlock subtitle, int column, Grid into)
    {
        var content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        subtitle.TextWrapping = TextWrapping.NoWrap;
        content.Children.Add(subtitle);
        // Selected as the tiles are: an accent border (a toggle button's checked look is too faint to read as a choice).
        var frame = Frame(content);
        frame.Padding = new Thickness(10, 8, 10, 8);
        chip.Content = frame;
        _chipFrames[chip] = frame;
        chip.HorizontalAlignment = HorizontalAlignment.Stretch;
        chip.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        chip.Padding = new Thickness(0);
        chip.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        chip.BorderThickness = new Thickness(0);
        Grid.SetColumn(chip, column);
        into.Children.Add(chip);
    }

    private StackPanel MotionLevelBlock()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 16, 0, 0) };
        panel.Children.Add(Sections.Label("Motion"));
        foreach (var (text, _) in MotionChoices)
            _motionLevel.Items.Add(text);
        _motionLevel.SelectionChanged += OnMotionLevelChanged;
        panel.Children.Add(_motionLevel);
        panel.Children.Add(Sections.Caption("Reduced and Off stop the moving background and the animated borders. Follow Windows uses the system's animation setting."));
        return panel;
    }

    private static readonly (string Text, string? Level)[] MotionChoices =
    [
        ("Follow Windows", null),
        ("Full", "full"),
        ("Reduced", "reduced"),
        ("Off", "off"),
    ];

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool stacked = e.NewSize.Width < StackBelow;
        if (stacked == _stacked)
            return;
        _stacked = stacked;
        // Below 1100 DIP the options go under the tiles.
        _root.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(OptionsWidth + 32);
        Grid.SetColumn(_right, stacked ? 0 : 1);
        Grid.SetRow(_right, stacked ? 1 : 0);
        _right.Width = double.NaN;
        _right.MaxHeight = stacked ? 560 : double.PositiveInfinity;
    }

    private void SizePreview()
    {
        var surface = _host.DeckSurface;
        double width = _previewFrame.ActualWidth;
        if (width <= 0 || surface.Size.Width <= 0)
            return;
        _preview.Canvas.Width = width;
        _preview.Canvas.Height = width * surface.Size.Height / surface.Size.Width;

        // The frame shows the top PreviewRows rows of the page: a cell is a quarter of the deck's width, whatever its resolution.
        double cell = surface.Size.Width / (double)GridLayoutManager.Columns;
        double rows = Math.Min(PreviewRows * cell, surface.Size.Height) * width / surface.Size.Width;
        if (Math.Abs(_previewFrame.Height - rows) > 0.5)
            _previewFrame.Height = Math.Round(rows);
    }

    // ---------------- state ----------------

    /// <summary>What the configuration says about the background, without reading it as the deck does (no warnings logged).</summary>
    private (Choice Kind, string? Name, string? Path, double? Dim, bool Still) Current()
    {
        if (_host.Document.Root["background"] is not JsonObject background)
            return (Choice.ThemesChoice, null, null, null, false);

        string? Text(string key) => Own(background, key) is JsonValue v && v.TryGetValue(out string? s) ? s : null;
        double? dim = BackgroundEditing.NumberOf(Own(background, "dim")) is { } number ? Math.Clamp(number, 0, BackgroundSettings.MaxDim) : null;
        bool still = BackgroundEditing.NumberOf(Own(background, "speed")) == 0;
        return Text("type")?.ToLowerInvariant() switch
        {
            "none" => (Choice.None, null, null, null, false),
            "shader" => (Choice.Shader, Text("name"), null, dim, still),
            "image" => (Choice.Picture, null, Text("path"), dim, false),
            _ => (Choice.ThemesChoice, null, null, null, false),
        };
    }

    private static JsonNode? Own(JsonObject o, string key) => o.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private ThemeCatalogEntry? CurrentTheme() => _themes.FirstOrDefault(t => t.Name == _host.Document.Typed.Theme);

    private string? ThemeChoiceName() => CurrentTheme()?.DefaultBackground;

    private bool Editable => !_host.Document.IsReadOnly;

    private void OnDocumentChanged(ConfigChange change)
    {
        if (!_active || _closed)
            return;
        Refresh();
    }

    private void Refresh()
    {
        if (_closed || !_active)
            return;

        _updating = true;
        try
        {
            var (kind, name, path, _, _) = Current();
            RefreshThemes();
            RefreshBackgrounds(kind, name);
            Select(_chipFrames[_chipTheme], kind == Choice.ThemesChoice);
            Select(_chipFrames[_chipNone], kind == Choice.None);
            Select(_chipFrames[_chipPicture], kind == Choice.Picture);
            _chipTheme.IsEnabled = _chipNone.IsEnabled = _chipPicture.IsEnabled = Editable;
            _chipThemeSub.Text = ThemesChoiceText();
            _chipPictureSub.Text = kind == Choice.Picture && path != null ? System.IO.Path.GetFileName(path) : "A file of your own";
            RefreshOptions();
            int level = Array.FindIndex(MotionChoices, c => c.Level == MotionLevelOf());
            _motionLevel.SelectedIndex = Math.Max(0, level);
            _motionLevel.IsEnabled = Editable;
            SizePreview();
        }
        finally
        {
            _updating = false;
        }

        ScheduleThumbnails();
    }

    private string? MotionLevelOf() =>
        _host.Document.Root["motion"] is JsonValue v && v.TryGetValue(out string? s) ? s?.Trim().ToLowerInvariant() : null;

    private string ThemesChoiceText()
    {
        if (CurrentTheme() is not { } theme)
            return "What the theme shows";
        if (theme.DefaultBackground is { } name)
            return _backgrounds.FirstOrDefault(b => b.Name == name)?.Title ?? name;
        return _host.Themes.Load(theme.Name).Definition.Background?.Static != null ? "Its own still background" : "Its colour";
    }

    // ---------------- themes ----------------

    private void RefreshThemes()
    {
        string key = string.Join("|", _themes.Select(t => t.Name));
        if (key != _themesKey)
        {
            _themesKey = key;
            _themeGrid.Children.Clear();
            _themeTiles.Clear();
            foreach (var theme in _themes)
                _themeGrid.Children.Add(ThemeTile(theme));
        }

        string current = _host.Document.Typed.Theme;
        foreach (var (name, tile) in _themeTiles)
            Select(tile.Frame, name == current);
    }

    private Button ThemeTile(ThemeCatalogEntry theme)
    {
        var image = new Border
        {
            Width = TileWidth,
            Height = TileImageHeight,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = ThemeVisuals.Swatch(theme.Swatch, TileWidth, TileImageHeight),
        };
        var text = new StackPanel { Spacing = 0, Margin = new Thickness(0, 6, 0, 0) };
        text.Children.Add(new TextBlock { Text = theme.DisplayName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(theme.Description))
            text.Children.Add(new TextBlock { Text = theme.Description, Opacity = 0.7, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, TextAlignment = TextAlignment.Center });
        var inner = new StackPanel { Children = { image, text }, HorizontalAlignment = HorizontalAlignment.Center };
        var frame = Frame(inner);
        var button = new Button
        {
            Content = frame,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(TileGap / 2, 0, TileGap / 2, TileGap),
            Width = TileWidth + FrameExtra,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, theme.DisplayName);
        string name = theme.Name;
        button.Click += (_, _) =>
        {
            _editing.SetTheme(name);
            Refresh();
        };
        _themeTiles[name] = (frame, image);
        if (_themePictures.TryGetValue(name, out string? file))
            ShowThemePicture(name, file);
        return button;
    }

    private static Border Frame(UIElement content) => new()
    {
        Child = content,
        Padding = new Thickness(4),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(2),
        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
    };

    private static void Select(Border frame, bool selected) =>
        frame.BorderBrush = selected ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"] : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    /// <summary>The theme's pictures are asked for after the page or the background settled for a moment (a slider drag changes them all).</summary>
    private void ScheduleThumbnails()
    {
        string key = ThemePreviewConfig.ContentKey(_host.Document.Root, _host.DeckPageId) + "|" + _themes.Count;
        if (key == _requestedKey)
            return;
        _requestedKey = key;
        _requestTimer.Stop();
        _requestTimer.Start();
    }

    private void OnRequestTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_closed || !_active)
            return;
        // The current theme first: it is the one the user compares the others with.
        string current = _host.Document.Typed.Theme;
        foreach (var theme in _themes.OrderBy(t => t.Name == current ? 0 : 1))
        {
            if (_thumbnails.Request(theme.Name, _host.DeckPageId) is { } file)
                ShowThemePicture(theme.Name, file);
        }
    }

    private void OnThumbnailReady(string theme, string file)
    {
        if (!_closed)
            ShowThemePicture(theme, file);
    }

    private void ShowThemePicture(string theme, string file)
    {
        _themePictures[theme] = file;
        if (!_themeTiles.TryGetValue(theme, out var tile))
            return;
        try
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(file)),
                Stretch = Stretch.Uniform,
                Width = TileWidth,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            tile.Image.Child = image;
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException)
        {
            UrDeckLog.Warn($"Theme picture: {file} could not be shown: {ex.Message}");
        }
    }

    private void OpenThemesFolder()
    {
        try
        {
            Directory.CreateDirectory(_host.Themes.ThemesDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_host.Themes.ThemesDirectory) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            UrDeckLog.Warn($"Could not open the themes folder {_host.Themes.ThemesDirectory}: {ex.Message}");
        }
    }

    // ---------------- backgrounds ----------------

    private void RefreshBackgrounds(Choice kind, string? name)
    {
        var theme = CurrentTheme();
        string themeName = theme?.DisplayName ?? _host.Document.Typed.Theme;
        var (tileWidth, tileHeight) = BackgroundTileSize();
        string key = $"{theme?.Name}|{tileWidth}x{tileHeight}|{string.Join("|", _backgrounds.Select(b => b.Name))}";
        if (key != _backgroundsKey)
        {
            _backgroundsKey = key;
            _backgroundTileWidth = tileWidth;
            _backgroundTileHeight = tileHeight;
            _suggestedGrid.ItemWidth = _allGrid.ItemWidth = tileWidth + FrameExtra + TileGap;
            _backgroundTiles.Clear();
            _suggestedGrid.Children.Clear();
            _allGrid.Children.Clear();

            var suggested = new List<string>();
            if (theme?.DefaultBackground is { } themeDefault && _backgrounds.Any(b => b.Name == themeDefault))
                suggested.Add(themeDefault);
            if (theme != null && _pairing != null)
            {
                foreach (var paired in _pairing.BackgroundsFor(theme.Name))
                {
                    if (!suggested.Contains(paired.Name) && _backgrounds.Any(b => b.Name == paired.Name))
                        suggested.Add(paired.Name);
                }
            }

            _suggestedLabel.Text = $"Suggested for {themeName}";
            _suggestedLabel.Visibility = suggested.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _suggestedGrid.Visibility = _suggestedLabel.Visibility;
            foreach (string suggestion in suggested)
                _suggestedGrid.Children.Add(BackgroundTile(_backgrounds.First(b => b.Name == suggestion)));
            foreach (var other in _backgrounds.Where(b => !suggested.Contains(b.Name)).OrderBy(b => b.Title ?? b.Name, StringComparer.CurrentCultureIgnoreCase))
                _allGrid.Children.Add(BackgroundTile(other));
        }

        // The background the deck shows is marked, also when the theme chooses it: with "Theme's choice" that is the theme's default.
        string? shown = kind == Choice.Shader ? name : kind == Choice.ThemesChoice ? ThemeChoiceName() : null;
        foreach (var (tileName, frame) in _backgroundTiles)
            Select(frame, string.Equals(tileName, shown, StringComparison.Ordinal));
    }

    /// <summary>
    /// A background's picture has the shape of the deck's display: a portrait panel gets a tall tile, a landscape one a wide tile, so
    /// the picture shows how much of the background the deck shows. The tile is never narrower than a title can be read.
    /// </summary>
    private (double Width, double Height) BackgroundTileSize()
    {
        var size = _host.DeckSurface.Size;
        double aspect = size.Width / (double)Math.Max(1, size.Height);
        if (aspect >= 1)
            return (240, Math.Round(240 / aspect));
        double height = 320;
        return (Math.Max(104, Math.Round(height * aspect)), height);
    }

    private Button BackgroundTile(BackgroundCatalogEntry entry)
    {
        var picture = new Border
        {
            Width = _backgroundTileWidth,
            Height = _backgroundTileHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
        };
        var text = new TextBlock
        {
            Text = entry.Title ?? entry.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxLines = 2,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var frame = Frame(new StackPanel { Children = { picture, text }, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button
        {
            Content = frame,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(TileGap / 2, 0, TileGap / 2, TileGap),
            Width = _backgroundTileWidth + FrameExtra,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, entry.Title ?? entry.Name);
        if (!string.IsNullOrWhiteSpace(entry.Description))
            ToolTipService.SetToolTip(button, entry.Description);
        string name = entry.Name;
        button.Click += (_, _) =>
        {
            _editing.UseShader(name);
            Refresh();
        };
        _backgroundTiles[name] = frame;
        _ = LoadTilePictureAsync(name, picture);
        return button;
    }

    /// <summary>The background's own sample; for one without a sample, one frame drawn here once and kept.</summary>
    private async Task LoadTilePictureAsync(string name, Border target)
    {
        try
        {
            if (!_backgroundPictures.TryGetValue(name, out BitmapImage? bitmap))
            {
                var load = _host.Shaders.TryLoad(name);
                if (load.Package is not { } package)
                    return;
                byte[]? sample = BackgroundTiles.SamplePicture(package);
                bitmap = sample != null
                    ? await FromBytesAsync(sample)
                    : await FromDrawnStillAsync(package, _host.Themes.Load(_host.Document.Typed.Theme), (int)_backgroundTileWidth * 2, (int)_backgroundTileHeight * 2);
                if (bitmap == null)
                    return;
                _backgroundPictures[name] = bitmap;
            }

            if (!_closed)
                target.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background '{name}': its picture could not be shown in the editor: {ex.Message}");
        }
    }

    private static async Task<BitmapImage> FromBytesAsync(byte[] bytes)
    {
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }

    private async Task<BitmapImage?> FromDrawnStillAsync(BackgroundPackage package, LoadedTheme theme, int width, int height)
    {
        string key = ThumbnailCache.Key(package.EntryText, package.ManifestText, theme.Name, $"{width}x{height}");
        string cacheName = "bg-" + package.Name;
        string? file = _tileCache.Find(cacheName, key);
        if (file == null)
        {
            byte[]? png = await Task.Run(() => BackgroundTiles.RenderStill(package, theme, width, height));
            if (png == null)
                return null;
            file = _tileCache.Store(cacheName, key, png);
            if (file == null)
                return await FromBytesAsync(png);
        }

        return new BitmapImage(new Uri(file));
    }

    // ---------------- options ----------------

    private void RefreshOptions()
    {
        var (kind, name, path, dim, still) = Current();
        string? problem = kind is Choice.Picture or Choice.Shader ? _host.BackgroundStatus().ConfiguredProblem : null;
        string key = $"{kind}|{name}|{path}|{problem}|{_host.Document.Typed.Theme}|{Editable}";
        if (key == _optionsKey)
        {
            UpdateLiveControls(dim, still);
            return;
        }

        _optionsKey = key;
        _form?.Dispose();
        _form = null;
        _options.Children.Clear();
        _dimSlider = null;
        _stillRadios = null;

        switch (kind)
        {
            case Choice.ThemesChoice:
                BuildThemesChoiceOptions();
                break;
            case Choice.None:
                _options.Children.Add(Sections.Label("Options"));
                _options.Children.Add(Sections.Caption("No background: only the theme's colour shows behind the cards."));
                break;
            case Choice.Shader:
                BuildShaderOptions(name!, problem);
                break;
            case Choice.Picture:
                BuildPictureOptions(path, problem);
                break;
        }

        UpdateLiveControls(dim, still);
    }

    private RadioButtons? _stillRadios;
    private Slider? _dimSlider;
    private TextBlock? _dimText;
    private Button? _dimReset;
    private TextBlock? _dimThemeText;

    private void BuildThemesChoiceOptions()
    {
        _options.Children.Add(Sections.Label("Options"));
        string? name = ThemeChoiceName();
        if (name == null)
        {
            _options.Children.Add(Sections.Caption("This theme shows " + ThemesChoiceText().ToLowerInvariant() + ". Choose a background to change it."));
            return;
        }

        string title = _backgrounds.FirstOrDefault(b => b.Name == name)?.Title ?? name;
        _options.Children.Add(Sections.Caption($"The theme shows {title}. Changing the theme changes the background with it."));
        var customise = new Button { Content = $"Change {title}'s options", IsEnabled = Editable };
        customise.Click += (_, _) =>
        {
            _editing.UseShader(name);
            Refresh();
        };
        _options.Children.Add(customise);
    }

    private void BuildShaderOptions(string name, string? problem)
    {
        var entry = _backgrounds.FirstOrDefault(b => b.Name == name);
        string title = entry?.Title ?? name;
        if (problem != null)
            _options.Children.Add(Problem(problem));

        // Moving or Still.
        _options.Children.Add(Sections.Label("Motion"));
        _stillRadios = new RadioButtons { IsEnabled = Editable };
        _stillRadios.Items.Add("Moving (30 frames a second)");
        _stillRadios.Items.Add("Still (one frame, no GPU cost)");
        _stillRadios.SelectionChanged += (_, _) =>
        {
            if (!_updating && _stillRadios.SelectedIndex >= 0)
                _editing.SetStill(_stillRadios.SelectedIndex == 1);
        };
        _options.Children.Add(_stillRadios);
        _options.Children.Add(Sections.Caption("A moving background draws 30 frames a second while the display is on. Still keeps the look at no cost."));

        // The shader's parameters, generated from its manifest.
        var settings = ManifestSettings.From(_host.Shaders, name);
        if (settings.Count > 0)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = $"{title} options", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var reset = new Button { Content = "Reset", IsEnabled = Editable };
            reset.Click += (_, _) => _editing.ResetParameters();
            Grid.SetColumn(reset, 1);
            header.Children.Add(reset);
            _options.Children.Add(header);
            _form = new Forms.FormBuilder(_formContext()).Build(SettingSchema.Of(typeof(BackgroundSettings), settings), "/background");
            _options.Children.Add(_form.Root);
        }

        AddDim();
    }

    private void BuildPictureOptions(string? path, string? problem)
    {
        _options.Children.Add(Sections.Label("Picture"));
        if (problem != null)
            _options.Children.Add(Problem(problem));

        string full = path == null ? "" : BackgroundImage.ResolvePath(path, _host.DataFolder);
        var thumbnail = new Border { Height = 120, CornerRadius = new CornerRadius(6), Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"], AllowDrop = true };
        thumbnail.DragOver += OnPictureDragOver;
        thumbnail.Drop += OnPictureDrop;
        if (File.Exists(full))
        {
            try
            {
                thumbnail.Child = new Image { Source = new BitmapImage(new Uri(full)) { DecodePixelWidth = 720 }, Stretch = Stretch.UniformToFill };
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException)
            {
                UrDeckLog.Warn($"Background: the picture {full} could not be shown in the editor: {ex.Message}");
            }
        }
        else
        {
            thumbnail.Child = new TextBlock { Text = "Drop a picture here", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        }

        _options.Children.Add(thumbnail);
        _options.Children.Add(new TextBlock { Text = System.IO.Path.GetFileName(full), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        _options.Children.Add(Sections.Caption(System.IO.Path.GetDirectoryName(full) ?? ""));
        _options.Children.Add(Sections.Caption("Cropped to fill the screen. If the file moves, the deck falls back to the theme's background and says so here."));
        var change = new Button { Content = "Change…", IsEnabled = Editable };
        change.Click += async (_, _) => await ChoosePictureAsync();
        _options.Children.Add(change);
        AddDim();
    }

    private InfoBar Problem(string problem) => new()
    {
        Severity = InfoBarSeverity.Warning,
        IsOpen = true,
        IsClosable = false,
        Title = "This background is not shown",
        Message = $"{char.ToUpperInvariant(problem[0])}{problem[1..]} The deck shows {_host.BackgroundStatus().ShownText} instead.",
    };

    private void AddDim()
    {
        _options.Children.Add(Sections.Label("Dim behind cards"));
        _dimSlider = new Slider { Minimum = 0, Maximum = BackgroundSettings.MaxDim * 100, StepFrequency = 1, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = Editable };
        _dimText = new TextBlock { MinWidth = 48, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _dimReset = new Button { Content = "Theme's", IsEnabled = Editable };
        _dimThemeText = Sections.Caption("");
        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(_dimSlider);
        Grid.SetColumn(_dimText, 1);
        line.Children.Add(_dimText);
        Grid.SetColumn(_dimReset, 2);
        line.Children.Add(_dimReset);
        _options.Children.Add(line);
        _options.Children.Add(_dimThemeText);
        _dimSlider.ValueChanged += (_, _) =>
        {
            _dimText.Text = $"{_dimSlider.Value:0} %";
            if (!_updating)
                _editing.SetDim(_dimSlider.Value / 100);
        };
        _dimReset.Click += (_, _) => _editing.SetDim(null);
    }

    /// <summary>The theme's own scrim as a percentage (0 when it has none).</summary>
    private double ThemeScrimPercent() =>
        Math.Round(Math.Clamp(_host.Themes.Load(_host.Document.Typed.Theme).Definition.Background?.Scrim?.Alpha ?? 0, 0, BackgroundSettings.MaxDim) * 100);

    private void UpdateLiveControls(double? dim, bool still)
    {
        if (_stillRadios != null)
            _stillRadios.SelectedIndex = still ? 1 : 0;
        if (_dimSlider == null || _dimText == null || _dimReset == null || _dimThemeText == null)
            return;

        double themed = ThemeScrimPercent();
        if (_dimSlider.FocusState == FocusState.Unfocused)
            _dimSlider.Value = Math.Round((dim ?? themed / 100) * 100);
        _dimText.Text = $"{_dimSlider.Value:0} %";
        _dimReset.Visibility = dim == null ? Visibility.Collapsed : Visibility.Visible;
        _dimThemeText.Text = dim == null ? "The theme's own dimming." : $"The theme's own dimming is {themed:0} %.";
    }

    private void OnMotionLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _motionLevel.SelectedIndex < 0)
            return;
        _editing.SetMotionLevel(MotionChoices[_motionLevel.SelectedIndex].Level);
    }

    // ---------------- pictures ----------------

    private static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private async Task ChoosePictureAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        foreach (string extension in PictureExtensions)
            picker.FileTypeFilter.Add(extension);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_owner));
        var file = await picker.PickSingleFileAsync();
        if (_closed)
            return;
        if (file?.Path is { } path)
            _editing.UsePicture(path);
        Refresh();
    }

    private static void OnPictureDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Use as the background";
        }
    }

    private async void OnPictureDrop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<Windows.Storage.StorageFile>()
                .FirstOrDefault(f => PictureExtensions.Contains(System.IO.Path.GetExtension(f.Path), StringComparer.OrdinalIgnoreCase));
            if (file != null && !_closed)
            {
                _editing.UsePicture(file.Path);
                Refresh();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background: the dropped file could not be used: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }
}
