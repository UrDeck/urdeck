// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Host.Editor.Forms;
using UrDeck.Sdk.Settings;

namespace UrDeck.Host.Editor;

/// <summary>
/// The Readings section (reading-picker D10): the picker's list as a page, for browsing what the PC and the providers offer, with
/// their live values and "Copy id" in a row's menu. Nothing is chosen. The list is made, and the providers are listed, when the
/// section is first shown; its values are subscribed only while it is.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The list is disposed in Close, when the window closes.")]
internal sealed class ReadingsSection(ReadingCatalog catalog, IEditorReadings readings, DispatcherQueue queue, Action<string> setUp)
{
    private readonly Grid _root = new() { RowSpacing = 8, MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left };
    private ReadingList? _list;

    public UIElement Root => _root;

    /// <summary>The section is shown (or not): only a shown section has its values subscribed.</summary>
    public void SetActive(bool active)
    {
        if (active && _list == null)
            Build();
        _list?.SetActive(active);
    }

    public void Close()
    {
        _list?.Dispose();
        _list = null;
        _root.Children.Clear();
    }

    private void Build()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Sections.Header("Readings");
        _root.Children.Add(header);
        var caption = Sections.Caption("Everything the deck can show, with its value now. Right-click a reading to copy its id.");
        Grid.SetRow(caption, 1);
        _root.Children.Add(caption);
        _list = new ReadingList(catalog, readings, new ReadingListOptions(ReadingFilter.None, null, null, Chooses: false), queue, setUp, null);
        Grid.SetRow(_list.Root as FrameworkElement ?? throw new InvalidOperationException(), 2);
        _root.Children.Add(_list.Root);
    }
}
