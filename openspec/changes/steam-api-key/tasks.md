## 1. Provider

- [x] 1.1 `SteamSettings`: `apiKey` (secret, not set by sign-in) and `steamId`; `ISteamWeb` and `SteamWebClient` on `ISteamUser` with the key; private list and refused key told apart
- [x] 1.2 `ConnectedReadings` and `SteamProvider`: key and account from the settings (fallback: this PC's account), private list, refused key, no QR sign-in, renewal, rotation or sign-out; `SteamJwt` deleted

## 2. Tests

- [x] 2.1 `SteamConnectedProviderTests` and `FakeSteamWeb` for the key, the account fallback, no account, a refused key, a private list; the opt-in real-Steam test on the key; the catalog test
- [x] 2.2 Engine and CLI test suites green

## 3. Documentation and check

- [x] 3.1 Site Steam page and widget page, README
- [x] 3.2 `dotnet build urdeck.slnx -c Release` clean, `dotnet format`, `openspec validate --all --strict`, site build
- [ ] 3.3 With a real key: friends list public and private, a wrong key (the owner; needs their account)
