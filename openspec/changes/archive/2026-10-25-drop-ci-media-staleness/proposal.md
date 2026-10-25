## Why

The `site` job in CI ran `npm run media -- --status` on every pull request that touched the product or the site. With .NET on
the runner the report builds `urdeck-cli` and renders probe frames for every entry, which took about 26 minutes, for a
non-blocking notice nobody acts on. Media are refreshed by hand through the *Site media* workflow, and later on tag pushes,
so a per-pull-request report earns nothing.

## What Changes

- The `site` job no longer runs the staleness report; it keeps `npm run build` and `npm test`.
- `npm run media -- --status` stays: anyone, and the *Site media* workflow's own pre-check, can still ask what is missing,
  stale or unneeded.
- `CONTRIBUTING.md` and `site/README.md` stop saying that CI reports staleness.
- **BREAKING** (process): a pull request no longer shows whether it made media stale. Regeneration is started by hand and,
  in a later change, by release tags.

## Capabilities

### New Capabilities

### Modified Capabilities

- `site-media`: "Staleness Report" keeps the `--status` command and drops the requirement that CI runs it and shows the counts.

## Impact

- `.github/workflows/ci.yml` (the `site` job), `CONTRIBUTING.md`, `site/README.md`.
- `openspec/specs/site-media/spec.md`.
- No product code, no change to `site-media.yml`.
