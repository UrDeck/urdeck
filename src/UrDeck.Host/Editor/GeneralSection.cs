// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Startup;

namespace UrDeck.Host.Editor;

/// <summary>
/// The General section: the version, "Check for updates automatically" (the configuration's <c>updates</c>, a document edit),
/// "Start with Windows" (the Run key, as the tray entry; not in the file), and buttons that show the configuration file in File
/// Explorer and open the log.
/// </summary>
internal sealed class GeneralSection
{
    private readonly ISettingsHost _host;
    private readonly PageEditing _editing;
    private readonly ToggleSwitch _updates = new() { Header = "Check for updates automatically", OnContent = "On", OffContent = "Off" };
    private readonly ToggleSwitch _autostart = new() { Header = "Start with Windows", OnContent = "On", OffContent = "Off" };
    private bool _refreshing;

    public GeneralSection(ISettingsHost host, PageEditing editing)
    {
        _host = host;
        _editing = editing;

        var root = new StackPanel { Spacing = 16, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(Sections.Header("General"));
        root.Children.Add(new TextBlock { Text = $"UrDeck {AppVersion.WithoutMetadata(Program.Version)}", FontSize = 16 });

        root.Children.Add(_updates);
        root.Children.Add(Sections.Caption("An installed copy looks for a newer release now and then and asks before it installs one."));
        _updates.Toggled += (_, _) =>
        {
            if (!_refreshing)
                _editing.SetUpdates(_updates.IsOn);
        };

        root.Children.Add(_autostart);
        _autostart.Toggled += (_, _) =>
        {
            if (_refreshing)
                return;
            _host.SetStartWithWindows(_autostart.IsOn);
            Refresh();
        };

        var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var showFile = new Button { Content = "Show the configuration file" };
        showFile.Click += (_, _) => Start("explorer.exe", $"/select,\"{_host.ConfigPath}\"");
        var openLog = new Button { Content = "Open the log" };
        openLog.Click += (_, _) => Start(_host.LogPath, null);
        files.Children.Add(showFile);
        files.Children.Add(openLog);
        root.Children.Add(files);

        Root = new ScrollViewer { Content = root, Padding = new Thickness(32, 24, 32, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    public UIElement Root { get; }

    /// <summary>Shows the values now (after a change of the document, or when the section is shown).</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            _updates.IsOn = UpdatesSetting.IsEnabled(_host.Document.Typed.Updates);
            _updates.IsEnabled = !_host.Document.IsReadOnly;
            _autostart.IsOn = _host.StartsWithWindows;
        }
        finally
        {
            _refreshing = false;
        }
    }

    internal static void Start(string file, string? arguments)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true };
            if (arguments != null)
                info.Arguments = arguments;
            System.Diagnostics.Process.Start(info)?.Dispose();
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Could not open {file}: {ex.Message}");
        }
    }
}
