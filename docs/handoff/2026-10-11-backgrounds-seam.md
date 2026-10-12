# Handoff: the diagonal seam under translucent cards (backgrounds)

**For:** an Opus session investigating one defect, in an isolated worktree off `feat/backgrounds`.
**Then:** the OpenSpec apply of `backgrounds` resumes (`/opsx:apply backgrounds`) with your findings.
**Date:** 2026-10-11. State: `feat/backgrounds`, everything committed; the change is 50 of 53 tasks done.

## Resolved 2026-10-12: resume the apply

**The seam is fixed on `feat/backgrounds`; `/opsx:apply backgrounds` can resume.** The rest of this document is the
original brief, kept for the record. Its claim that the line is "the diagonal of the surface's quad" turned out wrong.

### The fix

The deck window now has square corners: `WindowCorners.Square(hwnd)` (`src/UrDeck.Host/WindowCorners.cs`, called in
`MainWindow` right after `WindowFocus.Apply`) sets `DWMWA_WINDOW_CORNER_PREFERENCE` to `DWMWCP_DONOTROUND`. Nothing else
changed: the surface is still the `SKSwapChainPanel`, the layering and the render scale are as designed, so **no design
decision changes and the cost numbers of the spike still hold** (nothing was re-measured, nothing needed to be).
The log says `Window corners are square`, or `Window corners left to Windows (0x...)` where the call fails (expected on
Windows 10, which was not tested).

### Root cause (what was observed)

- The line is the diagonal of the **window**, not of the panel: a half-height panel and a panel twice the window's size
  both left it on the window's diagonal, and a resized window moved it to the new window diagonal.
- A seam pixel is the card blended **three times** over the background instead of once: with the glass fill (22% white)
  the neighbour (73,82,126) blended twice more gives (144,150,177); measured (143,149,176).
- A `SwapChainPanel` is external content: Windows draws the swap chain below the XAML output, which has a hole cut in it.
  Over-blending the XAML output is invisible where it is opaque or fully transparent, and shows only where it is
  translucent over the swap chain. Hence: only with a moving background, only under translucent cards.
- The trigger is the rounded-corner clip Windows 11 applies to a top-level window. Of six window-level switches tried one
  at a time, only square corners removed the seam; no brushes on `_root` and `_background`, `Opacity` 0.999 on the pages
  layer, `DWMNCRP_DISABLED`, a popup style and `WS_EX_LAYERED` each left about 566.
- Not established: why the corner clip makes the desktop compositor blend three times along the diagonal (inside DWM).
  Only the Y70 on the RTX 4090 was tested; no other display, GPU or scaling.

### Measured (glass, julia, `seam.py`)

| Setup | Seam pixels |
|---|---|
| Before, `scale` 0.5 | 566 |
| After, `scale` 0.5 | 3 |
| After, `scale` 1 | 3 |

A contrast-raised before and after crop was looked at: the line is gone. Where the 3 remaining detector hits are was not
looked at. Build 0 warnings, `dotnet test urdeck.slnx -c Release` passes, `dotnet format --verify-no-changes` is clean.

### Left for the apply session

- Add the gotcha to `AGENTS.md` (the background bullet): the deck window must keep square corners while a swap chain sits
  under translucent content; rounded corners bring the diagonal line back. Mention it in `docs/backgrounds.md` if the
  design notes list known pitfalls.
- No task in `openspec/changes/backgrounds/tasks.md` was ticked and the change was not archived. Still open: 9.4, 9.5,
  10.6, as below.
- `docs/handoff/backgrounds-seam/` (capture script and detector) can go once the change is archived.

## The defect

