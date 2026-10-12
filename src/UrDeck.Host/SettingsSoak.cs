// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using Microsoft.UI.Dispatching;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Development measurement behind <c>URDECK_SETTINGS_SOAK=1</c>: opens and closes the settings window a number of times,
/// forces a garbage collection after each close and logs the process's memory, so the cost of the window is a number in
/// <c>urdeck.log</c> (see settings-window, "Cost Of The Settings Window"). With no cycles it opens the window once
/// and leaves it open.
/// </summary>
internal sealed class SettingsSoak
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(600);

    private readonly DispatcherQueueTimer _timer;
    private readonly Action _open;
    private readonly Action _close;
    private readonly int _cycles;
    private int _cycle;
    private bool _isOpen;
    private (long Private, long WorkingSet) _baseline;
    private (long Private, long WorkingSet) _first;
    private bool _settling;

    public SettingsSoak(DispatcherQueue queue, Action open, Action close, int cycles)
    {
        _timer = queue.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Interval = Step;
        _timer.Tick += (_, _) => Advance();
        _open = open;
        _close = close;
        _cycles = cycles;
    }

    /// <summary>Takes the baseline, then starts the cycles after a pause for the deck to settle.</summary>
    public void Start()
    {
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Start();
    }

    private void Advance()
    {
        // One step per call, whatever the timer does: only the end of a step starts the next one.
        _timer.Stop();
        _timer.Interval = Step;
        if (_settling)
        {
            Finish();
            return;
        }

        if (_cycle == 0 && !_isOpen && _baseline == default)
        {
            _baseline = Measure("baseline");
            _open();
            _isOpen = true;
            if (_cycles == 0)
                return; // hold: the window stays open for a look or a screenshot
        }
        else if (_isOpen)
        {
            _close();
            _isOpen = false;
        }
        else
        {
            _cycle++;
            var now = Measure($"after {_cycle}");
            if (_cycle == 1)
                _first = now;
            if (_cycle >= _cycles)
            {
                // The windows are gone; let the process settle before the number that counts is taken.
                _settling = true;
                _timer.Interval = TimeSpan.FromSeconds(5);
                _timer.Start();
                return;
            }

            _open();
            _isOpen = true;
        }

        _timer.Start();
    }

    private void Finish()
    {
        var settled = Measure("settled, 5 s after the last close");
        UrDeckLog.Info($"Soak: {_cycles} cycles done. Settled private memory is {Mb(settled.Private - _baseline.Private):+0.0;-0.0} MB against " +
                       $"the baseline and {Mb(settled.Private - _first.Private):+0.0;-0.0} MB against the first cycle; working set " +
                       $"{Mb(settled.WorkingSet - _baseline.WorkingSet):+0.0;-0.0} MB and {Mb(settled.WorkingSet - _first.WorkingSet):+0.0;-0.0} MB");
    }

    private static (long Private, long WorkingSet) Measure(string label)
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        using var process = Process.GetCurrentProcess();
        var result = (process.PrivateMemorySize64, process.WorkingSet64);
        UrDeckLog.Info($"Soak {label}: private {Mb(result.Item1):0.0} MB, working set {Mb(result.Item2):0.0} MB, gc heap {Mb(GC.GetTotalMemory(false)):0.0} MB");
        return result;
    }

    private static double Mb(long bytes) => bytes / 1048576.0;
}
