// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Input;

namespace UrDeck.Widgets.Control;

/// <summary>
/// A tile that shows a reading's state (on, off, or neither) and asks a provider to act when it is tapped. It names no provider:
/// the reading's description says what a tap does and which icon fits, so the same tile serves a Home Assistant light, a media
/// player or a scene. A tap makes the tile pending until the state changes or the action ends; taps do not queue, except the
/// dimmer bar's latest one.
/// </summary>
[Widget("Control", "Shows a reading's state and acts on it when tapped", Id = "urdeck.widgets.control")]
[WidgetSize(1, 1)]
[WidgetSize(2, 1)]
[RefreshOnData]
[Category("Control")]
public class ControlWidget : Widget<ControlConfig>, ITapTarget
{
    /// <summary>A text value is on when it is one of these (ignoring case).</summary>
    private static readonly string[] OnWords = ["on", "open", "unlocked", "playing", "home"];

    /// <summary>How long a done action waits for the value to change before the tile gives up waiting.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1);

    /// <summary>How long the failed look stays.</summary>
    private static readonly TimeSpan FailedTime = TimeSpan.FromSeconds(2);

    /// <summary>The longest the tile waits for an action the invoker never completes; the provider's own limit ends it sooner.</summary>
    private static readonly TimeSpan GiveUpTime = TimeSpan.FromSeconds(15);

    /// <summary>How often a pending or failed tile looks at what has happened: only then, never at rest.</summary>
    private static readonly TimeSpan PollTime = TimeSpan.FromMilliseconds(100);

    /// <summary>A tap on the bar within this fraction of an end sets 0 or 100.</summary>
    private const float EndZone = 0.04f;

    private const int NotCompleted = 0;
    private const int CompletedDone = 1;
    private const int CompletedFailed = 2;

    private enum Tone { Neutral, Off, On }

    /// <summary>What a paint puts on the card; two equal values draw the same pixels.</summary>
    private readonly record struct Shown(
        string Label,
        string Icon,
        string State,
        Tone Tone,
        bool Stale,
        bool Pending,
        bool Failed,
        bool Dimmer,
        float? Level,
        string LevelText);

    /// <summary>The readings' values when a tap was sent; a pending tile ends when either differs.</summary>
    private readonly record struct Baseline(ReadingValue? Main, ReadingValue? Level);

    private string[]? _subscriptions;
    private Shown? _drawn;
    private DateTime _drawnAt;
    private SKRect _switchArea = SKRect.Empty;
    private SKRect _barArea = SKRect.Empty;
    private bool _missingLevelLogged;

    // The tap's state. Only the UI thread touches these, except the completion fields, which a worker thread sets.
    private bool _pending;
    private bool _stamp;
    private DateTime _pendingSince;
    private DateTime? _settledAt;
    private DateTime? _failedUntil;
    private Baseline _baseline;
    private float? _queuedLevel;
    private int _generation;
    private int _completion;

    /// <summary>A new tile is a neutral placeholder with a name and an icon: no reading, no action. The user points it at a reading.</summary>
    public override ControlConfig DefaultConfig => new() { Label = "Control", Icon = "home/switch", Action = "" };

    public override IReadOnlyCollection<string> Subscriptions => _subscriptions ??= BuildSubscriptions();

    protected override void OnConfigured()
    {
        _subscriptions = null;
        _drawn = null;
        _switchArea = SKRect.Empty;
        _barArea = SKRect.Empty;
        _pending = false;
        _failedUntil = null;
        _queuedLevel = null;
        Interlocked.Increment(ref _generation);

        if (IsDimmerRequested && string.IsNullOrWhiteSpace(Config.Level) && !_missingLevelLogged)
        {
            _missingLevelLogged = true;
            Log("Control: style 'dimmer' needs a 'level' reading; the tile is drawn as a switch.");
        }
    }

    // Configuration

    private string ReadingId => Config.Reading?.Trim() ?? "";

