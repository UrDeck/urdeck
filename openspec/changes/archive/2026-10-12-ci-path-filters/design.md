## Context

`ci.yml` has three jobs: `build` (Windows: restore, format, build, test, coverage floor, `urdeck-cli` smoke test, pack,
OpenSpec validation), `test-linux` and `pr-title`. The ruleset "main protection" requires `build` and `pr-title` and
requires the branch to be up to date. The workflow has no path filter. See `proposal.md` for the motivation.

Two facts about GitHub shape the design:

- A workflow skipped by a workflow-level `paths:` filter reports no status. A required check that never reports stays
  pending, so the pull request can never merge.
- A job skipped by a job-level `if:` reports "skipped", and a required check that was skipped counts as passed.

## Goals / Non-Goals

**Goals:**

- A pull request runs only the jobs its changes can affect.
- No change can merge with a check that should have run and did not.
- A new folder or file type is checked by default, without anybody remembering to add it to a list.

**Non-Goals:**

- `security.yml` (dependency review, OSV scan, CodeQL). CodeQL builds the solution as well; filtering it is a possible
  later change.
- Removing the `urdeck-cli` smoke test that `build` and `test-linux` both run. It is there to prove the tool on both
  systems.
- The `site` job. `landing-site` adds it, and its filter group, to the structure made here.
- Build caching or any other speed-up of a job that does run.

## Decisions

### 1. Job-level gating behind a `changes` job, not workflow `paths:`

`changes` runs first on `ubuntu-latest` and sets the outputs `code` and `openspec`. The other jobs have `needs: changes`
and `if: needs.changes.outputs.<area> == 'true'`.

*Rejected:* workflow-level `paths:` or `paths-ignore:`. A filtered-out workflow reports nothing and the required check
blocks the pull request (see Context). *Rejected:* one workflow per area, each with `paths:`: the same problem for
whichever one holds the required check.

### 2. `dorny/paths-filter`, pinned to a commit

It handles the pull request and push events, the first push of a branch and a shallow checkout. It is pinned to a full
commit SHA with the version in a comment, as a third-party action that runs with the repository's token should be;
Dependabot's `github-actions` entry keeps the pin current.

*Rejected:* a shell step with `git diff --name-only`. It needs the merge base (a deeper fetch), separate handling per
event and its own glob matching: more code to get wrong than the dependency it avoids.

### 3. `code` is a deny-list

`code` is true when at least one changed file is **not** in this list:

- `*.md` at the repository root
- `docs/**`, except `docs/schemas/**` (`BackgroundPackageTests` reads `docs/schemas/v1/shader-manifest.json`)
- `openspec/**`
- `site/**`
- `.github/**`, except `.github/workflows/ci.yml`
- `LICENSE`, `PLUGIN-EXCEPTION.md`, `THIRD-PARTY-NOTICES.md`

Everything else, a folder that does not exist yet included, counts as code.

*Rejected:* an allow-list (`sdk/**`, `src/**`, ...). A new top-level project that nobody added to the list would merge
unbuilt, with a green check. With a deny-list the mistake that can happen is a build that was not needed.

Before the list is final, search the tests and project files once more for reads of `docs/` or root Markdown (today the
schema is the only one).

### 4. OpenSpec validation as its own Linux job

`openspec` runs `npx --yes @fission-ai/openspec@1.7.0 validate --all --strict` on `ubuntu-latest` when `openspec/**`
changed. `build` loses the step and its `setup-node`.

A change that touches code and specs runs both jobs, as today. A change that touches only specs no longer builds on
Windows.

### 5. `ci-ok` is the one required check

`ci-ok` has `needs: [changes, build, test-linux, openspec]` and `if: always()`. It fails when any needed job's result is
`failure` or `cancelled`, and passes when each is `success` or `skipped`.

Without it, requiring `build` directly has a hole: if `changes` itself fails, `build` is skipped, skipped counts as
passed, and the pull request is green with nothing checked. `ci-ok` sees the failed `changes` and fails.

It also makes `test-linux` and `openspec` required in effect. Today neither is: a pull request can merge with the Linux
tests red.

*Rejected:* requiring every job by name in the ruleset. Each new job (the `site` job next) would need a ruleset edit by
the owner; with `ci-ok` a new job is added to `needs` in the same pull request that adds it.

### 6. Which events are filtered

- `pull_request` and `push` to `main`: filtered.
- `workflow_dispatch`: everything runs (`changes` sets every output to `true`), so there is a way to run it all by hand.

`edited` stays in the pull request event types (a title edit must re-run `pr-title`). It re-runs `changes` too; the
filter answer is the same, and the concurrency group cancels the older run.

## Risks / Trade-offs

- [The deny-list excludes a path that does affect the build] → Decision 3's search before the list is final; the push
  to `main` after a merge is filtered the same way, so add a note in `CONTRIBUTING.md` that `workflow_dispatch` runs
  everything, and run it once after this change merges.
- [A job added later is left out of `ci-ok`'s `needs`] → A comment at `ci-ok` saying every job except `pr-title` belongs
  there; `landing-site` is the first case and its tasks name the step.
- [The filter action is compromised upstream] → Pinned to a commit; the `changes` job has `contents: read` and
  `pull-requests: read` only.
- [A Markdown-only pull request now merges without a build, and a root `.md` is packed somewhere] → Checked: no project
  packs or embeds a root Markdown file today. Decision 3's search covers it.

## Migration Plan

1. The pull request changes `ci.yml`, so `code` is true for it and `build` runs: it merges under the current ruleset.
2. **(owner)** After the merge, edit the ruleset "main protection": remove the required check `build`, add `ci-ok`,
   keep `pr-title`.
3. Between 1 and 2 nothing is blocked: where `build` is skipped it counts as passed.
4. Open pull requests pick the new workflow up when they are updated from `main`, which the ruleset already demands.

Rollback: revert the pull request and put `build` back as the required check.
