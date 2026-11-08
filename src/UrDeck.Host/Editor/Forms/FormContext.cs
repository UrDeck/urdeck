// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Data;

namespace UrDeck.Host.Editor.Forms;

/// <summary>What a reading setting's form needs to know about readings (live or sample, as the preview shows them).</summary>
internal interface IEditorReadings
{
    /// <summary>The catalog entry of a reading; null when its provider has not described it.</summary>
    ReadingDescriptor? Describe(string id);

    /// <summary>The value as a card would write it ("37 %"); a dash when there is none.</summary>
    string ValueText(string id);

    /// <summary>Counts a user of the reading so it has a value; <paramref name="changed"/> runs on the UI thread when it changes. Dispose to stop.</summary>
    IDisposable Watch(string id, Action changed);

    /// <summary>
    /// Counts a user of each reading so that rows of a list have values; <paramref name="changed"/> runs on the UI thread with the
    /// ids that changed. Dispose to release them. Honours the preview's Live/Sample choice.
    /// </summary>
    IDisposable WatchMany(IReadOnlyCollection<string> ids, Action<IReadOnlyCollection<string>> changed);

    /// <summary>The value of a listed reading as a row shows it, formatted by the listing's own description; a dash when there is none.</summary>
    string RowValue(string id, ReadingDescriptor described);

    /// <summary>Whether the provider cannot start because a required setting is missing.</summary>
    bool ProviderNeedsSetup(string providerId);

    /// <summary>The actions the reading offers (natural action first).</summary>
    IReadOnlyList<ActionDescriptor> ActionsFor(string id);

    /// <summary>A provider's suggestions of a kind for the text typed.</summary>
    Task<SuggestionResult> SuggestAsync(string providerId, string kind, string text, CancellationToken cancellationToken);

    /// <summary>Whether a provider with this id is loaded.</summary>
    bool ProviderKnown(string providerId);

    /// <summary>Whether the user's region shows temperatures in Fahrenheit.</summary>
    bool RegionUsesFahrenheit { get; }
}

/// <summary>One glyph of the current theme's icon set, drawn for the picker.</summary>
/// <param name="Key">The icon key (<c>home/light</c>).</param>
/// <param name="Image">A small picture of it.</param>
internal sealed record IconChoice(string Key, ImageSource Image);

/// <summary>The glyphs of the current theme's icon set (editor-forms D8).</summary>
internal interface IIconCatalog
{
    /// <summary>The set's glyphs grouped as the keys are (<c>home/</c>, <c>weather/</c>, <c>app/</c>), drawn on demand.</summary>
    IReadOnlyList<IconChoice> Glyphs();
}

/// <summary>Asks the user for a reading. The editor's reading picker replaces the minimal dialog.</summary>
internal interface IReadingChooser
{
    /// <summary>The chosen reading's id; null when cancelled.</summary>
    Task<string?> ChooseAsync(ReadingSpec? filter, string? current);
}

/// <summary>Everything a form needs beyond the declaration and the object it edits.</summary>
/// <param name="Document">The configuration; the live form writes to it.</param>
/// <param name="SizeKey">The widget's size (<c>4x2</c>); null for a provider.</param>
/// <param name="Readings">Readings, actions and suggestions.</param>
/// <param name="Icons">The theme's glyphs.</param>
/// <param name="Owner">The window that owns the pickers.</param>
/// <param name="Chooser">The reading chooser.</param>
/// <param name="SetUpProvider">Opens the Providers section on a provider; null where there is none.</param>
internal sealed record FormContext(ConfigDocument Document, string? SizeKey, IEditorReadings Readings, IIconCatalog Icons, Window Owner, IReadingChooser Chooser, Action<string>? SetUpProvider = null);

/// <summary>The object a form edits: the configuration at a path, or a detached object (a provider's staged values).</summary>
internal abstract class FormTarget
{
    /// <summary>Whether nothing may be changed.</summary>
    public abstract bool ReadOnly { get; }

    /// <summary>The value of a key (case-insensitive, as the file is read); null when absent.</summary>
    public abstract JsonNode? Get(string key);

    /// <summary>
    /// Applies the changes as one step (a null value removes the key). Steps with the same <paramref name="mergeKey"/> in quick
    /// succession are one undo step.
    /// </summary>
    public abstract void Apply(string label, IReadOnlyList<KeyValuePair<string, JsonNode?>> changes, string? mergeKey);

    /// <summary>Applies one key. A merging key makes typing into one field one undo step.</summary>
    public void Set(string label, string key, JsonNode? value, bool merge = false) =>
        Apply(label, [new(key, value)], merge ? MergeKeyOf(key) : null);

    /// <summary>The key that makes the steps on <paramref name="key"/> one.</summary>
    protected abstract string MergeKeyOf(string key);
}

/// <summary>The object at a path of the configuration document: every change is a User transaction.</summary>
internal sealed class LiveTarget(ConfigDocument document, string path) : FormTarget
{
    public string Path => path;

    public override bool ReadOnly => document.IsReadOnly;

    public override JsonNode? Get(string key)
    {
        JsonNode? node = document.Root;
        foreach (string token in JsonPointer.Parse(path))
        {
            node = node switch
            {
                JsonObject obj => obj.FirstOrDefault(p => string.Equals(p.Key, token, StringComparison.Ordinal)).Value,
                JsonArray array when int.TryParse(token, out int index) && index >= 0 && index < array.Count => array[index],
                _ => null,
            };
            if (node == null)
                return null;
        }

        if (node is not JsonObject target)
            return null;
        if (target.TryGetPropertyValue(key, out var exact))
            return exact;
        return target.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    public override void Apply(string label, IReadOnlyList<KeyValuePair<string, JsonNode?>> changes, string? mergeKey) =>
        document.Edit(label, editor =>
        {
            foreach (var (key, value) in changes)
            {
                string actual = editor.FindKey(path, key) ?? key;
                string at = path + "/" + JsonPointer.Escape(actual);
                if (value == null)
                    editor.Remove(at);
                else
                    editor.Set(at, value);
            }
        }, mergeKey: mergeKey);

    protected override string MergeKeyOf(string key) => $"{path}#{key}";
}

/// <summary>A detached object (a provider's values before Save); <see cref="Changed"/> follows every edit.</summary>
internal sealed class StagedTarget(JsonObject staging) : FormTarget
{
    public event Action? Changed;

    public override bool ReadOnly => false;

    public override JsonNode? Get(string key) =>
        staging.TryGetPropertyValue(key, out var exact) ? exact : staging.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    public override void Apply(string label, IReadOnlyList<KeyValuePair<string, JsonNode?>> changes, string? mergeKey)
    {
        foreach (var (key, value) in changes)
        {
            string actual = staging.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) ?? key;
            if (value == null)
                staging.Remove(actual);
            else
                staging[actual] = value.DeepClone();
        }

        Changed?.Invoke();
    }

    protected override string MergeKeyOf(string key) => key;
}
