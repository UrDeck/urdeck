// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Settings;

/// <summary>Where a secret field stands: whether a value is kept and where it comes from.</summary>
/// <param name="HasValue">Whether the provider would receive a value now.</param>
/// <param name="StoredHere">Whether the persistent store holds one (it can be removed).</param>
/// <param name="Source">Where the value the provider receives comes from; null when there is none.</param>
/// <param name="SourceName">The environment variable's name or "Credential Manager"; empty when there is none.</param>
/// <param name="EnvironmentName">The variable that would override the stored value.</param>
/// <param name="EnvironmentOverrides">Whether the variable is set while a stored value also exists.</param>
public sealed record SecretStatus(bool HasValue, bool StoredHere, SecretSource? Source, string SourceName, string EnvironmentName, bool EnvironmentOverrides);

/// <summary>One field of a provider's form.</summary>
/// <param name="Setting">The declaration: key, type, label, help, required.</param>
/// <param name="Value">The text of the current value (what the file holds, else the default); empty for a secret.</param>
/// <param name="Secret">The state of a secret field; null for the others.</param>
public sealed record ProviderField(SettingDescriptor Setting, string Value, SecretStatus? Secret);

/// <summary>A provider's settings as the settings window shows them.</summary>
public sealed record ProviderForm(string ProviderId, string Name, IReadOnlyList<ProviderField> Fields, bool OffersConnectionTest, bool CanStoreSecrets);

/// <summary>The outcome of a save or a test: ok, or one sentence saying what is wrong.</summary>
public sealed record EditorResult(bool Ok, string Message)
{
    public static EditorResult Success(string message = "") => new(true, message);

    public static EditorResult Failure(string message) => new(false, message);
}

/// <summary>
/// What the settings window does with providers' declared settings, away from any window: lists the forms, saves the values
/// (non-secret ones to the configuration file, secrets to the secret store), removes a secret and tests a connection with the
/// values of the form. The window talks to the host and the host to this; nothing here knows about the screen.
/// </summary>
public sealed class ProviderSettingsEditor
{
    /// <summary>How long a connection test may take.</summary>
    public static readonly TimeSpan TestLimit = TimeSpan.FromSeconds(10);

    private readonly ConfigStore _config;
    private readonly ProviderRegistry _providers;
    private readonly ISecretStore _secrets;
    private readonly ReadingHub _hub;

    public ProviderSettingsEditor(ConfigStore config, ProviderRegistry providers, ISecretStore secrets, ReadingHub hub)
    {
        _config = config;
        _providers = providers;
        _secrets = secrets;
        _hub = hub;
    }

    /// <summary>The form of each loaded provider that declares settings beyond the base ones, by name.</summary>
    public IReadOnlyList<ProviderForm> GetForms()
    {
        var forms = new List<ProviderForm>();
        foreach (var descriptor in _providers.Descriptors.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var schema = descriptor.Schema;
            if (schema.Settings.Count == 0)
                continue;

            var raw = _config.Config.Providers?.GetValueOrDefault(descriptor.Id);
            var fields = schema.Settings.Select(s => new ProviderField(s, s.IsSecret ? "" : CurrentText(s, raw), s.IsSecret ? StatusOf(descriptor.Id, s) : null)).ToList();
            forms.Add(new ProviderForm(descriptor.Id, descriptor.Name, fields, OffersTest(descriptor), _secrets.CanStore));
        }

        return forms;
    }

    /// <summary>
    /// Saves the form: <paramref name="values"/> holds the text of each non-secret field by key; <paramref name="newSecrets"/> a
    /// secret's new text by key (a secret left out or empty is not touched). Restarts the provider when something it uses changed.
    /// </summary>
    public EditorResult Save(string providerId, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> newSecrets)
    {
        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
            return EditorResult.Failure($"There is no provider '{providerId}'.");
        if (_config.IsNewerThanHost)
            return EditorResult.Failure("urdeck-config.json is from a newer version of UrDeck and is never written by this one.");

        var parsed = new Dictionary<string, JsonElement?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in descriptor.Schema.Settings.Where(s => !s.IsSecret))
        {
            string text = values.GetValueOrDefault(setting.Key) ?? "";
            if (!TryParse(setting, text, out var element, out string? problem))
                return EditorResult.Failure(problem!);
            parsed[setting.Key] = element;
        }

