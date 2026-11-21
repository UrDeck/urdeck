## Context

The pickers get their provider list from `ReadingCatalogLoader` over `ProviderRegistry`; every registered provider gets
a tab. The hub already knows when a provider "needs setup": `ProviderSettingsBinder.Bind(...).Missing` and
`IsWaitingForSettings`, but only after a provider was asked for readings (`MarkMissing`). Steam's `Installed` is read once
when `WindowsSteamLocal` is built. The exploration that led here is the conversation behind this change: the goal is less
noise in the pickers, not a general enable/disable system.

## Decisions

1. **Readiness is derived, not configured.** No `enabled` key. The rule: `available && no required setting missing`.
   *Alternatives:* a stored switch per provider (an extra thing to explain and to get out of sync with setup; rejected),
   a `CountsAsSetUp` annotation on a secret so Steam would need its sign-in to be offered (rejected: Steam's local
   readings work without it, and hiding working readings is the wrong trade).
2. **The provider declares availability, the host does not guess.** `DataProvider<T>` gets a virtual availability check
   (default available) and a guidance value (sentence and optional link). One seam, no provider ids in the engine. The
   check is synchronous and cheap by contract; the host catches exceptions (available, logged once).
3. **Readiness is computed without starting the provider.** The hub already makes an instance for the catalog
   (`GetCatalog`) without starting it. Readiness binds the settings the same way the start does (the same
   `ProviderSettingsBinder`, so what "missing" means has one definition) and asks the instance for availability. It
   caches nothing across settings or secret changes: the editor asks again when a section opens, and when settings or
   secrets change (the hub already sees both).
4. **Steam's probe re-reads the registry** instead of the value `WindowsSteamLocal` cached at construction, so installing
   Steam is noticed. The local readings' behavior when it is not installed does not change.
5. **"In use" beats "hidden".** The picker's existing cache of the readings the pages use (`ReadingDescriptionCache`)
   already lists them without values; a hidden provider with a reading in use keeps its tab and the bar. A current choice
   is never blank.
6. **Settings, Providers is the discovery path** and lists every provider. It needs the list model to include providers
   with no settings of their own (today it lists those "that declare settings beyond the base").
7. **No gating when adding a widget.** A widget does not require a provider, its readings do; a widget on a provider that is
   not ready says so on the deck. (A declaration such as `[RequiresProvider]` was considered and left out; if the palette
   later wants to dim such widgets it can read the same readiness.)

## Risks

- Hiding by readiness means a user who deleted a token sees a tab vanish. Mitigation: decision 5 (in use stays), and the
  Providers section.
- A slow or throwing availability check in a third-party provider: bounded by the contract, the catch, and not being asked
  while a form is typed in (asked on opening the picker or the section, and on a settings or secret change).
- Steam installed where the registry does not record it (a portable copy) reads as not available, as the local readings
  already say "not installed" there; the guidance sentence says what Steam must record, and the Providers section still lists it.

## Open questions

- The exact SDK names (`IsAvailable` / `SetupGuidance`) are for the apply step; the specs stay behavioral.
- Whether "Not available here" providers sort below the ready ones in Settings, or only get a different label.
