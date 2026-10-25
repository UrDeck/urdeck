// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Input;

namespace UrDeck.Widgets.NowPlaying;

/// <summary>
/// A card that shows what the current media session plays (album art, title, artist, album, a progress bar) and lets the user
/// skip back, play or pause, and skip forward by touch. The media provider reads the session and sends the commands; the widget
/// only says what to show, draws it from the theme and asks for an action when a button is tapped. It keeps no state of its own
/// about the player: the card changes when the provider publishes the new state.
/// </summary>
[Widget("Now playing", "Shows what is playing and skips or pauses it by touch", Id = "urdeck.widgets.nowplaying")]
[WidgetSize(4, 2)]
[RefreshOnData]
[Category("Media")]
public class NowPlayingWidget : Widget<NowPlayingConfig>, ITapTarget, IDisposable
{
    private const string Prefix = "media:";

    /// <summary>The readings the card draws, as ids.</summary>
    private static readonly string[] ReadingIds =
    [
        Prefix + "status", Prefix + "title", Prefix + "artist", Prefix + "album", Prefix + "app", Prefix + "position",
        Prefix + "position/at", Prefix + "duration", Prefix + "art", Prefix + "can/previous", Prefix + "can/playpause", Prefix + "can/next",
    ];

    /// <summary>The middle button (play or pause) is larger than the other two.</summary>
    private static readonly float[] ButtonWeights = [1f, 1.25f, 1f];

    private static readonly string[] ButtonActions = [Prefix + "control/previous", Prefix + "control/playpause", Prefix + "control/next"];

    /// <summary>The widest a progress bar is stepped: once a second is plenty for a bar that spans the card.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>What the card draws from the readings; two equal values draw the same card (the clock aside).</summary>
    private readonly record struct Shown(
        string Status,
        string Title,
        string Artist,
        string Album,
        string App,
        string Art,
        double Duration,
        double Position,
        DateTimeOffset? PositionAt,
        bool? CanPrevious,
        bool? CanPlayPause,
        bool? CanNext)
    {
        public bool IsIdle => Status is "none" or "";

        /// <summary>Whether the bar moves on its own: something plays and its length is known.</summary>
        public bool IsTicking => Status == "playing" && Duration > 0;
    }

    private Shown? _drawn;
    private DateTime _drawnAt;
    private SKRect _buttons = SKRect.Empty;
    private string _artSource = "";

    public override IReadOnlyCollection<string> Subscriptions => ReadingIds;

    protected override void OnConfigured()
    {
        // A new configuration draws afresh.
        _drawn = null;
        _buttons = SKRect.Empty;
    }

    public void Dispose()
    {
        SetArtSource("");
        GC.SuppressFinalize(this);
    }

    public override bool NeedsRender(DateTime now) =>
        _drawn is not { } drawn || !drawn.Equals(Current()) || (drawn.IsTicking && now - _drawnAt >= Tick);

    /// <summary>While something plays the bar moves: the host paints the card again a second after the last paint, and only then.</summary>
    public override DateTime? NextAnimationAt => _drawn is { IsTicking: true } ? _drawnAt + Tick : null;

    public override void Render(WidgetRenderContext context)
    {
        Shown shown = Current();
        _drawn = shown;
        _drawnAt = context.Time;

        var canvas = context.Canvas;
        var theme = context.Theme;
        var layout = Layout.Of(context.ContentRect);

        if (shown.IsIdle)
        {
            SetArtSource("");
            _buttons = SKRect.Empty;
            var (idleArt, idleText) = Layout.Idle(context.ContentRect);
            ImageTile.Draw(canvas, theme, idleArt, null, null, null, "app/music");
            string label = string.IsNullOrWhiteSpace(Config.IdleLabel) ? "" : Config.IdleLabel;
            if (label.Length > 0)
                TextLine.Draw(canvas, theme, idleText, label, TextStep.Title, TextEmphasis.Muted, HorizontalAlign.Left);
            return;
        }

        DrawArt(canvas, theme, layout.Art, shown.Art);
        DrawText(canvas, theme, layout.Text, shown);
        if (shown.Duration > 0)
            DrawProgress(canvas, theme, layout.Bar, Fraction(shown, context.Time));

        _buttons = layout.Buttons;
        DrawButtons(canvas, theme, layout.Buttons, shown);
    }

