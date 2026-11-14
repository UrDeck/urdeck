// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Input;

namespace UrDeck.Widgets.Steam;

/// <summary>
/// A card for Steam: who I am and what I am playing (or downloading), and below it the friends who are online or playing and,
/// at the largest size, the games I played lately. Everything comes from the Steam provider's readings: what this PC knows works
/// without connecting, the friends after Connect Steam. It draws no card, names no font and takes every colour from the theme.
/// A tap opens the Steam client: the friends list from the card, a friend's profile, a game in the library.
/// </summary>
[Widget("Steam", "Shows what you are playing, your friends who are online or playing, and downloads", Id = "urdeck.widgets.steam")]
[WidgetSize(1, 1)]
[WidgetSize(2, 1)]
[WidgetSize(2, 2)]
[WidgetSize(4, 2)]
[WidgetSize(4, 4)]
[RefreshOnData]
[Category("Games")]
public class SteamWidget : Widget<SteamConfig>, ITapTarget, IDisposable
{
    public const string FriendsTarget = "steam://open/friends";

    private const string Prefix = "steam:";
    private const string NotConnected = "Connect Steam in Settings";

    /// <summary>The readings the card and the friends need at any size.</summary>
    private static readonly string[] CardReadings =
    [
        "client/running", "me/name", "me/state", "me/avatar", "me/playing", "me/game", "me/game/art", "friends/online", "friends/playing",
    ];

    private readonly TextList _friends = new() { WholeRows = true, AlignMarks = true };
    private readonly TextList _recent = new() { WholeRows = true, AlignMarks = true };
    private string[] _subscriptions = [];
    private ReadingList? _friendsFed;
    private ReadingList? _recentFed;
    private string? _friendsFedOptions;
    private SKRect _card = SKRect.Empty;
    private SKRect _friendsBody = SKRect.Empty;
    private SKRect _recentBody = SKRect.Empty;
    private string _imageSource = "";

    public override IReadOnlyCollection<string> Subscriptions => _subscriptions;

    public override bool IsAnimating => _friends.IsMoving || _recent.IsMoving;

    private bool Playing => string.Equals(Config.Friends?.Trim(), "playing", StringComparison.OrdinalIgnoreCase);

    private bool ShowsRecent => Config.ShowRecent && Config.Width >= 4 && Config.Height >= 4;

    public override void Attach(IWidgetHost host)
    {
        base.Attach(host);
        _friends.Icons = Icons;
        _recent.Icons = Icons;
    }

    protected override void OnConfigured()
    {
        var needed = new List<string>(CardReadings.Select(r => Prefix + r));
        if (Config.ShowDownloads)
        {
            needed.Add(Prefix + "download/name");
            needed.Add(Prefix + "download/progress");
        }

        if (ShowsRecent)
            needed.Add(Prefix + "games/recent");
        _subscriptions = [.. needed];

        _friendsFed = null;
        _recentFed = null;
        _friendsFedOptions = null;
        _friends.Clear();
        _recent.Clear();

        // The largest card is read from further away: its rows take the theme's title step.
        var step = Config.Width >= 4 && Config.Height >= 4 ? TextStep.Title : TextStep.Body;
        _friends.Step = step;
        _recent.Step = step;
    }

    public void Dispose()
    {
        SetImageSource("");
        GC.SuppressFinalize(this);
    }

    public override void Render(WidgetRenderContext context)
    {
        var canvas = context.Canvas;
        var theme = context.Theme;
        var rect = context.ContentRect;
        _card = rect;
        _friendsBody = SKRect.Empty;
        _recentBody = SKRect.Empty;

        var me = Me();
        if (Config.Width <= 1 && Config.Height <= 1)
        {
            DrawCompact(canvas, theme, rect, me);
            return;
        }

        float unit = rect.Height / Math.Max(1, Config.Height);
        var card = Config.Height <= 1 ? rect : new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + unit);
        _card = card;
        DrawCard(canvas, theme, card, me);
        if (Config.Height <= 1)
            return;

