// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Text.Json.Nodes;

namespace UrDeck.Engine.Config;

/// <summary>
/// The undo history of a <see cref="ConfigDocument"/>: snapshots of the tree before and after each user transaction and each
/// change from outside. Snapshots are trivially correct at the size of a configuration; the host's own edits are replayed on
/// every snapshot so that undoing never brings back an old remembered display or a secret that was moved out of the file.
/// </summary>
public sealed class ConfigHistory
{
    /// <summary>How many steps are kept; the oldest is dropped.</summary>
    public const int Limit = 100;

    /// <summary>Consecutive changes with the same merge key within this time are one step.</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1);

    private readonly ConfigDocument _document;
    private readonly TimeProvider _time;
    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];

    internal ConfigHistory(ConfigDocument document, TimeProvider time)
    {
        _document = document;
        _time = time;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>The label of the step an undo would revert, or null.</summary>
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;

    /// <summary>The label of the step a redo would apply again, or null.</summary>
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Raised when what can be undone or redone changed, for the editor's buttons.</summary>
    public event Action? StateChanged;

    /// <summary>Reverts the last step as a transaction of the user. False when there is none, or the document is read-only.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0 || _document.IsReadOnly)
            return false;
        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        entry.MergeKey = null;
        _redo.Add(entry);
        _document.Restore(entry.Before, EditOrigin.User, "Undo " + entry.Label);
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Applies the last undone step again.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0 || _document.IsReadOnly)
            return false;
        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        entry.MergeKey = null;
        _undo.Add(entry);
        _document.Restore(entry.After, EditOrigin.User, "Redo " + entry.Label);
        StateChanged?.Invoke();
        return true;
    }

    internal void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
            return;
        _undo.Clear();
        _redo.Clear();
        StateChanged?.Invoke();
    }

    internal void Record(string label, JsonObject before, JsonObject after, string? mergeKey)
    {
        var now = _time.GetUtcNow();
        if (mergeKey != null && _undo.Count > 0 && _undo[^1] is { } top
            && top.MergeKey == mergeKey && now - top.Time <= MergeWindow)
        {
            top.After = after;
            top.Time = now;
            _redo.Clear();
            StateChanged?.Invoke();
            return;
        }

        _redo.Clear();
        _undo.Add(new Entry(label, before, after, mergeKey, now));
        if (_undo.Count > Limit)
            _undo.RemoveAt(0);
        StateChanged?.Invoke();
    }

    /// <summary>Carries the host's operations into every snapshot of both stacks.</summary>
    internal void ReplayHost(IReadOnlyList<ConfigOperation> operations)
    {
        Debug.Assert(operations.All(o => o.Kind is ConfigOperationKind.Set or ConfigOperationKind.Remove),
            "Host transactions only set or remove: replay is exact for those.");
        foreach (var entry in _undo.Concat(_redo))
        {
            Replay(entry.Before, operations);
            Replay(entry.After, operations);
        }
    }

    private static void Replay(JsonObject snapshot, IReadOnlyList<ConfigOperation> operations)
    {
        var editor = new ConfigEditor(snapshot);
        foreach (var operation in operations)
        {
            try
            {
                editor.Replay(operation);
            }
            catch (ArgumentException)
            {
                // The path does not exist in this older shape; there is nothing to carry.
            }
        }
    }

    private sealed class Entry(string label, JsonObject before, JsonObject after, string? mergeKey, DateTimeOffset time)
    {
        public string Label { get; } = label;

        public JsonObject Before { get; } = before;

        public JsonObject After { get; set; } = after;

        public string? MergeKey { get; set; } = mergeKey;

        public DateTimeOffset Time { get; set; } = time;
    }
}
