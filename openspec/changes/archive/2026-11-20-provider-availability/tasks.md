## 1. SDK

- [x] 1.1 `DataProvider<T>`: an availability check (default available) and setup guidance (sentence, optional web address); XML docs; SDK tests of the defaults
- [x] 1.2 The analyzer, if it needs to know the new members (it should not); `docs`/site SDK reference mentions them

## 2. Engine

- [x] 2.1 Readiness of a provider (not available, needs setup, ready) computed from the descriptor, `ProviderSettingsBinder.Bind` and the instance's availability, without starting it or using the network; a throwing check is available and logged once
- [x] 2.2 Readiness re-asked on a settings or secrets change; the provider list model for the editor carries readiness and guidance, and includes providers with no settings
- [x] 2.3 `ReadingCatalogLoader` / catalog: providers that are not ready are left out, except those whose readings the pages use (with the needs-setup bar); a not-ready provider is never started for a listing
- [x] 2.4 Engine tests on Linux with fake providers: each state, default declarations, a throwing check, becoming ready on a saved setting, in-use readings kept

## 3. Host

- [x] 3.1 Picker: tabs, filter chips and "All" follow readiness; the needs-setup bar for an in-use provider; the not-connected bar only for ready-but-unreachable
- [x] 3.2 Settings, Providers: every provider, its state, its guidance and link (opens in the browser)
- [x] 3.3 Providers section: scrolls, one expander per provider with its description (`DataProvider` `Description`) and Built-in / readiness badges, not-ready open by default

## 4. Steam

- [x] 4.1 Availability from a fresh registry read; guidance "Steam is not installed" with Steam's download link; local readings unchanged
- [x] 4.2 Tests with the fake `ISteamLocal`: installed, not installed, installed later

## 5. Documentation and check

- [x] 5.1 Site: the picker page and the Steam page (what appears when Steam is not installed); update `docs/BACKLOG.md`
- [x] 5.2 Look at the editor with Steam not installed (rename the registry value in a test copy or use the fake) and with Home Assistant unconfigured, on the panel and on the editor
- [x] 5.3 `dotnet build urdeck.slnx -c Release` clean, tests, `dotnet format`, `openspec validate --all --strict`
