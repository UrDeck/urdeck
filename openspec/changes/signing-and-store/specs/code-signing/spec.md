## Purpose

Defines how UrDeck's releases are signed, so Windows and the Microsoft Store can tell where the files come from: which
files are signed, by what, from where a signature may be requested, and the check that stops an unsigned release.

## ADDED Requirements

### Requirement: Signed Files
Every PE file (`.exe` and `.dll`) that a release installs or ships MUST carry a valid Authenticode signature with a
timestamp, from a certificate that chains to a certificate authority in the Microsoft Trusted Root Program:

- The files built from this repository (the host, the engine, the first-party plugins, the SDK as shipped in the
  app) and the installer, updater and launcher stub that the release tooling generates are signed with the release's
  certificate
- A third-party file keeps the signature its vendor gave it. A third-party file that arrives unsigned is signed with
  the release's certificate, provided the signing service's terms allow it; otherwise the release cannot be made and
  the file is named
- This holds for the files in the installer, in the portable zip and in the update packages alike
- The signature carries a timestamp, so it stays valid after the certificate expires

#### Scenario: Installed files are signed
- **WHEN** a signed release is installed and every `.exe` and `.dll` under the application folder is checked
- **THEN** each has a valid signature that chains to a trusted root and has a timestamp

#### Scenario: Installer is signed
- **WHEN** `UrDeck-win-Setup.exe` of a signed release is downloaded with a browser
- **THEN** its properties show a valid digital signature, and SmartScreen does not call the publisher unknown

### Requirement: Where Signatures Come From
A signature MUST be requested only by the release workflow (see release-process, "Release Workflow") running on `main` on a
GitHub-hosted runner:

- The signing service verifies that the request comes from that workflow, repository and branch, and signs only files
  that the run built
- Every signing request needs the owner's approval in the signing service before it is signed
- No signing key or certificate is stored in the repository, its secrets or a runner. The repository holds only the
  credential to submit a request
- A workflow run from another branch, a fork or a pull request cannot request a signature

#### Scenario: Release run
- **WHEN** the release workflow runs on `main` and the owner approves the request
- **THEN** the files are signed and the run continues to the draft release

#### Scenario: Request not approved
- **WHEN** the owner rejects the request, or does not approve it before the run's time limit
- **THEN** the run fails and no draft release is created

#### Scenario: Other branch
- **WHEN** the release workflow is started on a branch other than `main`
- **THEN** it stops before requesting a signature

### Requirement: Signature Check Before Drafting
The release workflow MUST check every PE file of the release before it creates the draft release:

- It checks the published folder after signing, and the installer, the portable zip's content and the update packages'
  content after packing
- A file without a valid, timestamped signature that chains to a trusted root fails the run, and the log names every
  such file
- The check is a script that can be run on a local folder too

#### Scenario: Unsigned file
- **WHEN** a release's published folder holds a `.dll` without a signature
- **THEN** the run fails before the draft release, and the log names that file

### Requirement: Code Signing Policy
The project MUST publish its code signing policy on the website (see website, "Code Signing Policy Page") before the
first signed release, and keep it true:

- It names what is signed, the certificate's holder and the signing service, the people with each role (author,
  reviewer, approver) and the rule that every request is approved by the owner
- It links the app's privacy statement (see website, "App Privacy Statement")
- A change to who can approve, or to what is signed, updates the page in the same pull request

#### Scenario: Policy present
- **WHEN** the first signed release is published
- **THEN** `urdeck.app` has the code signing policy page, and the README and the release notes link it
