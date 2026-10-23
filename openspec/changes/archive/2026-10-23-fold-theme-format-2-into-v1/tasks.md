## 1. Branch and baseline

- [x] 1.1 Branch off `main` as `refactor/fold-theme-format-2-into-v1`; confirm `dotnet build urdeck.slnx -c Release` and `dotnet test urdeck.slnx -c Release` pass before any edit (the baseline the goldens are held to)
- [x] 1.2 List every remaining reference to fold in: `git grep -n -i "format 2\|format2\|\"format\"\|schemas/v2\|THEME010\|NewerFormat\|SupportedFormat"` over `src`, `tools`, `tests`, `site`, `docs` (not `docs/design/look-and-feel` or `openspec/changes/archive`), `README.md`, `AGENTS.md`, `CONTRIBUTING.md`

## 2. Engine

- [x] 2.1 Remove `Format`, `SupportedFormat`, `SanitizeFormat` and the merge of `Format` from `ThemeDefinition`; remove `ThemeCodes.NewerFormat` and its message in `ThemeLint`
- [x] 2.2 Remove `Format2Attribute`; in `ThemeLenientJson` drop the `classIsFormat2`/`format2` distinction so every scalar member of the wrong JSON kind is removed with one `THEME003` warning (keep the converter-member check and the gradient-stops case)
- [x] 2.3 Make `THEME003` a warning in `ThemeLint` for every key (no error for the original keys)
- [x] 2.4 Merge or rename each `ThemeFormat2.<Group>.cs` into `ThemeDefinition.<Group>.cs` (`git mv` where only renamed); fix doc comments and the "format 1"/"format 2" wording in `ResolvedCardStyle`, `ThemeDefinition.*` and tests' comments
- [x] 2.5 Delete `"format"` from `src/UrDeck.Engine/Themes/Builtin/*.json` and `src/UrDeck.Host/urdeck-config.json`
- [x] 2.6 Engine tests: replace the format-version tests (`format` of 1, 2, 3, 0, text) with one test that a leftover `format` key loads without a load-time warning; add tests that a wrong-kind value in an original key (`card.gap`, `colors.accent`, `typography.fontFamily` as a number) costs only that value with one `THEME003`-shaped warning; update `BackgroundPlanTests`, `CardChromeTests` and every other test theme that writes `"format": 2`

## 3. Schema, tool and fixtures

- [x] 3.1 `git mv docs/schemas/v2/theme.json docs/schemas/v1/theme.json`; update its `$id`, title and any "format 2" description text; make sure it has no `format` property
- [x] 3.2 Update the schema-versus-model test, `tools/UrDeck.Cli` (`ThemeSchemaRunner`, `ThemeInitRunner` including its `$schema` URI and the `"format": 2` line, `Commands.cs` description, the csproj's linked resource path) and the CLI tests
- [x] 3.3 Remove `THEME010` and its fixtures (`Fixtures/lint/THEME010*`) and update `ThemeValidateTests` (the SARIF test uses `format: 9` to get a warning; use another warning-producing edit and fix the expected rule ids)
- [x] 3.4 Remove `"format"` from every test fixture theme under `tests/**/Fixtures/themes` and `Fixtures/lint`; adjust the lint fixture for the wrong-type case (`THEME003-format1`) now that it is a warning, and rename it
- [x] 3.5 `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate --builtin --fail-on-warnings` is clean

## 4. Site and docs

- [x] 4.1 Remove `"format"` from `site/cookbook/*/theme.json`; point `$schema` at `/schemas/v1/theme.json`; fix `site/scripts/media/lib.test.mjs`
- [x] 4.2 Site scripts that copy or check the schema (`site/scripts/check-output.mjs`, `completeness.mjs` and its test, `site/README.md`) name `v1/theme.json`; the site publishes `/schemas/v1/theme.json`
- [x] 4.3 Rewrite the theme pages: `site/src/content/docs/themes/reference.mdx` (the "Formats" bullet and the hairline note), `quick-start.md` (the `$schema` line and the paragraph about `"format": 2`), and any other page `git grep` finds; regenerate `site/src/generated` if the build says it is stale
- [x] 4.4 Update `docs/themes.md` (step 1 "The file. Format 2, ...", the test names in step 6, the table row), `README.md`, `AGENTS.md` and `CONTRIBUTING.md` where they say format 1/2 or v2
- [x] 4.5 Troubleshooting page: remove the `THEME010` entry and make sure `THEME003` reads as a warning (the completeness check fails on a code without a description or a description without a code)

## 5. Verify

- [x] 5.1 `dotnet build urdeck.slnx -c Release` has 0 warnings; `dotnet test urdeck.slnx -c Release` passes with the goldens unchanged (no `URDECK_UPDATE_GOLDENS`, `git status` shows no `.png` changed)
- [x] 5.2 `dotnet format urdeck.slnx --severity warn --verify-no-changes` is clean
- [x] 5.3 `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate --all site/cookbook --fail-on-warnings` and `shader validate --all src/UrDeck.Engine/Backgrounds/Builtin --fail-on-warnings` are clean
- [x] 5.4 `cd site; npm ci; npm run build; npm test` pass
- [x] 5.5 `openspec validate --all --strict` passes
- [x] 5.6 Final `git grep -n -i "format 2\|format2\|\"format\"\|schemas/v2\|THEME010\|NewerFormat"` finds nothing outside `docs/design/look-and-feel`, `docs/design/theme-format-2-deferred-keys.md`, `openspec/changes/archive` and this change

## 6. Land

- [x] 6.1 Push the branch and open a PR titled `refactor(theme): fold theme format 2 into the one theme format`, with the description covering the removed `format` key, the removed `THEME010`, the moved schema URL and the lenient wrong-kind rule; wait for `ci-ok` and `pr-title`
- [x] 6.2 After merge, archive the change (`opsx:archive`), which syncs the delta specs into `openspec/specs/`
