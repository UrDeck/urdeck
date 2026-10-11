// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Display;

/// <summary>
/// Keeps the deck's window on its display: settle, place, verify, confirm. Any signal first looks whether the display
/// is connected at all and hides the deck at once when it is not. Otherwise it waits until signals stop, then checks
/// the placement (content area, scaling, laid-out size), covers the display again while the check fails, and checks
/// twice more after it passed.
/// <para>
/// It owns no timer and reads no clock. <see cref="Signal"/> and <see cref="Tick"/> take the caller's monotonic time
/// and return the delay after which <see cref="Tick"/> wants to be called, or null when nothing is pending. All calls
/// come from one thread.
/// </para>
/// </summary>
public sealed class PlacementController
{
    /// <summary>How long signals must have stopped before the window is placed.</summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest a stream of signals can postpone placement.</summary>
    public static readonly TimeSpan LongestSettle = TimeSpan.FromSeconds(2);

    /// <summary>How long attempts go on before the controller gives up until the next signal.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    // After a correct placement, measured from it: the question "did Windows report every late change?" has only
    // been answered for one panel and one driver.
    private static readonly TimeSpan[] ConfirmAfter = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)];

    private readonly IDisplayEnvironment _environment;
    private readonly IPlacementOwner _owner;
    private readonly Func<UrDeckConfig> _config;

    private bool _shown;
    private bool _forceRebuild;
    private TimeSpan? _due;
    private TimeSpan _burstStart;
    private TimeSpan _firstAttempt;
    private int _attempt;
    private TimeSpan _placedAt;
    private int _confirmed;
    private (string Path, Rectangle Bounds, double Scaling)? _lastPlaced;
    private string? _announcedPath;

    public PlacementController(IDisplayEnvironment environment, IPlacementOwner owner, Func<UrDeckConfig> config)
    {
        _environment = environment;
        _owner = owner;
        _config = config;
    }

    public PlacementState State { get; private set; } = PlacementState.Starting;

    /// <summary>Something changed. Returns the delay until <see cref="Tick"/> is due, or null.</summary>
    public TimeSpan? Signal(PlacementSignal signal, TimeSpan now)
    {
        if (signal == PlacementSignal.Resumed)
            _forceRebuild = true;

        if (signal == PlacementSignal.WindowChanged)
        {
            // While attempts run, a window event is the echo of the last one; the next attempt looks anyway, and
            // starting over here would keep a window that never verifies busy for ever. A hidden window has no say.
            if (State == PlacementState.Placing)
                return Remaining(now);
            if (State is PlacementState.Starting or PlacementState.Absent)
                return null;
        }

        // The cheap part runs before any wait: Windows moves the windows of a disconnected display onto another
        // one, and the deck must not sit there for the length of a timer.
        var display = FindDisplay(out var displays);
        if (display == null)
            return EnterAbsent(displays);

        if (!_shown)
        {
            // Covered first, so the deck never appears on another display.
            _environment.Cover(display.Bounds, nudge: false);
            _environment.Show();
            _shown = true;
            _forceRebuild = true;
            UrDeckLog.Info($"Deck shown on {display.Name}");
        }

        if (State != PlacementState.Settling)
        {
            _burstStart = now;
            SetState(PlacementState.Settling);
        }

        var quiet = now + QuietPeriod;
        var latest = _burstStart + LongestSettle;
        return Schedule(quiet < latest ? quiet : latest, now);
    }

    /// <summary>Does what is due, if anything. Returns the delay until the next call is due, or null.</summary>
    public TimeSpan? Tick(TimeSpan now)
    {
        if (_due is not { } due)
            return null;
        if (now < due)
            return due - now;

        switch (State)
        {
            case PlacementState.Settling:
                _firstAttempt = now;
                _attempt = 0;
                SetState(PlacementState.Placing);
                return Check(now);
            case PlacementState.Placing:
            case PlacementState.Placed:
                return Check(now);
            default:
                _due = null;
                return null;
        }
    }

    private TimeSpan? Check(TimeSpan now)
    {
        var display = FindDisplay(out var displays);
        if (display == null)
            return EnterAbsent(displays);

        var differences = Verify(display, out bool onlyLayout);
        if (differences.Count == 0)
            return State == PlacementState.Placed ? Confirmed(now) : EnterPlaced(display, now);

        string what = string.Join("; ", differences);
        if (State == PlacementState.Placed)
        {
            UrDeckLog.Info($"Placement on {display.Name} no longer holds {(now - _placedAt).TotalSeconds:0.#} s after it was verified: {what}");
            _firstAttempt = now;
            _attempt = 0;
            SetState(PlacementState.Placing);
        }

        if (now - _firstAttempt >= GiveUpAfter)
        {
            var content = _environment.ContentBounds;
            UrDeckLog.Warn($"Placement on {display.Name} not reached after {GiveUpAfter.TotalSeconds:0} s: {what} " +
                           $"(expected {Describe(display.Bounds)}, content area is {Describe(content)}). Waiting for the next display change.");
            _due = null;
            _lastPlaced = null;
            SetState(PlacementState.GaveUp);
            // A page on a window that is a little off is better than none.
            _owner.RebuildPage(_forceRebuild);
            _forceRebuild = false;
            return null;
        }

        _attempt++;
        UrDeckLog.Info($"Placement attempt {_attempt} on {display.Name}: {what}; covering {Describe(display.Bounds)}" +
                       (onlyLayout ? " with a resize to refresh the layout" : ""));
        _environment.Cover(display.Bounds, nudge: onlyLayout);
        return Schedule(now + RetryDelays[Math.Min(_attempt, RetryDelays.Length) - 1], now);
    }

    /// <summary>What differs from a correct placement on <paramref name="display"/>; empty when it is correct.</summary>
    private List<string> Verify(DisplayInfo display, out bool onlyLayout)
    {
        var differences = new List<string>();
        var content = _environment.ContentBounds;
        double scaling = _environment.WindowScaling;
        var layout = _environment.LayoutSize;

        if (content != display.Bounds)
            differences.Add($"content area {Describe(content)} instead of {Describe(display.Bounds)}");
        if (Math.Abs(scaling - display.Scaling) > 0.005)
            differences.Add($"window scaling {scaling * 100:0}% instead of {display.Scaling * 100:0}%");

        // XAML can keep the content size of the old scaling while the window is right.
        bool stale = Math.Abs(layout.Width * scaling - content.Width) > 2 || Math.Abs(layout.Height * scaling - content.Height) > 2;
        onlyLayout = stale && differences.Count == 0;
        if (stale)
            differences.Add($"layout {layout.Width:0.#}x{layout.Height:0.#} at {scaling * 100:0}% does not fill {content.Width}x{content.Height}");
        return differences;
    }

    private TimeSpan? EnterPlaced(DisplayInfo display, TimeSpan now)
    {
        var placed = (display.Path, display.Bounds, display.Scaling);
        bool changed = _lastPlaced != placed;
        bool rebuild = changed || _forceRebuild;
        UrDeckLog.Info($"Placement verified on {display.Name}: {Describe(display.Bounds)} at {display.Scaling * 100:0}%" +
                       (_attempt > 0 ? $" after {_attempt} attempt(s)" : ", window not moved") +
                       (rebuild ? "; rebuilding the page" : "; page kept"));
        SetState(PlacementState.Placed);

        bool force = _forceRebuild;
        _lastPlaced = placed;
        _forceRebuild = false;
        if (rebuild)
            _owner.RebuildPage(force);

        _placedAt = now;
        _confirmed = 0;
        return Schedule(now + ConfirmAfter[0], now);
    }

    private TimeSpan? Confirmed(TimeSpan now)
    {
        _confirmed++;
        if (_confirmed < ConfirmAfter.Length)
            return Schedule(_placedAt + ConfirmAfter[_confirmed], now);
        _due = null;
        return null;
    }

    private TimeSpan? EnterAbsent(IReadOnlyList<DisplayInfo> displays)
    {
        _due = null;
        if (State == PlacementState.Absent)
            return null;

        if (_shown)
        {
            _environment.Hide();
            _shown = false;
            _owner.ReleasePage();
        }

        string connected = displays.Count == 0 ? "none" : string.Join("; ", displays);
        string awaited = _config().Display?.Name is { Length: > 0 } name ? $"'{name}'" : "a display";
        UrDeckLog.Info($"Deck hidden: waiting for {awaited}, which is not connected. Connected: {connected}");
        _announcedPath = null;
        _lastPlaced = null;
        SetState(PlacementState.Absent);
        return null;
    }

    /// <summary>Enumerates and matches, stores what the match says to remember, and says once how the display was found.</summary>
    private DisplayInfo? FindDisplay(out IReadOnlyList<DisplayInfo> displays)
    {
        displays = _environment.GetDisplays();
        var config = _config();
        var selection = DisplaySelector.Select(config, displays);
        var display = selection.Display;
        if (display == null)
            return null;

        switch (selection.Match)
        {
            case DisplayMatch.Chosen:
                UrDeckLog.Info($"Display chosen with '{selection.Remember!.Selector}': {display}. It is remembered as 'display' " +
                               "in the configuration; remove that entry to choose again.");
                break;
            case DisplayMatch.Model:
                UrDeckLog.Info($"Display {display.Name} found by its model {display.Model} at another device path; remembering {display.Path}");
                break;
            case DisplayMatch.AmbiguousModel:
                UrDeckLog.Warn($"{selection.Candidates.Count} displays have the model {display.Model} and none has the remembered device path: " +
                               $"{string.Join("; ", selection.Candidates.Select(c => $"{c} [{c.Path}]"))}. Using the first, {display.DeviceName}.");
                break;
            default:
                if (!string.Equals(_announcedPath, display.Path, StringComparison.OrdinalIgnoreCase))
                    UrDeckLog.Info($"Display matched by device path: {display}");
                break;
        }

        _announcedPath = display.Path;
        if (selection.Remember != null)
        {
            config.Display = selection.Remember;
            _owner.SaveRememberedDisplay(selection.Remember);
        }

        return display;
    }

    private void SetState(PlacementState state)
    {
        if (State == state)
            return;
        UrDeckLog.Info($"Placement: {State} -> {state}");
        State = state;
        _owner.StateChanged(state, _config().Display?.Name);
    }

    private TimeSpan Schedule(TimeSpan due, TimeSpan now)
    {
        _due = due;
        return due > now ? due - now : TimeSpan.Zero;
    }

    private TimeSpan? Remaining(TimeSpan now) => _due is { } due ? (due > now ? due - now : TimeSpan.Zero) : null;

    private static string Describe(Rectangle r) => $"{r.Width}x{r.Height} at {r.X},{r.Y}";
}