        float gap = unit * 0.06f;
        var body = new SKRect(rect.Left, card.Bottom + gap, rect.Right, rect.Bottom);
        if (ShowsRecent)
        {
            float split = body.Top + (body.Height * 0.58f);
            _friendsBody = new SKRect(body.Left, body.Top, body.Right, split);
            _recentBody = new SKRect(body.Left, split + gap, body.Right, body.Bottom);
            DrawFriends(canvas, theme, _friendsBody, context);
            DrawRecent(canvas, theme, _recentBody, context);
        }
        else
        {
            _friendsBody = body;
            DrawFriends(canvas, theme, body, context);
        }
    }

    // What the card shows

    /// <summary>What the readings say about me, read once per paint.</summary>
    private readonly record struct MeState(
        string Name,
        string State,
        string ClientProblem,
        bool Running,
        bool PlayingGame,
        string Game,
        string Art,
        string Avatar,
        string Download,
        double DownloadPercent,
        bool HasDownload,
        int FriendsPlaying,
        int FriendsOnline,
        bool FriendsKnown,
        string FriendsProblem);

    private MeState Me()
    {
        var running = Readings.Read(Prefix + "client/running");
        var online = Readings.Read(Prefix + "friends/online");
        var playing = Readings.Read(Prefix + "friends/playing");
        string download = "";
        double percent = 0;
        bool hasDownload = false;
        if (Config.ShowDownloads
            && Readings.Read(Prefix + "download/name") is { State: not ReadingState.Unavailable, Value: { Type: ReadingValueType.Text } name }
            && name.Text.Length > 0
            && Readings.Read(Prefix + "download/progress").Value is { Type: ReadingValueType.Number } progress)
        {
            download = name.Text;
            percent = Math.Clamp(progress.Number, 0, 100);
            hasDownload = true;
        }

        bool known = online.Value is { Type: ReadingValueType.List } && playing.Value is { Type: ReadingValueType.List };
        return new MeState(
            Text("me/name"),
            Text("me/state"),
            running.State == ReadingState.Unavailable ? Reason(running, "Steam is not installed on this PC") : "",
            running.Value is { Type: ReadingValueType.OnOff } r && r.IsOn,
            Readings.Read(Prefix + "me/playing").Value is { Type: ReadingValueType.OnOff } p && p.IsOn,
            Text("me/game"),
            Text("me/game/art"),
            Text("me/avatar"),
            download,
            percent,
            hasDownload,
            playing.Value is { Type: ReadingValueType.List } pl ? pl.List.Count : 0,
            online.Value is { Type: ReadingValueType.List } ol ? ol.List.Count : 0,
            known,
            online.State == ReadingState.Unavailable ? Reason(online, NotConnected) : "");
    }

    private string Text(string path) =>
        Readings.Read(Prefix + path).Value is { Type: ReadingValueType.Text } value ? value.Text : "";

    private static string Reason(Reading reading, string fallback) =>
        string.IsNullOrWhiteSpace(reading.Reason) ? fallback : reading.Reason!;

    /// <summary>The second line of the card: a problem with Steam, the download, the game, or my state.</summary>
    private static string SecondLine(MeState me)
    {
        if (me.ClientProblem.Length > 0)
            return me.ClientProblem;
        if (me.HasDownload)
            return $"{me.Download}  {me.DownloadPercent.ToString("0", CultureInfo.InvariantCulture)}%";
        if (!me.Running)
            return "Steam is not running";
        if (me.PlayingGame && me.Game.Length > 0)
            return me.Game;
        return me.State.Length > 0 ? me.State : "Not playing";
    }

    private static string CountsLine(MeState me) =>
        me.FriendsKnown
            ? $"{me.FriendsPlaying.ToString(CultureInfo.InvariantCulture)} playing · {me.FriendsOnline.ToString(CultureInfo.InvariantCulture)} online"
            : me.FriendsProblem;

    /// <summary>The colour of the state mark: playing, online, away or busy, from the theme's status colours; muted when Steam is off.</summary>
    private static SKColor StateColor(Theme theme, MeState me)
    {
        if (me.ClientProblem.Length > 0 || !me.Running)
            return theme.TextMuted;
        if (me.PlayingGame)
            return theme.Accent;
        return me.State switch
        {
            "Away" or "Snooze" => theme.Warning,
            "Busy" => theme.Critical,
            "Offline" or "" => theme.TextMuted,
            _ => theme.Good,
        };
    }

    // Drawing

    /// <summary>1x1: the picture with its state mark, and below it the number of friends playing (or my name).</summary>
    private void DrawCompact(SKCanvas canvas, Theme theme, SKRect rect, MeState me)
    {
        string label = me.FriendsKnown ? $"{me.FriendsPlaying.ToString(CultureInfo.InvariantCulture)} playing" : me.Name;
        var image = Picture(me, (int)Math.Ceiling(Math.Min(rect.Width, rect.Height)));
        var drawn = ImageTile.Draw(canvas, theme, rect, image, label, Placeholder(me), "app/steam");
        DrawMark(canvas, theme, drawn, StateColor(theme, me));
    }

    /// <summary>2x1 and larger: the picture with its state mark, my name, the second line, the counts and a download's bar.</summary>
    private void DrawCard(SKCanvas canvas, Theme theme, SKRect rect, MeState me)
    {
        float gap = rect.Height * 0.08f;
        float side = Math.Min(rect.Height, rect.Width * 0.3f);
        var area = SKRect.Create(rect.Left, rect.MidY - (side / 2), side, side);
        var image = Picture(me, (int)Math.Ceiling(side));
        var drawn = ImageTile.Draw(canvas, theme, area, image, null, Placeholder(me), "app/steam");
        DrawMark(canvas, theme, drawn, StateColor(theme, me));

        var text = new SKRect(area.Right + gap, rect.Top, rect.Right, rect.Bottom);
        if (me.HasDownload)
        {
            float barHeight = Math.Max(3f, rect.Height * 0.06f);
            var bar = new SKRect(text.Left, text.Bottom - barHeight, text.Right, text.Bottom);
            DrawProgress(canvas, theme, bar, (float)(me.DownloadPercent / 100));
            text = new SKRect(text.Left, text.Top, text.Right, bar.Top - gap);
        }

        var lines = new List<(string Text, TextStep Step, TextEmphasis Emphasis)>(3);
        lines.Add((me.Name.Length > 0 ? me.Name : "Steam", TextStep.Title, TextEmphasis.Primary));
        lines.Add((SecondLine(me), TextStep.Body, me.PlayingGame || me.HasDownload ? TextEmphasis.Primary : TextEmphasis.Muted));
        string counts = CountsLine(me);
        if (counts.Length > 0)
            lines.Add((counts, TextStep.Label, TextEmphasis.Muted));

        // What does not fit goes, the counts first.
        while (lines.Count > 1 && Height(theme, lines) > text.Height)
            lines.RemoveAt(lines.Count - 1);

        float y = text.MidY - (Height(theme, lines) / 2);
        foreach (var (line, step, emphasis) in lines)
        {
            float height = LineHeight(theme, step);
            TextLine.Draw(canvas, theme, new SKRect(text.Left, y, text.Right, y + height), line, step, emphasis, HorizontalAlign.Left,
                VerticalAlign.Middle, step == TextStep.Label ? TextOverflow.Shrink : TextOverflow.Ellipsis);
            y += height;
        }
    }

    private void DrawFriends(SKCanvas canvas, Theme theme, SKRect body, WidgetRenderContext context)
    {
        string path = Playing ? "friends/playing" : "friends/online";
        var reading = Readings.Read(Prefix + path);
        if (reading.State == ReadingState.Unavailable)
        {
            ClearFriends();
            TextLine.Draw(canvas, theme, body, Reason(reading, NotConnected), TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        if (reading.Value is not { Type: ReadingValueType.List } value)
        {
            ClearFriends();
            return;
        }

        if (value.List.Count == 0)
        {
            ClearFriends();
            string empty = Readings.Describe(Prefix + path)?.EmptyText is { Length: > 0 } text ? text : Playing ? "Nobody is playing" : "No friends online";
            TextLine.Draw(canvas, theme, body, empty, TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        FeedFriends(value.List, path);
        DrawList(canvas, theme, _friends, body, reading.State == ReadingState.Stale, context);
    }

    private void DrawRecent(SKCanvas canvas, Theme theme, SKRect body, WidgetRenderContext context)
    {
        float titleHeight = theme.GetTextSize(TextStep.Label) * 1.6f;
        TextLine.Draw(canvas, theme, new SKRect(body.Left, body.Top, body.Right, body.Top + titleHeight), "Recent games", TextStep.Label,
            TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle, TextOverflow.Ellipsis);
        var rows = new SKRect(body.Left, body.Top + titleHeight, body.Right, body.Bottom);
        _recentBody = rows;

        var reading = Readings.Read(Prefix + "games/recent");
        if (reading.State == ReadingState.Unavailable)
        {
            ClearRecent();
            TextLine.Draw(canvas, theme, rows, Reason(reading, "Unavailable"), TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        if (reading.Value is not { Type: ReadingValueType.List } value)
        {
            ClearRecent();
            return;
        }

        if (value.List.Count == 0)
        {
            ClearRecent();
            string empty = Readings.Describe(Prefix + "games/recent")?.EmptyText is { Length: > 0 } text ? text : "No games played recently";
            TextLine.Draw(canvas, theme, rows, empty, TextStep.Label, TextEmphasis.Muted, overflow: TextOverflow.Ellipsis);
            return;
        }

        FeedRecent(value.List);
        DrawList(canvas, theme, _recent, rows, reading.State == ReadingState.Stale, context);
    }

    /// <summary>Forgets the rows, so a tap where they were opens nothing once they are not drawn.</summary>
    private void ClearFriends()
    {
        _friends.Clear();
        _friendsFed = null;
        _friendsFedOptions = null;
    }

    private void ClearRecent()
    {
        _recent.Clear();
        _recentFed = null;
    }

    private static void DrawList(SKCanvas canvas, Theme theme, TextList list, SKRect body, bool stale, WidgetRenderContext context)
    {
        // A stale list keeps its rows, drawn fainter, as elsewhere.
        int layer = -1;
        if (stale)
        {
            using var faint = new SKPaint { Color = SKColors.White.WithAlpha(140) };
            layer = canvas.SaveLayer(faint);
        }

        list.Draw(canvas, theme, body, context.Time, context.Motion);
        if (layer >= 0)
            canvas.RestoreToCount(layer);
    }

    private void FeedFriends(ReadingList list, string path)
    {
        if (_friendsFed != null && _friendsFed.Equals(list) && _friendsFedOptions == path)
            return;
        _friendsFed = list;
        _friendsFedOptions = path;
        _friends.SetItems(list.Rows.Select(FriendItem));
    }

    private void FeedRecent(ReadingList list)
    {
        if (_recentFed != null && _recentFed.Equals(list))
            return;
        _recentFed = list;
        _recent.SetItems(list.Rows.Select(GameItem));
    }

    private static TextListItem FriendItem(ReadingRow row)
    {
        string state = Field(row, "state");
        string game = Field(row, "game");
        // The game when there is one; otherwise how the friend is, unless that is the usual Online.
        string detail = game.Length > 0 ? game : state is "Online" ? "" : state;
        string avatar = Field(row, "avatar");
        return new TextListItem(Field(row, "name"), detail.Length > 0 ? detail : null, null, null, 0, row.Key, avatar.Length > 0 ? avatar : null);
    }

    private static TextListItem GameItem(ReadingRow row)
    {
        string art = Field(row, "art");
        string? hours = row.Get("hours2wks") is { Type: ReadingValueType.Number } h
            ? h.Number.ToString("0.#", CultureInfo.InvariantCulture) + " h"
            : null;
        return new TextListItem(Field(row, "name"), null, hours, null, 0, row.Key, art.Length > 0 ? art : null);
    }

    private static string Field(ReadingRow row, string name) =>
        row.Get(name) is { Type: ReadingValueType.Text } value ? value.Text : "";

    /// <summary>The picture of the card: the game's art while playing, else my avatar; null while it loads or when there is none.</summary>
    private SKImage? Picture(MeState me, int pixels)
    {
        string source = me.PlayingGame && me.Running && me.Art.Length > 0 ? me.Art : me.Avatar;
        SetImageSource(source);
        return source.Length == 0 ? null : Icons.GetIcon(source, pixels).Image;
    }

    private static string Placeholder(MeState me) => me.Name.Length > 0 ? me.Name : "Steam";

    /// <summary>Lets go of the picture the card stops showing, so the icon service keeps one decoded picture and not one per game.</summary>
    private void SetImageSource(string source)
    {
        if (string.Equals(source, _imageSource, StringComparison.Ordinal))
            return;
        if (_imageSource.Length > 0)
            Icons.Release(_imageSource);
        _imageSource = source;
    }

    /// <summary>A small filled circle at the picture's lower right corner, ringed in the card's colour so it reads on any picture.</summary>
    private static void DrawMark(SKCanvas canvas, Theme theme, SKRect picture, SKColor color)
    {
        if (picture.Width <= 0)
            return;
        float radius = Math.Max(3f, Math.Min(picture.Width, picture.Height) * 0.14f);
        var centre = new SKPoint(picture.Right - (radius * 0.9f), picture.Bottom - (radius * 0.9f));
        using var ring = new SKPaint { Color = theme.CardFill.WithAlpha(0xFF), IsAntialias = true };
        canvas.DrawCircle(centre, radius * 1.3f, ring);
        using var dot = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawCircle(centre, radius, dot);
    }

    private static void DrawProgress(SKCanvas canvas, Theme theme, SKRect bar, float fraction)
    {
        float radius = theme.StrokeCap == SKStrokeCap.Round ? bar.Height / 2 : 0;
        using var track = new SKPaint { Color = theme.AccentDim, IsAntialias = true };
        canvas.DrawRoundRect(bar, radius, radius, track);
        if (fraction <= 0)
            return;

        var filled = new SKRect(bar.Left, bar.Top, bar.Left + Math.Max(bar.Height, bar.Width * Math.Clamp(fraction, 0, 1)), bar.Bottom);
        using var fill = new SKPaint { Color = theme.Accent, IsAntialias = true };
        canvas.DrawRoundRect(filled, radius, radius, fill);
    }

    private static float LineHeight(Theme theme, TextStep step) => theme.GetTextSize(step) * 1.4f;

    private static float Height(Theme theme, List<(string Text, TextStep Step, TextEmphasis Emphasis)> lines) =>
        lines.Sum(line => LineHeight(theme, line.Step));

    // Taps

    public bool CanTap(SKPoint point) => TargetAt(point) != null;

    public void OnTap(SKPoint point)
    {
        if (TargetAt(point) is { } target)
            Launcher.Launch(target);
    }

    /// <summary>What a tap at <paramref name="point"/> opens: a friend's profile, a game, or (the card) the friends list.</summary>
    private string? TargetAt(SKPoint point)
    {
        if (_friendsBody.Contains(point))
            return RowLink(_friends, _friendsFed, point);
        if (_recentBody.Contains(point))
            return RowLink(_recent, _recentFed, point);
        return _card.Contains(point) ? FriendsTarget : null;
    }

    private static string? RowLink(TextList list, ReadingList? fed, SKPoint point)
    {
        if (fed == null || list.ItemAt(point) is not { } index || index >= fed.Count)
            return null;
        return fed[index].Get("link") is { Type: ReadingValueType.Text } link && link.Text.Trim().Length > 0 ? link.Text.Trim() : null;
    }
}
