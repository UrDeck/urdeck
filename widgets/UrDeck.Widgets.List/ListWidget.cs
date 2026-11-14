// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Input;

namespace UrDeck.Widgets.List;

/// <summary>
/// Shows any list reading as rows, by the roles its description gives the fields: the title, the detail line, the status mark and
/// the picture of each row. It names no provider and no font and draws no card. A tap on a row opens the row's link when the list
/// has one; elsewhere it pages the list when the rows do not fit (<c>scroll</c>).
/// </summary>
[Widget("List", "Shows a list reading as rows: friends who are playing, busy processes, headlines", Id = "urdeck.widgets.list")]
[WidgetSize(1, 1)]
[WidgetSize(2, 1)]
[WidgetSize(2, 2)]
[WidgetSize(4, 2)]
[WidgetSize(4, 4)]
[RefreshOnData]
[Category("Data")]
public class ListWidget : Widget<ListConfig>, ITapTarget
{
    /// <summary>Rotate shows as many rows as fit; the list clips to the view.</summary>
    private const int AllRows = 1000;

    /// <summary>How many placeholder rows a pending list shows.</summary>
    private const int PlaceholderRows = 3;

    /// <summary>The title is this many label lines tall.</summary>
    private const float TitleLines = 1.6f;

    private readonly TextList _list = new() { AlignMarks = true, WholeRows = true };
    private ReadingList? _fed;
    private ReadingDescriptor? _fedDescriptor;
    private ListRoles? _roles;
    private string? _fedOptions;
    private bool _placeholders;
    private SKRect _body = SKRect.Empty;
    private bool _paged;

    public override ListConfig DefaultConfig => new() { Reading = "system:processes/top" };

    public override IReadOnlyCollection<string> Subscriptions => ReadingId.Length > 0 ? [ReadingId] : [];

    public override bool IsAnimating => _list.IsMoving;

    public override DateTime? NextAnimationAt => _list.NextChangeAt;

    private string ReadingId => Config.Reading?.Trim() ?? "";

    private bool Rotates => string.Equals(Config.Overflow?.Trim(), "rotate", StringComparison.OrdinalIgnoreCase);

    public override void Attach(IWidgetHost host)
    {
        base.Attach(host);
        _list.Icons = Icons;
    }

    protected override void OnConfigured()
    {
        _fed = null;
        _fedDescriptor = null;
        _fedOptions = null;
        _placeholders = false;
        _list.Clear();
        _list.Mode = Rotates ? TextListMode.Rotate : TextListMode.Manual;

        // The largest card reads from further away: its rows take the theme's title step.
        _list.Step = Config.Width >= 4 && Config.Height >= 4 ? TextStep.Title : TextStep.Body;
        _list.RotateCount = AllRows;
    }

    public override void Render(WidgetRenderContext context)
    {
        var canvas = context.Canvas;
        var theme = context.Theme;
        var rect = context.ContentRect;
        _body = SKRect.Empty;
        _paged = false;

        string id = ReadingId;
        if (id.Length == 0)
        {
            TextLine.Draw(canvas, theme, rect, "Choose a list", TextStep.Body, TextEmphasis.Muted);
            return;
        }

        var reading = Readings.Read(id);
        var descriptor = Readings.Describe(id);
        string title = Config.Title ?? descriptor?.Label ?? "";

        if (Config.Width <= 1 && Config.Height <= 1)
        {
            DrawCount(canvas, theme, rect, reading, descriptor, title);
            return;
        }

        var body = rect;
        if (title.Length > 0 && Config.Height >= 2)
        {
            float height = theme.GetTextSize(TextStep.Label) * TitleLines;
            TextLine.Draw(canvas, theme, new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + height), title, TextStep.Label, TextEmphasis.Muted,
                HorizontalAlign.Left, VerticalAlign.Middle, TextOverflow.Ellipsis);
            body = new SKRect(rect.Left, rect.Top + height, rect.Right, rect.Bottom);
        }

