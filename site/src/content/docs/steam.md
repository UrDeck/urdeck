---
title: Steam
description: Show the game you are playing, your recent games and downloads with no setup, and your friends who are online or playing after one sign-in with the Steam app.
section: Start
order: 7
---

The `steam` data provider shows Steam on the deck: the game you are playing, the games you played lately, a download or an
update in progress and, after you connect Steam, your own state and the friends who are online or playing. The
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

Steam does not have to run for your name to show. Without Steam installed, these readings say
`Steam is not installed on this PC`. If a file cannot be read, because Steam is writing it at that moment, the reading keeps
its last value and is read again at the next change.

## Connecting Steam

Your friends' states are not on your PC, so showing them needs a sign-in to Steam. UrDeck never asks for a Steam Web API key
and never asks you to type your Steam password.

1. In the editor (click the tray icon), open **Providers**, **Steam**, and choose **Connect**.
2. A QR code appears. In the **Steam app** on your phone, choose the option to scan a QR code (it is under **Steam Guard**),
   scan it and approve the sign-in. The code is drawn on your PC and goes nowhere else; if Steam renews it while you wait, the editor draws
   the new one.
3. The editor shows your account name. Close it: the friends appear on the next sample.

| Reading | What it is |
|---|---|
| `steam:me/state` | Your state: Online, Away, Busy, Snooze, Looking to trade, Looking to play or Offline. |
| `steam:me/avatar` | Your avatar's picture. |
| `steam:friends/online` | A list: the friends who are not offline, the ones in a game first, then by name, with their game, state, avatar and a link to their profile. |
| `steam:friends/playing` | The same rows, only the friends playing a game. |

Before you connect, these readings say `Connect Steam in Settings`. A friend Steam does not report on (some accounts are left
out of its answers) counts as offline. A friend's game is named from your own games when you have it installed, otherwise
from Steam's store page, once, and kept.

The connection lasts about **seven months**: UrDeck keeps it alive by itself in the meantime. When Steam no longer accepts it
(it ran out, or you removed UrDeck from the Steam app), the connected readings say
`Steam sign-in expired; connect Steam again in Settings`, UrDeck stops asking Steam, and the local readings carry on. The
friends and states are polled once a minute (**Interval** under the provider, at least 30 seconds) and only while a card that
shows them is on the screen.

The connected half uses Steam's service interface, the calls Steam's own mobile app makes for your account. It is not the
documented, keyed Web API and Valve can change it; if it stops working, the local half is not affected.

## What is sent where

Nothing, before you connect. After you connect, and only while a connected reading is on the screen:

- **`api.steampowered.com`**: the sign-in, the renewal of its access, your friend list and your friends' states.
- **`store.steampowered.com`**: a game's name by its number, once per game, for a friend's game you do not have installed.
- **`avatars.fastly.steamstatic.com`**: the avatars of the friends in the list shown.

Nothing goes to urdeck.app. The sign-in token is kept in Windows Credential Manager, never in `urdeck-config.json`
(see [Secrets](/docs/#secrets)). UrDeck writes no friend's name, no game and no Steam id to its log. A small cache lives in
`cache/steam/` in the data folder: game names, and the avatars (at most 300, the least recently used removed). Deleting the
folder is always safe.

## Removing the connection

Choose **Disconnect** in the editor: UrDeck asks Steam to end the session, then deletes the token. If Steam cannot be reached,
the token is deleted anyway and the session stays in Steam until you remove it: in the Steam app, open the list of devices
signed in to your account (under **Steam Guard**) and remove **UrDeck**.

## Not covered

Chat, trading, the store, achievements, launching or joining a friend's game, more than one Steam account, and Steam on
Linux. A friend's game shows its name only, without art.
