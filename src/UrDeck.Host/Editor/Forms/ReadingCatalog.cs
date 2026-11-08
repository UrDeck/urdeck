// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Editor;

namespace UrDeck.Host.Editor.Forms;

/// <summary>
/// The readings every provider offers, read once for an editor window (reading-picker D1): the first time the picker or the
/// Readings section needs them each provider is listed off the UI thread, and the results are kept in a <see cref="ReadingIndex"/>
/// for the window's life. "Refresh" lists again. Costs nothing until someone asks, and nothing when closed.
/// </summary>
internal sealed class ReadingCatalog : IDisposable
{
    private readonly ISettingsHost _host;
    private readonly DispatcherQueue _queue;
    private readonly Dictionary<string, ProviderReadings> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancel;
    private bool _started;
    private bool _disposed;

    public ReadingCatalog(ISettingsHost host, DispatcherQueue queue)
    {
        _host = host;
        _queue = queue;
    }

    /// <summary>The readings of the providers loaded so far.</summary>
    public ReadingIndex Index { get; private set; } = new([]);

    /// <summary>The providers that are loaded, in name order.</summary>
    public IReadOnlyList<ProviderReadings> Providers => [.. _loaded.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The providers still being listed.</summary>
    public IReadOnlyCollection<string> Loading => _loading;

    /// <summary>Whether any provider is being listed.</summary>
    public bool IsLoading => _loading.Count > 0;

    /// <summary>The name of a provider; its id when it is unknown.</summary>
    public string NameOf(string providerId) =>
        _host.ProviderDescriptors.FirstOrDefault(p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Name ?? providerId;

    /// <summary>Raised on the UI thread when a provider has been listed.</summary>
    public event Action? Changed;

    /// <summary>Lists the providers the first time it is called.</summary>
    public void Ensure()
    {
        if (_started || _disposed)
            return;
        _started = true;
        Load();
    }

    /// <summary>Lists every provider again.</summary>
    public void Refresh()
    {
        if (_disposed)
            return;
        _started = true;
        Load();
    }

    public void Dispose()
    {
        _disposed = true;
        var cancel = _cancel;
        _cancel = null;
        cancel?.Cancel();
        cancel?.Dispose();
    }

    private void Load()
    {
        _cancel?.Cancel();
        _cancel?.Dispose();
        var cancel = _cancel = new CancellationTokenSource();
        var providers = _host.ProviderDescriptors.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var used = UsedReadings.Of(_host.Document.Typed, type => _host.DescribeWidget(type)?.Settings);
        var loader = new ReadingCatalogLoader(_host.Readings, _host.ReadingCache, () => used);
        _loading.Clear();
        foreach (var provider in providers)
            _loading.Add(provider.Id);
        Changed?.Invoke();

        foreach (var provider in providers)
            _ = LoadOneAsync(loader, provider, cancel);
    }

    private async Task LoadOneAsync(ReadingCatalogLoader loader, Engine.Data.ProviderDescriptor provider, CancellationTokenSource cancel)
    {
        ProviderReadings? result = null;
        try
        {
            result = await Task.Run(() => loader.LoadAsync(provider, cancel.Token), cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Editor: the readings of '{provider.Id}' could not be read: {ex.GetBaseException().Message}");
            result = new ProviderReadings(provider.Id, provider.Name, [], false, ex.GetBaseException().Message, false);
        }

        _queue.TryEnqueue(() =>
        {
            if (_disposed || cancel.IsCancellationRequested || !ReferenceEquals(cancel, _cancel))
                return;
            _loaded[provider.Id] = result;
            _loading.Remove(provider.Id);
            Index = new ReadingIndex(_loaded.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).SelectMany(p => p.Entries));
            Changed?.Invoke();
        });
    }
}
