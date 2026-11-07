// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json.Nodes;

namespace UrDeck.Engine.Config;

/// <summary>JSON Pointer (RFC 6901) paths into the configuration tree: <c>/pages/0/widgets/3/label</c>.</summary>
public static class JsonPointer
{
    /// <summary>The reference tokens of <paramref name="path"/>, unescaped. The empty path and <c>/</c> alone are the root: no tokens.</summary>
    public static IReadOnlyList<string> Parse(string path)
    {
        if (path.Length == 0 || path == "/")
            return [];
        if (path[0] != '/')
            throw new ArgumentException($"'{path}' is not a JSON Pointer: it must start with '/'.", nameof(path));

        var tokens = new List<string>();
        foreach (string raw in path[1..].Split('/'))
            tokens.Add(raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
        return tokens;
    }

    /// <summary>One key as a pointer token: <c>~</c> becomes <c>~0</c>, <c>/</c> becomes <c>~1</c>.</summary>
    public static string Escape(string key) => key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>The pointer of the keys and indexes in order.</summary>
    public static string Join(IEnumerable<string> tokens)
    {
        var builder = new StringBuilder();
        foreach (string token in tokens)
            builder.Append('/').Append(Escape(token));
        return builder.ToString();
    }
}

/// <summary>One operation of a transaction, kept so that the host's own edits can be replayed on the undo snapshots.</summary>
internal sealed record ConfigOperation(ConfigOperationKind Kind, string Path, JsonNode? Value, int Index, int Index2);

internal enum ConfigOperationKind { Set, Remove, Insert, Move }

/// <summary>
/// The operations of one transaction, addressed by JSON Pointer path. Only valid inside <c>ConfigDocument.Edit</c>'s
/// callback. A path through a missing array index, or an index out of range, throws <see cref="ArgumentException"/>: it is a
/// mistake of the caller, and the transaction is rolled back.
/// </summary>
public sealed class ConfigEditor
{
    private readonly JsonObject _root;
    private readonly List<ConfigOperation> _operations = [];
    private readonly List<string> _paths = [];

    internal ConfigEditor(JsonObject root) => _root = root;

    /// <summary>The paths touched so far, in order.</summary>
    public IReadOnlyList<string> Paths => _paths;

    internal IReadOnlyList<ConfigOperation> Operations => _operations;

    /// <summary>The node at <paramref name="path"/>, or null when there is none.</summary>
    public JsonNode? Get(string path)
    {
        JsonNode? node = _root;
        foreach (string token in JsonPointer.Parse(path))
        {
            node = Child(node, token);
            if (node == null)
                return null;
        }

        return node;
    }

    /// <summary>
    /// The key of the object at <paramref name="parentPath"/> that equals <paramref name="key"/> ignoring case (the file is read
    /// case-insensitively), the exact match first; null when there is none.
    /// </summary>
    public string? FindKey(string parentPath, string key)
    {
        if (Get(parentPath) is not JsonObject parent)
            return null;
        if (parent.ContainsKey(key))
            return key;
        return parent.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sets the value at <paramref name="path"/>, creating the objects on the way. A null value removes the key: the document never holds JSON null.</summary>
    public void Set(string path, JsonNode? value)
    {
        if (value == null)
        {
            Remove(path);
            return;
        }

        var tokens = JsonPointer.Parse(path);
        if (tokens.Count == 0)
            throw new ArgumentException("The root cannot be replaced by a path.", nameof(path));

        var copy = value.DeepClone();
        JsonNode? node = _root;
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            var next = Child(node, tokens[i]);
            if (next == null)
            {
                if (node is not JsonObject missingIn)
                    throw new ArgumentException($"'{path}' goes through a missing array index.", nameof(path));
                next = new JsonObject();
                missingIn[tokens[i]] = next;
            }

            node = next;
        }

        string last = tokens[^1];
        switch (node)
        {
            case JsonObject obj:
                // The format version leads the file, as it always did.
                if (tokens.Count == 1 && last == "version" && !obj.ContainsKey(last))
                    obj.Insert(0, last, copy);
                else
                    obj[last] = copy;
                break;
            case JsonArray array:
                array[IndexOf(array, last, path, allowEnd: false)] = copy;
                break;
            default:
                throw new ArgumentException($"'{path}' goes through a value that is not an object or a list.", nameof(path));
        }

        Record(new ConfigOperation(ConfigOperationKind.Set, path, copy.DeepClone(), 0, 0), path);
    }

