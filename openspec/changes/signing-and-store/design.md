## Context

`release-pipeline` makes unsigned releases: a `workflow_dispatch` run that publishes the host, packs it with Velopack
(`scripts/Pack-Release.ps1`), and creates a draft GitHub release. It leaves one seam for signing, between publish and
pack. Velopack generates three executables of its own (`Setup.exe`, `Update.exe` inside the packages, and the root
launcher stub) and signs them during `vpk pack` through `--signTemplate`, a command it runs per file or for a batch.
SignPath Foundation signs through its own service. A GitHub Actions step uploads a workflow artifact, submits a signing
request, and waits for the signed artifact; the service checks the request's origin (repository, branch, workflow,
GitHub-hosted runner), and a policy can require a manual approval. Microsoft Store policy 10.2.9 lists an EXE by a
versioned HTTPS address and wants the installer and all its PE files signed, a silent install and an unchanging binary
at that address. The website's "Download State" (made by `site-download-link`) already offers the latest stable release's installer, with a note that unsigned releases may meet SmartScreen.

## Goals / Non-Goals

**Goals:**
- One approval by the owner per release signs everything in it.
- A release that is not completely signed cannot be drafted.
- Store submissions are a short manual routine, written down, that reuses the GitHub release.

**Non-Goals:**
- Automating Partner Center submissions (its API), or signing anything outside the release workflow (local builds,
  pull requests, the CLI tool).
- The sensor helper's Store submission and its PawnIO disclosure (policy 10.2.4); that change adds a signed helper to
  the same workflow.

## Decisions

**1. A spike decides how Velopack's executables are signed.** Two shapes satisfy the specs:
- **A, sign during pack:** `--signTemplate` calls a script with `{{file...}}`. The script submits one SignPath request
  for the batch through SignPath's API and waits. This needs SignPath's origin verification to accept a request made
  from a script in the workflow rather than from its action.
- **B, sign twice:** sign the published folder with SignPath's action before packing. Then pack with Velopack's
  generated executables left unsigned, sign `Setup.exe`, `Update.exe` and the stub with a second request, and repack.
  This needs Velopack to accept pre-signed files for those three (its documentation of signing outside `vpk` decides
  whether repacking is possible) and costs a second approval.
The spike tries A first, because one approval per release is a goal, and falls back to B. It writes down which one
works in this design before the workflow is changed. Either way the signature check (decision 3) is the same.

**2. Inventory before signing.** The spike also lists every PE file in the published folder with its signature state.
Microsoft's runtime and Windows App SDK files and SkiaSharp's are expected to be signed by their vendors, while Velopack's
managed DLL and any small dependency may not be. The SignPath project's artifact configuration then names exactly the
first-party files plus the unsigned third-party ones, and asks SignPath before including any third-party file (code-signing,
"Signed Files").

**3. The signature check is a PowerShell script over folders and packages.** `scripts/Test-ReleaseSignatures.ps1` takes
folders and `.nupkg`/`.zip` files, extracts the archives to a temporary folder, and runs `Get-AuthenticodeSignature` on
every `.exe` and `.dll`. It requires `Status = Valid` and a timestamp, and fails listing the rest. The workflow runs it
after signing the published folder and again on the packed output. It is a separate script so the owner can run it on
a downloaded release.

**4. Approval is SignPath's manual approval on the release policy,** with the owner as the only approver. The
workflow's wait has a time limit (an hour), after which the run fails without drafting. Secrets: one SignPath API token
(`SIGNPATH_API_TOKEN`), stored as an environment secret of a `release` GitHub environment that only `main` can use. The
organization, project and policy slugs are variables.

**5. The site's download control uses GitHub's latest-release address,**
`https://github.com/UrDeck/urdeck/releases/latest/download/UrDeck-win-Setup.exe`. It always serves the latest stable
release, because prereleases are never "latest", and needs no site rebuild per release. `site-download-link` already put it
on the page; after the first signed release is published, one pull request removes the SmartScreen note and adds the Store link. The links are ordinary links the visitor
follows, so website "Visitor Privacy" holds.

**6. Store submissions stay manual.** Partner Center's form takes the versioned address and the `--silent` parameter.
A checklist in `CONTRIBUTING.md` covers each stable release: copy the address from the published release, update the
package URL, keep the notes, submit. The first submission also needs the listing text and pictures (from the site's
media), the age rating questionnaire, and the privacy address.
*Alternative:* the Partner Center submission API from the release workflow. It is worth it only once releases are
frequent, and it would put Partner Center credentials in the repository's secrets.

**7. Releases submitted to the Store are permanent.** GitHub lets a maintainer replace or delete release assets, and
the Store requires the binary at a submitted address never to change. The rule lives in `CONTRIBUTING.md` and in the
code-signing policy page. The release workflow already refuses to reuse a version.

## Risks / Trade-offs

- **SignPath Foundation declines or takes long.** → Releases stay unsigned on GitHub (0.1 path), and the site keeps
  "coming soon". The fallback is Azure Artifact Signing ($9.99 a month, available to the owner as an individual in the
  US), which fits shape A directly through Velopack's documented `--signTemplate` support. Only the signing script
  changes.
- **The certificate names SignPath Foundation, not the owner.** → The code-signing policy page explains it, and the
  Store listing's publisher name is the owner's Partner Center account either way.
- **SmartScreen reputation still has to build** even with a valid signature, since no certificate grants it at once
  any more. → The README keeps a short note for the first signed releases.
- **The Store rejects the submission** (silent install, the self-updater, a missing privacy address). → Each rejection
  reason is recorded, and the GitHub release is unaffected. The silent switch is tested before the first submission
  (`Setup.exe --silent` on a clean account).
- **A packaged (MSIX) build needs the `globalMediaControl` capability for the media session** that the now-playing widget's `media` provider reads
  (`docs/perf/now-playing.md`). The unpackaged installer needs nothing (proved with an unpackaged process). If the Store listing becomes an MSIX
  package, declare the capability in the manifest and expect certification to ask why. The `media` provider's assembly is large (about 25 MB, the embedded
  Windows Runtime projection) and carries Microsoft's MIT-licensed C#/WinRT support code, so it belongs in the inventory (decision 2) of what must be signed.
  - **A third-party unsigned DLL that SignPath will not sign** blocks the release. → The inventory (decision 2) finds it
  before the first signed release. The answers are to replace the dependency or ask its maintainers to sign.

## Migration Plan

1. After 0.1 is published (unsigned), apply to SignPath Foundation with the repository, the release and the draft
   policy page.
2. On acceptance: set up the SignPath project, artifact configuration and release policy; add the `release` environment
   and its secret; run the spike on a throwaway version (`0.1.1-rc.1`, deleted afterwards).
3. Publish the policy and privacy pages, then make the first signed release, then drop the SmartScreen note from the site's download control.
4. Reserve the name in Partner Center, prepare the listing, and submit the first signed stable release.