    private string LevelId => Config.Level?.Trim() ?? "";

    private bool IsDimmerRequested => string.Equals(Config.Style?.Trim(), "dimmer", StringComparison.OrdinalIgnoreCase);

    /// <summary>The dimmer needs the room of 2x1 and a level reading; anything else is a switch.</summary>
    private bool IsDimmer => IsDimmerRequested && Config.Width >= 2 && LevelId.Length > 0;

    private string[] BuildSubscriptions() =>
        new[] { ReadingId, LevelId }.Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>One rule for action, label, icon and level action: absent is the reading's, a text is itself, empty is none.</summary>
    private static string Resolve(string? setting, string? described) => setting ?? described ?? "";

    private static string ProviderOf(string readingId)
    {
        int colon = readingId.IndexOf(':');
        return colon > 0 ? readingId[..colon] : "";
    }

    /// <summary>An action written without a provider is taken under the reading's provider; "" when there is none.</summary>
    private static string QualifyAction(string action, string readingId)
    {
        action = action.Trim();
        if (action.Length == 0 || action.Contains(':'))
            return action;
        string provider = ProviderOf(readingId);
        return provider.Length == 0 ? action : provider + ":" + action;
    }

    private string ActionId()
    {
        string described = ReadingId.Length > 0 ? Readings.Describe(ReadingId)?.Action ?? "" : "";
        return QualifyAction(Resolve(Config.Action, described), ReadingId);
    }

    private string LevelActionId()
    {
        string described = LevelId.Length > 0 ? Readings.Describe(LevelId)?.Action ?? "" : "";
        return QualifyAction(Resolve(Config.LevelAction, described), LevelId);
    }

    // The state

    private Shown Current()
    {
        string id = ReadingId;
        var reading = id.Length > 0 ? Readings.Read(id) : default;
        var descriptor = id.Length > 0 ? Readings.Describe(id) : null;

        string label = Resolve(Config.Label, descriptor?.Label ?? (reading.State == ReadingState.Unavailable ? id : null));
        string icon = Resolve(Config.Icon, descriptor?.Icon).Trim();

        (Tone tone, string state) = id.Length == 0 ? (Tone.Neutral, "") : Describe(reading, descriptor);

        float? level = null;
        string levelText = "";
        if (IsDimmer)
        {
            var levelReading = Readings.Read(LevelId);
            if (levelReading.Value is { Type: ReadingValueType.Number } value && levelReading.State != ReadingState.Unavailable)
                level = (float)Math.Clamp(value.Number / 100.0, 0, 1);
            levelText = ReadingFormatter.Format(levelReading, Readings.Describe(LevelId), null, Readings) is { } text
                ? text.Value + (text.Unit ?? "")
                : "";
        }

        return new Shown(
            label, icon, state, tone, id.Length > 0 && reading.State == ReadingState.Stale, _pending, _failedUntil != null, IsDimmer, level, levelText);
    }

    private (Tone Tone, string State) Describe(Reading reading, ReadingDescriptor? descriptor)
    {
        if (reading.State == ReadingState.Unavailable || reading.Value is not { } value)
            return (Tone.Neutral, string.IsNullOrEmpty(reading.Reason) ? "Unavailable" : Capitalised(reading.Reason));

        var text = ReadingFormatter.Format(reading, descriptor, null, Readings);
        string shown = text.Value + (text.Unit is { Length: > 0 } unit ? (text.UnitPlacement == UnitPlacement.Baseline ? " " : "") + unit : "");
        if (value.Type == ReadingValueType.Text && shown.Length > 0)
            shown = Capitalised(shown);
        return (IsOn(value) ? Tone.On : Tone.Off, shown);
    }

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static bool IsOn(ReadingValue value) => value.Type switch
    {
        ReadingValueType.OnOff => value.IsOn,
        ReadingValueType.Text => OnWords.Contains(value.Text.Trim(), StringComparer.OrdinalIgnoreCase),
        ReadingValueType.Number => value.Number > 0,
        _ => false,
    };

