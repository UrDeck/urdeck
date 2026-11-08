// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Editor;

/// <summary>A page's grid: how many columns and whole rows the deck's layout gives it.</summary>
public readonly record struct GridSize(int Columns, int Rows)
{
    /// <summary>The grid of a layout the deck made.</summary>
    public static GridSize From(ChromeLayoutResult chrome) => new(GridLayoutManager.Columns, chrome.Rows);

    /// <summary>
    /// The grid the deck's layout gives a page of <paramref name="config"/> on <paramref name="surface"/> with
    /// <paramref name="theme"/>: the rows left by the dock and the page indicator, as the deck session computes them.
    /// </summary>
    public static GridSize For(DeckSurface surface, UrDeckConfig config, ThemeDefinition theme)
    {
        var chrome = ChromeLayout.Compute(surface.Size, config.Pager.GetIndicatorMode(), config.Pages.Count, theme.Indicator!.BandHeight!.Value,
            config.Dock.Count > 0 ? theme.Dock!.Height!.Value : 0);
        return From(chrome);
    }
}

/// <summary>A cell of the grid: column and row from the top left, 0-based.</summary>
public readonly record struct Cell(int Col, int Row);

/// <summary>
/// A widget of the configuration: the page it is on (by id) and its index there, or a slot of the dock. Resolved fresh
/// against the configuration each time it is used.
/// </summary>
public sealed record WidgetRef(string PageId, int Index, bool InDock = false)
{
    public static WidgetRef Dock(int index) => new("", index, InDock: true);
}

/// <summary>
/// The editor's selection: a widget and what it was (its type, and its cell or dock slot), so that after an undo or a change
/// from outside it is found again when it still exists, at its index or elsewhere on its page.
/// </summary>
public sealed record WidgetSelection(WidgetRef Ref, string Key)
{
    /// <summary>The selection of the widget at <paramref name="widget"/>; null when there is none.</summary>
    public static WidgetSelection? Of(UrDeckConfig config, WidgetRef widget) =>
        PageEditing.Widget(config, widget) is { } found ? new WidgetSelection(widget, KeyOf(found, widget)) : null;

    /// <summary>This selection in <paramref name="config"/>: the same widget, perhaps at another index; null when it is gone.</summary>
    public WidgetSelection? Resolve(UrDeckConfig config)
    {
        if (PageEditing.Widget(config, Ref) is { } same && KeyOf(same, Ref) == Key)
            return this;
        if (Ref.InDock)
            return null;
        int page = PageEditing.PageIndex(config, Ref.PageId);
        if (page < 0)
            return null;
        var widgets = config.Pages[page].Widgets;
        for (int i = 0; i < widgets.Count; i++)
        {
            var candidate = Ref with { Index = i };
            if (KeyOf(widgets[i], candidate) == Key)
                return new WidgetSelection(candidate, Key);
        }

        return null;
    }

    private static string KeyOf(WidgetConfig widget, WidgetRef at) =>
        at.InDock ? $"{widget.WidgetTypeId}#{at.Index}" : $"{widget.WidgetTypeId}@{widget.Col},{widget.Row}";
}

/// <summary>Why a size does not fit where a widget is: what is in the way (or that it runs past the bottom) and where it would fit.</summary>
/// <param name="Size">The size asked for, as <c>4x4</c>.</param>
/// <param name="Blocker">The display name of the widget in the way; null when nothing is (past the bottom).</param>
/// <param name="PastBottom">The size would run past the bottom of the page.</param>
/// <param name="FirstFree">The first free space of that size on the page; null when there is none.</param>
public sealed record FitProblem(string Size, string? Blocker, bool PastBottom, Cell? FirstFree)
{
    /// <summary>"4x4 does not fit here."</summary>
    public string Title => $"{Size} does not fit here.";

    /// <summary>"Weather is in the way." / "It would run past the bottom of the page.", and when there is no room at all, that too.</summary>
    public string Message
    {
        get
        {
            string why = PastBottom ? "It would run past the bottom of the page." : Blocker != null ? $"{Blocker} is in the way." : "";
            return FirstFree == null ? $"{why} There is no free {Size} space on this page.".Trim() : why;
        }
    }
}

/// <summary>What dropping a dragged widget at a cell would do.</summary>
public enum DropKind
{
    /// <summary>The cells are free: the widget moves there.</summary>
    Free,

    /// <summary>One widget of the same size is there: the two exchange places.</summary>
    Swap,

    /// <summary>Something is in the way, or the widget would leave the grid: nothing happens.</summary>
    Blocked,
}

/// <summary>The result of <see cref="PageEditing.DropTarget"/>.</summary>
/// <param name="Kind">What a drop would do.</param>
/// <param name="Cell">The cell the widget would take (its column clamped to the grid).</param>
/// <param name="Other">The widget to swap with (<see cref="DropKind.Swap"/>).</param>
/// <param name="Blocker">The display name of the widget in the way or to swap with; null when none is (past the bottom).</param>
/// <param name="PastBottom">The widget would run past the bottom of the page.</param>
public sealed record DropTarget(DropKind Kind, Cell Cell, WidgetRef? Other = null, string? Blocker = null, bool PastBottom = false)
{
    /// <summary>The text the editor shows while the pointer is over this target.</summary>
    public string Message => Kind switch
    {
        DropKind.Free => "",
        DropKind.Swap => $"Release to swap with {Blocker}",
        _ => PastBottom ? "It would run past the bottom of the page" : $"{Blocker} is in the way",
    };
}

