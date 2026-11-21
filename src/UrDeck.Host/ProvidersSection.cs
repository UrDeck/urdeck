// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;
using UrDeck.Host.Editor;
using UrDeck.Host.Editor.Forms;
using UrDeck.Sdk.Data;

namespace UrDeck.Host;

/// <summary>
/// The Providers section of the settings window, generated from what the providers declare: a text box for text and numbers,
/// a password box for a secret, a switch for on/off, each with its label and help. Nothing is applied until Save; a secret's
/// field never shows a stored value, only where it is kept. It talks to the host through <see cref="ISettingsHost"/> and
/// holds nothing of the engine.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The cancellation source is disposed when the window closes (Close).")]
internal sealed class ProvidersSection
{
    private readonly ISettingsHost _host;
    private readonly Window _owner;
    private readonly List<FormView> _views = [];
    private readonly List<IDisposable> _panels = [];
    private readonly StackPanel _root = new() { Spacing = 12 };
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<string, bool> _expanded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _messages = new(StringComparer.Ordinal);
    private CancellationTokenSource? _closing = new();

    public ProvidersSection(ISettingsHost host, Window owner)
    {
        _host = host;
        _owner = owner;
        _scroll = new ScrollViewer { Content = _root, Padding = new Thickness(32, 24, 32, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Rebuild();
    }

    public UIElement Root => _scroll;

    /// <summary>The window closed: a running test is cancelled and the controls are dropped.</summary>
    public void Close()
    {
        var closing = _closing;
        _closing = null;
        closing?.Cancel();
        closing?.Dispose();
        _host.SignIn.CancelAll();
        DisposeViews();
        _root.Children.Clear();
    }

    private void Rebuild()
    {
        if (_closing == null)
            return;

        DisposeViews();
        _root.Children.Clear();
        _root.Children.Add(new TextBlock { Text = "Providers", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
        _root.Children.Add(new TextBlock
        {
            Text = "The services the deck reads from. A secret is kept in Windows Credential Manager, never in the configuration file.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        });

        var forms = _host.GetProviderForms();
        if (forms.Count == 0)
        {
            _root.Children.Add(new TextBlock { Text = "No provider is installed.", Opacity = 0.7 });
            return;
        }

        foreach (var form in forms)
            _root.Children.Add(BuildForm(form));
    }

    /// <summary>
    /// One provider as an expander: the header says what it is (name, a sentence on what it gives, badges for built-in and its
    /// readiness) and the body is its guidance and its form. A provider that is not ready starts open, the others closed; what the
    /// user opens or closes stays so across a save.
    /// </summary>
    private Expander BuildForm(ProviderForm form)
    {
        var body = BuildBody(form);
        var expander = new Expander
        {
            Header = BuildHeader(form),
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsExpanded = _expanded.TryGetValue(form.ProviderId, out bool open) ? open : form.Status is { IsReady: false },
        };
        expander.Expanding += (_, _) => _expanded[form.ProviderId] = true;
        expander.Collapsed += (_, _) => _expanded[form.ProviderId] = false;
        return expander;
    }

    private static StackPanel BuildHeader(ProviderForm form)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        line.Children.Add(new TextBlock { Text = form.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (form.BuiltIn)
            line.Children.Add(Badge("Built-in", BadgeTone.Plain));
        switch (form.Status?.Readiness)
        {
            case Engine.Data.ProviderReadiness.NotAvailable:
                line.Children.Add(Badge("Not available here", BadgeTone.Attention));
                break;
            case Engine.Data.ProviderReadiness.NeedsSetup:
                line.Children.Add(Badge("Needs setup", BadgeTone.Attention));
                break;
            default:
                line.Children.Add(Badge("Ready", BadgeTone.Good));
                break;
        }

        var header = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 8) };
        header.Children.Add(line);
        if (form.Description.Length > 0)
            header.Children.Add(new TextBlock { Text = form.Description, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        return header;
    }

    private enum BadgeTone
    {
        Plain,
        Good,
        Attention,
    }

    /// <summary>A small rounded label: outlined in green when good, amber when it needs attention, the card's grey otherwise.</summary>
    private static Border Badge(string text, BadgeTone tone)
    {
        var color = tone switch
        {
            BadgeTone.Good => Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x4C, 0xC2, 0x5E),
            BadgeTone.Attention => Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE0, 0xA0, 0x30),
            _ => (Windows.UI.Color?)null,
        };
        return new Border
        {
            Padding = new Thickness(8, 1, 8, 2),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = color is { } c ? new Microsoft.UI.Xaml.Media.SolidColorBrush(c) : CardStroke(),
            Background = color is { } tint ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x26, tint.R, tint.G, tint.B)) : null,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, FontSize = 12, Opacity = tone == BadgeTone.Plain ? 0.8 : 1 },
        };
    }

    private StackPanel BuildBody(ProviderForm form)
    {
        var panel = new StackPanel { Spacing = 8 };
        AddReadiness(panel, form);
        if (form.Fields.Count == 0 && !form.OffersSignIn)
        {
            if (form.Status is not { IsReady: false })
                panel.Children.Add(new TextBlock { Text = "Nothing to set up. It works as soon as a widget shows one of its readings.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
            return panel;
        }

        var secrets = new Dictionary<string, PasswordBox>(StringComparer.Ordinal);
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
        if (_messages.TryGetValue(form.ProviderId, out string? message))
            result.Text = message;

        // The same controls as a widget's form, over a detached object that Save writes (nothing is applied before).
        var staging = new JsonObject();
        foreach (var field in form.Fields.Where(f => f.Setting.Kind != SettingKinds.Secret))
            Stage(staging, field);
        var fields = form.Fields.ToDictionary(f => f.Setting.Key, StringComparer.OrdinalIgnoreCase);
        var target = new StagedTarget(staging);
        var readings = new PreviewEditorReadings(_host.Readings, _host.Readings, _host.Readings, _owner.DispatcherQueue);
        var builder = new FormBuilder(new FormContext(_host.Document, null, readings, new NoIconCatalog(), _owner, new ReadingChooserDialog(() => _root.XamlRoot, readings)));
        var view = builder.BuildStaged(
            SettingSchema.Of(typeof(ProviderSettings), form.Fields.Where(f => !f.Setting.SetBySignIn).Select(f => f.Setting).ToList()),
            target,
            setting =>
            {
                var field = fields[setting.Key];
                var box = new PasswordBox { PlaceholderText = field.Secret!.HasValue ? "Leave empty to keep the current value" : "Enter a value" };
                secrets[setting.Key] = box;
                var secretPanel = new StackPanel { Spacing = 4 };
                secretPanel.Children.Add(box);
                secretPanel.Children.Add(BuildSecretStatus(form, field, result));
                return secretPanel;
            });
        _views.Add(view);
        panel.Children.Add(view.Root);

        Dictionary<string, string> Values() => form.Fields.Where(f => f.Setting.Kind != SettingKinds.Secret).ToDictionary(f => f.Setting.Key, f => TextOf(staging, f.Setting), StringComparer.Ordinal);
        Dictionary<string, string> NewSecrets() => secrets.Where(s => s.Value.Password.Length > 0).ToDictionary(s => s.Key, s => s.Value.Password, StringComparer.Ordinal);

        if (form.OffersSignIn)
        {
            var account = new SignInPanel(_host, _owner.DispatcherQueue, form, Values);
            _panels.Add(account);
            panel.Children.Add(account.Root);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var save = new Button { Content = "Save" };
        save.Click += (_, _) =>
        {
            var saved = _host.SaveProvider(form.ProviderId, Values(), NewSecrets());
            if (saved.Ok)
            {
                _messages[form.ProviderId] = "Saved.";
                Rebuild();
            }
            else
            {
                result.Text = saved.Message;
            }
        };
        buttons.Children.Add(save);

        if (form.OffersConnectionTest)
        {
            var test = new Button { Content = "Test connection" };
            test.Click += async (_, _) => await RunTestAsync(form, test, result, Values(), NewSecrets());
            buttons.Children.Add(test);
        }

        panel.Children.Add(buttons);
        panel.Children.Add(result);
        return panel;
    }

    /// <summary>
    /// Says how to get a provider that is not ready going (provider-readiness spec): its own sentence and, when it gave one, its
    /// link, which opens in the browser. The badge in the header says which state it is in; a ready provider says nothing here.
    /// </summary>
    private static void AddReadiness(StackPanel panel, ProviderForm form)
    {
        if (form.Status is not { IsReady: false } status)
            return;

        string? sentence = status.Guidance?.Text
            ?? (status.Missing.Count > 0 ? $"Set {string.Join(", ", status.Missing)} below." : null);
        if (sentence != null)
            panel.Children.Add(new TextBlock { Text = sentence, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        if (status.Guidance?.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            panel.Children.Add(new HyperlinkButton { Content = status.Guidance.LinkText.Length > 0 ? status.Guidance.LinkText : uri.Host, NavigateUri = uri, Padding = new Thickness(0) });
    }

    private void DisposeViews()
    {
        foreach (var view in _views)
            view.Dispose();
        _views.Clear();
        foreach (var panel in _panels)
            panel.Dispose();
        _panels.Clear();
    }

    /// <summary>The value the form starts from: what the file holds (else the default), as the kind types it.</summary>
    private static void Stage(JsonObject staging, ProviderField field)
    {
        var setting = field.Setting;
        string text = field.Value;
        if (setting.Kind == SettingKinds.Toggle)
            staging[setting.Key] = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        else if (text.Length == 0)
            return;
        else if (setting.Kind == SettingKinds.Number && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            staging[setting.Key] = setting.Number is { WholeNumber: true } ? JsonValue.Create((long)number) : JsonValue.Create(number);
        else
            staging[setting.Key] = text;
    }

    /// <summary>The text Save takes for a field: what the form holds now (a switch never left alone is off).</summary>
    private static string TextOf(JsonObject staging, SettingDescriptor setting)
    {
        var node = staging.FirstOrDefault(p => string.Equals(p.Key, setting.Key, StringComparison.OrdinalIgnoreCase)).Value;
        return node switch
        {
            null => setting.Kind == SettingKinds.Toggle ? "false" : "",
            JsonValue value when value.TryGetValue<bool>(out bool on) => on ? "true" : "false",
            JsonValue value when value.TryGetValue<string>(out string? s) => s ?? "",
            _ => node.ToJsonString(),
        };
    }

    /// <summary>
    /// The card outline of the system theme when the application's resources hold it, else a neutral grey. App.xaml merges no
    /// control resources, and a missing key throws from the indexer, which took the whole app down when a form was first shown.
    /// </summary>
    private static Microsoft.UI.Xaml.Media.Brush CardStroke() =>
        Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? value) && value is Microsoft.UI.Xaml.Media.Brush brush
            ? brush
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x40, 0x80, 0x80, 0x80));

    private StackPanel BuildSecretStatus(ProviderForm form, ProviderField field, TextBlock result)
    {
        var secret = field.Secret!;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(new TextBlock
        {
            Text = StatusText(secret, form.CanStoreSecrets),
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 380,
        });
        if (secret.StoredHere)
        {
            var remove = new Button { Content = "Remove" };
            remove.Click += (_, _) =>
            {
                var removed = _host.RemoveProviderSecret(form.ProviderId, field.Setting.Key);
                if (removed.Ok)
                {
                    _messages[form.ProviderId] = $"{field.Setting.Label} removed.";
                    Rebuild();
                }
                else
                {
                    result.Text = removed.Message;
                }
            };
            row.Children.Add(remove);
        }

        return row;
    }

    private static string StatusText(SecretStatus secret, bool canStore)
    {
        if (!secret.HasValue)
            return canStore ? "No value is stored." : $"Nothing on this system can store it. Set the environment variable {secret.EnvironmentName}.";
        return secret.Source switch
        {
            SecretSource.Environment => $"Set by the environment variable {secret.EnvironmentName}."
                + (secret.EnvironmentOverrides ? " It overrides the value stored in Credential Manager." : ""),
            SecretSource.Stored => "A value is stored in Credential Manager.",
            _ => "Kept in urdeck-config.json in plain text.",
        };
    }

    private async Task RunTestAsync(ProviderForm form, Button button, TextBlock result, Dictionary<string, string> values, Dictionary<string, string> secrets)
    {
        var closing = _closing;
        if (closing == null)
            return;

        button.IsEnabled = false;
        result.Text = "Testing…";
        try
        {
            var outcome = await _host.TestProviderAsync(form.ProviderId, values, secrets, closing.Token);
            if (_closing != null)
                result.Text = outcome.Ok ? (outcome.Message.Length == 0 ? "The connection works." : outcome.Message) : outcome.Message;
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Settings: the connection test of {form.ProviderId} failed unexpectedly: {ex.GetBaseException().Message}");
            if (_closing != null)
                result.Text = ex.GetBaseException().Message;
        }
        finally
        {
            if (_closing != null)
                button.IsEnabled = true;
        }
    }
}
