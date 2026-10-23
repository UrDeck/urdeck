---
title: The command line tool
description: What urdeck-cli does, how to get it, and what its exit codes mean.
section: Start
order: 2
---

`urdeck-cli` is a small tool that works on the files UrDeck reads, without a deck: no window, no GPU, no network. It runs on
Windows and on Linux, and it calls the same engine code the deck does, so it cannot disagree with the deck about what is wrong
with a file. It never reads or writes the deck's configuration or log.

| Command | Does |
|---|---|
| `theme init`, `validate`, `render`, `schema` | Makes, checks and draws a theme folder. See [checking a theme](/docs/themes/checking/). |
| `shader init`, `validate`, `render` | Makes, checks and draws a background folder. See [the shader tool](/docs/backgrounds/tool/). |
| `preview render` | Draws a configuration's page, or one widget, in a theme over a background, from sample data. |
| `catalog export` | Writes what the product ships (themes, backgrounds, widgets and their settings, finding codes) as JSON. This site's reference is built from it. |

`--help` on the tool and on each command lists the options and the exit codes, and `--version` prints the version.

## Getting it

Until the tool is published, build and run it from the repository (it needs the .NET 10 SDK):

```powershell
dotnet build tools/UrDeck.Cli -c Release
dotnet tools/UrDeck.Cli/bin/Release/net10.0/urdeck-cli.dll shader validate backgrounds/waves
```

or install the package you made with `dotnet pack tools/UrDeck.Cli -c Release -o out`:

```powershell
dotnet tool install --tool-path tools-bin --add-source out urdeck-cli
```

which gives `urdeck-cli` (`urdeck-cli.exe` on Windows).

## Exit codes

They are the same in every command and do not change in later versions.

| Code | Meaning |
|---|---|
| 0 | The command did its job and found nothing that fails the run. |
| 1 | The command did its job and there are findings that fail the run: an error, or a warning with `--fail-on-warnings`; for a render, also when no picture could be made. |
| 2 | The command could not do its job: unknown or malformed arguments, a folder that does not exist, an output that cannot be written. The reason is on the error stream and nothing is on the output stream. |

An information finding never changes the exit code, and neither does the output format.

## Findings

Every finding has a code that never changes meaning (`THEME…` for themes, `SHADER…` for backgrounds). The
[troubleshooting page](/docs/troubleshooting/) lists them all with what each means and how to fix it.

## Licence

The tool links the GPL-licensed engine, but running it on a shader or a theme does not affect that file's licence, any more than
running a compiler does: the file is read as data and the tool's output (findings, a picture) is not a derivative of the tool.
