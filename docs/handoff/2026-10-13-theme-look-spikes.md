# Handoff: look-and-feel spikes (S1 underlay, S2 animated border, S3 fonts)

**For:** fresh sessions that run three throwaway spikes before the look-and-feel changes are specified. **Date:** 2026-10-13.
**Read first:** `AGENTS.md`, then the package at `../urdeck-look-and-feel-handoff` (add it with `/add-dir`): `README.md`,
`00-overview.md`, `03-engine-work.md` (sections 1.3 to 1.5), `09-phasing-and-tasks.md` (PR 0 and the spikes recap) and
`10-decisions-and-risks.md`.

Nothing here is built. The spikes answer three questions that decide the designs of the card-chrome, typography and
motion changes. Each spike ends in numbers and a go/no-go, **not** in shippable code.

## What the exploration already decided (do not re-litigate)

- **Chunked delivery.** Chunk 1 is the format-2 model plus backgrounds and pairing (no spikes needed); card chrome waits on S1,
  typography on S3, motion and Rigline on S2.
- **Key trimming.** Only keys a draft theme uses go into the first specs. The rest, with their definitions, are in
  `docs/design/theme-format-2-deferred-keys.md`.
- **Cost that matters is CPU and memory.** Installer size and disk do **not** matter to the owner. Do not measure or optimise
  them. Decision D6 (embed the fonts) stands whatever the size.
- **Backdrop blur (D1):** out. Prism ships frostless; `card.blur` is deferred.
- **Static background image** is kept (cover only, a file inside the theme folder); a user's `background` replaces any theme default.
- The package's numbers are first guesses: the budget to test against is in `03-engine-work.md` section 5
  (animated border, 8 cards, 30 fps: at most 5 % of a core and +8 MB; underlay: at most 20 ms to build and 2 MB; v1 themes: no regression).

## Ground rules for every spike

- **Branch `spike/theme-look-<s1|s2|s3>` off `main`.** Never commit to `main`. Never merge. Do not open a PR. Prototype code
  stays on the branch; only the notes below are meant to be read.
- Write results to `docs/perf/theme-look.md` on the branch, one section per spike (`## S1 ...`), in the style of
  `docs/perf/theme-card-paint.md`: what was measured, how, the numbers, the decision. Commit the notes; also copy the section
  into your final reply so it can be pasted into the main session.
- Measure CPU and working set the way `docs/perf/stats-gauges.md` does. Compare with the unchanged build in the same session.
- `dotnet build urdeck.slnx -c Release` must still build with 0 warnings on the branch if you want to run the host; spike code
  may skip tests and `dotnet format`.

## S3: fonts (no panel needed)

**Question.** Does each font in the package's `assets/fonts.md` load and weight correctly through the existing resolver, and
what does a typeface cost in memory?
**Do.**
1. Download the families listed in `assets/fonts.md` (Google Fonts, OFL), verify each licence file, name the files as the table says.
2. Load each through `ThemeResolver` in a throwaway test or console harness: confirm the `wght` axis drives weights 300 to 900 on
   the variable files (Bricolage, Instrument Sans, JetBrains Mono, Archivo, Bodoni Moda, Jost) and that the static files
   (Chakra Petch, Barlow Condensed, IBM Plex Mono, Archivo Black) load; render a specimen line per family at three weights to a PNG.
3. Check the glyphs `° % : · + - ↑ ↓` and digits in each font.
4. Memory: working set before and after loading all families and creating one clone per weight per family; with the
   plugin-collectible loading path, confirm typefaces are disposed when a theme is switched (no growth over 20 switches).
**Skip:** installer size, subsetting.
**Report.** A table (family, form, weights verified, missing glyphs, licence file present, MB resident per family) and any font
that needs a fallback or a static instance instead of the variable file.

## S1: page underlay below the widget surfaces (panel needed)

**Question.** Can one extra surface under all widgets draw shadows, glows, outside corner marks and a pattern without
hurting memory, the page slide, the press effect, input or z-order? (`03` section 1.3, option B.)
**Do.**
1. Prototype a full-page `SKXamlCanvas` in `PageHost` below the widget elements, `IsHitTestVisible = false`, drawing from
   the layout rects: a blurred drop shadow per card, a hard offset shadow per card, and an outside corner mark.
2. On the panel (below): working set with and without it on a 4x8 page; swipe between pages (does the underlay slide with the page,
   any tearing or lag?); tap and hold a widget (does the compositor press scale still work, and what does the shadow do?); a tap
   through the underlay still reaches the widget.
3. Rebuild cost: time to draw the underlay for 8 cards; confirm it is redrawn on layout or theme change only.
**Decision to record.** Option B go/no-go with numbers; if no-go, the cap for option A (larger widget surfaces) and why.

## S2: animated border (panel needed; run after S1, on the same panel)

**Question.** What is the cheapest way to animate a spinning gradient border on 8 cards (`03` section 1.4)?
**Do.** Try, in this order, stopping at the first that meets the budget:
1. A border as a separate visual masked or rotated by the system compositor (not Skia), over a card painted once.
2. Cached ring bitmaps for N angle steps (start with 72), blitted per frame at 15 fps and at 30 fps.
3. Baseline: repaint the whole card each frame (to show why the others are needed).
Measure CPU (percent of one core) and working set for 8 cards for 60 seconds each, plus the value fill animation
(`typography.valueFill.animate`) as a second test if time allows.
**Decision to record.** The chosen approach with numbers, or "ship Rigline's border static".

## Using the panel (S1 and S2 only)

- Build, then copy the Release output to a scratch folder so the daily install is untouched (one host per application folder).
- Launch `UrDeck.Host.exe` **detached** (it blocks the shell) and read `urdeck.log` next to it. Quit with `UrDeck.Host.exe --quit`
  after each run, and do not leave a host running when you finish.
- Screen capture: Windows PowerShell 5.1 (`powershell.exe`), `SetProcessDpiAwarenessContext(-4)`, then `Graphics.CopyFromScreen`.
  One capture per scenario at most.
- The display is the Y70 panel; see the memory notes `display-setup` for coordinates. The machine is free during the run, but
  do not edit `urdeck-config.json` under `src/`: use the copy in your scratch folder.
- Never run S1 and S2 at the same time: there is one screen.

## Prompts to paste

**S3:** "Read `docs/handoff/2026-10-13-theme-look-spikes.md` and the package in `../urdeck-look-and-feel-handoff`. Run spike S3
only, on branch `spike/theme-look-s3`. Report the table and the notes section."

**S1 then S2:** "Read `docs/handoff/2026-10-13-theme-look-spikes.md` and the package in `../urdeck-look-and-feel-handoff`. Run
spike S1, then S2, on the panel, on branches `spike/theme-look-s1` and `spike/theme-look-s2` (S2 may branch from S1). Report
the two notes sections and the go/no-go for each."
