// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Config;

/// <summary>The edges of the configuration file, as a seam: the document's logic is tested without a disk or a watcher.</summary>
internal interface IConfigFileSystem : IDisposable
{
    /// <summary>The file's bytes (read shared, retried while an editor holds it); null when there is no file.</summary>
    byte[]? Read();

    /// <summary>Replaces the file atomically: a reader sees the old content or the new, never part of it.</summary>
    void Write(byte[] bytes);

    /// <summary>Keeps <paramref name="bytes"/> next to the file under <paramref name="name"/> (the migration backup).</summary>
    void WriteBackup(string name, byte[] bytes);

    /// <summary>The path in messages.</summary>
    string Description { get; }

    /// <summary>Raised on any thread, debounced, after the file may have changed.</summary>
    event Action? Changed;
}

/// <summary>The real thing: the file on disk and a <see cref="FileSystemWatcher"/> with a one second debounce.</summary>
internal sealed class DiskConfigFileSystem : IConfigFileSystem
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private readonly string _path;
    private readonly string _directory;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _debounce;

    public DiskConfigFileSystem(string path, bool watch = true)
    {
        _path = path;
        _directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        _debounce = new Timer(_ => Changed?.Invoke());
        if (!watch || string.IsNullOrEmpty(_directory) || !Directory.Exists(_directory))
            return;

        _watcher = new FileSystemWatcher(_directory, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    public event Action? Changed;

    public string Description => _path;

    public byte[]? Read()
    {
        // Editors often hold the file briefly while saving; retry a few times.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(_path))
                    return null;
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100);
            }
        }
    }

    public void Write(byte[] bytes)
    {
        Directory.CreateDirectory(_directory);
        string temp = _path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        try
        {
            File.Move(temp, _path, overwrite: true);
        }
        catch
        {
            try
            { File.Delete(temp); }
            catch (IOException) { }
            throw;
        }
    }

    public void WriteBackup(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(_directory, name), bytes);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => _debounce.Change(Debounce, Timeout.InfiniteTimeSpan);
}
