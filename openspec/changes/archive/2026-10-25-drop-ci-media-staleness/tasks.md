## 1. CI

- [x] 1.1 Remove the `Site media staleness` step from the `site` job in `.github/workflows/ci.yml`; `Build and check` and `Test the media generator` stay
- [x] 1.2 Confirm `ci-ok` still lists every job and no other step reads the removed step's output

## 2. Documentation

- [x] 2.1 `CONTRIBUTING.md` ("Refreshing the site's media"): CI does not run `--status`
- [x] 2.2 `site/README.md` ("Refreshing"): CI does not check for stale media

## 3. Verify

- [x] 3.1 `openspec validate --all --strict` passes
- [x] 3.2 `npm run build` and `npm test` pass in `site/`
