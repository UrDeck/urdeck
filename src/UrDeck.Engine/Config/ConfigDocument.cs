// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Config;

/// <summary>Who made a transaction: the user through the editor, the host itself, the file changing on disk, or the first load.</summary>
public enum EditOrigin { User, Host, Outside, Load }

/// <summary>A transaction that was applied: its origin, label, the paths it touched (<c>/</c> for undo, redo and a changed file) and the new revision.</summary>
public sealed record ConfigChange(EditOrigin Origin, string Label, IReadOnlyList<string> Paths, int Revision);

/// <summary>Something that went wrong with the file: a change that could not be read (with where) or a write that failed.</summary>
public sealed record ConfigProblem(string Message, int? Line, int? Column);

/// <summary>A configuration file as read: its tree (migrated to the host's version) and what the writer must remember.</summary>
internal sealed record ParsedConfig(JsonObject Root, bool NewerThanHost, int? MigratedFrom, byte[]? Original);

/// <summary>
/// The configuration as one document in the running host: the JSON tree in memory, the only writer of
/// <c>urdeck-config.json</c>. Every change is a transaction of edits by path; everyone in the process is told at once, and the
/// file is written behind the change. Keys the host does not understand stay in the tree and in the file.
/// </summary>
/// <remarks>The document belongs to the thread that created it (see the <c>post</c> delegate of the constructor).</remarks>
public sealed class ConfigDocument : IDisposable
{
    /// <summary>How long after the last change the file is written.</summary>
    public static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    private const int MaxRetries = 3;

    private readonly IConfigFileSystem? _files;
    private readonly Func<string, byte[], ParsedConfig>? _reader;
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private readonly int? _ownerThread;
    private readonly int _currentVersion;
    private readonly bool _readOnlyRequested;
    private JsonObject _root;
    private UrDeckConfig? _typed;
    private bool _warnTyped = true;
    private bool _newerThanHost;
    private byte[]? _fileHash;
    private ITimer? _writeTimer;
    private bool _writePending;
    private int _failures;
    private (int From, byte[] Original)? _backup;
    private bool _disposed;

    /// <param name="root">The tree. The document owns it from here on.</param>
    /// <param name="readOnly">Opened to be looked at (<c>--snapshot</c>, preview): edits are refused, nothing is written.</param>
    /// <param name="files">The file; null for a document that never touches a disk.</param>
    /// <param name="reader">Reads the file's text into a tree (migrating it); throws <see cref="JsonException"/> on text that is not a configuration.</param>
    /// <param name="time">The clock of the write delay and of merging typing into one undo step.</param>
    /// <param name="currentVersion">The format version the host writes.</param>
    /// <param name="newerThanHost">The tree came from a file with a newer version: the document is read-only.</param>
    /// <param name="post">Runs an action on the owner thread; null runs it where it is called (tests, and a process without a synchronization context).</param>
    internal ConfigDocument(
        JsonObject root,
        bool readOnly,
        IConfigFileSystem? files = null,
        Func<string, byte[], ParsedConfig>? reader = null,
        TimeProvider? time = null,
        Action<Action>? post = null,
        int currentVersion = ConfigStore.CurrentVersion,
        bool newerThanHost = false)
    {
        _root = root;
        _readOnlyRequested = readOnly;
        _files = files;
        _reader = reader;
        _time = time ?? TimeProvider.System;
        _post = post ?? (action => action());
        _ownerThread = post != null ? Environment.CurrentManagedThreadId : null;
        _currentVersion = currentVersion;
        _newerThanHost = newerThanHost;
        History = new ConfigHistory(this, _time);
        if (_files != null)
            _files.Changed += OnFileChanged;
    }

    /// <summary>The tree. Read it; change it only through <see cref="Edit"/>.</summary>
    public JsonObject Root => _root;

    /// <summary>Increases by one with every applied transaction, undo, redo and change from outside.</summary>
    public int Revision { get; private set; }

    /// <summary>Whether edits are refused: the file is from a newer version, or the document was opened read-only.</summary>
    public bool IsReadOnly => _readOnlyRequested || _newerThanHost;

    /// <summary>Whether the file's <c>version</c> is newer than this build's: it is read as well as possible and never written.</summary>
    public bool IsNewerThanHost => _newerThanHost;

