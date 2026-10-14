# 10 Decisions, risks and open questions

Each decision has a **recommendation** so the agent is never blocked; Pat can overrule any of them. Record the final choice in the relevant OpenSpec `design.md`.

## Decisions

| ID | Question | Recommendation | Why / consequence if different |
|---|---|---|---|
| D1 | Real backdrop blur for Prism (`card.blur`)? | **No for now.** Reserve the key, ship Prism "frostless" (translucent gradient + highlight + sheen). Revisit with a GPU-compositor acrylic/backdrop brush. | Blur needs the pixels *behind* each card; cards are separate surfaces over a swap chain. A per-card CPU blur of a background crop would be costly and would need the shader frame readback. Prism still looks good without it. |
| D2 | Version the theme format? | **Optional `format: 2`**, one `ThemeDefinition` class, unknown keys ignored. | A hard version gate would break old themes or old hosts needlessly. |
| D3 | Where does pairing live? | **Both sides** (`meta.pairsWith.backgrounds`, manifest `pairsWith.themes`), symmetric closure in the engine. | One side only would force edits to built-ins whenever a user adds a background. |
| D4 | Animation budget and cut order | Budget in `03 §5`. If exceeded cut in this order: pattern scroll, value-fill animation, animated border (becomes static), then reduce frame rate. | Keeps the deck cheap on the secondary-display CPU budget. |
| D5 | Replace `glass` with Prism? | **Keep glass.** Prism is the grown-up variant. | Continuity for users and the site; one-line change later. |
| D6 | Embed fonts or download on first use? | **Embed** (variable fonts, OFL, licences shipped). | Offline-first app; first-run network calls would be a surprise. Installer grows a few MB. Reconsider if > 10 MB. |
| D7 | Meaning of an absent `background` in config | **Theme default applies** (if the theme has one); add `{"type":"none"}` for plain colour. | Required for "pick a theme, get its background". Existing users unaffected because the old built-ins define none. |
| D8 | Icon format | **Stroke-only SVG paths on a 24 grid** parsed to `SKPath`. | No new dependency, themeable by stroke/tint, tiny files, safe to validate. Not for multicolour icons (that is Lottie/PNG territory). |
| D9 | Icon art licence | **Original or CC0/MIT** only; each set carries a LICENSE. Do not copy Lucide/Feather/Material verbatim without checking attribution requirements (Lucide is ISC, Feather MIT; both fine with notices). | Keep `THIRD-PARTY-NOTICES.md` honest. |
| D10 | Should `card.compact` override gauge/typography too? | **No** in this iteration; chrome only. | Keeps the compact path small; revisit if 1x1 stats look wrong. |
| D11 | Names and trademarks | Ship **"Warp Gate"** with no mention of Stargate or Star Trek in shipped text. Run a quick trademark/name search on **Rigline, Prism, Obsidian, Carbon** before the site announces them; they are common words but "Prism"/"Obsidian"/"Carbon" are crowded product names. | Cheap insurance; renaming a theme after release is a breaking change for users. |
| D12 | Theme `mode` vs OS light/dark | `meta.mode` is informational (editor grouping). No automatic switching in this work. | Auto-switch is a separate feature. |
| D13 | Audio-reactive backgrounds | **Out of scope.** Equalizer is free-running. A later `iAudio` host uniform is a separate spec. | Needs a capture pipeline and privacy review. |
| D14 | Unknown keys in a theme | Silent at load (as v1), warned by `theme validate` with suggestions. | Do not change load-time behaviour of old themes. |

## Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Animated chrome busts the CPU/memory budget (Rigline) | Medium | High | S2, budgets, `motion` ceiling, ship static border if needed |
| R2 | Underlay interacts badly with page slide / press effect (S1) | Medium | Medium | spike first; option A fallback |
| R3 | Font licences or installer size | Low | Medium | all OFL; ship licences; S3; variable fonts |
| R4 | CPU-validated shaders look different on the GPU | Medium | Low-Med | panel check per shader in PR 6/7; fix per findings |
| R5 | Nebula/Shapes too heavy | Medium | Medium | measure; lower layers/count defaults; `scale` |
| R6 | Schema sprawl: about 160 new keys is a lot to document and maintain | Medium | Medium | generated docs; the no-drift test; themes stay small because every key has a default |
| R7 | SDK API growth breaks third-party widgets | Low | High | additive only; API compatibility check in CI; compact handled by the host |
| R8 | Hairlines blur at some resolutions | Medium | Low | pixel snapping rule in `03 §3`; log when < 1 px |
| R9 | Contrast failures in light/bold themes over busy backgrounds | Medium | Low | validator contrast check; `background.scrim` |
| R10 | Icon art volume | Medium | Low | fallbacks guarantee nothing breaks; art is incremental |
| R11 | Scope creep into the editor | Medium | Medium | PR 18 is seams only |
| R12 | Windows-only host means most visual checks cannot run on CI | Known | Medium | goldens via the cross-platform `PageRenderer`; panel checks are manual and recorded |

## Open questions for Pat (non-blocking; defaults chosen above)

1. Is `background.default` the right name, or `background.selects`? (Keep `default`; it matches the editor's "Theme default".)
2. Should user themes be allowed to ship **fonts** with unknown licences? Today yes (their business). The validator only *warns* (THEME021).
3. Should the built-in `outline` icon set be the visual default for `theme:` glyphs in docs and decks? (Yes.)
4. Do you want a "Pairs with" **badge on the site** for each background? (Proposed in `08`.)