With a **moving** background (an `SKSwapChainPanel`) and **translucent cards** over it, a one-pixel dotted line runs along
the diagonal of the whole panel, from the top-left corner down (slope 1100/3840 = 0.2865 on the 1100x3840 panel; the
diagonal of the surface's quad). It is clearly visible on the `glass` theme (card fill `#38ffffff`, 22% white) as a bright
dotted hairline through the clock, stats and weather cards. The owner calls it "a pretty ugly looking defect" and wants it
fixed, not documented.

### What was measured (all on the real host, Y70 panel, 100% scaling, glass theme, julia shader)

Capture and detector are in `docs/handoff/backgrounds-seam/` (`capture-panel.ps1` with Windows PowerShell 5.1, and
`seam.py <png>`, which counts pixels on the diagonal that differ from both neighbours; about 570 means the line is there,
0 or 1 means it is not).

| Setup | Seam pixels |
|---|---|
| No `background` | 1 (none) |
| Still shader (`speed: 0`, so an `SKXamlCanvas`) | 1 (none) |
| Moving shader, `scale` 0.5 (surface 550x1920, composition visual scaled 2x) | ~575 |
| Moving shader, `scale` 1 (surface 1100x3840) | ~575 |
| Same, theme `default-dark` or `default-light` (cards 70% opaque) | 0 detected (not looked at closely) |

The line is **only where cards are**: it is absent on the bare background below the cards and in the gaps between cards.
A seam pixel under a glass card was (147,153,180) against (78,87,132) around it, so brighter, and not the theme colour.

### What was tried and did NOT change it (each ~570 seam pixels afterwards)

- `element.Opacity = 0.999` on the panel.
- `DrawInBackground = true` on the panel.
- An `InsetClip` on the panel's composition visual.
- Not setting the visual's `Scale` at all (scale 1 case).
- A solid red brush on the `_background` grid under the panel: the seam is not red, so it is not a hole through to the
  grid's brush (XAML may cull what is under an opaque swap chain; that was not confirmed).

Not tried: another surface type, a separate `SwapChainPanel` with Skia/ANGLE set up by hand, a GPU surface read back into
an `SKXamlCanvas`, a different order or grouping of layers, `CacheMode`, `ContentsScale`, the Windows App SDK or
SkiaSharp.Views.WinUI version (4.153.1 and Windows App SDK 2.5.1 now), a hardware overlay or MPO angle (`DXGI` flip model
and DWM direct flip), forcing the panel's pixels to alpha 1 in a way that changes the swap chain's alpha mode.

## Where the code is

- `src/UrDeck.Host/BackgroundLayer.cs`: creates the `SKSwapChainPanel` (`EnableRenderLoop = false`), sizes it to the screen
  times `scale`, scales its composition visual back up (`ElementCompositionPreview.GetElementVisual(el).Scale`), and paces
  frames from `CompositionTarget.Rendering` (30 per second). A still background uses an `SKXamlCanvas` instead, and has no
  seam.
- `src/UrDeck.Host/MainWindow.cs`: the layer stack is `_root` > `_background` (a `Grid` with the theme colour brush, holds the
  background element) > `_surface` (the pager's pages, one `SKXamlCanvas` per widget, translucent) > `_overlay`.
- `docs/perf/backgrounds-spike.md` has the measurements that chose this surface, and why the alternatives lost.
- `AGENTS.md` has the render-scale rule (never a `RenderTransform` or `Viewbox`, they make the panel render at full size).

## Constraints a fix must respect

- **Cost budget** (`docs/backgrounds.md`, design decision 14): a moving built-in at the default scale at most 5% of one
  core, 2% GPU 3D, 60 MB private, 60 MB dedicated video memory over the same page without a background. Spike numbers for
  the current approach: about 3% of one core, 0.5 to 0.9% GPU, +25 MB. The CPU rasteriser for the shader costs about half a
  core at quarter resolution, so it is not an option for the live layer (it is what `--snapshot` uses).
- Frames only while the deck is shown and the display is on; nothing at all without a background; one frame source.
- The background must never take pointer input, and pages must slide over it while it keeps moving.
- The render scale must stay a setting (`scale`, 0.1 to 1, default 0.5) and the user must never see a resolution.
- The engine stays testable without a window: `DeckBackground.Paint(SKCanvas, w, h, seconds)` is the only painter, so any
  surface can host it. Keep it that way.
- Host code stays in `UrDeck.Host`; do not touch the SDK.

## What to deliver

1. **A root cause**, or if there is none to be had, a precise statement of what is known (which compositor path draws the
   seam and why only under translucent cards).
2. **A fix** in `BackgroundLayer` (or a replacement surface) that brings the seam count to about 0 with `seam.py` on glass,
   at `scale` 0.5 and 1, with the cost within the budget above. If the fix is a different surface, measure it with the method
   of `docs/perf/nexus-baseline.md` (CPU from `TotalProcessorTime`, GPU Engine and GPU Process Memory counters) and say the
   numbers. Report a case over budget; do not tune silently.
3. Leave the tree building with 0 warnings and `dotnet test urdeck.slnx -c Release` passing. Do not tick tasks in
   `openspec/changes/backgrounds/tasks.md` and do not archive the change; the resumed apply session does that.
4. If the fix changes a design decision (surface type, layering), say which and why so the design and `AGENTS.md` can be
   updated.

## How to run it

```powershell
dotnet build src/UrDeck.Host -c Release
# Work on a copy of the output so the tracked sample config is not edited:
Copy-Item src/UrDeck.Host/bin/Release/net10.0-windows10.0.19041.0 $env:TEMP\urdeck-seam -Recurse
# In the copy's urdeck-config.json set "theme": "glass" and "background": { "type": "shader", "name": "julia", "speed": 3 }
Start-Process $env:TEMP\urdeck-seam\UrDeck.Host.exe -WorkingDirectory $env:TEMP\urdeck-seam   # blocks the shell otherwise
powershell.exe -NoProfile -File docs\handoff\backgrounds-seam\capture-panel.ps1 $env:TEMP\seam.png   # 5.1, not pwsh
python docs\handoff\backgrounds-seam\seam.py $env:TEMP\seam.png
& $env:TEMP\urdeck-seam\UrDeck.Host.exe --quit
```

The capture script copies the Y70's bounds (1100x3840 at 3840,-3573); change them if the layout differs. The host places its
window on the panel, over any other UrDeck that is running there. `urdeck.log` next to the exe logs `Background: shader ...,
surface WxH, scale S, moving` and the frame start and stop lines. One capture per configuration is enough; the owner prefers
a quick look to long pixel loops, so look at a crop with the contrast raised before counting.

## Other things done in this session worth knowing

- `default-dark` card fill is now `#b31a1a2e` (70% opaque) and `default-light` is `#b3ffffff` with a stronger palette; the
  julia shader uses a smooth escape time. These are in the commit and in `docs/themes.md`.
- Still open in the change besides this defect: 9.4 (cost table, owner), 9.5 (what `FrameClock` delivers), 10.6 (archive).
