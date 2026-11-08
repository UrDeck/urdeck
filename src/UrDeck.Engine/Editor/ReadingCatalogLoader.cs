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
public sealed record ProviderReadings(string ProviderId, string Name, IReadOnlyList<ReadingEntry> Entries, bool Connected, string? Message, bool FromCache);

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
        if (provider.OffersBrowsing)
        {
            var listing = await hub.BrowseAsync(id, cancellationToken).ConfigureAwait(false);
            if (listing.Succeeded)
            {
                cache.Store(id, listing.Readings!, usedIds());
                return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, listing.Readings!)], true, null, false);
            }

            return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, cache.Read(id))], false, listing.Message, true);
        }

        var catalog = await Task.Run(() => hub.GetCatalog(id), cancellationToken).ConfigureAwait(false);
        if (catalog == null)
            return new ProviderReadings(id, provider.Name, [], true, null, false);
        return new ProviderReadings(id, provider.Name, [.. ReadingIndex.EntriesOf(id, catalog, d => HasAction(id, d))], true, null, false);
    }

    private bool HasAction(string providerId, ReadingDescriptor descriptor) =>
        descriptor.Action != null || (!descriptor.Path.Contains('{', StringComparison.Ordinal) && hub.ActionsFor($"{providerId}:{descriptor.Path}").Count > 0);
}
