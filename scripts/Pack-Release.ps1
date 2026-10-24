# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
Publishes UrDeck with a version and packs it into the files of a release.

.DESCRIPTION
The one place that makes a release's files; the release workflow calls this script, and the same call on the owner's PC
makes a release to try (install it, update from it). It checks that the Velopack package and the vpk tool are the same
version, publishes the host with the version (release-process spec, "Version Numbers"), and packs the published folder
with vpk into the output folder: UrDeck-win-Setup.exe, UrDeck-win-Portable.zip, the full package, a delta package from
the previous release when there is one, and the feed (releases.win.json). Signing, when there is some, goes between the
publish and the pack (the -SignTemplate parameter).

.PARAMETER Version
The release version: 0.2.0 or 0.2.0-rc.1. Checked with scripts/Test-ReleaseVersion.ps1.

.PARAMETER Previous
A folder with the files of the previous release (what `vpk download github` writes). Its files are copied to the output
folder first so that vpk can make a delta package. Without it the release has only a full package.

.PARAMETER OutputDir
Where the release's files go. Default: artifacts/release in the repository. It is emptied first, except for the files
copied from -Previous.

.PARAMETER SignTemplate
A vpk signing command ({{file}} is the file to sign), passed to `vpk pack --signTemplate`. Default: no signing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Previous,
    [string]$OutputDir,
    [string]$SignTemplate
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'artifacts/release' }
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$publishDir = Join-Path $repo 'artifacts/publish'

function Invoke-Native([string]$What, [scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE." }
}

& (Join-Path $PSScriptRoot 'Test-ReleaseVersion.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { exit 1 }

# The package inside the app and the tool that packs it must be the same Velopack version, or the app cannot apply the package.
$packages = [xml](Get-Content (Join-Path $repo 'Directory.Packages.props') -Raw)
$packageVersion = ($packages.Project.ItemGroup.PackageVersion | Where-Object Include -eq 'Velopack').Version
$tools = Get-Content (Join-Path $repo 'dotnet-tools.json') -Raw | ConvertFrom-Json
$toolVersion = $tools.tools.vpk.version
if (-not $packageVersion -or $packageVersion -ne $toolVersion) {
    throw "The Velopack package ($packageVersion, Directory.Packages.props) and the vpk tool ($toolVersion, dotnet-tools.json) must be the same version."
}

Push-Location $repo
try {
    Invoke-Native 'dotnet tool restore' { dotnet tool restore | Out-Host }

    $revision = (git rev-parse --short=12 HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'git rev-parse failed.' }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    Invoke-Native 'dotnet publish' {
        dotnet publish src/UrDeck.Host -c Release -o $publishDir "-p:UrDeckAppVersion=$Version" "-p:SourceRevisionId=$revision" | Out-Host
    }

    if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    if ($Previous) {
        if (-not (Test-Path $Previous)) { throw "The previous release folder '$Previous' does not exist." }
        Copy-Item (Join-Path $Previous '*') $OutputDir -Recurse
        Write-Host "Previous release files: $((Get-ChildItem $OutputDir -File).Name -join ', ')"
    }

    $pack = @(
        'pack',
        '--packId', 'UrDeck',
        '--packTitle', 'UrDeck',
        '--packAuthors', 'Patrick Bigler',
        '--packVersion', $Version,
        '--packDir', $publishDir,
        '--mainExe', 'UrDeck.Host.exe',
        '--icon', (Join-Path $repo 'src/UrDeck.Host/Resources/urdeck.ico'),
        '--runtime', 'win-x64',
        '--channel', 'win',
        '--shortcuts', 'StartMenuRoot',
        '--instLocation', 'PerUser',
        '--outputDir', $OutputDir
    )
    if ($SignTemplate) { $pack += @('--signTemplate', $SignTemplate) }
    Invoke-Native 'vpk pack' { dotnet vpk @pack | Out-Host }

    Write-Host ''
    Write-Host "Release $Version packed into $OutputDir"
    Get-ChildItem $OutputDir -File | ForEach-Object { Write-Host ("  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
}
finally {
    Pop-Location
}
