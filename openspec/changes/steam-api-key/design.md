## Decisions

- **The documented API, the user's own key.** `ISteamUser/GetFriendList/v1` (key, steamid, relationship=friend) and
  `GetPlayerSummaries/v2` (up to 100 ids; persona name, state, `gameid`, avatar hash). UrDeck ships no key of its own: a shared
  key would put every user's traffic under one key and one set of Steam's terms.
- **Friends list must be public.** Steam answers 401 for a private list and 403 for a bad key; the client tells them apart (the
  list call only). A private list is not an error: the provider keeps publishing `me/*` and marks `friends/*` unavailable with
  the reason, so one setting in Steam fixes it.
- **SteamID is optional.** Empty means the account Steam is signed in to on this PC (already read for the local readings). With
  neither, the connected readings say "Enter your SteamID in Settings". A configured id that differs from this PC's account is
  logged once, without naming either.
- **Provider stays Ready without the key.** The key is not `Required`: the local readings work without it, and a required
  setting would hide the whole provider from the pickers (see `provider-readiness`).
- **Game names** keep the existing order (this PC, cache, store page); `gameextrainfo` is not used, so the behaviour and the
  cache are unchanged.
- **Generic sign-in framework untouched.** Removing it is churn across the SDK, engine and editor and is a separate call.