        _config.Config.Providers ??= new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase);
        if (!_config.Config.Providers.TryGetValue(providerId, out var settings))
            _config.Config.Providers[providerId] = settings = new ProviderSettings();
        settings.ExtensionData ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, element) in parsed)
        {
            RemoveKey(settings, key);
            if (element is { } value)
                settings.ExtensionData[key] = value;
        }

        // A secret may never stay in the file, however it got there.
        foreach (var secret in descriptor.Schema.Secrets)
            RemoveKey(settings, secret.Key);

        foreach (var secret in descriptor.Schema.Secrets)
        {
            if (newSecrets.GetValueOrDefault(secret.Key) is not { Length: > 0 } text)
                continue;
            try
            {
                _secrets.Save(providerId, secret.Key, text, ContextValue(secret, parsed));
            }
            catch (SecretStoreException ex)
            {
                return EditorResult.Failure(ex.Message);
            }

            UrDeckLog.Info($"Settings: secret {providerId}/{secret.Key} saved");
        }

        _config.Save();
        _hub.ApplySettings(_config.Config.Providers);
        _hub.SecretsChanged();
        UrDeckLog.Info($"Settings: {descriptor.Name} saved");
        return EditorResult.Success();
    }

    /// <summary>Deletes the stored secret and restarts the provider without it.</summary>
    public EditorResult RemoveSecret(string providerId, string key)
    {
        try
        {
            _secrets.Remove(providerId, key);
        }
        catch (SecretStoreException ex)
        {
            return EditorResult.Failure(ex.Message);
        }

        UrDeckLog.Info($"Settings: secret {providerId}/{key} removed");
        _hub.SecretsChanged();
        return EditorResult.Success();
    }

    /// <summary>
    /// Runs the provider's connection test with the values of the form (secrets left empty use the stored ones), without saving
    /// anything. Cancelled after <see cref="TestLimit"/> or when <paramref name="cancellationToken"/> is.
    /// </summary>
    public async Task<EditorResult> TestAsync(
        string providerId,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> newSecrets,
        CancellationToken cancellationToken)
    {
        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
            return EditorResult.Failure($"There is no provider '{providerId}'.");

        var raw = new ProviderSettings { ExtensionData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase) };
        foreach (var setting in descriptor.Schema.Settings.Where(s => !s.IsSecret))
        {
            if (!TryParse(setting, values.GetValueOrDefault(setting.Key) ?? "", out var element, out string? problem))
                return EditorResult.Failure(problem!);
            if (element is { } value)
                raw.ExtensionData[setting.Key] = value;
        }

        var overlay = new OverlaySecrets(_secrets, providerId, newSecrets);
        var bound = ProviderSettingsBinder.Bind(descriptor, raw, overlay);
        if (bound.Missing.Count > 0)
            return EditorResult.Failure($"Set {string.Join(", ", bound.Missing.Select(m => m.Label))} first.");

        IDataProvider instance;
        try
        {
            instance = descriptor.CreateInstance();
        }
        catch (Exception ex)
        {
            return EditorResult.Failure(ex.GetBaseException().Message);
        }

        try
        {
            if (instance is not IConnectionTest { Offered: true } test)
                return EditorResult.Failure($"{descriptor.Name} has no connection test.");

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(TestLimit);
            try
            {
                string line = await test.ValidateAsync(bound.Settings, limit.Token).WaitAsync(limit.Token);
                return EditorResult.Success(line);
            }
            catch (OperationCanceledException)
            {
                return EditorResult.Failure(cancellationToken.IsCancellationRequested ? "The test was cancelled." : $"The test did not finish within {TestLimit.TotalSeconds:0} seconds.");
            }
            catch (Exception ex)
            {
                return EditorResult.Failure(ex.GetBaseException().Message);
            }
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    private static bool OffersTest(ProviderDescriptor descriptor)
    {
        if (!typeof(IConnectionTest).IsAssignableFrom(descriptor.ProviderType))
            return false;
        try
        {
            var instance = descriptor.CreateInstance();
            try
            {
                return instance is IConnectionTest { Offered: true };
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private SecretStatus StatusOf(string providerId, SettingDescriptor secret)
    {
        string environmentName = SecretStore.EnvironmentName(providerId, secret.Key);
        var found = _secrets.Find(providerId, secret.Key);
        var stored = _secrets.FindStored(providerId, secret.Key);
        return new SecretStatus(
            found != null,
            stored != null,
            found?.Source,
            found?.SourceName ?? "",
            environmentName,
            found?.Source == SecretSource.Environment && stored != null);
    }

    private static string CurrentText(SettingDescriptor setting, ProviderSettings? raw)
    {
        if (raw?.ExtensionData != null)
        {
            foreach (var (key, element) in raw.ExtensionData)
            {
                if (!string.Equals(key, setting.Key, StringComparison.OrdinalIgnoreCase))
                    continue;
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? "",
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => "",
                };
            }
        }

        return setting.Default switch
        {
            null => setting.Type == SettingType.Toggle ? "false" : "",
            bool on => on ? "true" : "false",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            var other => other.ToString() ?? "",
        };
    }

    private static string? ContextValue(SettingDescriptor secret, Dictionary<string, JsonElement?> parsed)
    {
        if (secret.ContextKey != null && parsed.TryGetValue(secret.ContextKey, out var value) && value is { ValueKind: JsonValueKind.String } text)
            return text.GetString();
        return null;
    }

    private static void RemoveKey(ProviderSettings settings, string key)
    {
        if (settings.ExtensionData == null)
            return;
        foreach (string existing in settings.ExtensionData.Keys.Where(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)).ToList())
            settings.ExtensionData.Remove(existing);
    }

    /// <summary>Reads a field's text into the JSON value the file keeps; null removes the key (an empty field).</summary>
    private static bool TryParse(SettingDescriptor setting, string text, out JsonElement? element, out string? problem)
    {
        element = null;
        problem = null;
        text = text.Trim();
        if (setting.Type == SettingType.Toggle)
        {
            bool on = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
            element = JsonSerializer.SerializeToElement(on);
            return true;
        }

        if (text.Length == 0)
            return true;

        switch (setting.Type)
        {
            case SettingType.WholeNumber when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole):
                element = JsonSerializer.SerializeToElement(whole);
                return true;
            case SettingType.Number when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number):
                element = JsonSerializer.SerializeToElement(number);
                return true;
            case SettingType.Text:
                element = JsonSerializer.SerializeToElement(text);
                return true;
            default:
                problem = $"{setting.Label} must be {(setting.Type == SettingType.WholeNumber ? "a whole number" : "a number")}.";
                return false;
        }
    }

    /// <summary>The store with the unsaved secrets of a form laid over it, for a test that must not save.</summary>
    private sealed class OverlaySecrets(ISecretStore inner, string providerId, IReadOnlyDictionary<string, string> overrides) : ISecretStore
    {
        public bool CanStore => inner.CanStore;

        public SecretRecord? Find(string provider, string setting) =>
            provider == providerId && overrides.GetValueOrDefault(setting) is { Length: > 0 } text
                ? new SecretRecord(text, null, SecretSource.Stored, "the form")
                : inner.Find(provider, setting);

        public SecretRecord? FindStored(string provider, string setting) => inner.FindStored(provider, setting);

        public void Save(string provider, string setting, string value, string? context) => throw new InvalidOperationException("A test saves nothing.");

        public bool Remove(string provider, string setting) => throw new InvalidOperationException("A test removes nothing.");
    }
}
