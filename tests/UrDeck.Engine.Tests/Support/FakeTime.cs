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
    private long _version;

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

    /// <summary>Counts timers created, changed, fired and disposed, so a test can tell when the code under test reacted.</summary>
    private long Version
    {
        get
        {
            lock (_gate)
                return _version;
        }
    }

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
        {
            _timers.Add(timer);
            _version++;
        }

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
    /// Moves the clock from one timer to the next (never past a timer the code under test has not yet scheduled) until the
    /// condition holds. After each jump it waits for the code under test to react and go quiet, so the result does not
    /// depend on how fast the machine is. Returns false when the virtual or the real limit is reached first.
    /// </summary>
    public bool AdvanceUntil(Func<bool> condition, TimeSpan step, TimeSpan virtualLimit, int realLimitMs = 20000)
    {
        long started = Environment.TickCount64;
        var limit = Elapsed + virtualLimit;
        while (!condition())
        {
            if (Elapsed >= limit || Environment.TickCount64 - started > realLimitMs)
                return false;

            long? due;
            lock (_gate)
                due = _timers.Where(t => t.Active).Select(t => (long?)t.Due).Min();
            if (due == null)
            {
                // Nothing is scheduled yet: the code under test is still getting to its next wait.
                WaitReal(() => condition() || ActiveTimers > 0, 1000);
                continue;
            }

            Advance(TimeSpan.FromTicks(Math.Max(0, due.Value - GetTimestamp())));
            long fired = Version;
            WaitReal(() => condition() || Version != fired, 2000);
            WaitQuiet();
        }

        return true;
    }

    private static void WaitReal(Func<bool> done, int timeoutMs)
    {
        long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 1000 * timeoutMs;
        while (!done() && Stopwatch.GetTimestamp() < until)
            Thread.Yield();
    }

    /// <summary>Waits until no timer event happened for a few milliseconds of real time.</summary>
    private void WaitQuiet()
    {
        long seen = Version;
        long quietSince = Stopwatch.GetTimestamp();
        long until = quietSince + Stopwatch.Frequency * 2; // never wait longer than two seconds
        long window = Stopwatch.Frequency / 1000 * 10;
        while (Stopwatch.GetTimestamp() < until)
        {
            long now = Stopwatch.GetTimestamp();
            long version = Version;
            if (version != seen)
            {
                seen = version;
                quietSince = now;
            }
            else if (now - quietSince >= window)
            {
                return;
            }

            Thread.Yield();
        }
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
                owner._version++;
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
                owner._version++;
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
                owner._version++;
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
