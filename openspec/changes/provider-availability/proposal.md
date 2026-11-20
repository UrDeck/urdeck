## Why

The reading picker lists every installed provider, so a user without Steam or Home Assistant sees their tabs, filters and
rows when adding a Stats or List widget. That is noise: the user can neither use those readings nor, in the picker, learn
how to get them. The goal is a picker that shows only what could work on this PC, with one place (Settings, Providers)
that lists everything and says how to get a provider going.

## What Changes

- **A provider has a readiness**, derived and never stored in the configuration:
  - **Not available**: the provider says it makes no sense on this PC (Steam when the Steam client is not installed).
  - **Needs setup**: it is available but a required setting has no value (Home Assistant without an address or token).
  - **Ready**: neither. Everything else follows from this; there is no per-provider enabled flag.
  - Steam installed but not signed in is **ready**: its local readings work, and its friends readings keep saying they
    need the sign-in.
- **The SDK lets a provider declare two things**: whether it is available on this PC (default: yes) and a short setup
  guidance, a sentence and an optional link, shown on its Settings page while it is not ready ("Steam is not installed.
  Get Steam"). A provider that declares neither is always ready unless a required setting is missing, so existing and
  third-party providers behave as before.
- **The reading picker hides providers that are not ready**: no tab, no filter chip, no rows. A provider whose readings
  the pages already use still appears, so a current choice never goes blank, with the existing "needs setup" bar.
  A provider that is ready but unreachable keeps its "not connected" bar.
- **Settings, Providers lists every provider** whatever its readiness, with its state and its guidance. It is the one
  way to discover a provider the pickers hide.
- **The Steam provider** declares itself available when the Steam client is installed, and gives the download link as
  its guidance when it is not.
- The pickers' readiness check is local (settings and an installed check): it makes no network call and does not wait on
  a provider starting.

Not in this change: the tap from a widget or a picker to the provider's page (`provider-setup-route`); a per-provider
enabled switch; blocking a widget from being added because a provider is not ready (a widget does not require a
provider, a reading does; a widget on a provider that is not ready says so on the deck); a provider that hides itself
for any reason other than "not on this PC".

## Capabilities

### New Capabilities

- `provider-readiness`: the derived states (not available, needs setup, ready), the provider's declaration of availability and
  guidance, what is shown where for each state.

### Modified Capabilities

- `reading-picker`: providers that are not ready are not offered; "Provider Not Connected" is narrowed to a provider
  that is ready but unreachable, plus the readings in use.
- `settings-window`: the Providers section lists every provider with its readiness and guidance.
- `provider-steam`: availability is whether the client is installed, with the download guidance.

## Impact

- `sdk/UrDeck.Sdk` (`DataProvider`, a provider's declaration; the SDK is MIT and a breaking change is fine before release).
- `src/UrDeck.Engine/Data` (readiness computed from the descriptor, the binder's missing settings and the provider's
  availability), `Engine/Editor/ReadingCatalogLoader` and the catalog filters, `Engine/Settings` (the provider list
  model).
- `src/UrDeck.Host/Editor` (picker tabs, the Providers section), `providers/UrDeck.Providers.Steam`.
- Site user documentation for the picker and Steam; no configuration change and no migration.
