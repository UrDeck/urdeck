---
title: Installing and updating
description: How to install UrDeck, how it keeps itself up to date, how to turn the update check off, how to uninstall, and what it sends over the network.
section: Start
order: -1
---

## Installing

Download `UrDeck-win-Setup.exe` from the [latest release](https://github.com/UrDeck/urdeck/releases/latest) and run it. It
installs UrDeck for you alone, without administrator rights, into `%LocalAppData%\UrDeck`, adds **UrDeck** to the Start menu
(no desktop shortcut) and starts the deck. Your first page is the sample page; the [getting started](/docs/) page says how to
change it. A page should be on your display within five minutes of starting the download.

`UrDeck-win-Setup.exe --silent` installs without showing a window. Installing a newer version over an installed one keeps
your files.

UrDeck does not start with Windows until you ask: right-click the tray icon and choose **Start with Windows**.

### The "Windows protected your PC" warning

The first releases are not signed yet, so Windows SmartScreen may show a blue window saying it protected your PC. Choose
**More info**, then **Run anyway**. Signing follows in a later release, and the warning goes away as it does.

## Updating

An installed UrDeck looks for a newer release about a minute after it starts and then once a day. It downloads the release in
the background and adds **Restart to update to 0.2.0** to the top of the tray menu. Choose it and the deck restarts as the new
version with the same page. If you do not, the update is applied when you choose **Quit**, so the next start is the new
version.

A copy on a stable version takes only stable releases. A copy on a release candidate (`0.2.0-rc.1`) also takes release
candidates. A check that fails (no network, say) is logged as a warning in `urdeck.log` and tried again at the next check;
the deck is never interrupted. The settings window and the first lines of the log say which version you are running.

A copy you built yourself, or ran from a folder rather than installed, never checks.

### Turning the update check off

Add `"updates": "off"` to `urdeck-config.json`. UrDeck then makes no request and offers nothing; the change applies when the file
is reloaded. `"updates": "auto"` (or no key) is the default; another value is treated as `"auto"` with a warning in the log.

## Uninstalling

Use **Installed apps** in Windows Settings. The uninstaller closes the deck, removes the Start with Windows registration, the
application folder and the Start menu shortcut. It leaves `%APPDATA%\UrDeck`, so installing again shows the page you had;
delete that folder by hand if you want everything gone.

## The portable zip

`UrDeck-win-Portable.zip` is the same program without an installer. Unpack it into a folder you own (a USB stick, say) and
start `UrDeck.exe` from the folder's top level. Your configuration, log, themes and the rest are kept in
the folder you unpacked into, next to the `current` folder that an update replaces, so an update keeps your page. The log's
first line says `Data folder: ... (portable release)`.

## What UrDeck sends over the network

- **The update check** reads the list of releases of the GitHub repository `UrDeck/urdeck`, once a day, and downloads a
  newer release when there is one. It sends no account, no token and nothing about you or your PC beyond what any web request
  carries (your address and the program's name). Turn it off with `"updates": "off"`.
- **The weather widget** asks Open-Meteo for the forecast of the places you configured.
- **The shortcut widget** may ask a web site for its icon when you give it a web address.

Nothing else leaves your PC.