/// <summary>One declared size of a widget as a resize stop: whether it fits where the widget is, and what is in the way if not.</summary>
public sealed record ResizeStop(int Width, int Height, bool Fits, string? Blocker, bool PastBottom)
{
    /// <summary>"4x2".</summary>
    public string Label => $"{Width}x{Height}";
}

/// <summary>What an editing operation did: the widget it made or changed, or why it did nothing.</summary>
public sealed record EditOutcome(bool Applied, WidgetRef? Widget = null, FitProblem? Problem = null, string? Message = null, string? PageId = null)
{
    public static readonly EditOutcome Nothing = new(false);
}

public enum PlacementIssueKind
{
    /// <summary>The widget's size is not one its type declares.</summary>
    UndeclaredSize,

    /// <summary>The widget lies on cells an earlier widget of the page takes.</summary>
    Overlap,

    /// <summary>The widget lies partly or wholly outside the page's grid.</summary>
    OffGrid,
}

/// <summary>A declared size offered for a widget at <see cref="At"/>; <see cref="Moves"/> when that is not where the widget is.</summary>
public sealed record SizeSuggestion(int Width, int Height, Cell At, bool Moves)
{
    public string Label => $"{Width}x{Height}";
}

/// <summary>
/// One thing wrong with a widget's placement: <paramref name="Index"/> is the widget (the later one of an overlap, which
/// is also the one to move), <paramref name="OtherIndex"/> the earlier one it overlaps.
/// </summary>
public sealed record PlacementIssue(
    PlacementIssueKind Kind,
    int Index,
    int? OtherIndex,
    string Message,
    string Size,
    IReadOnlyList<string> DeclaredSizes,
    IReadOnlyList<SizeSuggestion> Suggestions);

/// <summary>
/// The editor's operations on the configuration: each is one transaction of the document (one undo step, labelled as the
/// editor's Undo says it), written behind and applied to the deck. They never place a widget where it does not fit and never
/// move another widget. Owner thread only, like the document.
/// </summary>
public sealed class PageEditing
{
    private readonly ConfigDocument _document;
    private readonly Func<string, WidgetDescriptor?> _describe;
    private readonly Func<string, string?>? _readingLabel;

    /// <param name="document">The configuration.</param>
    /// <param name="describe">The installed widget types, by id.</param>
    /// <param name="readingLabel">A reading's label for the display names of widgets (a Stats card's first reading); optional.</param>
    public PageEditing(ConfigDocument document, Func<string, WidgetDescriptor?> describe, Func<string, string?>? readingLabel = null)
    {
        _document = document;
        _describe = describe;
        _readingLabel = readingLabel;
    }

    private UrDeckConfig Config => _document.Typed;

    // ---------------- reading the configuration ----------------

    /// <summary>The index of the page with <paramref name="pageId"/>, or -1.</summary>
    public static int PageIndex(UrDeckConfig config, string? pageId) =>
        pageId == null ? -1 : config.Pages.FindIndex(p => string.Equals(p.Id, pageId, StringComparison.Ordinal));

    /// <summary>The widget at <paramref name="widget"/>; null when there is none.</summary>
    public static WidgetConfig? Widget(UrDeckConfig config, WidgetRef widget)
    {
        if (widget.InDock)
            return widget.Index >= 0 && widget.Index < config.Dock.Count ? config.Dock[widget.Index] : null;
        int page = PageIndex(config, widget.PageId);
        if (page < 0)
            return null;
        var widgets = config.Pages[page].Widgets;
        return widget.Index >= 0 && widget.Index < widgets.Count ? widgets[widget.Index] : null;
    }

    /// <summary>The cells a widget takes, clamped as the deck's layout clamps them.</summary>
    public static (int Col, int Row, int Width, int Height) Area(WidgetConfig widget)
    {
        int width = Math.Clamp(widget.Width, 1, GridLayoutManager.Columns);
        return (Math.Clamp(widget.Col, 0, GridLayoutManager.Columns - width), Math.Max(0, widget.Row), width, Math.Max(1, widget.Height));
    }

    /// <summary>Whether a <paramref name="w"/>x<paramref name="h"/> widget at the cell is inside the grid and overlaps no widget but <paramref name="except"/>.</summary>
    public static bool Fits(IReadOnlyList<WidgetConfig> widgets, int? except, int col, int row, int w, int h, GridSize grid) =>
        col >= 0 && row >= 0 && col + w <= grid.Columns && row + h <= grid.Rows && BlockerIndex(widgets, except, col, row, w, h) < 0;

