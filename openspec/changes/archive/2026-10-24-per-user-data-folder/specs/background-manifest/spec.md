## MODIFIED Requirements

### Requirement: Background Folder
A shader background MUST be a folder `backgrounds/<name>/` in the data folder (see data-folder), or a built-in one,
holding a `manifest.json` and the entry shader file:

- The folder name is the background's name in the configuration. It follows the name rule of "Shader Backgrounds"
- The entry is `<name>.sksl` in the folder unless the manifest's `entry` names another file. `entry` MUST name a file
  inside the folder: an absolute path or one that leaves the folder is refused
- Other files in the folder (a readme, sample pictures) are never read by the host
- Removing the folder removes the background; copying a folder in adds one
- A loose `backgrounds/<name>.sksl` is not a background. When one exists and the configuration selects its name, the
  warning says that a background is now a folder with a `manifest.json` and names the folder to create
- A user folder with the name of a built-in one takes precedence over it
- Folders are re-read when the configuration is reloaded

#### Scenario: Background installed by copying a folder
- **WHEN** a folder `waves` holding `manifest.json` and `waves.sksl` is copied into `backgrounds/` and the
  configuration selects `waves`
- **THEN** the deck shows that shader

#### Scenario: Entry named in the manifest
- **WHEN** the manifest has `"entry": "main.sksl"` and the folder holds `main.sksl`
- **THEN** that file is the shader

#### Scenario: Entry leaves the folder
- **WHEN** the manifest has `"entry": "../other/x.sksl"`
- **THEN** no file is read, a warning is logged and the deck shows the theme's background colour

#### Scenario: Loose file
- **WHEN** `backgrounds/waves.sksl` exists, there is no folder `backgrounds/waves`, and the configuration selects `waves`
- **THEN** the warning names the folder layout, and the deck shows the theme's background colour

#### Scenario: Built-in replaced
- **WHEN** a valid folder `backgrounds/plasma` exists and the configuration selects `plasma`
- **THEN** the user's folder is used
