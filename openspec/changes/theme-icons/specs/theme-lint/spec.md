## MODIFIED Requirements

### Requirement: Findings About Backgrounds And Pairing
A theme's background and its pairings MUST be checked against the backgrounds that exist. The backgrounds that exist are
the built-in ones and those in the directories the tool is given:

| Code | Severity | Meaning |
|---|---|---|
| `THEME040` | warning | `background.default.name` is not a background that exists |
| `THEME041` | warning | `meta.pairsWith.backgrounds` names a background that does not exist |
| `THEME042` | warning | the theme's default background is not in its own `pairsWith` list |

- A theme with no `background` or no `pairsWith` has no finding from these codes
- The deck does not check any of these when it loads a theme; the tool alone reports them

#### Scenario: Unknown default background
- **WHEN** `background.default.name` is `nosuch`
- **THEN** a `THEME040` warning names `nosuch`

#### Scenario: Pairing names a missing background
- **WHEN** `meta.pairsWith.backgrounds` is `["contour", "nosuch"]`
- **THEN** a `THEME041` warning names `nosuch` and none names `contour`

#### Scenario: Default not paired
- **WHEN** a theme's default background is `contour` and `pairsWith` lists only `streams`
- **THEN** a `THEME042` warning says so

#### Scenario: User background directory
- **WHEN** the tool is given a directory holding a background `mine` and a theme pairs with `mine`
- **THEN** there is no `THEME041` finding

## ADDED Requirements

### Requirement: Findings About Icon Sets
The set a theme names, and the icon sets in the theme's folder, MUST be checked as files. The sets that exist are the
built-in ones and the folders `icons/<name>/` in the theme's folder:

| Code | Severity | Meaning |
|---|---|---|
| `THEME044` | warning | `icons.set` names an icon set that exists neither in the theme's folder nor among the built-in sets |
| `THEME070` | error | a set's `manifest.json` is missing, is not valid JSON, has a `version` other than 1, or lacks `keys` |
| `THEME071` | error | a manifest entry names a file that is missing, does not end in `.svg`, or leaves the set's folder |
| `THEME072` | error | an icon file breaks the file rules (over 4 KB, no `viewBox` of `0 0 24 24`, an element or attribute that is not allowed, a `transform`, not valid XML) |
| `THEME073` | warning | a manifest key is not in the product's key lists |
| `THEME074` | warning | the manifest's `license` or `source` is empty, or no licence file is in the set's folder |
| `THEME075` | warning | an `.svg` file in the set's folder, at any depth, is named by no manifest entry |
| `THEME076` | info | the set has no icon for some keys of the product's lists (the finding names them and the set they fall back to) |

- A set that is not named by `icons.set` but sits in the theme's `icons/` folder is checked the same way
- `THEME075` ignores files that are not `.svg`, so the manifest, a licence file and a readme are never reported
- A set with fewer keys than the lists is allowed; `THEME076` is informational and does not fail a run, even with warnings
  counted as failures
- The deck does not scan a set's folder for files the manifest does not name; it reads the manifest and the files it names. The
  tool alone reports `THEME075`

#### Scenario: Unknown set
- **WHEN** `icons.set` is `nosuch` and the theme's folder has no `icons/nosuch`
- **THEN** a `THEME044` warning names `nosuch`

#### Scenario: No manifest
- **WHEN** a theme's folder holds `icons/mine/rain.svg` and no `icons/mine/manifest.json`
- **THEN** a `THEME070` error names `icons/mine`

#### Scenario: Entry leaves the folder
- **WHEN** a manifest maps `weather/rain` to `../x.svg`
- **THEN** a `THEME071` error names the key and the path, on Windows and on Linux

#### Scenario: A file the format does not allow
- **WHEN** an icon file contains an `image` element
- **THEN** a `THEME072` error names the file and the element

#### Scenario: Forgotten manifest line
- **WHEN** the set's folder holds `cloud-rain.svg` and `cloud-snow.svg` and the manifest names only the first
- **THEN** a `THEME075` warning names `cloud-snow.svg`, and none names the manifest or a licence file

#### Scenario: A partial set
- **WHEN** a set maps only the 13 weather keys
- **THEN** a `THEME076` info finding names the 24 `app` keys and says `outline` answers them, and the run still succeeds with
  warnings counted as failures

#### Scenario: The built-in set
- **WHEN** the built-in `outline` set is checked
- **THEN** there is no finding
