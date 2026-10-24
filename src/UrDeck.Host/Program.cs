// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Startup;

namespace UrDeck.Host;

public static class Program
{
    /// <summary>The instance guard of this process; the application listens on it for <c>--quit</c>.</summary>
    internal static SingleInstance? Instance { get; private set; }

    /// <summary>Where the user's files live, decided once at the start of <see cref="Main"/>.</summary>
    internal static DataFolder Data { get; private set; } = null!;

    // A hand-written entry point (the generated one is disabled in the csproj): the data folder is resolved first, then
    // --snapshot and --quit run with the engine alone and exit before the XAML application is created; the guard comes
    // after them, so both can run while a host does.
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => UrDeckLog.Error("Unhandled exception", e.ExceptionObject as Exception);

        // AppContext.BaseDirectory is the application folder here: the folder of UrDeck.Host.exe.
        Data = DataFolder.Resolve(AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        UrDeckLog.SetDefaultPath(Data.LogPath);

        // --snapshot creates the per-user folder so its log lines are kept (it still writes no configuration); it renders
        // without one when the folder cannot be created. --quit creates nothing: with no folder there is no host to close.
        if (Array.IndexOf(args, "--snapshot") >= 0)
        {
            TryCreateDataFolder(Data);
            UrDeckLog.Info(Data.Describe());
            return SnapshotCommand.Run(args, Data);
        }

        if (Array.IndexOf(args, "--quit") >= 0)
            return SingleInstance.Quit(Data.Root);

        if (!PrepareDataFolder(Data))
            return 1;

        UrDeckLog.Info(Data.Describe());

        Instance = SingleInstance.TryAcquire(Data.Root);
        if (Instance is null)
        {
            UrDeckLog.Info("Another UrDeck is already running from this data folder; exiting");
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

    private static void TryCreateDataFolder(DataFolder data)
    {
        if (data.Mode == DataFolderMode.Portable)
            return;

        try
        {
            Directory.CreateDirectory(data.Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"UrDeck cannot create its data folder {data.Root}: {ex.Message}");
        }
    }

    // The per-user folder is created here and proven writable; a portable copy's folder is the application folder, which
    // is not created or probed (--snapshot of a read-only copy must work). A failure goes to the debug output: the log has no home yet.
    private static bool PrepareDataFolder(DataFolder data)
    {
        if (data.Mode == DataFolderMode.Portable)
            return true;

        try
        {
            Directory.CreateDirectory(data.Root);
            string probe = Path.Combine(data.Root, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"UrDeck cannot use its data folder {data.Root}: {ex.Message}");
            return false;
        }
    }
}
