---
title: Steam
description: Show the game you are playing, your recent games and downloads with no setup, and your friends who are online or playing with your own Steam Web API key.
section: Start
order: 7
---

The `steam` data provider shows Steam on the deck: the game you are playing, the games you played lately, a download or an
update in progress and, with your own Steam Web API key, your own state and the friends who are online or playing. The
[Steam widget](/docs/widgets/steam/) draws it as a card, and any widget that shows readings can show a single value, for
example [Stats](/docs/widgets/stats/) or the [List](/docs/widgets/list/) widget.

## What works without connecting

Everything about **you** comes from your PC, with no sign-in, no key and no network. UrDeck reads what the Steam client keeps in
Windows' registry and in its own files, read-only, and only while a card that shows it is on the screen.

| Reading | What it is |
|---|---|
| `steam:client/running` | Whether Steam is running. |
| `steam:me/name` | The persona name of the account signed in to Steam on this PC (or the one that signed in last). |
| `steam:me/playing`, `steam:me/game` | Whether you are playing, and the game's name. It changes within a couple of seconds of starting or quitting a game. |
| `steam:me/game/art` | The game's picture, from Steam's own library cache on your disk. |
| `steam:games/recent` | A list: the ten games you played most recently in the last 30 days, with the hours of the last two weeks and the total. |
| `steam:downloads` | A list: games downloading, updating or paused, with their progress. |
| `steam:download/progress`, `steam:download/name` | The first download in progress, for a gauge. Unavailable, with the reason `Nothing downloading`, when there is none. |

Steam does not have to run for your name to show. Without Steam installed, the reading picker offers no Steam readings, and
**Providers**, **Steam** in the editor says `Steam is not installed.` with a **Get Steam** link; a card that already shows one of
these readings says `Steam is not installed on this PC`. Installing Steam is noticed the next time you open the picker or Providers. If a file cannot be read, because Steam is writing it at that moment, the reading keeps
its last value and is read again at the next change.

## Showing your friends

Your friends' states are not on your PC, so showing them needs Steam's own Web API and **your own API key**. UrDeck never asks
for your Steam password and does not sign in to your account: the key only reads what a Steam profile makes public.

1. Open [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey) while signed in to Steam and register a key.
   Steam asks for a domain name; it is not checked for personal use, so anything such as `localhost` will do. The key is yours:
   do not share it.
2. In Steam, under your profile's **Privacy Settings**, set **Friends List** to **Public**. Steam only gives out a friend list
   that is public. (Your own state works without it.)
3. In the editor (click the tray icon), open **Providers**, **Steam**, and paste the key into **Web API key**. Leave **SteamID**
   empty to use the account Steam is signed in to on this PC, or enter your 17-digit SteamID64 (the number in your profile's
   address, or on [steamid.io](https://steamid.io/)).

| Reading | What it is |
|---|---|
| `steam:me/state` | Your state: Online, Away, Busy, Snooze, Looking to trade, Looking to play or Offline. |
| `steam:me/avatar` | Your avatar's picture. |
| `steam:friends/online` | A list: the friends who are not offline, the ones in a game first, then by name, with their game, state, avatar and a link to their profile. |
| `steam:friends/playing` | The same rows, only the friends playing a game. |

Before you add a key, these readings say `Connect Steam in Settings`. If Steam refuses the key they say
`Steam rejected the API key; check it in Settings` and UrDeck stops asking Steam until you change the key. If the friend list is
private, `friends/online` and `friends/playing` say `Your Steam friends list is private; make it public to show friends` and your
own state still shows. A friend Steam does not report on counts as offline. A friend's game is named from your own games when you
have it installed, otherwise from Steam's store page, once, and kept. The friends and states are polled once a minute
(**Interval** under the provider, at least 30 seconds) and only while a card that shows them is on the screen.

The key is subject to [Steam's Web API terms](https://steamcommunity.com/dev/apiterms); it is for your own use, and UrDeck makes
a few calls a minute with it.

## What is sent where

Nothing, until you add a key. After that, and only while a connected reading is on the screen:

- **`api.steampowered.com`**: your friend list and your friends' states, with your key.
- **`store.steampowered.com`**: a game's name by its number, once per game, for a friend's game you do not have installed.
- **`avatars.fastly.steamstatic.com`**: the avatars of the friends in the list shown.

Nothing goes to urdeck.app. The key is kept in Windows Credential Manager, never in `urdeck-config.json` (see
[Secrets](/docs/#secrets)); the environment variable `URDECK_SECRET_STEAM_APIKEY` overrides it. UrDeck writes no friend's name,
no game, no Steam id and no key to its log. A small cache lives in `cache/steam/` in the data folder: game names, and the avatars
(at most 300, the least recently used removed). Deleting the folder is always safe.

## Removing the key

Clear **Web API key** in the editor, and UrDeck stops calling Steam. To make the key itself useless, revoke it on
[steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey).

## Not covered

Chat, trading, the store, achievements, launching or joining a friend's game, more than one Steam account, and Steam on
Linux. A friend's game shows its name only, without art.
