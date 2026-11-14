// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace UrDeck.Host.Editor;

/// <summary>
/// The account part of a provider's form in the Providers section (settings-window, "Connecting An Account"): Connect, the sign-in
/// in progress (a QR code drawn here, or a code with Copy and Open, the instructions, the status, the time left, Cancel),
/// Connected as with Disconnect, and a failure with Connect again. It holds no state of the sign-in beyond the session it shows.
/// </summary>
internal sealed class SignInPanel : IDisposable
{
    private readonly ISettingsHost _host;
    private readonly DispatcherQueue _queue;
    private readonly ProviderForm _form;
    private readonly Func<IReadOnlyDictionary<string, string>> _values;
    private readonly DispatcherQueueTimer _clock;
    private readonly StackPanel _root = new() { Spacing = 8 };
    private readonly TextBlock _timeLeft = new() { Opacity = 0.7, FontSize = 12 };
    private SignInSession? _session;
    private string? _message;
    private bool _disposed;

    public SignInPanel(ISettingsHost host, DispatcherQueue queue, ProviderForm form, Func<IReadOnlyDictionary<string, string>> values)
    {
        _host = host;
        _queue = queue;
        _form = form;
        _values = values;
        _clock = queue.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) => UpdateTimeLeft();
        Attach(host.SignIn.Current(form.ProviderId));
        Render();
    }

    public UIElement Root => _root;

    public void Dispose()
    {
        _disposed = true;
        _clock.Stop();
        Attach(null);
    }

    private void Attach(SignInSession? session)
    {
        if (_session != null)
            _session.Changed -= OnChanged;
        _session = session;
        if (_session != null)
            _session.Changed += OnChanged;
    }

    private void OnChanged(SignInState state) => _queue.TryEnqueue(() =>
    {
        if (!_disposed)
            Render();
    });

    private void Render()
    {
        _clock.Stop();
        _root.Children.Clear();
        var state = _session?.State;
        if (state is { Phase: SignInPhase.Running })
        {
            RenderRunning(state);
            return;
        }

        if (state is { Phase: SignInPhase.Failed })
            _message = state.Message;
        else if (state is { Phase: SignInPhase.Cancelled })
            _message = null;

        var connection = _host.SignIn.GetConnection(_form.ProviderId);
        _root.Children.Add(new TextBlock { Text = "Account", FontWeight = FontWeights.SemiBold });
        if (connection.Connected)
            RenderConnected(connection);
        else
            RenderDisconnected(connection, state is { Phase: SignInPhase.Failed });
    }

    private void RenderDisconnected(ConnectionInfo connection, bool failed)
    {
        if (connection.Description.Length > 0)
            _root.Children.Add(Note(connection.Description));
        if (_message is { Length: > 0 })
            _root.Children.Add(Note(_message));
        var connect = new Button { Content = failed ? "Connect again" : "Connect" };
        connect.Click += (_, _) =>
        {
            _message = null;
            Attach(_host.SignIn.Start(_form.ProviderId, _values()));
            Render();
        };
        _root.Children.Add(connect);
    }

    private void RenderConnected(ConnectionInfo connection)
    {
        _root.Children.Add(new TextBlock
        {
            Text = connection.EnvironmentName != null
                ? $"Connected (from the environment variable {connection.EnvironmentName})."
                : connection.Account is { Length: > 0 } ? $"Connected as {connection.Account}." : "Connected.",
            TextWrapping = TextWrapping.Wrap,
        });
        if (_message is { Length: > 0 })
            _root.Children.Add(Note(_message));
        if (!connection.StoredHere)
            return;

        var disconnect = new Button { Content = "Disconnect" };
        disconnect.Click += async (_, _) =>
        {
            disconnect.IsEnabled = false;
            var result = await _host.SignIn.DisconnectAsync(_form.ProviderId);
            if (_disposed)
                return;
            Attach(null);
            _message = result.Error ?? (result.ServiceTold
                ? "Disconnected."
                : $"Disconnected here; the session may still be listed in your {_form.Name} account"
                    + (result.Hint.Length > 0 ? $" ({result.Hint})." : "."));
            Render();
        };
        _root.Children.Add(disconnect);
    }

    private void RenderRunning(SignInState state)
    {
        _root.Children.Add(new TextBlock { Text = "Account", FontWeight = FontWeights.SemiBold });
        if (state.Challenge is not { } challenge)
        {
            _root.Children.Add(Note("Starting…"));
        }
        else
        {
            if (challenge.QrText is { } qr)
            {
                int modules = QrImage.Modules(qr).GetLength(0) + 8;
                _root.Children.Add(new Image
                {
                    Source = QrImage.Render(qr, Math.Max(2, 260 / modules)),
                    Stretch = Stretch.None,
                    HorizontalAlignment = HorizontalAlignment.Left,
                });
            }
            else
            {
                _root.Children.Add(new TextBlock
                {
                    Text = challenge.Code ?? "",
                    FontSize = 28,
                    FontFamily = new FontFamily("Consolas"),
                    IsTextSelectionEnabled = true,
                });
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var copy = new Button { Content = "Copy" };
                copy.Click += (_, _) =>
                {
                    var package = new DataPackage();
                    package.SetText(challenge.Code ?? "");
                    Clipboard.SetContent(package);
                };
                row.Children.Add(copy);
                if (challenge.Address is { Length: > 0 } address)
                {
                    var open = new Button { Content = "Open" };
                    open.Click += (_, _) => _host.OpenAddress(address);
                    row.Children.Add(open);
                    row.Children.Add(new TextBlock { Text = address, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
                }

                _root.Children.Add(row);
            }

            _root.Children.Add(new TextBlock { Text = challenge.Instructions, TextWrapping = TextWrapping.Wrap });
            _root.Children.Add(_timeLeft);
            UpdateTimeLeft();
            _clock.Start();
        }

        if (state.Status is { Length: > 0 } status)
            _root.Children.Add(Note(status));
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) =>
        {
            _session?.Cancel();
            _message = null;
            Render();
        };
        _root.Children.Add(cancel);
    }

    private void UpdateTimeLeft()
    {
        if (_session?.State.Challenge is { } challenge)
            _timeLeft.Text = $"Time left: {ProviderSignIn.TimeLeft(challenge, DateTimeOffset.UtcNow)}";
    }

    private static TextBlock Note(string text) => new() { Text = text, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap };
}
