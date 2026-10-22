// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Motion;

namespace UrDeck.Host;

/// <summary>
/// Whether the deck's continuous animations may run now: the effective motion level is <see cref="MotionLevel.Full"/>, the
/// display is on and the deck is shown. <see cref="MainWindow"/> feeds it from the same places as the
/// <see cref="BackgroundLayer"/> (<see cref="DisplayPowerWatcher"/>, the deck being shown and hidden), so the background and
/// the border sprites never disagree about display off and a hidden deck. UI thread only.
/// </summary>
internal sealed class MotionGate
{
    private MotionLevel _level;
    private bool _displayOn = true;
    private bool _deckShown;

    public MotionGate(MotionLevel level) => _level = level;

    /// <summary>True when animations may run.</summary>
    public bool MayRun => _level == MotionLevel.Full && _displayOn && _deckShown;

    public MotionLevel Level => _level;

    /// <summary>Raised when <see cref="MayRun"/> changes, with the reason for the log line.</summary>
    public event Action<bool, string>? MayRunChanged;

    public void SetLevel(MotionLevel level) => Change(() => _level = level, "motion level");

    public void SetDisplayOn(bool on) => Change(() => _displayOn = on, on ? "display on" : "display off");

    public void SetDeckShown(bool shown) => Change(() => _deckShown = shown, shown ? "deck shown" : "deck hidden");

    private void Change(Action set, string reason)
    {
        bool before = MayRun;
        set();
        if (MayRun != before)
            MayRunChanged?.Invoke(MayRun, reason);
    }
}