    public bool CanTap(SKPoint point) => HitButton(point) is >= 0;

    public void OnTap(SKPoint point)
    {
        int button = HitButton(point);
        if (button >= 0)
            _ = Actions.Invoke(ButtonActions[button]);
    }

    /// <summary>The button a point hits and the player accepts, or -1; the card keeps no other tap zone.</summary>
    private int HitButton(SKPoint point)
    {
        if (_drawn is not { IsIdle: false } shown)
            return -1;
        int button = ButtonRow.HitTest(_buttons, ButtonWeights.Length, point, ButtonWeights);
        return button >= 0 && Accepts(shown, button) ? button : -1;
    }

    /// <summary>A command is refused only when the player says so; a player that does not say is trusted.</summary>
    private static bool Accepts(Shown shown, int button) => button switch
    {
        0 => shown.CanPrevious != false,
        1 => shown.CanPlayPause != false,
        _ => shown.CanNext != false,
    };

    // Reading the provider

    private Shown Current()
    {
        string status = Text("status");
        return new Shown(
            status.Length == 0 ? "none" : status,
            Text("title"),
            Text("artist"),
            Text("album"),
            Config.ShowApp ? Text("app") : "",
            Text("art"),
            Math.Max(0, Number("duration")),
            Math.Max(0, Number("position")),
            Time("position/at"),
            Flag("can/previous"),
            Flag("can/playpause"),
            Flag("can/next"));
    }

    private string Text(string path) => Readings.Read(Prefix + path).Value is { Type: ReadingValueType.Text } value ? value.Text : "";

    private double Number(string path) => Readings.Read(Prefix + path).Value is { Type: ReadingValueType.Number } value ? value.Number : 0;

    private DateTimeOffset? Time(string path) => Readings.Read(Prefix + path).Value is { Type: ReadingValueType.Time } value ? value.Time : null;

    private bool? Flag(string path) => Readings.Read(Prefix + path).Value is { Type: ReadingValueType.OnOff } value ? value.IsOn : null;

    /// <summary>
    /// How far along the track is, 0 to 1: the position the player gave plus the time since it gave it while it plays. A stamp
    /// ahead of the clock adds nothing, so a position reported "later today" (the sample's) holds still.
    /// </summary>
    private static float Fraction(Shown shown, DateTime now)
    {
        double seconds = shown.Position;
        if (shown.Status == "playing" && shown.PositionAt is { } at)
            seconds += Math.Max(0, (now - at.LocalDateTime).TotalSeconds);
        return (float)Math.Clamp(seconds / shown.Duration, 0, 1);
    }

    // Drawing

    private void DrawArt(SKCanvas canvas, Theme theme, SKRect rect, string art)
    {
        SetArtSource(art);
        SKImage? image = null;
        if (art.Length > 0)
            image = Icons.GetIcon(art, (int)Math.Ceiling(Math.Min(rect.Width, rect.Height))).Image;

        // The placeholder doubles as the loading state, so a track never shows the previous cover.
        ImageTile.Draw(canvas, theme, rect, image, null, null, "app/music");
    }

    /// <summary>Lets go of the cover the card stops showing, so the icon service keeps one decoded cover and not one per track.</summary>
    private void SetArtSource(string art)
    {
        if (string.Equals(art, _artSource, StringComparison.Ordinal))
            return;
        if (_artSource.Length > 0)
            Icons.Release(_artSource);
        _artSource = art;
    }

    /// <summary>
    /// The title, the artist and, when there is room and the setting asks, the album and the player, each on its own line and
    /// shrunk to fit its width; the block is centred beside the art.
    /// </summary>
    private void DrawText(SKCanvas canvas, Theme theme, SKRect area, Shown shown)
    {
        var lines = new List<(string Text, TextStep Step, TextEmphasis Emphasis)>(4);
        if (shown.Title.Length > 0)
            lines.Add((shown.Title, TextStep.Title, TextEmphasis.Primary));
        if (shown.Artist.Length > 0)
            lines.Add((shown.Artist, TextStep.Body, TextEmphasis.Muted));
        if (Config.ShowAlbum && shown.Album.Length > 0)
            lines.Add((shown.Album, TextStep.Label, TextEmphasis.Muted));
        if (shown.App.Length > 0)
            lines.Add((shown.App, TextStep.Label, TextEmphasis.Muted));

        // What does not fit goes: the player first, then the album.
        while (lines.Count > 1 && Height(theme, lines) > area.Height)
            lines.RemoveAt(lines.Count - 1);

        float y = area.MidY - Height(theme, lines) / 2;
        foreach (var (text, step, emphasis) in lines)
        {
            float height = LineHeight(theme, step);
            TextLine.Draw(canvas, theme, new SKRect(area.Left, y, area.Right, y + height), text, step, emphasis, HorizontalAlign.Left);
            y += height;
        }
    }

