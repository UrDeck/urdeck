// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;

namespace UrDeck.Sdk.Components;

/// <summary>How a <see cref="TextList"/> moves through its items.</summary>
public enum TextListMode
{
    /// <summary>The list rests at the offset the widget sets.</summary>
    Manual,

    /// <summary>For a feed with its newest item at the bottom: while the view is at the bottom, new items keep it there.</summary>
    Follow,

    /// <summary>A carousel: shows <see cref="TextList.RotateCount"/> items and replaces them with the next on a timer.</summary>
    Rotate,
}

/// <summary>How much attention an item's mark deserves; it picks the theme's status colour when the item has no colour of its own.</summary>
public enum TextListLevel
{
    /// <summary>The theme's accent.</summary>
    None,
    /// <summary>The theme's good colour.</summary>
    Good,
    /// <summary>The theme's warning colour.</summary>
    Warning,
    /// <summary>The theme's critical colour.</summary>
    Critical,
}

/// <summary>One entry of a <see cref="TextList"/>.</summary>
/// <param name="Text">The text, wrapped to the list's width.</param>
/// <param name="Detail">An optional second line in the label step and the muted colour, ended with an ellipsis when too long.</param>
/// <param name="Mark">An optional short leading text (a user name) drawn on the first line; the text is indented past it.</param>
/// <param name="MarkColor">The mark's colour; the theme's accent when null.</param>
/// <param name="MaxLines">The most lines the text may take (the last ends in an ellipsis); 0 for no limit.</param>
/// <param name="Key">A stable identity (a list row's key); the list keeps the scroll position on an item whose key survives a replacement.</param>
/// <param name="ImageSource">
/// A leading image, asked from the list's <see cref="TextList.Icons"/> as a launch-target-like text (a file or an address). It is
/// drawn as a square the height of the first line before the mark and the text, which start after it; the place stays empty
/// while the image loads, so nothing moves when it arrives.
/// </param>
/// <param name="MarkLevel">Colours the mark by the theme's status colours when <paramref name="MarkColor"/> is null.</param>
public sealed record TextListItem(
    string Text,
    string? Detail = null,
    string? Mark = null,
    SKColor? MarkColor = null,
    int MaxLines = 0,
    string? Key = null,
    string? ImageSource = null,
    TextListLevel MarkLevel = TextListLevel.None);

/// <summary>
/// A list of text items drawn in a rectangle, scrolled vertically and clipped to it: a chat feed that follows its newest
/// message, a carousel of headlines, or a list the widget scrolls itself. Unlike the other components it keeps state (the
/// items, their layout, the offset and the scroll in progress), so the widget creates it, feeds it, draws it on the UI thread
/// and drops it with the widget. It draws no card and names no font: everything comes from the theme.
/// <para>
/// Draw it with the paint's time and the effective motion level. While <see cref="IsMoving"/> is true after a draw, return
/// true from <see cref="IWidget.IsAnimating"/>; while <see cref="NextChangeAt"/> has a value, return it from
/// <see cref="IWidget.NextAnimationAt"/>. Below <see cref="MotionLevel.Full"/> the list jumps instead of scrolling or
/// sliding; it still follows and still rotates.
/// </para>
/// </summary>
public sealed class TextList
{
    private const double ScrollSeconds = 0.2;
    private const float GapRatio = 0.5f;

    private sealed class Layout
    {
        public required string[] Lines;
        public required float Indent;
        public required float ImageWidth;
        public string? Detail;
        public required float Height;
    }

    private readonly List<TextListItem> _items = [];
    private readonly List<Layout?> _layouts = [];
    private readonly List<(int Index, float OldTop, float OldHeight)> _replaced = [];
    private float[] _tops = [];
    private float _total;
    private float _view;
    private float _width = -1;
    private Theme? _theme;
    private bool _hasPainted;

