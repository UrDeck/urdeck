// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Icons;

/// <summary>
/// One worker thread in a single-threaded apartment, which the shell's COM objects need. The thread is created by the
/// first piece of work and ends when it has had nothing to do for a while, so an idle deck has no such thread.
/// </summary>
internal sealed class StaWorker(TimeSpan idleTime) : IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<Action> _queue = new();
    private Thread? _thread;
    private bool _disposed;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _thread != null;
        }
    }

    /// <summary>Queues <paramref name="work"/>; returns false when the worker has been disposed.</summary>
    public bool Post(Action work)
    {
        lock (_gate)
        {
            if (_disposed)
                return false;

            _queue.Enqueue(work);
            if (_thread != null)
            {
                Monitor.Pulse(_gate);
                return true;
            }

            _thread = new Thread(Run) { IsBackground = true, Name = "UrDeck icons" };
            if (OperatingSystem.IsWindows())
                _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            UrDeckLog.Info("Icon worker started");
            return true;
        }
    }

    private void Run()
    {
        while (true)
        {
            Action work;
            lock (_gate)
            {
                while (_queue.Count == 0)
                {
                    bool signalled = !_disposed && Monitor.Wait(_gate, idleTime);
                    if (!signalled && _queue.Count == 0)
                    {
                        _thread = null;
                        return;
                    }
                }

                work = _queue.Dequeue();
            }

            try
            {
                work();
            }
            catch (Exception ex)
            {
                UrDeckLog.Error("Icon worker: a load failed", ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Monitor.PulseAll(_gate);
        }
    }
}
