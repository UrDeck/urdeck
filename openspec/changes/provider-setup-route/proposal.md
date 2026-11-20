## Why

When a provider is not ready, every place that says so should behave the same: say the provider needs setup, and take
the user to that provider's page in the editor, where the way forward is. Today they differ. The Steam widget says
"Connect Steam in Settings" and a tap on it opens Steam's friends list (which does nothing useful without Steam),
Home Assistant says "set up Home Assistant in Settings", and the picker's and the form's "Set up" buttons only select
the Providers section: `ShowProviders(providerId)` ignores which provider was asked for.

## What Changes

- **A launch target for the editor**: `urdeck://settings/providers/<id>` is handled by the host, not the shell. It opens
  the editor on that provider in the Providers section (focused and expanded); it opens the editor if it is closed. The SDK
  offers a helper that builds it, so a widget names a provider and no editor.
- **The editor honors the provider asked for**: the picker's and the form's "Set up" buttons and the deck's tap all arrive
  at the same place. Today none of them focuses the provider.
- **One wording for a provider that is not ready**: `<Provider> needs setup` for a reading that cannot be shown for lack
  of setup or a connection. It replaces `Connect <Provider> in Settings`, the sentence a user sees for Steam today.
  Providers still say why in their own page's text.
- **The Steam widget tells the user the provider needs setup and a tap on that notice opens Steam's page in the
  editor.** The same widget keeps its other taps (a friend's profile, a game, the friends list) when it is connected. The
  Providers page for Steam carries the guidance from `provider-availability`: Connect when installed, "Get Steam" when
  not.
- Other widgets show provider-not-ready text as they do now and are not changed beyond the wording; a widget opts into the
  tap by the SDK helper.

Not in this change: whether the pickers show a provider at all (`provider-availability`); stopping a widget from being
added; a tap target for any other part of the editor.

## Capabilities

### New Capabilities

### Modified Capabilities

- `launcher`: the host-handled `urdeck://settings/providers/<id>` target.
- `settings-window`: the Providers section can be opened on a provider and focuses it.
- `provider-sign-in`: the wording of a reading that needs a connection.
- `provider-steam`: the widget's notice and its tap.

## Impact

- `sdk/UrDeck.Sdk` (`Launch`: a target builder), `src/UrDeck.Engine/Launch` (route the target to a settings handler;
  no shell start), `src/UrDeck.Host` (`EditorWindow.ShowProviders` focuses the provider; the handler),
  `providers/UrDeck.Providers.Steam` and `widgets/UrDeck.Widgets.Steam` (wording, tap).
- Site: the Steam page and the troubleshooting page mention the wording. No configuration change.
- Depends on `provider-availability` only for the Steam guidance text; the two can be applied in either order.