    /// <summary>Removes the key (or list entry) at <paramref name="path"/>; nothing happens when it is absent.</summary>
    public void Remove(string path)
    {
        var tokens = JsonPointer.Parse(path);
        if (tokens.Count == 0)
            throw new ArgumentException("The root cannot be removed.", nameof(path));

        JsonNode? parent = _root;
        for (int i = 0; i < tokens.Count - 1 && parent != null; i++)
            parent = Child(parent, tokens[i]);

        string last = tokens[^1];
        bool removed = false;
        switch (parent)
        {
            case JsonObject obj:
                removed = obj.Remove(last);
                break;
            case JsonArray array when int.TryParse(last, out int index) && index >= 0 && index < array.Count:
                array.RemoveAt(index);
                removed = true;
                break;
        }

        if (removed)
            Record(new ConfigOperation(ConfigOperationKind.Remove, path, null, 0, 0), path);
    }

    /// <summary>Inserts <paramref name="value"/> into the list at <paramref name="arrayPath"/> so that it ends up at <paramref name="index"/>.</summary>
    public void Insert(string arrayPath, int index, JsonNode value)
    {
        var array = ArrayAt(arrayPath);
        if (index < 0 || index > array.Count)
            throw new ArgumentException($"Index {index} is outside the list at '{arrayPath}' ({array.Count} entries).", nameof(index));
        var copy = value.DeepClone();
        array.Insert(index, copy);
        Record(new ConfigOperation(ConfigOperationKind.Insert, arrayPath, copy.DeepClone(), index, 0), arrayPath);
    }

    /// <summary>Moves the entry at <paramref name="from"/> of the list at <paramref name="arrayPath"/> so that it ends up at <paramref name="to"/>.</summary>
    public void Move(string arrayPath, int from, int to)
    {
        var array = ArrayAt(arrayPath);
        if (from < 0 || from >= array.Count)
            throw new ArgumentException($"Index {from} is outside the list at '{arrayPath}' ({array.Count} entries).", nameof(from));
        if (to < 0 || to >= array.Count)
            throw new ArgumentException($"Index {to} is outside the list at '{arrayPath}' ({array.Count} entries).", nameof(to));
        if (from == to)
            return;

        var item = array[from];
        array.RemoveAt(from);
        array.Insert(to, item);
        Record(new ConfigOperation(ConfigOperationKind.Move, arrayPath, null, from, to), arrayPath);
    }

    /// <summary>Applies a recorded operation again (the host's edits, replayed on the undo snapshots).</summary>
    internal void Replay(ConfigOperation operation)
    {
        switch (operation.Kind)
        {
            case ConfigOperationKind.Set:
                Set(operation.Path, operation.Value);
                break;
            case ConfigOperationKind.Remove:
                Remove(operation.Path);
                break;
            case ConfigOperationKind.Insert:
                Insert(operation.Path, operation.Index, operation.Value!);
                break;
            case ConfigOperationKind.Move:
                Move(operation.Path, operation.Index, operation.Index2);
                break;
        }
    }

    private void Record(ConfigOperation operation, string path)
    {
        _operations.Add(operation);
        _paths.Add(path);
    }

    private JsonArray ArrayAt(string path) =>
        Get(path) as JsonArray ?? throw new ArgumentException($"There is no list at '{path}'.", nameof(path));

    private static JsonNode? Child(JsonNode? node, string token) => node switch
    {
        JsonObject obj => obj.TryGetPropertyValue(token, out var value) ? value : null,
        JsonArray array when int.TryParse(token, out int index) && index >= 0 && index < array.Count => array[index],
        _ => null,
    };

    private static int IndexOf(JsonArray array, string token, string path, bool allowEnd)
    {
        if (!int.TryParse(token, out int index) || index < 0 || index > array.Count || (index == array.Count && !allowEnd))
            throw new ArgumentException($"'{path}' addresses an index that is not in the list ({array.Count} entries).", nameof(path));
        return index;
    }
}
