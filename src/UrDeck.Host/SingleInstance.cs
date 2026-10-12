// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Startup;

namespace UrDeck.Host;

/// <summary>
/// One host per application folder: a session-local mutex held for the life of the process (the system releases it
/// however the process ends, so a crash never blocks the next start) and a session-local event that <c>--quit</c>
/// sets. Create it on the thread that lives as long as the process, and dispose it there.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _quitEvent;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle quitEvent)
    {
        _mutex = mutex;
        _quitEvent = quitEvent;
    }

    /// <summary>Takes the instance for the folder, or returns null when another host from it is running.</summary>
    public static SingleInstance? TryAcquire(string applicationFolder)
    {
        var mutex = new Mutex(initiallyOwned: true, InstanceNames.Mutex(applicationFolder), out bool created);
        if (!created)
        {
            mutex.Dispose();
            return null;
        }

        var quitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, InstanceNames.QuitEvent(applicationFolder));
        return new SingleInstance(mutex, quitEvent);
    }

    /// <summary>Calls <paramref name="onQuit"/> once, on a thread-pool thread, when <c>--quit</c> asks the host to close.</summary>
    public void ListenForQuit(Action onQuit) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_quitEvent, (_, _) => onQuit(), null, Timeout.Infinite, executeOnlyOnce: true);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _wait = null;
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread: the system releases it when the process ends.
        }

        _mutex.Dispose();
        _quitEvent.Dispose();
    }

    /// <summary><c>--quit</c>: asks the host of the folder to close and waits for it. 0 when it ended or none runs, 1 on timeout.</summary>
    public static int Quit(string applicationFolder)
    {
        if (!Mutex.TryOpenExisting(InstanceNames.Mutex(applicationFolder), out var mutex))
            return 0;

        using (mutex)
        {
            if (!EventWaitHandle.TryOpenExisting(InstanceNames.QuitEvent(applicationFolder), out var quitEvent))
                return 0;
            using (quitEvent)
                quitEvent.Set();

            try
            {
                if (mutex.WaitOne(QuitTimeout))
                {
                    mutex.ReleaseMutex();
                    return 0;
                }
            }
            catch (AbandonedMutexException)
            {
                // The host was terminated rather than closed; the mutex is ours now, and it is gone either way.
                mutex.ReleaseMutex();
                return 0;
            }
        }

        UrDeckLog.Warn($"--quit: the host in {applicationFolder} did not end within {QuitTimeout.TotalSeconds:0} s");
        return 1;
    }
}
