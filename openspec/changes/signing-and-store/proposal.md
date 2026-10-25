## Why

An unsigned download meets SmartScreen's warning, which is a poor first impression for a project whose point is
trust, and the Microsoft Store refuses an installer whose files are not signed (Store policy 10.2.9). SignPath
Foundation signs open source projects for free, but only a project that already has a release in the form to be
signed. So this change follows the first unsigned release (`release-pipeline`, 0.1): signing is added to the release
workflow, the site gains the pages SignPath and the Store ask for, the site's download control (live since `site-download-link`) drops its unsigned-release note, and the same
signed `Setup.exe` is listed in the Microsoft Store. The decisions are in backlog item 12 (2026-10-24).

## What Changes

- **Signing in the release workflow.** Between publishing and packing, the first-party binaries are signed through
  SignPath Foundation's GitHub integration (origin verification, an approval by the owner in SignPath for every
  request). The installer, the updater and the stub that Velopack produces are signed too. Before the draft release is
  created, the workflow checks that every PE file in the release carries a valid, timestamped signature, and fails if
  one does not. Signing requests come only from the release workflow on `main`.
- **A spike first:** which files in the published folder arrive unsigned from their vendors, and how Velopack's
  generated executables get signed through an asynchronous service (a sign command that waits for SignPath, or a
  second signing request after packing).
- **The site:**
  - A code-signing policy page (what is signed, by whom, the roles, the approval rule, SignPath's attribution).
  - A privacy statement for the app: every request it makes (the weather provider to Open-Meteo, site icons from the
    sites a shortcut names, the daily update check to GitHub) and that it sends nothing else. The milestone 6 statement
    starts here.
  - The landing page's download control (already offering the latest stable release's `Setup.exe`, see
    `site-download-link`) loses its SmartScreen note once releases are signed, and gains the Store listing once it is live.
- **The Microsoft Store listing.** An "EXE or MSI app" in Partner Center that points at the versioned GitHub address of
  the latest stable release's `Setup.exe` with `--silent` as its install parameter, a listing made from the site's
  media, the privacy statement's address, and certification notes (self-updating under 10.2.9, no driver). Each stable
  release is submitted with its new address; prereleases never are. A release that was submitted is never deleted or
  replaced.

## Capabilities

### New Capabilities
- `code-signing`: what is signed and how: the files, the certificate's chain and timestamp, the only workflow that may
  request a signature, the approval, and the check before a release is drafted.
- `store-listing`: the Microsoft Store listing: what it points at, the silent install, which releases are submitted,
  the certification notes, and the rule that a submitted installer address never changes.

### Modified Capabilities
- `website`: "Download State" (the control offers the signed installer and the Store listing). Added: the code-signing
  policy page and the app's privacy statement.

## Impact

- **CI:** `.github/workflows/release.yml` (the signing steps, the signature check), SignPath's GitHub action, and the
  repository secrets and variables SignPath needs (an API token as a secret; organization, project and policy ids as
  variables).
- **Scripts:** `scripts/Pack-Release.ps1` (signing hook), a signature check script.
- **Site:** two new pages, the landing page's download control, the footer links.
- **Outside the repository:** the SignPath Foundation application and project setup, the Partner Center app
  reservation, listing and submissions (by hand, by the owner).
- **Docs:** README (the SmartScreen paragraph goes, "Signed by SignPath Foundation" and the Store link come),
  `CONTRIBUTING.md` (approving a signing request, submitting to the Store), `docs/BACKLOG.md`, `docs/ROADMAP.md`.
- **Not in this change:** the sensor helper and its second Store submission (it discloses PawnIO under policy 10.2.4),
  the Partner Center submission API, winget, an MSIX package, Azure Artifact Signing (the fallback if SignPath
  declines).
