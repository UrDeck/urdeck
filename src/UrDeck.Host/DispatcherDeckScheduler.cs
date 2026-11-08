// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using UrDeck.Engine.Deck;

namespace UrDeck.Host;

/// <summary>
/// The deck session's thread: the window's dispatcher queue and its timers, the wall clock and the tick count. Disposing it
/// stops every timer it made and detaches their handlers: a dispatcher timer holds its handler from native code, and a handler
/// that leads back to the timer's owner keeps all of it alive after it is gone (the editor's preview, opened and closed).
/// </summary>
internal sealed class DispatcherDeckScheduler(DispatcherQueue queue) : IDeckScheduler, IDisposable
{
    private readonly List<Timer> _timers = [];
    private bool _disposed;

    public DateTime Now => DateTime.Now;

    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public void Post(Action action, DeckPriority priority = DeckPriority.Normal) =>
        queue.TryEnqueue(priority == DeckPriority.Low ? DispatcherQueuePriority.Low : DispatcherQueuePriority.Normal, () => action());

    public IDeckTimer CreateTimer(Action tick)
    {
        var timer = new Timer(queue.CreateTimer(), tick);
        if (!_disposed)
            _timers.Add(timer);
        return timer;
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var timer in _timers)
            timer.Detach();
        _timers.Clear();
    }

    private sealed class Timer : IDeckTimer
    {
        private readonly DispatcherQueueTimer _timer;
        private readonly Windows.Foundation.TypedEventHandler<DispatcherQueueTimer, object> _handler;

        public Timer(DispatcherQueueTimer timer, Action tick)
        {
            _timer = timer;
            _handler = (_, _) => tick();
            _timer.Tick += _handler;
        }

        public void Detach()
        {
            _timer.Stop();
            _timer.Tick -= _handler;
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
