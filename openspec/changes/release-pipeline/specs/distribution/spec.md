## MODIFIED Requirements

### Requirement: Published Application Folder
Publishing the host (`dotnet publish` of `UrDeck.Host` in the Release configuration) MUST produce a folder that runs
on a PC with neither the .NET SDK nor a .NET or Windows App SDK runtime installed. It contains:

- `UrDeck.Host.exe` with its runtime and dependencies
- `plugins/` with every first-party widget and provider assembly that the build places in the build output's
  `plugins/`
- `urdeck.ico` and the sample page as `urdeck-config.default.json`
- `LICENSE`, `PLUGIN-EXCEPTION.md` and `THIRD-PARTY-NOTICES.md`. The notices cover every third-party component in the
  folder: the .NET runtime, the Windows App SDK, SkiaSharp, Velopack and the material listed there today
- No `urdeck-config.json` and no `urdeck.portable`: a published copy runs in per-user mode (see data-folder)

When a version is given to the publish (see release, "Version Numbers"), the host, the engine and the first-party
plugins carry it; the SDK does not.

The folder can be copied anywhere and started from there; it needs write access only to the data folder.

The build output (`dotnet build`) holds the same files plus `urdeck.portable`, so a development build keeps its
configuration, log and user files in its own folder. The build never writes `urdeck-config.json`.

#### Scenario: Published copy runs
- **WHEN** the published folder is copied to another location and `UrDeck.Host.exe --snapshot out.png --size 1100x3840`
  is run there
- **THEN** a PNG with the sample page's widgets is written and the log reports no missing plugin or widget type

#### Scenario: Same plugins as the build
- **WHEN** the host is built and published from the same sources
- **THEN** the published `plugins/` holds the same assemblies as the build output's `plugins/`

#### Scenario: Build keeps the working configuration
- **WHEN** the configuration in a build output folder was edited and the tracked sample page is then changed and the
  host rebuilt
- **THEN** the build output's `urdeck-config.json` still holds the edited configuration, and
  `urdeck-config.default.json` holds the new sample

#### Scenario: Notices shipped
- **WHEN** the published folder is listed
- **THEN** it holds `LICENSE`, `PLUGIN-EXCEPTION.md` and `THIRD-PARTY-NOTICES.md`, and the notices name SkiaSharp and
  Velopack with their licences
