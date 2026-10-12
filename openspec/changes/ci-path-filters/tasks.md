Read `proposal.md` and `design.md` first. Work on the branch `ci/path-filters`; one pull request, title
`ci: run only the jobs a change needs`. Run `openspec validate --all --strict` before pushing (see `AGENTS.md`).
Tasks marked **(owner)** need the owner: prepare what they need, then stop and ask.

## 1. The filter

- [x] 1.1 Search the tests, project files and build props for reads of `docs/`, root Markdown, `openspec/` or `.github/` (design decision 3). Add what is found to the exceptions; record the result in the pull request description
- [x] 1.2 Add the `changes` job to `ci.yml`: `ubuntu-latest`, permissions `contents: read` and `pull-requests: read`, `dorny/paths-filter` pinned to a commit SHA with the version in a comment, outputs `code` and `openspec` with the lists of design decisions 3 and 4
- [x] 1.3 On `workflow_dispatch`, set both outputs to `true` without running the filter (design decision 6)

## 2. The jobs

- [x] 2.1 `build` and `test-linux`: `needs: changes` and `if: needs.changes.outputs.code == 'true'`
- [x] 2.2 Move "Validate OpenSpec" out of `build` into a new `openspec` job on `ubuntu-latest` (checkout, `setup-node` 22, the same `npx` command), gated on `needs.changes.outputs.openspec`; remove `setup-node` from `build`
- [x] 2.3 Add `ci-ok`: `needs: [changes, build, test-linux, openspec]`, `if: always()`, failing when any needed result is `failure` or `cancelled` (design decision 5). A comment above it: every job except `pr-title` belongs in `needs`
- [x] 2.4 Update the comments in `ci.yml` that call `build` a required status check

## 3. Check it

- [x] 3.1 On the pull request itself: `changes` reports `code` true, `build`, `test-linux` and `ci-ok` pass
- [x] 3.2 A scratch pull request that changes only `docs/BACKLOG.md`: `build`, `test-linux` and `openspec` are skipped, `ci-ok` and `pr-title` pass. Close it without merging
- [x] 3.3 A scratch pull request that changes only a file under `openspec/`: only `openspec` runs. Close it
- [x] 3.4 A scratch pull request that changes `docs/schemas/v1/shader-manifest.json`: `build` runs. Close it
- [x] 3.5 A scratch pull request with a deliberately failing test: `ci-ok` fails. Close it

## 4. Documents and the ruleset

- [x] 4.1 `CONTRIBUTING.md`: the required checks are `ci-ok` and `pr-title`; a short table of what runs for which change; `workflow_dispatch` runs everything
- [x] 4.2 `AGENTS.md`: replace "runs all of the above on `windows-latest`" with what runs when, and keep "run them locally before pushing"
- [ ] 4.3 **(owner)** After the merge, edit the ruleset "main protection": required checks `ci-ok` and `pr-title`, `build` removed (design, Migration Plan)
- [ ] 4.4 After 4.3, run the workflow once by hand (`workflow_dispatch`) on `main` and confirm every job runs and passes
- [x] 4.5 `docs/BACKLOG.md`: note the change under item 1 ("Git workflow and GitHub maintenance")