        if (reading.State == ReadingState.Unavailable)
        {
            string reason = string.IsNullOrWhiteSpace(reading.Reason) ? "Unavailable" : reading.Reason!;
            TextLine.Draw(canvas, theme, body, reason, TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        if (reading.Value is not { Type: ReadingValueType.List } value)
        {
            // Pending with nothing yet: the shape of a list, so the card does not jump when the rows arrive.
            DrawPlaceholders(canvas, theme, body, context);
            return;
        }

        if (value.List.Count == 0)
        {
            string empty = string.IsNullOrWhiteSpace(descriptor?.EmptyText) ? "Nothing to show" : descriptor!.EmptyText!;
            TextLine.Draw(canvas, theme, body, empty, TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        Feed(value.List, descriptor);
        _body = body;
        _paged = !Rotates;

        // A stale list keeps its rows, drawn fainter.
        int layer = -1;
        if (reading.State == ReadingState.Stale)
        {
            using var faint = new SKPaint { Color = SKColors.White.WithAlpha(140) };
            layer = canvas.SaveLayer(faint);
        }

        _list.Draw(canvas, theme, body, context.Time, context.Motion);
        if (layer >= 0)
            canvas.RestoreToCount(layer);
    }

    /// <summary>1x1: the title and the number of rows, as a readout.</summary>
    private void DrawCount(SKCanvas canvas, Theme theme, SKRect rect, Reading reading, ReadingDescriptor? descriptor, string title)
    {
        var text = ReadingFormatter.Format(reading, descriptor, null, Readings);
        Readout.Draw(canvas, theme, rect, text, new ReadoutOptions { Label = title.Length > 0 ? title : null });
    }

    private void DrawPlaceholders(SKCanvas canvas, Theme theme, SKRect body, WidgetRenderContext context)
    {
        if (!_placeholders)
        {
            _placeholders = true;
            _fed = null;
            _list.SetItems(Enumerable.Range(0, PlaceholderRows).Select(_ => new TextListItem(ReadingFormatter.Dash, ReadingFormatter.Dash)));
        }

        _list.Draw(canvas, theme, body, context.Time, context.Motion);
    }

    /// <summary>Gives the text list the rows, once per change: an unchanged list costs nothing and keeps its scroll position.</summary>
    private void Feed(ReadingList list, ReadingDescriptor? descriptor)
    {
        string options = $"{Config.MaxRows}|{Config.ShowImages}";
        if (!_placeholders && _fed != null && _fed.Equals(list) && Equals(_fedDescriptor, descriptor) && _fedOptions == options)
            return;

        _placeholders = false;
        _fed = list;
        _fedDescriptor = descriptor;
        _fedOptions = options;
        var roles = descriptor == null ? FallbackRoles(list) : ListRoles.Of(descriptor);
        if (!Config.ShowImages)
            roles = roles with { Image = null };
        _roles = roles;
        int max = Config.MaxRows is > 0 ? Config.MaxRows.Value : 0;
        _list.SetItems(list, roles, Readings, null, max);
    }

    /// <summary>A list whose description is unknown (a provider not running): the first text the first row has is its title.</summary>
    private static ListRoles FallbackRoles(ReadingList list)
    {
        string? name = list.Count == 0 ? null : list[0].Names.FirstOrDefault(n => list[0].Get(n) is { Type: ReadingValueType.Text });
        return new ListRoles(name == null ? null : new ReadingField(name, ReadingKind.Text, name), null, null, null, null);
    }

    // Taps

    public bool CanTap(SKPoint point) => LinkAt(point) != null || (_paged && _body.Contains(point) && _list.MaxOffset > 0);

    public void OnTap(SKPoint point)
    {
        if (LinkAt(point) is { } link)
        {
            Launcher.Launch(link);
            return;
        }

        if (!_paged || !_body.Contains(point) || _list.MaxOffset <= 0)
            return;

        // The lower part pages forward, the upper part back, by whole rows.
        _list.Page(point.Y >= _body.MidY ? 1 : -1);
    }

    /// <summary>The launch target of the row at <paramref name="point"/>; null when the row has none or the point is no row.</summary>
    private string? LinkAt(SKPoint point)
    {
        if (_fed == null || _roles?.Link is not { } field || _list.ItemAt(point) is not { } index || index >= _fed.Count)
            return null;
        return _fed[index].TryGet(field.Name, out var value) && value.Type == ReadingValueType.Text && value.Text.Trim().Length > 0
            ? value.Text.Trim()
            : null;
    }
}
