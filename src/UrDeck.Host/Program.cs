// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

public static class Program
{
    /// <summary>The instance guard of this process; the application listens on it for <c>--quit</c>.</summary>
    internal static SingleInstance? Instance { get; private set; }

    // A hand-written entry point (the generated one is disabled in the csproj): --snapshot and --quit run with the
    // engine alone and exit before the XAML application is created; the guard comes after them, so both can run
    // while a host does.
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => UrDeckLog.Error("Unhandled exception", e.ExceptionObject as Exception);

        if (Array.IndexOf(args, "--snapshot") >= 0)
            return SnapshotCommand.Run(args);

        if (Array.IndexOf(args, "--quit") >= 0)
            return SingleInstance.Quit(AppContext.BaseDirectory);

        Instance = SingleInstance.TryAcquire(AppContext.BaseDirectory);
        if (Instance is null)
        {
            UrDeckLog.Info("Another UrDeck is already running from this folder; exiting");
            return 0;
        }

        if (Array.IndexOf(args, "--autostart") >= 0)
            UrDeckLog.Info("Started by Windows at sign-in (--autostart)");

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(callback =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        Instance.Dispose();
        return 0;
    }
}
