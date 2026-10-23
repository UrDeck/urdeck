## MODIFIED Requirements

### Requirement: Creating A Theme
`urdeck-cli theme init <name>` MUST make a theme folder that loads and checks clean:

- The folder `<name>` is made in the current directory, or in the directory `--output` names, which must exist
- The name is a single folder name of letters, digits, `-` and `_`; anything else is a failure of the tool (exit code 2)
  and nothing is created
- Without `--from`, the folder holds a `theme.json` with the `$schema` line, a `meta` group with the
  name, an empty description for the author to fill in and a `mode` of `dark`, and a `colors` group holding the default
  dark theme's colours
- With `--from <built-in name>`, the folder holds that theme's settings as shipped, with a `meta.name` of the new name,
  and the font files and licence files it names. An unknown built-in name is a failure of the tool (exit code 2)
- `init` MUST NOT overwrite: an existing folder of that name is left as it is, with exit code 2 and the reason
- The output names the folder, the files written and the command to run next

#### Scenario: A starter
- **WHEN** `theme init mine` is run
- **THEN** the folder `mine` holds a `theme.json` that `theme validate mine` reports no error for, and the exit code is 0

#### Scenario: From a built-in theme
- **WHEN** `theme init mine --from carbon` is run
- **THEN** the folder holds Carbon's settings, its fonts and their licences, and `theme validate mine` reports no finding
  that Carbon itself has not

#### Scenario: The folder exists
- **WHEN** `theme init mine` is run and a folder `mine` exists
- **THEN** nothing is written and the exit code is 2

#### Scenario: A name that is not valid
- **WHEN** `theme init "my theme"` is run
- **THEN** nothing is created, the error stream names the rule for names, and the exit code is 2

### Requirement: Printing The Theme Schema
`urdeck-cli theme schema` MUST print the JSON Schema of the theme format that the tool was built with:

- The schema goes to the output stream, or to the file `--output` names, which is replaced when it exists
- It is the schema the product publishes as `docs/schemas/v1/theme.json`, byte for byte
- It needs no network

#### Scenario: Printed
- **WHEN** `theme schema` is run
- **THEN** the output stream is the published schema and the exit code is 0

#### Scenario: To a file
- **WHEN** `theme schema --output theme.schema.json` is run
- **THEN** the file holds the schema and nothing is printed to the output stream
