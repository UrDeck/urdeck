// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when a test advances it, and whose timers fire in order while it
/// does. <c>Task.Delay(delay, time, token)</c> and <c>time.CreateTimer</c> work against it.
/// </summary>
internal sealed class FakeTime : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private long _now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (_gate)
            return _now;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(_now);
    }

    public TimeSpan Elapsed => TimeSpan.FromTicks(GetTimestamp());

    public int ActiveTimers
    {
        get
        {
            lock (_gate)
                return _timers.Count(t => t.Active);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
            _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves the clock forward, firing every timer that comes due on the way, earliest first.</summary>
    public void Advance(TimeSpan by)
    {
        long target;
        lock (_gate)
            target = _now + by.Ticks;

        while (true)
        {
            FakeTimer? next;
            lock (_gate)
            {
                next = _timers.Where(t => t.Active && t.Due <= target).OrderBy(t => t.Due).FirstOrDefault();
                if (next == null)
                {
                    _now = target;
                    _timers.RemoveAll(t => t.Disposed);
                    return;
                }

                _now = Math.Max(_now, next.Due);
            }

            next.Fire();
        }
    }

    /// <summary>
    /// Advances in small steps, giving the code under test real time to react in between, until the condition holds.
    /// Returns false when the virtual or the real limit is reached first.
    /// </summary>
    public bool AdvanceUntil(Func<bool> condition, TimeSpan step, TimeSpan virtualLimit, int realLimitMs = 20000)
    {
        long started = Environment.TickCount64;
        var limit = Elapsed + virtualLimit;
        while (!condition())
        {
            if (Elapsed >= limit || Environment.TickCount64 - started > realLimitMs)
                return false;
            Advance(step);
            Settle();
        }

        return true;
    }

    /// <summary>
    /// Lets other threads react to the last advance. A sleep would take a whole timer tick (15 ms) on Windows, so spin and
    /// yield for a fraction of a millisecond instead.
    /// </summary>
    private static void Settle()
    {
        long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2000;
        while (Stopwatch.GetTimestamp() < until)
            Thread.Yield();
    }

    private sealed class FakeTimer(FakeTime owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public bool Active { get; private set; }

        public bool Disposed { get; private set; }

        public long Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (Disposed)
                    return false;
                _period = period;
                Active = dueTime != Timeout.InfiniteTimeSpan;
                Due = owner._now + (Active ? dueTime.Ticks : 0);
                return true;
            }
        }

        public void Fire()
        {
            lock (owner._gate)
            {
                if (!Active || Disposed)
                    return;
                if (_period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero)
                    Active = false;
                else
                    Due += _period.Ticks;
            }

            callback(state);
        }

        public void Dispose()
        {
            lock (owner._gate)
            {
                Disposed = true;
                Active = false;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
