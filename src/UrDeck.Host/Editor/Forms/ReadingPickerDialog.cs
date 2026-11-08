// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Settings;

namespace UrDeck.Host.Editor.Forms;

/// <summary>
/// The reading picker (reading-picker D3): a dialog over the <see cref="ReadingList"/>, restricted to what the setting accepts,
/// with "Type an id by hand" in its footer (the typed dialog, which a template opens with its pattern filled in). Enter or a
/// double click chooses; the current reading is selected when it opens; nothing stays subscribed after it closes.
/// </summary>
internal sealed class ReadingPickerDialog(
    Func<XamlRoot?> root,
    IEditorReadings readings,
    ReadingCatalog catalog,
    DispatcherQueue queue,
    ReadingChooserDialog typed,
    Action<string>? setUp) : IReadingChooser
{
    public async Task<string?> ChooseAsync(ReadingSpec? filter, string? current)
    {
        if (root() is not { } xamlRoot)
            return null;
        var restrict = filter?.Filter ?? ReadingFilter.None;
        ContentDialog dialog = null!;
        ReadingEntry? picked = null;
        string? setUpProvider = null;
        var list = new ReadingList(
            catalog,
            readings,
            new ReadingListOptions(restrict, filter?.Provider, string.IsNullOrEmpty(current) ? null : current, Chooses: true),
            queue,
            provider =>
            {
                setUpProvider = provider;
                dialog.Hide();
            },
            entry =>
            {
                picked = entry;
                dialog.Hide();
            });

        var content = new Grid { Width = 700, Height = 520 };
        content.Children.Add(list.Root);
        dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = restrict == ReadingFilter.HasAction ? "Choose what it controls" : "Choose a reading",
            Content = content,
            PrimaryButtonText = "Choose",
            SecondaryButtonText = "Type an id by hand",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = list.Selected != null,
        };
        // The dialog's own width limit is narrower than a list of ids needs.
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;
        list.SelectionChanged += () =>
        {
            var selected = list.Selected;
            dialog.IsPrimaryButtonEnabled = selected != null;
            dialog.PrimaryButtonText = selected?.IsTemplate == true ? "Fill in" : "Choose";
        };
        dialog.Opened += (_, _) => list.FocusSearch();

        ContentDialogResult result;
        ReadingEntry? selection;
        try
        {
            result = await dialog.ShowAsync();
            selection = list.Selected;
        }
        finally
        {
            list.Dispose();
        }

        if (setUpProvider != null)
        {
            setUp?.Invoke(setUpProvider);
            return null;
        }

        picked ??= result == ContentDialogResult.Primary ? selection : null;
        if (picked != null)
            return picked.IsTemplate ? await typed.ChooseTypedAsync(filter, picked.Id, selectPlaceholder: true) : picked.Id;
        return result == ContentDialogResult.Secondary ? await typed.ChooseTypedAsync(filter, current ?? "", selectPlaceholder: false) : null;
    }
}
