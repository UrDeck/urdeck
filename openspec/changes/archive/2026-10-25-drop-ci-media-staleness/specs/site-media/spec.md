## MODIFIED Requirements

### Requirement: Staleness Report
`npm run media -- --status` MUST print, without rendering, how many files of the matrix are missing, stale and
unneeded, and list them. The CI of a pull request MUST NOT run it: the check is slow, and media are refreshed by the
refresh workflow, not per pull request.

#### Scenario: A pull request that changes a theme
- **WHEN** a pull request changes Paper's colours
- **THEN** its CI does not report Paper's files as stale, and the checks pass

#### Scenario: Asking by hand
- **WHEN** a contributor runs `npm run media -- --status`
- **THEN** it lists the missing, stale and unneeded files and renders nothing
