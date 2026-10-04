// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

public static class Program
{
    // A hand-written entry point (the generated one is disabled in the csproj): --snapshot renders with the engine
    // alone and exits before the XAML application is created.
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => UrDeckLog.Error("Unhandled exception", e.ExceptionObject as Exception);

        if (Array.IndexOf(args, "--snapshot") >= 0)
            return SnapshotCommand.Run(args);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(callback =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}
