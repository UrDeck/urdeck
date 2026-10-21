---
title: Shortcut
description: A tappable icon that opens an application, a folder, a file or a web address.
section: Widgets
order: 13
---

`urdeck.widgets.shortcut` is a 1x1 card that shows the icon of an application, a file, a folder or a web address and opens
it when you tap it, the way a desktop shortcut does. While your finger is down the card shrinks and dims slightly (the
amounts are the theme's `press` values); a swipe that starts on a shortcut still changes the page and opens nothing, and
so does a long hold.

```json
{ "typeId": "urdeck.widgets.shortcut", "col": 0, "row": 8, "target": "C:\\Program Files (x86)\\Steam\\steam.exe" },
{ "typeId": "urdeck.widgets.shortcut", "col": 1, "row": 8, "target": "shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App" },
{ "typeId": "urdeck.widgets.shortcut", "col": 2, "row": 8, "target": "%USERPROFILE%\\Documents" },
{ "typeId": "urdeck.widgets.shortcut", "col": 3, "row": 8, "target": "https://example.com", "icon": "C:\\Icons\\example.png" }
```

| Setting | Meaning |
|---|---|
| `target` | What to open. A rooted path to an executable, a `.lnk`, a document or a folder (`%NAME%` environment variables are expanded); a bare name Windows resolves itself (`notepad`); a `shell:` item; a web address (`http` or `https`); or any other address Windows has an application for (`steam://open/main`, `ms-settings:display`). A relative path is refused. With no target the card shows an empty placeholder and does not react. |
| `arguments` | Arguments for a file target. Ignored, with a warning in the log, for an address. They are never written to the log. |
| `icon` | Where the icon comes from instead of `target`: a local image file (PNG, JPEG, WebP, BMP, GIF or ICO), anything else that could be a target, or `theme:<key>` for a glyph of the theme's [icon set](/docs/themes/#icon-sets) (`theme:app/terminal`). |
| `label` | A text under the icon. Absent or `""`: no label, and the icon takes the whole card. |

A packaged (Store) application has no path; its target is `shell:AppsFolder\` followed by its id, which `Get-StartApps`
lists in PowerShell:

```powershell
Get-StartApps | Where-Object Name -like '*Terminal*'   # AppID: Microsoft.WindowsTerminal_8wekyb3d8bbwe!App
```

## Launching

The target is handed to the Windows shell with its default action: never through a command interpreter and never
elevated. Whether a running application is brought forward or a second window opens is the application's decision, as it
is for a desktop shortcut, and Windows and the application decide which monitor the window opens on. A double tap opens
one window. A target Windows refuses (a missing file, no application for the address) opens nothing and `urdeck.log` says
why.

Targets come from your own configuration file and are trusted like a shortcut on your desktop: do not paste in a page
from somebody else without reading its targets.

## Icons

A local image file is shown as it is. For any other file, a folder or a `shell:` item the card shows the icon Windows shows
for it. For a web address it shows the site's own icon, and the default browser's icon when the site offers none. Until
the icon is there the card shows a placeholder with the first letter of the label or of the target's name. When there is
none (a site with no icon, a file Windows has none for) the card shows a glyph chosen by what the target is: a link for a web
address, a folder, or a file.

`"icon": "theme:app/terminal"` draws a glyph of the theme's icon set in the theme's stroke and tint instead of the icon of the target, and asks Windows for no icon; the target still opens on tap. The keys are `app/terminal`, `app/folder`, `app/file`, `app/music`, `app/gamepad`, `app/browser`, `app/mail`, `app/chat`, `app/settings`, `app/code`, `app/camera`, `app/image`, `app/video`, `app/calendar`, `app/clock`, `app/home`, `app/power`, `app/lock`, `app/link`, `app/cloud`, `app/download`, `app/star`, `app/heart`, `app/bolt` and `app/chart`; a key the theme's set lacks is drawn from the built-in `outline` set, and an unknown key shows the placeholder. A real icon is never tinted.

## Network

A site's icon is the one thing UrDeck itself fetches from the network (the weather data is fetched by the weather
provider). It happens only for a shortcut whose icon source is an `http(s)` address, at most once per address in 30 days:
a request for the address, one for the web manifest the page links, and at most four for icon candidates (the manifest's
icons, the page's `apple-touch-icon` and `icon` links, `/favicon.ico`), which may live on another host such as a CDN,
followed there over `https` only. The requests carry no cookies and no credentials, name UrDeck in the `User-Agent`, give
up after ten seconds and never ignore a certificate error. A site behind a sign-in page yields no icon.

To make no request at all, set `icon` to a local file. Fetched icons are kept as PNG files in `cache/icons` next to the
executable; deleting the folder is always safe, the icons are fetched again.