    // Taps

    public bool CanTap(SKPoint point)
    {
        if (_drawn is not { } shown)
            return false;
        if (shown.Dimmer)
        {
            if (_barArea.Contains(point))
                return LevelActionId().Length > 0;
            return _switchArea.Contains(point) && !_pending && ActionId().Length > 0;
        }

        return !_pending && ActionId().Length > 0;
    }

    public void OnTap(SKPoint point)
    {
        if (_drawn is { Dimmer: true } && _barArea.Contains(point))
        {
            string levelAction = LevelActionId();
            if (levelAction.Length == 0 || _barArea.Width <= 0)
                return;

            float fraction = Math.Clamp((point.X - _barArea.Left) / _barArea.Width, 0f, 1f);
            float percent = fraction <= EndZone ? 0f : fraction >= 1f - EndZone ? 100f : MathF.Round(100f * fraction);
            if (_pending)
                _queuedLevel = percent;
            else
                Send(levelAction, percent);
            return;
        }

        string action = ActionId();
        if (!_pending && action.Length > 0)
            Send(action, null);
    }

    /// <summary>Invokes the action and puts the tile in the pending look. The completion only sets a flag the next paint reads.</summary>
    private void Send(string action, float? argument)
    {
        _pending = true;
        _stamp = true;
        _settledAt = null;
        _failedUntil = null;
        _baseline = new Baseline(ValueOf(ReadingId), ValueOf(IsDimmer ? LevelId : ""));
        Volatile.Write(ref _completion, NotCompleted);
        int generation = Interlocked.Increment(ref _generation);

        ReadingValue? value = argument is { } number ? ReadingValue.FromNumber(number) : default(ReadingValue?);
        _ = Actions.Invoke(action, value).ContinueWith(
            task =>
            {
                if (Volatile.Read(ref _generation) != generation)
                    return;
                bool done = task is { IsCompletedSuccessfully: true, Result.Succeeded: true };
                Volatile.Write(ref _completion, done ? CompletedDone : CompletedFailed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private ReadingValue? ValueOf(string id) => id.Length == 0 ? null : Readings.Read(id).Value;

    /// <summary>
    /// Moves the tap's state to <paramref name="now"/>: a changed value, a settled action, a failure or the give-up time ends the
    /// pending look; the failed look ends after its time; a kept bar tap is sent when pending ends. Safe to call again.
    /// </summary>
    private void Advance(DateTime now)
    {
        if (_stamp)
        {
            _pendingSince = now;
            _stamp = false;
        }

        if (_failedUntil is { } until && now >= until)
            _failedUntil = null;

        if (!_pending)
            return;

        int completion = Volatile.Read(ref _completion);
        if (completion == CompletedFailed || now - _pendingSince >= GiveUpTime)
        {
            _pending = false;
            _queuedLevel = null;
            _failedUntil = now + FailedTime;
            return;
        }

        if (completion == CompletedDone)
            _settledAt ??= now;

        bool changed = ValueOf(ReadingId) != _baseline.Main || ValueOf(IsDimmer ? LevelId : "") != _baseline.Level;
        if (!changed && !(_settledAt is { } settled && now - settled >= SettleTime))
            return;

        _pending = false;
        if (_queuedLevel is { } queued)
        {
            _queuedLevel = null;
            string levelAction = LevelActionId();
            if (levelAction.Length > 0)
                Send(levelAction, queued);
        }
    }

    // Painting

    public override bool NeedsRender(DateTime now)
    {
        Advance(now);
        return _drawn is not { } drawn || drawn != Current();
    }

    /// <summary>While pending or failed the host paints the tile again shortly, and a paint looks at what happened; at rest never.</summary>
    public override DateTime? NextAnimationAt => _pending || _failedUntil != null ? _drawnAt + PollTime : null;

    public override void Render(WidgetRenderContext context)
    {
        Advance(context.Time);
        var shown = Current();
        _drawn = shown;
        _drawnAt = context.Time;

        var canvas = context.Canvas;
        var theme = context.Theme;
        SKColor color = ColorOf(theme, shown);

        int save = -1;
        if (shown.Stale)
        {
            using var layer = new SKPaint { Color = SKColors.White.WithAlpha(0x99) };
            save = canvas.SaveLayer(layer);
        }

        var content = context.ContentRect;
        if (shown.Dimmer)
        {
            float side = Math.Min(content.Height, content.Width / 2);
            _switchArea = SKRect.Create(content.Left, content.Top, side, content.Height);
            _barArea = new SKRect(_switchArea.Right + theme.Gap, content.Top, content.Right, content.Bottom);
            DrawStacked(canvas, theme, _switchArea, shown, color, withLabel: false);
            DrawBar(canvas, theme, _barArea, shown);
        }
        else
        {
            _switchArea = content;
            _barArea = SKRect.Empty;
            if (Config.Width >= 2)
                DrawRow(canvas, theme, content, shown, color);
            else
                DrawStacked(canvas, theme, content, shown, color, withLabel: true);
        }

        if (save >= 0)
            canvas.RestoreToCount(save);
    }

    /// <summary>On in the accent, off in the muted text, neither in the text colour; pending dimmed, failed critical.</summary>
    private static SKColor ColorOf(Theme theme, Shown shown) =>
        shown.Failed ? theme.Critical
        : shown.Pending ? theme.AccentDim
        : shown.Tone switch
        {
            Tone.On => theme.Accent,
            Tone.Off => theme.TextMuted,
            _ => theme.Text,
        };

    private static string StateText(Shown shown) => shown.Failed ? "Failed" : shown.Pending ? "…" : shown.State;

    private static float LineHeight(Theme theme, TextStep step) => theme.GetTextSize(step) * 1.4f;

    /// <summary>Icon above, label and state below. The label goes first when there is no room, then the state, then the icon's share.</summary>
    private void DrawStacked(SKCanvas canvas, Theme theme, SKRect area, Shown shown, SKColor color, bool withLabel)
    {
        float labelHeight = withLabel && shown.Label.Length > 0 ? LineHeight(theme, TextStep.Label) : 0f;
        float stateHeight = StateText(shown).Length > 0 ? LineHeight(theme, TextStep.Body) : 0f;

        // The icon keeps at least a third of the card; what does not fit goes, the label before the state.
        float minIcon = shown.Icon.Length > 0 ? area.Height / 3f : 0f;
        if (area.Height - labelHeight - stateHeight < minIcon)
            labelHeight = 0f;
        if (area.Height - stateHeight < minIcon)
            stateHeight = 0f;

        float textHeight = labelHeight + stateHeight;
        float gap = textHeight > 0 && shown.Icon.Length > 0 ? area.Height * 0.04f : 0f;
        var iconArea = new SKRect(area.Left, area.Top, area.Right, area.Bottom - textHeight - gap);
        if (shown.Icon.Length == 0)
            iconArea = SKRect.Create(area.Left, area.Top, area.Width, 0);

        float top = shown.Icon.Length == 0 ? area.MidY - textHeight / 2 : iconArea.Bottom + gap;
        DrawIcon(canvas, theme, Inset(iconArea, 0.06f), shown.Icon, color);
        if (labelHeight > 0)
        {
            TextLine.Draw(canvas, theme, new SKRect(area.Left, top, area.Right, top + labelHeight), shown.Label, TextStep.Label, TextEmphasis.Muted);
            top += labelHeight;
        }

        if (stateHeight > 0)
            DrawState(canvas, theme, new SKRect(area.Left, top, area.Right, top + stateHeight), StateText(shown), color, HorizontalAlign.Center);
    }

    /// <summary>Icon at the left, label and state in a column beside it.</summary>
    private void DrawRow(SKCanvas canvas, Theme theme, SKRect area, Shown shown, SKColor color)
    {
        float side = shown.Icon.Length > 0 ? Math.Min(area.Height, area.Width / 3f) : 0f;
        var iconArea = SKRect.Create(area.Left, area.Top, side, area.Height);
        DrawIcon(canvas, theme, Inset(iconArea, 0.08f), shown.Icon, color);

        float left = side > 0 ? iconArea.Right + theme.Gap : area.Left;
        var text = new SKRect(left, area.Top, area.Right, area.Bottom);
        float labelHeight = shown.Label.Length > 0 ? LineHeight(theme, TextStep.Title) : 0f;
        float stateHeight = StateText(shown).Length > 0 ? LineHeight(theme, TextStep.Body) : 0f;

        // The label is dropped before the state when the column is too short for both.
        if (labelHeight + stateHeight > text.Height)
            labelHeight = 0f;

        float top = text.MidY - (labelHeight + stateHeight) / 2;
        if (labelHeight > 0)
        {
            TextLine.Draw(canvas, theme, new SKRect(text.Left, top, text.Right, top + labelHeight), shown.Label, TextStep.Title, TextEmphasis.Primary, HorizontalAlign.Left);
            top += labelHeight;
        }

        if (stateHeight > 0)
            DrawState(canvas, theme, new SKRect(text.Left, top, text.Right, top + stateHeight), StateText(shown), color, HorizontalAlign.Left);
    }

    /// <summary>The level bar of a dimmer: the theme's bar gauge from the level reading, with the label above its readout.</summary>
    private static void DrawBar(SKCanvas canvas, Theme theme, SKRect area, Shown shown)
    {
        var reading = new ReadingText(shown.LevelText.Length > 0 ? shown.LevelText : ReadingFormatter.Dash, null, UnitPlacement.Baseline, "100%", shown.Level != null);
        var options = new GaugeOptions
        {
            Label = shown.Label.Length > 0 ? shown.Label : null,
            Fraction = shown.Level,
            TargetFraction = shown.Level,
        };
        Gauge.Draw(canvas, theme, area, GaugeStyle.Bar, reading, options);
    }

    private static void DrawState(SKCanvas canvas, Theme theme, SKRect rect, string text, SKColor color, HorizontalAlign align) =>
        Readout.Draw(canvas, theme, rect, text, new ReadoutOptions { ValueColor = color, Horizontal = align, Scale = 0.7f });

    private static SKRect Inset(SKRect rect, float fraction)
    {
        float dx = rect.Width * fraction;
        float dy = rect.Height * fraction;
        return new SKRect(rect.Left + dx, rect.Top + dy, rect.Right - dx, rect.Bottom - dy);
    }

    /// <summary>A key of the theme's icon set is drawn tinted; otherwise a local image file comes from the icon service.</summary>
    private void DrawIcon(SKCanvas canvas, Theme theme, SKRect rect, string key, SKColor color)
    {
        if (key.Length == 0 || rect.Width <= 0 || rect.Height <= 0)
            return;

        const string prefix = "theme:";
        bool themed = key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        if (ThemedIcon.Draw(canvas, theme, rect, themed ? key[prefix.Length..].Trim() : key, color) || themed)
            return;

        // Not a key of the set: a file. The service answers with the image (or loads it and repaints the tile).
        if (!Path.IsPathRooted(key) && !key.Contains('.'))
            return;
        var image = Icons.GetIcon(key, (int)Math.Ceiling(Math.Min(rect.Width, rect.Height))).Image;
        if (image == null)
            return;

        float side = Math.Min(rect.Width, rect.Height);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, SKRect.Create(rect.MidX - side / 2, rect.MidY - side / 2, side, side), new SKSamplingOptions(SKFilterMode.Linear), paint);
    }
}
