# Fonts

All ten families are SIL Open Font License 1.1 (free to bundle and embed; do not sell the fonts on their own; keep the licence text; do not use a Reserved Font Name for a modified font).
Source for all: Google Fonts (`https://fonts.google.com/specimen/<Family+Name>`; the upstream repos are linked from each page). Download the files, **verify the licence file that
comes with each**, and copy the OFL text to `src/UrDeck.Engine/Themes/Builtin/<Family>-OFL.txt` (the repo does this for Inter already).

| Family | Used by (role) | Draft file name in theme.json | Form | Notes |
|---|---|---|---|---|
| Inter (existing) | default themes | `InterVariable.ttf` | variable | already bundled |
| Instrument Sans | Carbon (body), Paper (values, body) | `InstrumentSans-Variable.ttf` | variable (wght, wdth) | |
| JetBrains Mono | Carbon (values, units, labels), Paper (units, labels) | `JetBrainsMono-Variable.ttf` | variable (wght) | tabular by nature |
| Chakra Petch | Rigline | `ChakraPetch-Bold.ttf` | static | an italic exists (`ChakraPetch-BoldItalic.ttf`); the draft uses `slant: 10` instead; either is fine |
| Bricolage Grotesque | Prism | `BricolageGrotesque-Variable.ttf` | variable (opsz, wdth, wght) | the weight 300 numerals need the wght axis |
| Barlow Condensed | Ember | `BarlowCondensed-Bold.ttf` | static | italic exists; draft uses `slant` |
| IBM Plex Mono | Blueprint | `IBMPlexMono-Regular.ttf` | static | add `-Medium` if `title` weight 500 should be real |
| Archivo | Pop (labels, units, body) | `Archivo-Variable.ttf` | variable (wdth, wght) | |
| Archivo Black | Pop (values) | `ArchivoBlack-Regular.ttf` | static | single weight, so `weights.value` has no effect |
| Bodoni Moda | Obsidian (values) | `BodoniModa-Variable.ttf` | variable (opsz, wght) | **replaces the font Pat disliked** |
| Jost | Obsidian (labels, body) | `Jost-Variable.ttf` | variable (wght) | |

Rules for the implementing agent:
- Rename Google's bracketed file names (`Archivo[wdth,wght].ttf`) to the draft names above; the loader takes plain file names inside the theme folder.
- Prefer the variable file; the resolver already drives the `wght` axis. **Spike S3** (`09`) confirms each one.
- If the installer grows too much, subset to Latin + Latin-1 + common punctuation + digits + the degree sign and arrows **only after** confirming the font's RFN rules (OFL permits subsetting; some fonts reserve a name, in which case rename the subset).
- Glyph checks that matter: `°`, `%`, `:`, `·`, `+`, `-`, digits (tabular for Readout: the component already equalises widths), `↑↓` if used by readings.
- Record each font in `THIRD-PARTY-NOTICES.md` with copyright holder and version.
