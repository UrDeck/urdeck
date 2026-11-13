## 1. SDK

- [ ] 1.1 `SignInChallenge` (QR text, or code and address; instructions; expiry), `ISignInProgress` (`Show`, `Status`), `SignInResult` (approved with account and secrets, failed with reason)
- [ ] 1.2 `DataProvider<TSettings>`: `OffersSignIn`, `SignInAsync`, `SignOutAsync` (defaults: not offered), and a sign-out hint text
- [ ] 1.3 `SecretAttribute.SetBySignIn`; `IReadingSink.StoreSecret(setting, value)` default member returning false
- [ ] 1.4 Doc comments with the rules; one minor SDK version; `SdkContractTests`

## 2. Engine

- [ ] 2.1 Secret store: account name kept as the credential's user name; renewal from a running provider without a restart; refusals (undeclared, other provider, too large, environment source) logged once
- [ ] 2.2 Sign-in session behind `ISettingsHost`: one per provider, cancellation, expiry plus 30 s, abandoned late results, approval stores secrets and account in one step and restarts the provider; sign-out with 10 s, deletion whatever the outcome
- [ ] 2.3 Log lines per the spec, with no challenge, code, secret or account
- [ ] 2.4 Linux tests with a fake provider: approved, declined, expired, rotated challenge, cancelled, window closed, unexpected secret, hung provider, renewal (store, environment, other provider), disconnect online and offline
- [ ] 2.5 Engine-side `Connect <name> in Settings` reason helper for providers; samples and `--demo` offer no sign-in

## 3. Host

- [ ] 3.1 `QRCoder` in `Directory.Packages.props` (host only); QR module matrix drawn into a bitmap with the theme's editor colours and a quiet zone
- [ ] 3.2 Providers section states: Connect, signing in (challenge, instructions, status, time left, Cancel), Connected as / from variable with Disconnect, failed; no field for secrets set by sign-in
- [ ] 3.3 Code-and-address challenge: Copy and Open (through the launcher)
- [ ] 3.4 Manual check with a fake QR provider in a development build, recorded in the PR

## 4. Site and docs

- [ ] 4.1 Providers page: connecting an account, what is stored where, disconnecting; privacy page: sign-in tokens in Credential Manager, nothing sent but to the provider's service
- [ ] 4.2 SDK page for provider authors: offering a sign-in, renewing a secret
