// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrDeck.Sdk.Data;

/// <summary>
/// The user's settings for one data provider: the object <c>providers.&lt;id&gt;</c> of <c>urdeck-config.json</c>. A provider
/// that needs more (an address, a token) derives from <see cref="DataProvider{TSettings}"/> with a subclass of this and marks
/// each property with <see cref="SettingAttribute"/>; the engine binds the file to it and hands it over at
/// <see cref="IDataProvider.Start"/>. Keys the engine does not know are kept in the file for the provider.
/// </summary>
public class ProviderSettings
{
    /// <summary>The smallest action time limit; a smaller <see cref="ActionTimeoutMs"/> is raised to it.</summary>
    public const int MinActionTimeoutMs = 1000;

    /// <summary>Sampling interval in milliseconds; raised to the provider's minimum.</summary>
    [JsonPropertyName("intervalMs")]
    public int? IntervalMs { get; set; }

    /// <summary>How long an action may run before it fails, in milliseconds. Default 10,000; at least 1,000.</summary>
    [JsonPropertyName("actionTimeoutMs")]
    public int? ActionTimeoutMs { get; set; }

    /// <summary>An action slower than this is logged as a warning, in milliseconds. Default 2,000.</summary>
    [JsonPropertyName("actionWarnMs")]
    public int? ActionWarnMs { get; set; }

    /// <summary>The keys of the provider's object that the declared properties do not take, kept for the file.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