    private float _target;
    private float _animFrom;
    private DateTime _animStart;
    private bool _animating;
    private bool _following = true;
    private int _first;
    private DateTime? _nextAt;
    private int? _pendingItem;
    private float _pendingDelta;
    private bool _alignMarks;
    private bool _markDirty = true;
    private float _alignedMark;

    private SKRect _rect;
    private float _origin;
    private float _clipBottom;

    /// <summary>How the list moves; see <see cref="TextListMode"/>.</summary>
    public TextListMode Mode { get; set; }

    /// <summary>The most items the list keeps; adding more removes the oldest from the top. 0 keeps them all.</summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Whether every item's text starts after the widest mark, so the texts of a list of values line up. Off, each item is indented
    /// by its own mark (a chat feed).
    /// </summary>
    public bool AlignMarks
    {
        get => _alignMarks;
        set
        {
            if (_alignMarks == value)
                return;
            _alignMarks = value;
            _markDirty = true;
        }
    }

    /// <summary>
    /// For <see cref="TextListMode.Manual"/>: a list at rest draws only the items that fit whole, so no row is cut through its
    /// text, and <see cref="Page"/> moves by whole items. Off, the last item shows as far as it reaches, as a scrolled view does.
    /// </summary>
    public bool WholeRows { get; set; }

    /// <summary>Where item images come from; without it an item's image place stays empty.</summary>
    public IIconSource? Icons { get; set; }

    /// <summary>The theme step the items' text is drawn in. The detail line is always the label step.</summary>
    public TextStep Step { get; set; } = TextStep.Body;