    /// <summary>The index of the first widget but <paramref name="except"/> that a <paramref name="w"/>x<paramref name="h"/> widget at the cell would overlap, or -1.</summary>
    public static int BlockerIndex(IReadOnlyList<WidgetConfig> widgets, int? except, int col, int row, int w, int h)
    {
        for (int i = 0; i < widgets.Count; i++)
        {
            if (i == except)
                continue;
            var (oc, or, ow, oh) = Area(widgets[i]);
            if (col < oc + ow && oc < col + w && row < or + oh && or < row + h)
                return i;
        }

        return -1;
    }

    /// <summary>The first cell, scanning rows from the top and columns from the left, where the size fits; null when there is none.</summary>
    public static Cell? FirstFree(IReadOnlyList<WidgetConfig> widgets, int w, int h, GridSize grid, int? except = null)
    {
        for (int row = 0; row + h <= grid.Rows; row++)
        {
            for (int col = 0; col + w <= grid.Columns; col++)
            {
                if (Fits(widgets, except, col, row, w, h, grid))
                    return new Cell(col, row);
            }
        }

        return null;
    }

    /// <summary>The empty cells of a page: the cells no widget takes.</summary>
    public static IReadOnlyList<Cell> EmptyCells(IReadOnlyList<WidgetConfig> widgets, GridSize grid)
    {
        var cells = new List<Cell>();
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int col = 0; col < grid.Columns; col++)
            {
                if (BlockerIndex(widgets, null, col, row, 1, 1) < 0)
                    cells.Add(new Cell(col, row));
            }
        }

        return cells;
    }

    /// <summary>The type's name, or its id when it is not installed.</summary>
    public string TypeName(WidgetConfig widget) => _describe(widget.WidgetTypeId)?.Name ?? widget.WidgetTypeId;

    /// <summary>
    /// The widget as a message names it: its type and, when it has one, the first label-like value in quotes (its <c>label</c>,
    /// its first reading's label, or its target's file name): <c>Stats "CPU load"</c>.
    /// </summary>
    public string Describe(WidgetConfig widget)
    {
        string name = TypeName(widget);
        return LabelOf(widget) is { Length: > 0 } label ? $"{name} \"{label}\"" : name;
    }

    private string? LabelOf(WidgetConfig widget)
    {
        var extra = widget.ExtensionData;
        if (extra == null)
            return null;
        if (Text(extra, "label") is { } label)
            return label;
        if (Get(extra, "slots") is { ValueKind: JsonValueKind.Array } slots && slots.GetArrayLength() > 0 && slots[0].ValueKind == JsonValueKind.Object)
        {
            var first = slots[0];
            if (first.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String && l.GetString() is { Length: > 0 } slotLabel)
                return slotLabel;
            if (first.TryGetProperty("reading", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() is { Length: > 0 } reading)
                return _readingLabel?.Invoke(reading) ?? reading;
        }

        if (Text(extra, "reading") is { } control)
            return _readingLabel?.Invoke(control) ?? control;
        if (Text(extra, "target") is { } target)
        {
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
                return uri.Host;
            // A target is a Windows path; split on both separators so the name is the same on every system.
            string trimmed = target.TrimEnd('\\', '/');
            string last = trimmed[(trimmed.LastIndexOfAny(['\\', '/']) + 1)..];
            string file = Path.GetFileNameWithoutExtension(last);
            return file.Length > 0 ? file : target;
        }

        return null;
    }

    private static JsonElement? Get(Dictionary<string, JsonElement> extra, string key) =>
        extra.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } pair ? pair.Value : null;

    private static string? Text(Dictionary<string, JsonElement> extra, string key) =>
        Get(extra, key) is { ValueKind: JsonValueKind.String } value && value.GetString() is { Length: > 0 } text ? text : null;

    // ---------------- widgets ----------------

    /// <summary>
    /// Adds a widget of <paramref name="typeId"/> with its default settings at its default size: at <paramref name="at"/> (its
    /// column moved left as far as needed) when it fits there, else at the first free space. When the default size has no room, the largest
    /// declared size that fits is used. "Add Stats".
    /// </summary>
    public EditOutcome AddWidget(string pageId, string typeId, GridSize grid, Cell? at = null)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || _describe(typeId) is not { } descriptor)
            return EditOutcome.Nothing;

        var widgets = config.Pages[page].Widgets;
        var preferred = WidgetDefaults.Size(descriptor);
        // The default size first, then the other declared sizes from the largest down: a page with only a 4x1 row left still takes a clock.
        var sizes = new List<System.Drawing.Size> { preferred };
        sizes.AddRange(descriptor.SupportedSizes.Where(z => z != preferred).OrderByDescending(z => z.Width * z.Height).ThenByDescending(z => z.Width));
        System.Drawing.Size size = preferred;
        Cell? spot = null;
        foreach (var candidate in sizes)
        {
            if (at is { } cell)
            {
                int col = Math.Min(cell.Col, grid.Columns - candidate.Width);
                if (Fits(widgets, null, col, cell.Row, candidate.Width, candidate.Height, grid))
                    spot = new Cell(col, cell.Row);
            }

            spot ??= FirstFree(widgets, candidate.Width, candidate.Height, grid);
            if (spot != null)
            {
                size = candidate;
                break;
            }
        }

        if (spot is not { } place)
            return new EditOutcome(false, Message: "This page has no free space for it. Add a page, or remove a widget first.");

        var node = WidgetDefaults.Node(descriptor, place.Col, place.Row, size);
        int index = widgets.Count;
        bool applied = _document.Edit($"Add {descriptor.Name}", e =>
        {
            if (e.Get($"/pages/{page}/widgets") is not JsonArray)
                e.Set($"/pages/{page}/widgets", new JsonArray());
            e.Insert($"/pages/{page}/widgets", index, node);
        });
        return applied ? new EditOutcome(true, new WidgetRef(pageId, index)) : EditOutcome.Nothing;
    }

    /// <summary>
    /// Makes the widget <paramref name="w"/>x<paramref name="h"/> in place: the same row, its column moved left as far as needed.
    /// When that does not fit nothing changes and the outcome says why. "Make Stats 4x2".
    /// </summary>
    public EditOutcome Resize(WidgetRef widget, int w, int h, GridSize grid)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current)
            return EditOutcome.Nothing;
        int page = PageIndex(config, widget.PageId);
        var widgets = config.Pages[page].Widgets;
        var (col, row, _, _) = Area(current);
        col = Math.Min(col, grid.Columns - w);
        string size = $"{w}x{h}";
        if (!Fits(widgets, widget.Index, col, row, w, h, grid))
        {
            bool past = row + h > grid.Rows;
            int blocker = past ? -1 : BlockerIndex(widgets, widget.Index, col, row, w, h);
            var free = FirstFree(widgets, w, h, grid, widget.Index);
            return new EditOutcome(false, widget, new FitProblem(size, blocker >= 0 ? Describe(widgets[blocker]) : null, past, free));
        }

        bool applied = _document.Edit($"Make {TypeName(current)} {size}", e => Place(e, $"/pages/{page}/widgets/{widget.Index}", col, row, w, h));
        return new EditOutcome(applied, widget);
    }

    /// <summary>Moves the widget to <paramref name="cell"/> at <paramref name="w"/>x<paramref name="h"/> when it fits there: "Move Stats".</summary>
    public EditOutcome MoveTo(WidgetRef widget, Cell cell, int w, int h, GridSize grid)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current)
            return EditOutcome.Nothing;
        int page = PageIndex(config, widget.PageId);
        if (!Fits(config.Pages[page].Widgets, widget.Index, cell.Col, cell.Row, w, h, grid))
            return new EditOutcome(false, widget);
        bool applied = _document.Edit($"Move {TypeName(current)}", e => Place(e, $"/pages/{page}/widgets/{widget.Index}", cell.Col, cell.Row, w, h));
        return new EditOutcome(applied, widget);
    }

    /// <summary>
    /// What dropping the page widget <paramref name="widget"/> with its top-left corner over <paramref name="cell"/> would do: its
    /// column is moved left as far as needed to stay in the grid. Free when it fits; Swap when exactly one widget of its own size
    /// is under it; otherwise Blocked, with the widget in the way or that it runs past the bottom.
    /// </summary>
    public DropTarget DropTarget(WidgetRef widget, Cell cell, GridSize grid)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current)
            return new DropTarget(DropKind.Blocked, cell);
        var widgets = config.Pages[PageIndex(config, widget.PageId)].Widgets;
        var (_, _, w, h) = Area(current);
        var at = new Cell(Math.Clamp(cell.Col, 0, Math.Max(0, grid.Columns - w)), Math.Max(0, cell.Row));
        if (at.Row + h > grid.Rows)
            return new DropTarget(DropKind.Blocked, at, PastBottom: true);

        var hit = new List<int>();
        for (int i = 0; i < widgets.Count; i++)
        {
            if (i == widget.Index)
                continue;
            var (oc, or, ow, oh) = Area(widgets[i]);
            if (at.Col < oc + ow && oc < at.Col + w && at.Row < or + oh && or < at.Row + h)
                hit.Add(i);
        }

        if (hit.Count == 0)
            return new DropTarget(DropKind.Free, at);
        var (_, _, bw, bh) = Area(widgets[hit[0]]);
        if (hit.Count == 1 && bw == w && bh == h)
            return new DropTarget(DropKind.Swap, at, widget with { Index = hit[0] }, Describe(widgets[hit[0]]));
        return new DropTarget(DropKind.Blocked, at, Blocker: Describe(widgets[hit[0]]));
    }

    /// <summary>
    /// What is wrong with the placement of the widgets of a page (a hand-edited file can hold all of it), ordered by row then
    /// column: a widget at a size its type does not declare (installed types only), two widgets on the same cells (reported on
    /// the later one) and a widget outside the grid. Nothing is changed.
    /// </summary>
    public IReadOnlyList<PlacementIssue> PlacementIssues(IReadOnlyList<WidgetConfig> widgets, GridSize grid)
    {
        var issues = new List<PlacementIssue>();
        for (int i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            var (col, row, w, h) = Area(widget);
            if (widget.Col < 0 || widget.Row < 0 || widget.Width > grid.Columns || widget.Col + widget.Width > grid.Columns || widget.Row + widget.Height > grid.Rows)
                issues.Add(new PlacementIssue(PlacementIssueKind.OffGrid, i, null, $"{Describe(widget)} is outside the page.", $"{widget.Width}x{widget.Height}", [], []));

            for (int j = 0; j < i; j++)
            {
                var (oc, or, ow, oh) = Area(widgets[j]);
                if (col < oc + ow && oc < col + w && row < or + oh && or < row + h)
                    issues.Add(new PlacementIssue(PlacementIssueKind.Overlap, i, j, $"Two widgets overlap: {Describe(widgets[j])} and {Describe(widget)}.", $"{w}x{h}", [], []));
            }

            if (_describe(widget.WidgetTypeId) is { } descriptor && !descriptor.SupportedSizes.Contains(new System.Drawing.Size(w, h)))
            {
                string declared = string.Join(", ", descriptor.SupportedSizes.Select(s => $"{s.Width}x{s.Height}"));
                issues.Add(new PlacementIssue(PlacementIssueKind.UndeclaredSize, i, null,
                    $"{w}x{h} is not a {descriptor.Name} size. {descriptor.Name} comes in {declared}. Nothing changes until you choose.",
                    $"{w}x{h}", [.. descriptor.SupportedSizes.Select(s => $"{s.Width}x{s.Height}")], NearestSizes(widgets, i, descriptor, grid)));
            }
        }

        return [.. issues.OrderBy(x => Area(widgets[x.Index]).Row).ThenBy(x => Area(widgets[x.Index]).Col).ThenBy(x => x.Index)];
    }

    // Up to two declared sizes that fit where the widget is, nearest first (difference in cell count, then the same width); when none fits, the nearest that fits at the first free space.
    private static List<SizeSuggestion> NearestSizes(IReadOnlyList<WidgetConfig> widgets, int index, WidgetDescriptor descriptor, GridSize grid)
    {
        var (col, row, w, h) = Area(widgets[index]);
        var ordered = descriptor.SupportedSizes
            .OrderBy(s => Math.Abs(s.Width * s.Height - w * h))
            .ThenBy(s => Math.Abs(s.Width - w))
            .ThenBy(s => s.Width)
            .ThenBy(s => s.Height)
            .ToList();
        var here = ordered
            .Select(s => (Size: s, Col: Math.Min(col, grid.Columns - s.Width)))
            .Where(s => s.Col >= 0 && Fits(widgets, index, s.Col, row, s.Size.Width, s.Size.Height, grid))
            .Take(2)
            .Select(s => new SizeSuggestion(s.Size.Width, s.Size.Height, new Cell(s.Col, row), Moves: false))
            .ToList();
        if (here.Count > 0)
            return here;
        foreach (var size in ordered)
        {
            if (FirstFree(widgets, size.Width, size.Height, grid, index) is { } free)
                return [new SizeSuggestion(size.Width, size.Height, free, Moves: true)];
        }

        return [];
    }

    /// <summary>
    /// Moves a page widget to the first free space for its size, as one step: "Move Stats". Nothing changes when the grid has no
    /// room for it.
    /// </summary>
    public EditOutcome MoveToFirstFree(WidgetRef widget, GridSize grid)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current)
            return EditOutcome.Nothing;
        var widgets = config.Pages[PageIndex(config, widget.PageId)].Widgets;
        var (_, _, w, h) = Area(current);
        return FirstFree(widgets, w, h, grid, widget.Index) is { } free ? MoveTo(widget, free, w, h, grid) : EditOutcome.Nothing;
    }

    /// <summary>Exchanges the places of two page widgets of the same size in one step: "Swap Stats and Weather".</summary>
    public EditOutcome Swap(WidgetRef a, WidgetRef b)
    {
        var config = Config;
        if (a.InDock || b.InDock || a.PageId != b.PageId || a.Index == b.Index
            || Widget(config, a) is not { } first || Widget(config, b) is not { } second)
            return EditOutcome.Nothing;
        var (ac, ar, aw, ah) = Area(first);
        var (bc, br, bw, bh) = Area(second);
        if (aw != bw || ah != bh)
            return EditOutcome.Nothing;
        int page = PageIndex(config, a.PageId);
        bool applied = _document.Edit($"Swap {TypeName(first)} and {TypeName(second)}", e =>
        {
            Place(e, $"/pages/{page}/widgets/{a.Index}", bc, br, aw, ah);
            Place(e, $"/pages/{page}/widgets/{b.Index}", ac, ar, bw, bh);
        });
        return new EditOutcome(applied, a);
    }

    /// <summary>The widget's declared sizes as stops anchored at its top-left, each with whether it fits (as <see cref="Resize"/> places it).</summary>
    public IReadOnlyList<ResizeStop> ResizeStops(WidgetRef widget, GridSize grid)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current || _describe(current.WidgetTypeId) is not { } descriptor)
            return [];
        var widgets = config.Pages[PageIndex(config, widget.PageId)].Widgets;
        var (col, row, _, _) = Area(current);
        var stops = new List<ResizeStop>();
        foreach (var size in descriptor.SupportedSizes)
        {
            int c = Math.Min(col, grid.Columns - size.Width);
            bool past = row + size.Height > grid.Rows;
            int blocker = past || c < 0 ? -1 : BlockerIndex(widgets, widget.Index, c, row, size.Width, size.Height);
            stops.Add(new ResizeStop(size.Width, size.Height, !past && c >= 0 && blocker < 0, blocker >= 0 ? Describe(widgets[blocker]) : null, past));
        }

        return stops;
    }

    /// <summary>Whether the widget's type supports 1x1, the size of a dock slot.</summary>
    public bool SupportsDock(WidgetConfig widget) => _describe(widget.WidgetTypeId)?.SupportedSizes.Contains(new System.Drawing.Size(1, 1)) == true;

    /// <summary>
    /// Moves the page widget into the dock at <paramref name="index"/> (clamped): removed from the page, its object kept as it is.
    /// Nothing when the dock is full or the type has no 1x1. "Move Stats to the dock".
    /// </summary>
    public EditOutcome MoveToDock(WidgetRef widget, int index)
    {
        var config = Config;
        if (widget.InDock || Widget(config, widget) is not { } current || !SupportsDock(current) || config.Dock.Count >= DockLayout.MaxSlots)
            return EditOutcome.Nothing;
        int page = PageIndex(config, widget.PageId);
        if (_document.Root["pages"] is not JsonArray pages || pages[page]?["widgets"] is not JsonArray list || list[widget.Index] is not { } node)
            return EditOutcome.Nothing;
        int at = Math.Clamp(index, 0, config.Dock.Count);
        var copy = node.DeepClone();
        bool applied = _document.Edit($"Move {TypeName(current)} to the dock", e =>
        {
            e.Remove($"/pages/{page}/widgets/{widget.Index}");
            if (e.Get("/dock") is not JsonArray)
                e.Set("/dock", new JsonArray());
            e.Insert("/dock", at, copy);
        });
        return applied ? new EditOutcome(true, WidgetRef.Dock(at)) : EditOutcome.Nothing;
    }

    /// <summary>Adds a widget of the type with its default settings at 1x1 as the dock's last slot; the type must support 1x1 and the dock have room. "Add Shortcut to the dock".</summary>
    public EditOutcome AddToDock(string typeId)
    {
        var config = Config;
        if (_describe(typeId) is not { } descriptor || !descriptor.SupportedSizes.Contains(new System.Drawing.Size(1, 1)) || config.Dock.Count >= DockLayout.MaxSlots)
            return EditOutcome.Nothing;
        var node = WidgetDefaults.Node(descriptor, 0, 0, new System.Drawing.Size(1, 1));
        int index = config.Dock.Count;
        bool applied = _document.Edit($"Add {descriptor.Name} to the dock", e =>
        {
            if (e.Get("/dock") is not JsonArray)
                e.Set("/dock", new JsonArray());
            e.Insert("/dock", index, node);
        });
        return applied ? new EditOutcome(true, WidgetRef.Dock(index)) : EditOutcome.Nothing;
    }

    /// <summary>Moves a dock slot to <paramref name="to"/> among the slots: "Move Shortcut earlier" / "later" / "in the dock".</summary>
    public EditOutcome MoveInDock(int from, int to)
    {
        var config = Config;
        if (from < 0 || from >= config.Dock.Count || to < 0 || to >= config.Dock.Count || from == to)
            return EditOutcome.Nothing;
        string where = Math.Abs(from - to) == 1 ? (to < from ? "earlier" : "later") : "in the dock";
        bool applied = _document.Edit($"Move {TypeName(config.Dock[from])} {where}", e => e.Move("/dock", from, to));
        return applied ? new EditOutcome(true, WidgetRef.Dock(to)) : EditOutcome.Nothing;
    }

    /// <summary>
    /// Moves a dock slot onto the page <paramref name="pageId"/>: at <paramref name="cell"/> or, without one, the first free space.
    /// It takes the size it had on the page when the type supports it and it fits, else 1x1. Nothing (with a message) when no
    /// space fits. "Move Shortcut to the page".
    /// </summary>
    public EditOutcome MoveToPage(int slot, string pageId, GridSize grid, Cell? cell = null)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || slot < 0 || slot >= config.Dock.Count || _document.Root["dock"] is not JsonArray dock || dock[slot] is not { } node)
            return EditOutcome.Nothing;
        var current = config.Dock[slot];
        var widgets = config.Pages[page].Widgets;
        var one = new System.Drawing.Size(1, 1);
        var kept = new System.Drawing.Size(current.Width, current.Height);
        var sizes = kept != one && _describe(current.WidgetTypeId)?.SupportedSizes.Contains(kept) == true ? new[] { kept, one } : new[] { one };

        Cell? spot = null;
        var chosen = one;
        foreach (var size in sizes)
        {
            if (cell is { } c)
            {
                int col = Math.Min(c.Col, grid.Columns - size.Width);
                if (Fits(widgets, null, col, c.Row, size.Width, size.Height, grid))
                    spot = new Cell(col, c.Row);
            }
            else
            {
                spot = FirstFree(widgets, size.Width, size.Height, grid);
            }

            if (spot != null)
            {
                chosen = size;
                break;
            }
        }

        if (spot is not { } place)
            return new EditOutcome(false, Message: "There is no free space for it on this page.");
        var copy = node.DeepClone();
        int index = widgets.Count;
        bool applied = _document.Edit($"Move {TypeName(current)} to the page", e =>
        {
            e.Remove($"/dock/{slot}");
            if (e.Get($"/pages/{page}/widgets") is not JsonArray)
                e.Set($"/pages/{page}/widgets", new JsonArray());
            e.Insert($"/pages/{page}/widgets", index, copy);
            Place(e, $"/pages/{page}/widgets/{index}", place.Col, place.Row, chosen.Width, chosen.Height);
        });
        return applied ? new EditOutcome(true, new WidgetRef(pageId, index)) : EditOutcome.Nothing;
    }

    /// <summary>
    /// Moves a page widget to the first free space of the same size on another page, keeping its size and settings.
    /// Nothing (with a message) when that page has no such space. "Move Stats to Other".
    /// </summary>
    public EditOutcome MoveWidgetToPage(WidgetRef widget, string targetPageId, GridSize grid)
    {
        var config = Config;
        int from = PageIndex(config, widget.PageId);
        int to = PageIndex(config, targetPageId);
        if (widget.InDock || from < 0 || to < 0 || from == to || Widget(config, widget) is not { } current
            || _document.Root["pages"] is not JsonArray pages || pages[from]?["widgets"] is not JsonArray list || list[widget.Index] is not { } node)
            return EditOutcome.Nothing;
        var target = config.Pages[to];
        if (FirstFree(target.Widgets, current.Width, current.Height, grid) is not { } spot)
            return new EditOutcome(false, Message: $"There is no free {current.Width}x{current.Height} space on {target.Name}.");
        var copy = node.DeepClone();
        int index = target.Widgets.Count;
        bool applied = _document.Edit($"Move {TypeName(current)} to {target.Name}", e =>
        {
            e.Remove($"/pages/{from}/widgets/{widget.Index}");
            if (e.Get($"/pages/{to}/widgets") is not JsonArray)
                e.Set($"/pages/{to}/widgets", new JsonArray());
            e.Insert($"/pages/{to}/widgets", index, copy);
            Place(e, $"/pages/{to}/widgets/{index}", spot.Col, spot.Row, current.Width, current.Height);
        });
        return applied ? new EditOutcome(true, new WidgetRef(targetPageId, index)) : EditOutcome.Nothing;
    }

    /// <summary>Removes the widget, from its page or the dock: "Remove Stats".</summary>
    public EditOutcome RemoveWidget(WidgetRef widget)
    {
        var config = Config;
        if (Widget(config, widget) is not { } current)
            return EditOutcome.Nothing;
        string path = widget.InDock ? $"/dock/{widget.Index}" : $"/pages/{PageIndex(config, widget.PageId)}/widgets/{widget.Index}";
        bool applied = _document.Edit($"Remove {TypeName(current)}", e => e.Remove(path));
        return new EditOutcome(applied);
    }

    private static void Place(ConfigEditor e, string path, int col, int row, int w, int h)
    {
        e.Set(Key(e, path, "col"), col);
        e.Set(Key(e, path, "row"), row);
        e.Set(Key(e, path, "width"), w);
        e.Set(Key(e, path, "height"), h);
    }

    // The file is read case-insensitively: write the key the file already uses.
    private static string Key(ConfigEditor e, string objectPath, string key) => $"{objectPath}/{JsonPointer.Escape(e.FindKey(objectPath, key) ?? key)}";

    // ---------------- pages ----------------

    /// <summary>Adds an empty page named "Page N" after the page <paramref name="afterPageId"/> (at the end without one): "Add a page".</summary>
    public EditOutcome AddPage(string? afterPageId)
    {
        var config = Config;
        int after = PageIndex(config, afterPageId);
        int at = after < 0 ? config.Pages.Count : after + 1;
        var names = config.Pages.Select(p => p.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        int n = config.Pages.Count + 1;
        while (names.Contains($"Page {n}"))
            n++;
        string id = PageIds.New(TakenIds(config));
        var node = new JsonObject { ["id"] = id, ["name"] = $"Page {n}", ["widgets"] = new JsonArray() };
        bool applied = _document.Edit("Add a page", e => InsertPage(e, at, node));
        return new EditOutcome(applied, PageId: applied ? id : null);
    }

    /// <summary>
    /// The first run's "Start with an empty page": the pages become one empty page called Main, the start page, and the dock
    /// is emptied, as one undo step (back to what was there).
    /// </summary>
    public EditOutcome StartWithEmptyPage()
    {
        string id = PageIds.New(new HashSet<string>(StringComparer.Ordinal));
        bool applied = _document.Edit("Start with an empty page", e =>
        {
            e.Set("/pages", new JsonArray(new JsonObject { ["id"] = id, ["name"] = "Main", ["widgets"] = new JsonArray() }));
            e.Set("/startPage", id);
            e.Set("/dock", new JsonArray());
        });
        return new EditOutcome(applied, PageId: applied ? id : null);
    }

    /// <summary>Renames the page; typing into the name is one undo step (merged by the page). "Rename Page 2".</summary>
    public bool RenamePage(string pageId, string name)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || config.Pages[page].Name == name)
            return false;
        return _document.Edit($"Rename {config.Pages[page].Name}", e => e.Set(Key(e, $"/pages/{page}", "name"), name), mergeKey: $"page-name:{pageId}");
    }

    /// <summary>Copies the page after itself, named "&lt;name&gt; copy", with a new id: "Duplicate Main".</summary>
    public EditOutcome DuplicatePage(string pageId)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || _document.Root["pages"] is not JsonArray pages || pages[page] is not JsonObject source)
            return EditOutcome.Nothing;
        string id = PageIds.New(TakenIds(config));
        var copy = (JsonObject)source.DeepClone();
        foreach (string key in copy.Select(p => p.Key).Where(k => string.Equals(k, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "name", StringComparison.OrdinalIgnoreCase)).ToList())
            copy.Remove(key);
        copy.Insert(0, "id", id);
        copy.Insert(1, "name", config.Pages[page].Name + " copy");
        bool applied = _document.Edit($"Duplicate {config.Pages[page].Name}", e => InsertPage(e, page + 1, copy));
        return new EditOutcome(applied, PageId: applied ? id : null);
    }

    /// <summary>Moves the page one place to the left among the tabs (<paramref name="by"/> -1) or to the right (+1): "Move Main right".</summary>
    public bool MovePage(string pageId, int by)
    {
        var config = Config;
        int from = PageIndex(config, pageId);
        int to = from + by;
        if (from < 0 || to < 0 || to >= config.Pages.Count || by == 0)
            return false;
        return _document.Edit($"Move {config.Pages[from].Name} {(by < 0 ? "left" : "right")}", e => e.Move("/pages", from, to));
    }

    /// <summary>
    /// The page that becomes the start page when <paramref name="pageId"/>, the start page, is deleted: the one after it, or the
    /// one before for the last page. Null when the page is not the start page (or is the only one).
    /// </summary>
    public string? NextStartPage(string pageId)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || config.Pages.Count < 2 || StartPageResolver.Resolve(config) != page || !StartPageResolver.IsSet(config))
            return null;
        return config.Pages[page + 1 < config.Pages.Count ? page + 1 : page - 1].Id;
    }

    /// <summary>
    /// Deletes the page; when it is the start page, the same step makes <see cref="NextStartPage"/> the start page. The last page
    /// cannot be deleted. "Delete Main".
    /// </summary>
    public bool DeletePage(string pageId)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || config.Pages.Count < 2)
            return false;
        string? next = NextStartPage(pageId);
        return _document.Edit($"Delete {config.Pages[page].Name}", e =>
        {
            e.Remove($"/pages/{page}");
            if (next != null)
                e.Set(Key(e, "", "startPage"), next);
        });
    }

    /// <summary>Makes the page the one the deck starts on: "Make Home the start page".</summary>
    public bool SetStartPage(string pageId)
    {
        var config = Config;
        int page = PageIndex(config, pageId);
        if (page < 0 || (StartPageResolver.IsSet(config) && config.StartPage == pageId))
            return false;
        return _document.Edit($"Make {config.Pages[page].Name} the start page", e => e.Set(Key(e, "", "startPage"), pageId));
    }

    /// <summary>Sets the page indicator's mode for every page (<c>auto</c> removes the key): "Page dots: Always".</summary>
    public bool SetIndicator(IndicatorMode mode)
    {
        string? value = mode switch
        {
            IndicatorMode.Always => "always",
            IndicatorMode.Fade => "fade",
            IndicatorMode.Off => "off",
            _ => null,
        };
        if (Config.Pager.GetIndicatorMode() == mode && (value != null || Config.Pager.Indicator == null))
            return false;
        string label = mode switch
        {
            IndicatorMode.Always => "Always",
            IndicatorMode.Fade => "Show then fade",
            IndicatorMode.Off => "Off",
            _ => "Automatic",
        };
        return _document.Edit($"Page dots: {label}", e =>
        {
            string pager = Key(e, "", "pager");
            if (value == null)
                e.Remove($"{pager}/{JsonPointer.Escape(e.FindKey(pager, "indicator") ?? "indicator")}");
            else
                e.Set($"{pager}/{JsonPointer.Escape(e.FindKey(pager, "indicator") ?? "indicator")}", value);
        });
    }

    /// <summary>Turns the automatic update check on (the key removed) or off: "Turn off update checks".</summary>
    public bool SetUpdates(bool on)
    {
        // On is the absent key; off is "off" (any value that is not "off" already checks).
        if (on ? Config.Updates == null : !UpdatesSetting.IsEnabled(Config.Updates))
            return false;
        return _document.Edit(on ? "Turn on update checks" : "Turn off update checks", e =>
        {
            if (on)
                e.Remove(Key(e, "", "updates"));
            else
                e.Set(Key(e, "", "updates"), "off");
        });
    }

    private static void InsertPage(ConfigEditor e, int at, JsonObject node)
    {
        if (e.Get("/pages") is not JsonArray)
            e.Set("/pages", new JsonArray());
        e.Insert("/pages", at, node);
    }

    private static HashSet<string> TakenIds(UrDeckConfig config) =>
        config.Pages.Select(p => p.Id).OfType<string>().ToHashSet(StringComparer.Ordinal);
}
