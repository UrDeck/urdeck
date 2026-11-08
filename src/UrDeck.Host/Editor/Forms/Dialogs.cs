// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Settings;

namespace UrDeck.Host.Editor.Forms;

/// <summary>An application of the Start menu.</summary>
/// <param name="Name">What Start calls it.</param>
/// <param name="AppId">Its AppUserModelID, the part after <c>shell:AppsFolder\</c>.</param>
internal sealed record StartApp(string Name, string AppId)
{
    public string Target => @"shell:AppsFolder\" + AppId;

    public override string ToString() => Name;
}

/// <summary>"App…" of a launch target: the Start menu's applications, searchable (editor-forms D7).</summary>
internal static class AppPicker
{
    /// <summary>The chosen application; null when cancelled.</summary>
    public static async Task<StartApp?> ChooseAsync(XamlRoot? root)
    {
        if (root == null)
            return null;
        var search = new TextBox { PlaceholderText = "Search applications" };
        var list = new ListView { Height = 320, SelectionMode = ListViewSelectionMode.Single };
        var status = new TextBlock { Text = "Reading the Start menu…", Opacity = 0.7 };
        var content = new StackPanel { Spacing = 8, Width = 380 };
        content.Children.Add(search);
        content.Children.Add(status);
        content.Children.Add(list);
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Choose an application",
            Content = content,
            PrimaryButtonText = "Choose",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        List<StartApp> all = [];
        void Filter()
        {
            string text = search.Text.Trim();
            list.ItemsSource = all.Where(a => text.Length == 0 || a.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        search.TextChanged += (_, _) => Filter();
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItem is StartApp;
        list.DoubleTapped += (_, _) =>
        {
            if (list.SelectedItem is StartApp)
                dialog.Hide();
        };

        _ = LoadAsync();
        async Task LoadAsync()
        {
            all = await Task.Run(Read);
            status.Text = all.Count == 0 ? "No applications were found. Use File… to pick the program." : "";
            status.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Filter();
        }

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary || list.SelectedItem is StartApp ? list.SelectedItem as StartApp : null;
    }

    /// <summary>The Start menu's applications by Windows PowerShell's <c>Get-StartApps</c>; empty when it cannot be run.</summary>
    private static List<StartApp> Read()
    {
        try
        {
            var info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"Get-StartApps | ConvertTo-Json -Compress\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(info);
            if (process == null)
                return [];
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(15_000))
            {
                process.Kill();
                return [];
            }

            using var document = JsonDocument.Parse(output);
            var apps = new List<StartApp>();
            var items = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray().ToList() : [document.RootElement];
            foreach (var item in items)
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("Name", out var name) && item.TryGetProperty("AppID", out var id)
                    && name.GetString() is { Length: > 0 } label && id.GetString() is { Length: > 0 } appId)
                    apps.Add(new StartApp(label, appId));
            }

            return [.. apps.DistinctBy(a => a.AppId).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Editor: the Start menu's applications could not be listed: {ex.GetBaseException().Message}");
            return [];
        }
    }
}

/// <summary>
/// The minimal reading chooser (editor-forms D10): type an id, see what it is and its value. The reading picker replaces it.
/// </summary>
internal sealed class ReadingChooserDialog(Func<XamlRoot?> root, IEditorReadings readings) : IReadingChooser
{
    public async Task<string?> ChooseAsync(ReadingSpec? filter, string? current)
    {
        if (root() is not { } xamlRoot)
            return null;
        var id = new TextBox { Text = current ?? "", PlaceholderText = "provider:path, for example system:cpu/load" };
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var content = new StackPanel { Spacing = 8, Width = 380 };
        content.Children.Add(id);
        content.Children.Add(info);
        if (filter is { Provider: { } provider })
            content.Children.Add(Sections.Caption($"Readings of {provider} only."));
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Choose a reading",
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var watcher = new ReadingWatcher(readings, Check);
        void Check()
        {
            string text = id.Text.Trim();
            if (!ReadingId.TryParse(text, out string providerId, out _, out string? problem))
            {
                info.Text = text.Length == 0 ? "" : problem ?? "";
                dialog.IsPrimaryButtonEnabled = text.Length == 0;
                return;
            }

            if (filter?.Provider is { } only && !string.Equals(only, providerId, StringComparison.OrdinalIgnoreCase))
            {
                info.Text = $"This setting takes readings of {only}.";
                dialog.IsPrimaryButtonEnabled = false;
                return;
            }

            if (readings.Describe(text) is { } found)
            {
                info.Text = $"{found.Label} · {readings.ValueText(text)}";
                dialog.IsPrimaryButtonEnabled = true;
            }
            else if (!readings.ProviderKnown(providerId))
            {
                info.Text = "No provider has this reading.";
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                info.Text = $"{providerId} has not listed this reading (yet). The card shows dashes until it exists.";
                dialog.IsPrimaryButtonEnabled = true;
            }
        }

        id.TextChanged += (_, _) =>
        {
            watcher.Set(id.Text.Trim());
            Check();
        };
        watcher.Set(id.Text.Trim());
        Check();
        try
        {
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? id.Text.Trim() : null;
        }
        finally
        {
            watcher.Dispose();
        }
    }
}
