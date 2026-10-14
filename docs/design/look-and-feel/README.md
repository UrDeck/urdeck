# UrDeck look-and-feel handoff package

> **Status of this folder.** A snapshot of the look-and-feel handoff package (written 2026-10-13 by Claude Design for
> the owner), copied into the repository so the changes `theme-format-2`, `theme-backgrounds` and `builtin-shaders`
> can cite it. The OpenSpec changes under `openspec/changes/` are the specification; where they differ from this text
> they win. Differences so far: keys no drafted theme uses are deferred (`docs/design/theme-format-2-deferred-keys.md`);
> the drafted themes are test fixtures until each ships; the underlay is compositor sprites and the animated border a
> stepped compositor rotation (`docs/perf/theme-look.md`); installer size is not a concern; Pop takes its numerals from
> Archivo; `motion.level` does not affect the background. `assets/` holds the drafted themes, the schema and the
> twelve shaders (source and manifest; the design-board pictures and seed icons stay with the original package).
> The files below are otherwise unedited, so they still say "PR 0" to "PR 18" and "Pat".


**Audience:** an implementing agent (Sonnet-class is fine) and the OpenSpec skill that will turn this into
`openspec/changes/*` proposals.
**Owner:** Pat (Patrick Bigler), UrDeck.
**Status:** design is approved by Pat; nothing here has been built or merged. Shaders were compiled and rendered with
skia-python only. **`urdeck-cli shader validate` and the .NET build were NOT run** (no dotnet in the authoring
environment), so the first task in the first PR is to run them.

## What this package asks you to build

UrDeck's widgets should stop looking like a Nexus clone. The package defines, with enough precision to build without
guessing:

1. **Eight new built-in themes** (Carbon, Rigline, Prism, Paper, Ember, Blueprint, Pop, Obsidian) with draft
   `theme.json` files.
2. **A theme schema extension ("format 2")**: about 160 new optional keys in about 25 new objects (card chrome, page patterns, typography
   roles, gauge variants, icon sets, background pairing, motion ceiling). Every v1 key keeps its meaning; a v1 host
   ignores the new keys, so a format-2 theme degrades instead of breaking.
3. **Twelve new animated SkSL backgrounds**, already written, compiled and rendered, with manifests and samples.
4. **Theme-specific icon sets** and a canonical icon key list (weather, shortcut categories).
5. **"Pairs well with"**: themes recommend backgrounds, backgrounds recommend themes, so the future config editor can
   offer good picks. Plus the rules for a theme's default and static background.
6. The widget, engine, CLI, docs and website work that makes all of the above real.

## Read in this order

| # | File | What it is |
|---|---|---|
| 00 | `00-overview.md` | Goals, principles, scope, non-goals, glossary. 10-minute read. |
| 01 | `01-current-state.md` | Audit of what exists today, with file references. Verify before you rely on it. |
| 02 | `02-theme-schema.md` | **The core spec.** Every knob: meaning, range, default, who uses it, validation, merge rules. Appendix A is generated from the JSON Schema. |
| 03 | `03-engine-work.md` | How the host draws it: card pipeline, bleed surfaces, animation clock, resolver and sanitizer changes, perf budgets. |
| 04 | `04-widgets-and-icons.md` | Per-widget changes (Clock, Stats, Weather, Shortcut), new components, icon system, canonical keys. |
| 05 | `05-backgrounds.md` | The 12 shaders: parameters, palette roles, cost, how to ship them, the CPU-only validation caveat. |
| 06 | `06-themes.md` | Each theme's intent, knob usage, fonts, pairings, acceptance picture. |
| 07 | `07-pairing-and-editor.md` | Pairing data model, precedence, scoring, what the editor needs from the engine. |
| 08 | `08-cli-validation-docs-site.md` | `urdeck-cli theme validate`, schema publishing, docs, the Astro site, snapshot tests. |
| 09 | `09-phasing-and-tasks.md` | **PR-sized work items** with acceptance criteria, in dependency order. Start here when you are ready to code. |
| 10 | `10-decisions-and-risks.md` | Open decisions (with a recommendation each), risks, spikes. |
| 11 | `11-gap-audit.md` | The "did we miss any knob or feature" audit that Pat asked for: mock-feature to knob matrix and theme-by-knob usage. |

## Assets

```
assets/
  design-board/        Mock images of every theme and background (the visual source of truth), plus the HTML source
  schema/theme.schema.json   JSON Schema (2020-12) for format 2. Validates all 8 draft themes.
  themes/<name>/theme.json   Draft themes (8). Values are converted from the mocks: mock px / 110 = cell fraction.
  shaders/<name>/<name>.sksl + manifest.json + sample.png   The 12 backgrounds. Drop into src/UrDeck.Engine/Backgrounds/Builtin/<name>/.
  icons/<set>/<key>.svg      Seed SVG icons (a few keys); the rest must be drawn. See 04.
  fonts.md             Font list, licences, file names, where to get them.
```

## Ground rules from the repo (AGENTS.md, restated because they are non-negotiable)

- Never commit to `main`. Branch (`feat/...`), push, open a PR; squash-merge; the PR title is a conventional commit.
- One OpenSpec change per PR where possible. Pat generates OpenSpec from a skill; this package is the *input* to that
  skill, so the work items in `09` are written at proposal size (why / what changes / impact / tasks / acceptance).
- `dotnet build` must have 0 warnings (warnings are errors). `dotnet format urdeck.slnx --severity warn`.
- `urdeck-cli shader validate --all <dir> --fail-on-warnings` must pass for every shader you add.
- The SDK (`sdk/UrDeck.Sdk`) is MIT; the engine and host are GPL-3.0-or-later. Widgets reference only UrDeck.Sdk. Do not
  make the SDK depend on the engine. New SDK API needs care: widgets outside this repo compile against it.
- Anything that visibly changes needs a before/after picture. `UrDeck.Host.exe --snapshot out.png --size 1100x3840`
  renders without a screen (Windows).

## Things Pat explicitly asked for (checklist for the final review)

- [ ] Themes that make the product pop, in the base package; glass/default-dark/default-light may be kept, replaced or enhanced.
- [ ] Unique widget appearances, not only recolours.
- [ ] Animated SkSL backgrounds with a strong vibe. Horizon has `gridDensity`; Nebula's stars are irregular; the old "Silk" is replaced by **Warp Gate**.
- [ ] Obsidian does not use the earlier font (Bodoni Moda replaced it).
- [ ] A knob for a theme's default *static* background (used when no animated background is chosen).
- [ ] Theme-specific icon sets (weather icons etc.).
- [ ] A "pairs well with" construct for the config editor.
- [ ] Deep documentation with the same verbosity as an OpenSpec change, so the spec can be generated from it.
