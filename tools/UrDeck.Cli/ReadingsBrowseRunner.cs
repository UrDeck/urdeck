// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Startup;
using UrDeck.Preview;
using UrDeck.Sdk.Data;

namespace UrDeck.Cli;

internal sealed record ReadingsBrowseOptions(string Provider, string? Search, string? Domain, string? Url, string? Config, string Format);

/// <summary>
/// <c>readings browse</c>: the concrete readings a provider can list (urdeck-cli spec, "Browsing Readings"), each with its kind,
/// its current value and its label, for finding an entity id. The provider runs as the deck runs it, with the user's settings and
/// secrets (the environment, and on Windows Credential Manager); a secret is used and never printed.
/// </summary>
internal static class ReadingsBrowseRunner
{
    /// <summary>The longest wait for the values of the listed readings.</summary>
    public static readonly TimeSpan ValueWait = TimeSpan.FromSeconds(5);

    public static int Run(ReadingsBrowseOptions options, TextWriter output, TextWriter error)
    {
        var registry = new ProviderRegistry();
        foreach (var provider in PreviewProviders.Descriptors)
            registry.TryRegister(provider, out _);
        return Run(options, output, error, registry, SecretStores.CreateDefault(), ValueWait);
    }

    /// <summary>The command with the providers, the secret store and the wait for values given: for the tests.</summary>
    internal static int Run(ReadingsBrowseOptions options, TextWriter output, TextWriter error, ProviderRegistry registry, ISecretStore secrets, TimeSpan valueWait)
    {
        var settings = Settings(options);
        using var log = new LogCapture();
        using var hub = new ReadingHub(registry, secrets: secrets);
        hub.ApplySettings(settings);

        var listing = hub.BrowseAsync(options.Provider).GetAwaiter().GetResult();
        if (!listing.Succeeded)
            throw new ToolFailureException(listing.Message!);

        var readings = listing.Readings!
            .Where(d => Matches(d, options.Search, options.Domain))
            .OrderBy(d => d.Path, StringComparer.Ordinal)
            .ToList();
        string[] ids = readings.Select(d => $"{options.Provider}:{d.Path}").ToArray();
        if (ids.Length > 0)
        {
            hub.Subscribe(ids);
            long until = Environment.TickCount64 + (long)valueWait.TotalMilliseconds;
            while (hub.AnyPending(ids) && Environment.TickCount64 < until)
                Thread.Sleep(50);
        }

        var rows = readings.Select((d, i) => Row(hub, ids[i], hub.Describe(ids[i]) ?? d)).ToList();
        if (ids.Length > 0)
            hub.Unsubscribe(ids);

        output.Write(options.Format == "json" ? Json(rows) : Text(rows));
        return ExitCodes.Ok;
    }

    private sealed record BrowseRow(string Id, string Kind, string State, string Value, string? Unit, string Label, string? Action, string? Icon);

    /// <summary>
    /// The providers' settings: the configuration given (else the user's, when it exists), with <c>--url</c> in place of the
    /// browsed provider's address.
    /// </summary>
    private static Dictionary<string, ProviderSettings> Settings(ReadingsBrowseOptions options)
    {
        string? path = options.Config != null ? Path.GetFullPath(options.Config) : UserConfig();
        Dictionary<string, ProviderSettings> settings = new(StringComparer.OrdinalIgnoreCase);
        if (path != null)
        {
            if (!File.Exists(path))
                throw new ToolFailureException($"--config {PathDisplay.Of(path)} does not exist.");
            try
            {
                foreach (var (id, setting) in ConfigStore.Parse(File.ReadAllText(path)).Providers ?? [])
                    settings[id] = setting;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                throw new ToolFailureException($"the configuration {PathDisplay.Of(path)} cannot be read: {ex.Message}");
            }
        }

        if (options.Url != null)
        {
            var setting = settings.GetValueOrDefault(options.Provider) ?? new ProviderSettings();
            setting.ExtensionData ??= [];
            setting.ExtensionData["url"] = JsonSerializer.SerializeToElement(options.Url);
            settings[options.Provider] = setting;
        }

        return settings;
    }

    /// <summary>The configuration of the per-user data folder, when there is one.</summary>
    private static string? UserConfig()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DataFolder.FolderName, "urdeck-config.json");
        return File.Exists(path) ? path : null;
    }

    private static bool Matches(ReadingDescriptor reading, string? search, string? domain)
    {
        if (domain is { Length: > 0 } && !reading.Path.StartsWith(domain.TrimEnd('.') + ".", StringComparison.OrdinalIgnoreCase))
            return false;
        return search is not { Length: > 0 }
            || reading.Path.Contains(search, StringComparison.OrdinalIgnoreCase)
            || reading.Label.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static BrowseRow Row(ReadingHub hub, string id, ReadingDescriptor descriptor)
    {
        var reading = hub.Read(id);
        string state = reading.State.ToString().ToLowerInvariant();
        string value;
        string? unit = null;
        if (reading.State == ReadingState.Unavailable)
        {
            value = $"({reading.Reason})";
        }
        else if (reading.Value == null)
        {
            value = $"({state})";
        }
        else
        {
            // A listing is for finding a reading: one decimal of a measured value, unless the provider says how many.
            int? decimals = descriptor.Decimals ?? (descriptor.Kind is ReadingKind.Number or ReadingKind.Temperature or ReadingKind.Speed or ReadingKind.Depth ? 1 : null);
            var text = ReadingFormatter.Format(reading, descriptor, new ReadingFormatOptions { Decimals = decimals }, hub);
            value = text.Value;
            unit = string.IsNullOrEmpty(text.Unit) ? null : text.Unit;
        }

        string kind = descriptor.Kind.ToString();
        return new BrowseRow(id, char.ToLowerInvariant(kind[0]) + kind[1..], state, value, unit, descriptor.Label, descriptor.Action, descriptor.Icon);
    }

    private static string Text(List<BrowseRow> rows)
    {
        var text = new StringBuilder();
        if (rows.Count == 0)
        {
            text.Append("No readings match.\n");
            return text.ToString();
        }

        int idWidth = rows.Max(r => r.Id.Length);
        int kindWidth = rows.Max(r => r.Kind.Length);
        var values = rows.Select(r => r.Unit == null ? r.Value : $"{r.Value} {r.Unit}").ToList();
        int valueWidth = values.Max(v => v.Length);
        for (int i = 0; i < rows.Count; i++)
        {
            text.Append((rows[i].Id.PadRight(idWidth) + "  " + rows[i].Kind.PadRight(kindWidth) + "  " + values[i].PadRight(valueWidth) + "  " + rows[i].Label).TrimEnd());
            text.Append('\n');
        }

        return text.ToString();
    }

    private static string Json(List<BrowseRow> rows)
    {
        var list = new JsonArray();
        foreach (var row in rows)
        {
            list.Add(new JsonObject
            {
                ["id"] = row.Id,
                ["kind"] = row.Kind,
                ["state"] = row.State,
                ["value"] = row.Value,
                ["unit"] = row.Unit,
                ["label"] = row.Label,
                ["action"] = row.Action,
                ["icon"] = row.Icon,
            });
        }

        return list.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
}