    public ConfigHistory History { get; }

    /// <summary>The configuration the deck uses: the tree bound to <see cref="UrDeckConfig"/>, current after every change.</summary>
    public UrDeckConfig Typed => _typed ??= BindTyped();

    /// <summary>Raised on the owner thread after <see cref="Root"/> and <see cref="Typed"/> are current.</summary>
    public event Action<ConfigChange>? Changed;

    /// <summary>Raised for a change from outside that could not be read and for a write that failed.</summary>
    public event Action<ConfigProblem>? Problem;

    /// <summary>The SHA-256 of what the file is believed to hold: what was last written or read.</summary>
    internal byte[]? FileHash => _fileHash;

    /// <summary>
    /// Applies a transaction: the callback's operations, whole or not at all. Returns false and does nothing when the document
    /// is read-only. A callback that throws changes nothing and the exception passes on.
    /// </summary>
    /// <param name="label">Names the step for the editor's Undo.</param>
    /// <param name="edit">The operations.</param>
    /// <param name="origin"><see cref="EditOrigin.User"/> steps are undoable; <see cref="EditOrigin.Host"/> ones are not and are carried into the history.</param>
    /// <param name="mergeKey">User steps with the same key within a second are one step (typing into one field).</param>
    public bool Edit(string label, Action<ConfigEditor> edit, EditOrigin origin = EditOrigin.User, string? mergeKey = null)
    {
        AssertOwner();
        if (IsReadOnly)
            return false;

        var before = (JsonObject)_root.DeepClone();
        var editor = new ConfigEditor(_root);
        try
        {
            edit(editor);
            if (editor.Operations.Count == 0 || JsonNode.DeepEquals(before, _root))
                return true;
            _typed = BindTyped(warn: origin is EditOrigin.Outside or EditOrigin.Load);
        }
        catch
        {
            _root = before;
            throw;
        }

        Revision++;
        if (origin == EditOrigin.User)
            History.Record(label, before, (JsonObject)_root.DeepClone(), mergeKey);
        else if (origin == EditOrigin.Host)
            History.ReplayHost(editor.Operations);

        ScheduleWrite();
        Changed?.Invoke(new ConfigChange(origin, label, editor.Paths.Distinct().ToList(), Revision));
        return true;
    }

    /// <summary>Writes now if a write is pending: on exit, and when the editor closes.</summary>
    public void Flush()
    {
        AssertOwner();
        if (_writePending)
            WriteNow();
    }

    public void Dispose()
    {
        _disposed = true;
        _writeTimer?.Dispose();
        if (_files != null)
        {
            _files.Changed -= OnFileChanged;
            _files.Dispose();
        }
    }

    /// <summary>Marks the document as changed so that the next flush writes it (tests of what survives a write).</summary>
    internal void RequestWrite() => ScheduleWrite();

    /// <summary>The hash of what the file holds at the start (or after a first-run write), so that an event about it is ignored.</summary>
    internal void NoteFileContent(byte[] bytes) => _fileHash = SHA256.HashData(bytes);

    /// <summary>The original of a migrated file, kept as <c>urdeck-config.v&lt;from&gt;.json</c> before the first write.</summary>
    internal void NoteMigration(int from, byte[] original) => _backup = (from, original);

    /// <summary>Replaces the tree by a snapshot (undo and redo).</summary>
    internal void Restore(JsonObject snapshot, EditOrigin origin, string label)
    {
        _root = (JsonObject)snapshot.DeepClone();
        _typed = null;
        _warnTyped = false;
        Revision++;
        ScheduleWrite();
        Changed?.Invoke(new ConfigChange(origin, label, ["/"], Revision));
    }

    private UrDeckConfig BindTyped(bool? warn = null)
    {
        bool shouldWarn = warn ?? _warnTyped;
        _warnTyped = false;
        return ConfigStore.Parse(_root.ToJsonString(), shouldWarn);
    }