    /// <summary>How long <see cref="TextListMode.Rotate"/> shows each item.</summary>
    public TimeSpan RotateInterval { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>How many items <see cref="TextListMode.Rotate"/> shows at once.</summary>
    public int RotateCount { get; set; } = 1;

    /// <summary>The number of items.</summary>
    public int Count => _items.Count;

    /// <summary>The item at <paramref name="index"/>.</summary>
    public TextListItem this[int index] => _items[index];

    /// <summary>The height of all items with the gaps between them, as of the last draw or <see cref="Prepare"/>.</summary>
    public float ContentHeight => _total;

    /// <summary>The largest offset: the content's height less the view's, never below zero, as of the last draw.</summary>
    public float MaxOffset => Math.Max(0, _total - _view);

    /// <summary>
    /// The scroll position in pixels from the top of the content. Setting it clamps it, and stops following unless it is
    /// at the bottom. Before the first draw the value is kept and clamped by the first draw.
    /// </summary>
    public float Offset
    {
        get => _target;
        set
        {
            _animating = false;
            _target = _width < 0 ? Math.Max(0, value) : Math.Clamp(value, 0, MaxOffset);
            _following = _width >= 0 && _target >= MaxOffset - 0.5f;
        }
    }

    /// <summary>Whether a scroll is in progress, as of the last draw. A widget returns it from <see cref="IWidget.IsAnimating"/>.</summary>
    public bool IsMoving => _animating;

    /// <summary>
    /// When a <see cref="TextListMode.Rotate"/> list will show its next items, or null while nothing rotates. A widget
    /// returns it from <see cref="IWidget.NextAnimationAt"/>.
    /// </summary>
    public DateTime? NextChangeAt => Mode == TextListMode.Rotate ? _nextAt : null;

    /// <summary>Replaces every item.</summary>
    public void SetItems(IEnumerable<TextListItem> items)
    {
        _items.Clear();
        _layouts.Clear();
        _replaced.Clear();
        _first = 0;
        _markDirty = true;
        foreach (var item in items)
        {
            _items.Add(item);
            _layouts.Add(null);
        }

        TrimToCapacity();
    }

    /// <summary>
    /// Replaces every item with the rows of a list reading, by the roles the description gives its fields: the title field is the
    /// text, the detail field the second line, the status field the mark (coloured by its levels), the image field the image
    /// and the row's key the item's key. Values are formatted with <see cref="ReadingFormatter"/>. A field a row lacks leaves
    /// that part out. When some of the keys survive, the first row that was visible and still is stays at the top of the view.
    /// </summary>
    /// <param name="list">The rows.</param>
    /// <param name="descriptor">The reading's description, which declares the fields and their roles.</param>
    /// <param name="source">The user's region, for formatting.</param>
    /// <param name="options">Formatting choices; none for the description's own.</param>
    /// <param name="maxRows">The most rows to take, from the start; 0 for all.</param>
    public void SetItems(ReadingList list, ReadingDescriptor descriptor, IReadingSource source, ReadingFormatOptions? options = null, int maxRows = 0) =>
        SetItems(list, ListRoles.Of(descriptor), source, options, maxRows);

    /// <summary>
    /// As <see cref="SetItems(ReadingList, ReadingDescriptor, IReadingSource, ReadingFormatOptions?, int)"/> with the roles chosen by
    /// the caller, which may leave a part out (no image) of what the description gives.
    /// </summary>
    /// <param name="list">The rows.</param>
    /// <param name="roles">The fields to draw, by what they are for.</param>
    /// <param name="source">The user's region, for formatting.</param>
    /// <param name="options">Formatting choices; none for the description's own.</param>
    /// <param name="maxRows">The most rows to take, from the start; 0 for all.</param>
    public void SetItems(ReadingList list, ListRoles roles, IReadingSource source, ReadingFormatOptions? options = null, int maxRows = 0)
    {
        var items = new List<TextListItem>(list.Count);
        foreach (var row in list.Rows)
        {
            if (maxRows > 0 && items.Count >= maxRows)
                break;
            items.Add(ItemOf(row, roles, source, options));
        }

        var (anchorKey, anchorDelta) = Anchor();
        SetItems(items);
        if (anchorKey == null)
            return;
        int at = _items.FindIndex(i => i.Key == anchorKey);
        if (at < 0)
            return;
        _pendingItem = at;
        _pendingDelta = anchorDelta;
    }

    /// <summary>The row of a list as an item, by roles.</summary>
    public static TextListItem ItemOf(ReadingRow row, ListRoles roles, IReadingSource source, ReadingFormatOptions? options = null)
    {
        string text = roles.Title is { } title && row.TryGet(title.Name, out _) ? FieldText(row, title, source, options) : "";
        string? detail = roles.Detail is { } d && row.TryGet(d.Name, out _) ? FieldText(row, d, source, options) : null;
        string? mark = null;
        var level = TextListLevel.None;
        if (roles.Status is { } status && row.TryGet(status.Name, out var statusValue))
        {
            mark = FieldText(row, status, source, options);
            level = LevelOf(status, statusValue);
        }

        string? image = roles.Image is { } img && row.TryGet(img.Name, out var imageValue) && imageValue.Type == ReadingValueType.Text && imageValue.Text.Length > 0
            ? imageValue.Text
            : null;
        return new TextListItem(text, detail, mark, null, 0, row.Key, image, level);
    }

    /// <summary>A field as the text a widget draws: the value and its unit.</summary>
    public static string FieldText(ReadingRow row, ReadingField field, IReadingSource source, ReadingFormatOptions? options = null)
    {
        var formatted = ReadingFormatter.FormatField(row, field, options, source);
        if (string.IsNullOrEmpty(formatted.Unit))
            return formatted.Value;
        return char.IsLetter(formatted.Unit[0]) ? formatted.Value + " " + formatted.Unit : formatted.Value + formatted.Unit;
    }

    private static TextListLevel LevelOf(ReadingField field, ReadingValue value)
    {
        if (value.Type != ReadingValueType.Number || (field.Warning == null && field.Critical == null))
            return TextListLevel.None;
        if (field.Critical is { } critical && value.Number >= critical)
            return TextListLevel.Critical;
        if (field.Warning is { } warning && value.Number >= warning)
            return TextListLevel.Warning;
        return TextListLevel.Good;
    }

    /// <summary>The key of the first item in view and how far into it the view is; null when there is nothing to keep.</summary>
    private (string? Key, float Delta) Anchor()
    {
        if (_width < 0 || _tops.Length != _items.Count || _items.Count == 0)
            return (null, 0);
        int index = Mode == TextListMode.Rotate ? _first : FirstVisible();
        if (index < 0 || index >= _items.Count || _items[index].Key == null)
            return (null, 0);
        return (_items[index].Key, Mode == TextListMode.Rotate ? 0 : Math.Max(0, _target - _tops[index]));
    }

    private int FirstVisible()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_layouts[i] is { } layout && _tops[i] + layout.Height > _target)
                return i;
        }

        return -1;
    }

    private static SKColor LevelColor(Theme theme, TextListLevel level) => level switch
    {
        TextListLevel.Good => theme.Good,
        TextListLevel.Warning => theme.Warning,
        TextListLevel.Critical => theme.Critical,
        _ => theme.Accent,
    };

    /// <summary>Removes every item.</summary>
    public void Clear() => SetItems([]);

    /// <summary>Adds an item at the end, removing the oldest from the top beyond <see cref="Capacity"/>.</summary>
    public void Add(TextListItem item)
    {
        _markDirty = true;
        _items.Add(item);
        _layouts.Add(null);
        TrimToCapacity();
    }

    /// <summary>Replaces one item (an edited message). The view stays where it is.</summary>
    public void Replace(int index, TextListItem item)
    {
        if (_layouts[index] is { } old && index < _tops.Length)
            _replaced.Add((index, _tops[index], old.Height));
        _markDirty = true;
        _items[index] = item;
        _layouts[index] = null;
    }

    /// <summary>
    /// Scrolls by a page of whole items: forward (<paramref name="direction"/> above zero) so that the first item that was not
    /// whole in the view is at the top, back so that the items before the view fill it. Based on the last draw.
    /// </summary>
    public void Page(int direction)
    {
        if (_width < 0 || _tops.Length != _items.Count || _items.Count == 0)
            return;
        if (direction > 0)
        {
            float bottom = _target + _view;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_tops[i] + _layouts[i]!.Height > bottom + 0.5f)
                {
                    ScrollToItem(i);
                    return;
                }
            }

            return;
        }

        float want = _target - _view;
        if (want <= 0.5f)
        {
            Offset = 0;
            return;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            if (_tops[i] >= want - 0.5f)
            {
                ScrollToItem(i);
                return;
            }
        }
    }

    /// <summary>Scrolls by <paramref name="pixels"/> (positive moves the content up).</summary>
    public void ScrollBy(float pixels) => Offset = _target + pixels;

    /// <summary>Scrolls so that the item at <paramref name="index"/> is at the top of the view.</summary>
    public void ScrollToItem(int index)
    {
        if (index < 0 || index >= _items.Count)
            return;
        if (_width < 0)
        {
            // No layout yet: the first draw knows where the item is.
            _pendingItem = index;
            _pendingDelta = 0;
            return;
        }

        Offset = _tops[index];
    }

    /// <summary>The index of the item drawn at <paramref name="point"/> (the widget's pixels) at the last draw, or null for a gap or outside.</summary>
    public int? ItemAt(SKPoint point)
    {
        if (!_rect.Contains(point) || point.Y > _clipBottom)
            return null;
        float y = point.Y - _origin;
        for (int i = 0; i < _tops.Length && i < _layouts.Count; i++)
        {
            if (_layouts[i] is { } layout && y >= _tops[i] && y < _tops[i] + layout.Height)
                return i;
        }

        return null;
    }

    private void TrimToCapacity()
    {
        while (Capacity > 0 && _items.Count > Capacity)
        {
            if (_layouts[0] is { } layout && _width >= 0)
            {
                float shift = layout.Height + _theme!.GetTextSize(Step) * GapRatio;
                _target -= shift;
                _animFrom -= shift;
                for (int i = 0; i < _replaced.Count; i++)
                    _replaced[i] = (_replaced[i].Index - 1, _replaced[i].OldTop - shift, _replaced[i].OldHeight);
            }

            _items.RemoveAt(0);
            _layouts.RemoveAt(0);
            _first = Math.Max(0, _first - 1);
        }

        _replaced.RemoveAll(r => r.Index < 0);
    }

    /// <summary>Lays out the items for <paramref name="theme"/> and <paramref name="rect"/> and advances the list to <paramref name="time"/>, without drawing.</summary>
    public void Prepare(Theme theme, SKRect rect, DateTime time, MotionLevel motion = MotionLevel.Full)
    {
        if (!ReferenceEquals(theme, _theme) || MathF.Abs(rect.Width - _width) > 0.01f)
        {
            _theme = theme;
            _width = rect.Width;
            _markDirty = true;
            for (int i = 0; i < _layouts.Count; i++)
                _layouts[i] = null;
            _replaced.Clear();
        }

        _view = rect.Height;
        float size = theme.GetTextSize(Step);
        float gap = size * GapRatio;
        if (_alignMarks && _markDirty)
        {
            _markDirty = false;
            _alignedMark = WidestMark(theme, rect.Width);
            for (int i = 0; i < _layouts.Count; i++)
                _layouts[i] = null;
        }
        else if (!_alignMarks)
        {
            _alignedMark = 0;
        }

        for (int i = 0; i < _items.Count; i++)
            _layouts[i] ??= LayOut(theme, _items[i], rect.Width);

        _tops = new float[_items.Count];
        float y = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            _tops[i] = y;
            y += _layouts[i]!.Height + gap;
        }

        _total = _items.Count == 0 ? 0 : y - gap;
        if (_pendingItem is { } pending)
        {
            _pendingItem = null;
            if (pending < _items.Count)
            {
                float delta = Math.Min(_pendingDelta, _layouts[pending]!.Height);
                _target = Math.Clamp(_tops[pending] + delta, 0, MaxOffset);
                _animating = false;
                _following = _target >= MaxOffset - 0.5f;
                if (Mode == TextListMode.Rotate)
                    _first = pending;
            }

            _pendingDelta = 0;
        }

        if (!_following || Mode != TextListMode.Follow)
        {
            // An edited item above the view changes its height: keep what the viewer sees where it is.
            foreach (var (index, oldTop, oldHeight) in _replaced)
            {
                if (index < _layouts.Count && oldTop + oldHeight <= _target)
                {
                    float delta = _layouts[index]!.Height - oldHeight;
                    _target += delta;
                    _animFrom += delta;
                }
            }
        }

        _replaced.Clear();

        switch (Mode)
        {
            case TextListMode.Follow:
                Follow(time, motion);
                break;
            case TextListMode.Rotate:
                Rotate(time, motion);
                break;
            default:
                _target = Math.Clamp(_target, 0, MaxOffset);
                break;
        }

        if (_animating && (time - _animStart).TotalSeconds >= ScrollSeconds)
            _animating = false;
        _hasPainted = true;
    }

    private void Follow(DateTime time, MotionLevel motion)
    {
        if (_total <= _view)
        {
            _target = 0;
            _animating = false;
            _following = true;
            return;
        }

        if (_following)
            MoveTo(MaxOffset, time, motion);
        else
            _target = Math.Clamp(_target, 0, MaxOffset);
    }

    private void Rotate(DateTime time, MotionLevel motion)
    {
        int count = _items.Count;
        if (count <= 1 || _total <= _view)
        {
            _first = 0;
            _target = 0;
            _animating = false;
            _nextAt = null;
            return;
        }

        if (_first >= count)
            _first = 0;
        _nextAt ??= time + RotateInterval;
        if (time >= _nextAt)
        {
            int next = (_first + 1) % count;
            bool wrapped = next == 0;
            _first = next;
            _nextAt = time + RotateInterval;
            if (wrapped)
            {
                _animating = false;
                _target = 0;
            }
            else
            {
                MoveTo(_tops[_first], time, motion);
            }
        }
        else if (!_animating)
        {
            _target = _tops[_first];
        }
    }

    private void MoveTo(float target, DateTime time, MotionLevel motion)
    {
        if (target == _target)
            return;
        if (motion == MotionLevel.Full && _hasPainted)
        {
            _animFrom = VisibleOffset(time);
            _animStart = time;
            _animating = true;
        }
        else
        {
            _animating = false;
        }

        _target = target;
    }

    private float VisibleOffset(DateTime time)
    {
        if (!_animating)
            return _target;
        double u = (time - _animStart).TotalSeconds / ScrollSeconds;
        if (u >= 1)
            return _target;
        if (u <= 0)
            return _animFrom;
        double eased = u * u * (3 - 2 * u);
        return (float)(_animFrom + (_target - _animFrom) * eased);
    }

    /// <summary>The room the widest mark needs, with the gap after it; 0 when no item has a mark.</summary>
    private float WidestMark(Theme theme, float width)
    {
        float size = theme.GetTextSize(Step);
        float tracking = TextStyle.TrackingFor(theme.TextStyle, Step);
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextLayoutCache.RoleOf(Step)), size);
        float widest = 0;
        foreach (var item in _items)
        {
            if (!string.IsNullOrEmpty(item.Mark))
                widest = Math.Max(widest, TextMetrics.Measure(font, item.Mark, tracking));
        }

        return widest <= 0 ? 0 : Math.Min(width / 2, widest + size * 0.4f);
    }

    private Layout LayOut(Theme theme, TextListItem item, float width)
    {
        float size = theme.GetTextSize(Step);
        float lineHeight = size * TextBlock.LineSpacing;
        float imageWidth = string.IsNullOrEmpty(item.ImageSource) ? 0 : Math.Min(width / 3, lineHeight + size * 0.4f);
        float indent = imageWidth;
        float tracking = TextStyle.TrackingFor(theme.TextStyle, Step);
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextLayoutCache.RoleOf(Step)), size);
        if (_alignMarks)
            indent += _alignedMark;
        else if (!string.IsNullOrEmpty(item.Mark))
            indent += Math.Min(width / 2, TextMetrics.Measure(font, item.Mark, tracking) + size * 0.4f);

        string[] lines = [.. TextLayoutCache.Lines(theme, Step, item.Text, Math.Max(1, width - indent))];
        if (item.MaxLines > 0 && lines.Length > item.MaxLines)
        {
            lines = lines[..item.MaxLines];
            lines[^1] = TextMetrics.Ellipsize(font, lines[^1] + TextMetrics.Ellipsis, tracking, width - indent);
        }

        float height = lines.Length * lineHeight;
        string? detail = null;
        if (!string.IsNullOrEmpty(item.Detail))
        {
            float detailSize = theme.GetTextSize(TextStep.Label);
            using var detailFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), detailSize);
            detail = TextMetrics.Ellipsize(detailFont, item.Detail, TextStyle.TrackingFor(theme.TextStyle, TextStep.Label), width - indent);
            height += detailSize * TextBlock.LineSpacing;
        }

        return new Layout { Lines = lines, Indent = indent, ImageWidth = imageWidth, Detail = detail, Height = height };
    }

    /// <summary>Draws the list in <paramref name="rect"/> at <paramref name="time"/> (see <see cref="Prepare"/>).</summary>
    public void Draw(SKCanvas canvas, Theme theme, SKRect rect, DateTime time, MotionLevel motion = MotionLevel.Full)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;
        Prepare(theme, rect, time, motion);
        _rect = rect;
        float visible = VisibleOffset(time);
        _origin = rect.Top - visible;
        _clipBottom = rect.Bottom;
        if (Mode == TextListMode.Follow && _total < _view)
            _origin = rect.Bottom - _total;
        if (Mode == TextListMode.Rotate && !_animating && _items.Count > 0 && _total > _view)
        {
            int last = Math.Min(_items.Count - 1, _first + Math.Max(1, RotateCount) - 1);
            _clipBottom = Math.Min(rect.Bottom, _origin + _tops[last] + _layouts[last]!.Height);
        }

        if (WholeRows && Mode == TextListMode.Manual && !_animating && _items.Count > 0 && _total > _view)
        {
            // At rest, only what fits whole: the first item always shows, even if it is taller than the view.
            int last = 0;
            for (int i = 1; i < _items.Count && _tops[i] + _layouts[i]!.Height <= visible + rect.Height + 0.5f; i++)
                last = i;
            _clipBottom = Math.Min(rect.Bottom, _origin + _tops[last] + _layouts[last]!.Height);
        }

        if (_items.Count == 0)
            return;

        float size = theme.GetTextSize(Step);
        float lineHeight = size * TextBlock.LineSpacing;
        float tracking = TextStyle.TrackingFor(theme.TextStyle, Step);
        float detailSize = theme.GetTextSize(TextStep.Label);
        float detailTracking = TextStyle.TrackingFor(theme.TextStyle, TextStep.Label);
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextLayoutCache.RoleOf(Step)), size);
        using var detailFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), detailSize);
        float cap = TextMetrics.CapHeight(font);
        float detailCap = TextMetrics.CapHeight(detailFont);
        using var text = new SKPaint { Color = theme.Text, IsAntialias = true };
        using var muted = new SKPaint { Color = theme.TextMuted, IsAntialias = true };
        using var mark = new SKPaint { IsAntialias = true };

        int save = canvas.Save();
        canvas.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, _clipBottom));
        for (int i = 0; i < _items.Count; i++)
        {
            var layout = _layouts[i]!;
            float top = _origin + _tops[i];
            if (top + layout.Height < rect.Top)
                continue;
            if (top > rect.Bottom)
                break;

            if (layout.ImageWidth > 0)
                DrawImage(canvas, theme, new SKRect(rect.Left, top, rect.Left + layout.ImageWidth, top + lineHeight), _items[i].ImageSource!);

            for (int line = 0; line < layout.Lines.Length; line++)
            {
                float baseline = top + line * lineHeight + (lineHeight + cap) / 2;
                if (line == 0 && !string.IsNullOrEmpty(_items[i].Mark))
                {
                    mark.Color = _items[i].MarkColor ?? LevelColor(theme, _items[i].MarkLevel);
                    TextMetrics.DrawLine(canvas, _items[i].Mark!, rect.Left + layout.ImageWidth, baseline, font, mark, tracking);
                }

                if (layout.Lines[line].Length > 0)
                    TextMetrics.DrawLine(canvas, layout.Lines[line], rect.Left + layout.Indent, baseline, font, text, tracking);
            }

            if (layout.Detail is { Length: > 0 } detail)
            {
                float detailLine = detailSize * TextBlock.LineSpacing;
                float baseline = top + layout.Lines.Length * lineHeight + (detailLine + detailCap) / 2;
                TextMetrics.DrawLine(canvas, detail, rect.Left + layout.Indent, baseline, detailFont, muted, detailTracking);
            }
        }

        canvas.RestoreToCount(save);
    }

    /// <summary>Draws an item's image in the square at the start of its first line; nothing while it loads or when it has none.</summary>
    private void DrawImage(SKCanvas canvas, Theme theme, SKRect cell, string source)
    {
        float side = Math.Min(cell.Height, cell.Width);
        if (Icons is not { } icons || side < 1)
            return;
        var result = icons.GetIcon(source, (int)Math.Ceiling(side));
        if (result.Image is not { } image || image.Width <= 0 || image.Height <= 0)
            return;
        var square = SKRect.Create(cell.Left, cell.Top, side, side);
        float radius = Math.Min(theme.CardRadius, side / 2);
        int save = canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(square, radius, radius), SKClipOperation.Intersect, true);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, square, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.RestoreToCount(save);
    }
}
