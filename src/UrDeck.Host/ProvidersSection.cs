// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;

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
    private readonly StackPanel _root = new() { Spacing = 12 };
    private readonly Dictionary<string, string> _messages = new(StringComparer.Ordinal);
    private CancellationTokenSource? _closing = new();

    public ProvidersSection(ISettingsHost host)
    {
        _host = host;
        Rebuild();
    }

    public UIElement Root => _root;

    /// <summary>The window closed: a running test is cancelled and the controls are dropped.</summary>
    public void Close()
    {
        var closing = _closing;
        _closing = null;
        closing?.Cancel();
        closing?.Dispose();
        _root.Children.Clear();
    }

    private void Rebuild()
    {
        if (_closing == null)
            return;

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
            _root.Children.Add(new TextBlock { Text = "No provider has settings to change.", Opacity = 0.7 });
            return;
        }

        foreach (var form in forms)
            _root.Children.Add(BuildForm(form));
    }

    private Border BuildForm(ProviderForm form)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = form.Name, FontWeight = FontWeights.SemiBold });

        var texts = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
        var secrets = new Dictionary<string, PasswordBox>(StringComparer.Ordinal);
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
        if (_messages.TryGetValue(form.ProviderId, out string? message))
            result.Text = message;

        foreach (var field in form.Fields)
        {
            panel.Children.Add(new TextBlock { Text = field.Setting.Label + (field.Setting.Required ? " *" : ""), Margin = new Thickness(0, 4, 0, 0) });
            switch (field.Setting.Type)
            {
                case SettingType.Toggle:
                    var toggle = new ToggleSwitch { IsOn = field.Value == "true" };
                    texts[field.Setting.Key] = () => toggle.IsOn ? "true" : "false";
                    panel.Children.Add(toggle);
                    break;
                case SettingType.Secret:
                    var box = new PasswordBox
                    {
                        PlaceholderText = field.Secret!.HasValue ? "Leave empty to keep the current value" : "Enter a value",
                    };
                    secrets[field.Setting.Key] = box;
                    panel.Children.Add(box);
                    panel.Children.Add(BuildSecretStatus(form, field, result));
                    break;
                default:
                    var text = new TextBox { Text = field.Value };
                    texts[field.Setting.Key] = () => text.Text;
                    panel.Children.Add(text);
                    break;
            }

            if (field.Setting.Help.Length > 0)
                panel.Children.Add(new TextBlock { Text = field.Setting.Help, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }

        Dictionary<string, string> Values() => texts.ToDictionary(t => t.Key, t => t.Value(), StringComparer.Ordinal);
        Dictionary<string, string> NewSecrets() => secrets.Where(s => s.Value.Password.Length > 0).ToDictionary(s => s.Key, s => s.Value.Password, StringComparer.Ordinal);

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
        return new Border { Padding = new Thickness(16), CornerRadius = new CornerRadius(8), Child = panel, BorderThickness = new Thickness(1), BorderBrush = CardStroke() };
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
