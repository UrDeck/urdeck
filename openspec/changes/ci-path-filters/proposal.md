## Why

Every pull request runs the full Windows build, the Linux tests and the OpenSpec validation, whatever it changed: a typo
in `docs/BACKLOG.md` waits for the same jobs as an engine change. A website is about to join the repository
(`landing-site`), which adds a class of pull requests that touch no .NET code at all. The checks should follow what a
pull request changed.

## What Changes

- A first job, `changes`, works out which areas a pull request or push touched and publishes one output per area.
- `build` and `test-linux` run only when code changed. Code is everything except a short list of paths known not to
  affect the build, so a new top-level folder is built by default.
- OpenSpec validation leaves the Windows `build` job and becomes its own Linux job, `openspec`, which runs only when
  `openspec/` changed.
- A last job, `ci-ok`, always runs and fails when any job it waits for failed or was cancelled. It becomes the required
  status check in place of `build`. **(owner)** The ruleset "main protection" is edited by the owner.
- `pr-title` is unchanged and still required.
- `CONTRIBUTING.md` and `AGENTS.md` name the new required checks and say what runs when.

A pull request that changes only Markdown outside `openspec/` then runs `changes`, `pr-title` and `ci-ok`.

## Capabilities

### New Capabilities

None. This changes how the repository is checked, not what UrDeck does (`skip_specs: true`).

### Modified Capabilities

None.

## Impact

- `.github/workflows/ci.yml`: restructured.
- The ruleset "main protection" on `UrDeck/urdeck`: required checks become `ci-ok` and `pr-title`.
- `CONTRIBUTING.md` (the line naming the required checks), `AGENTS.md` (the line saying CI runs everything).
- One new third-party action in CI (the path filter), pinned to a commit.
- `.github/workflows/security.yml` is not touched.