    private void ScheduleWrite()
    {
        if (_files == null || IsReadOnly || _disposed)
            return;
        _writePending = true;
        _failures = 0;
        _writeTimer ??= _time.CreateTimer(_ => _post(OnWriteTimer), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _writeTimer.Change(WriteDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnWriteTimer()
    {
        if (!_disposed)
            WriteNow();
    }

    private void WriteNow()
    {
        if (!_writePending || _files == null || IsReadOnly)
            return;
        _writeTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        EnsureVersion();
        byte[] bytes = Encoding.UTF8.GetBytes(_root.ToJsonString(UrDeckJson.Options) + "\n");
        byte[] hash = SHA256.HashData(bytes);
        if (_fileHash != null && hash.AsSpan().SequenceEqual(_fileHash))
        {
            _writePending = false;
            return;
        }

        try
        {
            if (_backup is { } backup)
            {
                _files.WriteBackup($"urdeck-config.v{backup.From}.json", backup.Original);
                UrDeckLog.Info($"Configuration version {backup.From} kept as urdeck-config.v{backup.From}.json");
                _backup = null;
            }

            _files.Write(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _failures++;
            if (_failures <= MaxRetries)
            {
                _writeTimer?.Change(RetryDelay, Timeout.InfiniteTimeSpan);
                return;
            }

            _failures = 0;
            string message = $"Could not save {_files.Description}: {ex.Message}";
            UrDeckLog.Warn(message);
            Problem?.Invoke(new ConfigProblem(message, null, null));
            return; // still pending: the next change tries again
        }

        _fileHash = hash;
        _writePending = false;
        _failures = 0;
    }

    // A loaded file without the current version gets it as the first key before its first write; the tree changes silently
    // (no announcement, no write of its own) and the history carries it like any host edit.
    private void EnsureVersion()
    {
        if (_root["version"] is JsonValue value && value.TryGetValue<int>(out int version) && version == _currentVersion)
            return;
        var editor = new ConfigEditor(_root);
        editor.Set("/version", _currentVersion);
        History.ReplayHost(editor.Operations);
        _typed = null;
    }

    private void OnFileChanged()
    {
        if (_disposed || _files == null || _reader == null)
            return;
        byte[]? bytes;
        try
        {
            bytes = _files.Read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Config reload failed, keeping previous config: {ex.Message}");
            return;
        }

        if (bytes != null)
            _post(() => HandleFileContent(bytes));
    }

    /// <summary>A file event's content: nothing when it is what the document wrote or read; otherwise an outside change.</summary>
    internal void HandleFileContent(byte[] bytes)
    {
        if (_disposed || _reader == null)
            return;
        byte[] hash = SHA256.HashData(bytes);
        if (_fileHash != null && hash.AsSpan().SequenceEqual(_fileHash))
            return;

        // The user's last act was editing the file: whatever the document was about to write loses.
        _writePending = false;
        _writeTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _fileHash = hash;

        ParsedConfig parsed;
        try
        {
            string text = new StreamReader(new MemoryStream(bytes), Encoding.UTF8).ReadToEnd();
            parsed = _reader(text, bytes);
            ConfigStore.Parse(parsed.Root.ToJsonString()); // binds, so a value of the wrong type is a problem here, not later
        }
        catch (JsonException ex)
        {
            UrDeckLog.Warn($"Config reload failed, keeping previous config: {ex.Message}");
            int? line = ex.LineNumber is { } l ? (int)l + 1 : null;
            int? column = ex.BytePositionInLine is { } c ? (int)c + 1 : null;
            Problem?.Invoke(new ConfigProblem(ex.Message, line, column));
            return;
        }

        if (parsed.MigratedFrom is { } from && parsed.Original is { } original)
            _backup = (from, original);

        bool wasNewer = _newerThanHost;
        _newerThanHost = parsed.NewerThanHost;
        if (_newerThanHost || wasNewer)
            History.Clear();

        if (JsonNode.DeepEquals(parsed.Root, _root))
            return;

        var before = _root;
        _root = parsed.Root;
        _typed = null;
        _warnTyped = true;
        _ = Typed;
        Revision++;
        if (!IsReadOnly)
            History.Record("Changed outside the editor", before, (JsonObject)_root.DeepClone(), null);
        UrDeckLog.Info($"Config reloaded from {_files!.Description}");
        Changed?.Invoke(new ConfigChange(EditOrigin.Outside, "Changed outside the editor", ["/"], Revision));
    }

    [Conditional("DEBUG")]
    private void AssertOwner() =>
        Debug.Assert(_ownerThread is null || _ownerThread == Environment.CurrentManagedThreadId, "ConfigDocument belongs to the thread that created it.");
}