    private static float LineHeight(Theme theme, TextStep step) => theme.GetTextSize(step) * 1.4f;

    private static float Height(Theme theme, List<(string Text, TextStep Step, TextEmphasis Emphasis)> lines) =>
        lines.Sum(line => LineHeight(theme, line.Step));

    private static void DrawProgress(SKCanvas canvas, Theme theme, SKRect bar, float fraction)
    {
        float radius = theme.StrokeCap == SKStrokeCap.Round ? bar.Height / 2 : 0;
        using var track = new SKPaint { Color = theme.AccentDim, IsAntialias = true };
        canvas.DrawRoundRect(bar, radius, radius, track);
        if (fraction <= 0)
            return;

        var filled = new SKRect(bar.Left, bar.Top, bar.Left + Math.Max(bar.Height, bar.Width * fraction), bar.Bottom);
        using var fill = new SKPaint { Color = theme.Accent, IsAntialias = true };
        canvas.DrawRoundRect(filled, radius, radius, fill);
    }

    private static void DrawButtons(SKCanvas canvas, Theme theme, SKRect area, Shown shown)
    {
        SKRect[] rects = ButtonRow.Layout(area, ButtonWeights.Length, ButtonWeights);
        (string Glyph, string Text)[] faces =
        [
            ("media/previous", "Prev"),
            (shown.Status == "playing" ? "media/pause" : "media/play", shown.Status == "playing" ? "Pause" : "Play"),
            ("media/next", "Next"),
        ];

        for (int i = 0; i < rects.Length; i++)
        {
            // A command the player refuses is dimmed, so the card says why a tap does nothing.
            bool dimmed = !Accepts(shown, i);
            if (dimmed)
            {
                using var layer = new SKPaint { Color = SKColors.White.WithAlpha(0x66) };
                canvas.SaveLayer(layer);
            }

            float inset = rects[i].Width * 0.25f;
            var face = new SKRect(rects[i].Left + inset, rects[i].Top + inset, rects[i].Right - inset, rects[i].Bottom - inset);
            if (!ThemedIcon.Draw(canvas, theme, face, faces[i].Glyph))
                TextLine.Draw(canvas, theme, rects[i], faces[i].Text, TextStep.Label);

            if (dimmed)
                canvas.Restore();
        }
    }

    /// <summary>The card's rectangles, all from the content rectangle's height and width: no resolution is assumed.</summary>
    private readonly record struct Layout(SKRect Art, SKRect Text, SKRect Bar, SKRect Buttons)
    {
        public static Layout Of(SKRect content)
        {
            float height = content.Height;
            float top = height * 0.50f;
            float gap = height * 0.06f;
            float side = Math.Min(top, content.Width * 0.4f);
            var art = SKRect.Create(content.Left, content.Top + (top - side) / 2, side, side);
            var text = new SKRect(art.Right + gap, content.Top, content.Right, content.Top + top);

            float barHeight = Math.Max(3f, height * 0.045f);
            float barTop = content.Top + top + gap * 1.2f;
            var bar = new SKRect(content.Left, barTop, content.Right, barTop + barHeight);

            var buttons = new SKRect(content.Left, bar.Bottom + gap * 0.8f, content.Right, content.Bottom);
            return new Layout(art, text, bar, buttons);
        }

        /// <summary>With nothing playing the placeholder and its words sit in the middle of the card's height, not at the top.</summary>
        public static (SKRect Art, SKRect Text) Idle(SKRect content)
        {
            float side = Math.Min(content.Height * 0.5f, content.Width * 0.4f);
            float gap = content.Height * 0.06f;
            var art = SKRect.Create(content.Left, content.MidY - side / 2, side, side);
            return (art, new SKRect(art.Right + gap, art.Top, content.Right, art.Bottom));
        }
    }
}
