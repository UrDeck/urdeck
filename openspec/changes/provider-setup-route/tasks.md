## 1. SDK and launcher

- [ ] 1.1 SDK: a builder for the provider-settings target from a provider id; tests of the text and of an id that needs escaping
- [ ] 1.2 Launcher: recognize the target before the shell, route it to a host handler, report failure with one log line when there is none; the one-second repeat rule applies; tests on Linux with a fake handler
- [ ] 1.3 Engine editor model: "show provider X" (focus, expand) with tests, including an unknown id

## 2. Host

- [ ] 2.1 `EditorWindow.ShowProviders(providerId)` focuses and expands the provider; the picker's and the form's "Set up" use it; the handler opens the editor when it is closed
- [ ] 2.2 The wording `<Name> needs setup` for not-connected readings; Home Assistant's missing-setting text reviewed against it

## 3. Steam

- [ ] 3.1 Steam widget: a notice for a setup-needed reading taps to the settings target and never to `steam://`; connected taps unchanged; widget tests with fake readings
- [ ] 3.2 Steam page and troubleshooting page on the site use the new wording

## 4. Check

- [ ] 4.1 `dotnet build urdeck.slnx -c Release` clean, tests, `dotnet format`, `openspec validate --all --strict`
- [ ] 4.2 On the panel: tap the Steam notice with the editor closed and open; the editor reaches the front and shows Steam
