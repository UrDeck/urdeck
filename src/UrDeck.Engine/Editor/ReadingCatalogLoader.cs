// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Editor;

/// <summary>What the editor knows of one provider's readings.</summary>
/// <param name="ProviderId">The provider's id.</param>
/// <param name="Name">The provider's name.</param>
/// <param name="Entries">The readings: a listing, the catalog's, or the cached descriptions of the ones in use.</param>
/// <param name="Connected">False when the provider could not list its readings (it is not set up, or not reachable).</param>
/// <param name="Message">Why it could not; null when it could.</param>
/// <param name="FromCache">Whether <paramref name="Entries"/> are the cached descriptions of the readings in use.</param>
/// <param name="Status">The provider's readiness when it was listed; null when it was not asked.</param>
/// <param name="Hidden">Whether the picker offers nothing of the provider: it is not ready and no page uses one of its readings.</param>
public sealed record ProviderReadings(
    string ProviderId,
    string Name,
    IReadOnlyList<ReadingEntry> Entries,
    bool Connected,
    string? Message,
    bool FromCache,
    ProviderStatus? Status = null,
    bool Hidden = false);

/// <summary>
/// Reads each provider's readings for the editor (reading-picker spec, "Provider Not Connected"): the provider's listing when it
/// can list (kept in the cache for the readings in use), else the cache when the listing fails, and for a provider that does not
/// list its catalog: the fixed readings and the patterns as templates. Never throws for a provider.
/// </summary>
/// <param name="hub">The host's hub, which starts a provider for a listing.</param>
/// <param name="cache">The descriptions of the readings in use.</param>
/// <param name="usedIds">The full ids of the readings the pages use now.</param>
public sealed class ReadingCatalogLoader(ReadingHub hub, ReadingDescriptionCache cache, Func<IReadOnlyCollection<string>> usedIds)
{
    /// <summary>Reads the readings of <paramref name="provider"/>.</summary>
    public async Task<ProviderReadings> LoadAsync(ProviderDescriptor provider, CancellationToken cancellationToken = default)
    {
        string id = provider.Id;
        var status = hub.GetStatus(id);
        if (status is { IsReady: false })
            return await Task.Run(() => NotReady(provider, status), cancellationToken).ConfigureAwait(false);

        if (provider.OffersBrowsing)
        {
            var listing = await hub.BrowseAsync(id, cancellationToken).ConfigureAwait(false);
            if (listing.Succeeded)
            {
                cache.Store(id, listing.Readings!, usedIds());
                return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, listing.Readings!)], true, null, false, status);
            }

            return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, cache.Read(id))], false, listing.Message, true, status);
        }

        var catalog = await Task.Run(() => hub.GetCatalog(id), cancellationToken).ConfigureAwait(false);
        if (catalog == null)
            return new ProviderReadings(id, provider.Name, [], true, null, false, status);
        return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, catalog, d => HasAction(id, d))], true, null, false, status);
    }

    /// <summary>
    /// A provider that is not ready is not started and not listed. When the pages use one of its readings it is still shown, with the
    /// descriptions of the readings in use and a message saying it needs setup; otherwise it is hidden.
    /// </summary>
    private ProviderReadings NotReady(ProviderDescriptor provider, ProviderStatus status)
    {
        string id = provider.Id;
        var inUse = usedIds().Where(u => u.StartsWith(id + ":", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (inUse.Count == 0)
            return new ProviderReadings(id, provider.Name, [], false, null, false, status, Hidden: true);

        var described = cache.Read(id).Where(d => inUse.Contains($"{id}:{d.Path}")).ToList();
        if (described.Count == 0 && !provider.OffersBrowsing)
            described = [.. (hub.GetCatalog(id) ?? []).Where(d => inUse.Contains($"{id}:{d.Path}"))];
        return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, described)], false, $"{provider.Name} needs setup", true, status);
    }

    private bool HasAction(string providerId, ReadingDescriptor descriptor) =>
        descriptor.Action != null || (!descriptor.Path.Contains('{', StringComparison.Ordinal) && hub.ActionsFor($"{providerId}:{descriptor.Path}").Count > 0);
}
