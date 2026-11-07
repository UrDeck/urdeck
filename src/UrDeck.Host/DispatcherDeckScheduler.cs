// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using UrDeck.Engine.Deck;

namespace UrDeck.Host;

/// <summary>The deck session's thread: the window's dispatcher queue and its timers, the wall clock and the tick count.</summary>
internal sealed class DispatcherDeckScheduler(DispatcherQueue queue) : IDeckScheduler
{
    public DateTime Now => DateTime.Now;

    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public void Post(Action action, DeckPriority priority = DeckPriority.Normal) =>
        queue.TryEnqueue(priority == DeckPriority.Low ? DispatcherQueuePriority.Low : DispatcherQueuePriority.Normal, () => action());

    public IDeckTimer CreateTimer(Action tick) => new Timer(queue.CreateTimer(), tick);

    private sealed class Timer : IDeckTimer
    {
        private readonly DispatcherQueueTimer _timer;

        public Timer(DispatcherQueueTimer timer, Action tick)
        {
            _timer = timer;
            _timer.Tick += (_, _) => tick();
        }

        public bool IsRunning => _timer.IsRunning;

        public void Start(TimeSpan interval, bool repeat = false)
        {
            _timer.Stop();
            _timer.Interval = interval;
            _timer.IsRepeating = repeat;
            _timer.Start();
        }

        public void Cancel() => _timer.Stop();
    }
}
