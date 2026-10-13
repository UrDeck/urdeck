# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
Renders the sample decks of the website with UrDeck itself.

.DESCRIPTION
Builds the host, copies its output to a temporary folder (the host build overwrites a config in bin/, and --snapshot
reads the config next to the executable), and for each folder site/decks/<name>/ puts its urdeck-config.json there and
runs UrDeck.Host.exe --snapshot --size 1100x3840 to site/src/assets/decks/<name>.png. Fails when a snapshot fails or a
file is missing. The readings in a picture are those of this machine and the weather needs the network: look at each
picture before committing it (nothing personal, readings present, weather drawn).

.PARAMETER Size
The size of the picture, WxH. Default 1100x3840, the panel the decks are made for.

.PARAMETER Name
Render only these decks (folder names under site/decks/).
#>
[CmdletBinding()]
param(
    [string]$Size = '1100x3840',
    [string[]]$Name
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$decks = Join-Path $repo 'site\decks'
$out = Join-Path $repo 'site\src\assets\decks'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('urdeck-site-decks-' + [guid]::NewGuid().ToString('N'))

try {
    Write-Host 'Building the host'
    & dotnet build (Join-Path $repo 'src\UrDeck.Host') -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

    $bin = Join-Path $repo 'src\UrDeck.Host\bin\Release\net10.0-windows10.0.19041.0'
    if (-not (Test-Path (Join-Path $bin 'UrDeck.Host.exe'))) { throw "The build did not produce UrDeck.Host.exe in $bin" }

    New-Item -ItemType Directory -Force -Path $stage, $out | Out-Null
    # The exe next to its plugins, themes and backgrounds; nothing of a previous run (log, cache) comes along.
    & robocopy $bin $stage /E /XF urdeck.log urdeck.log.1 /XD cache /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    $exe = Join-Path $stage 'UrDeck.Host.exe'

    $folders = Get-ChildItem $decks -Directory | Where-Object { -not $Name -or $Name -contains $_.Name }
    if (-not $folders) { throw "No deck found under $decks" }

    foreach ($folder in $folders) {
        $config = Join-Path $folder.FullName 'urdeck-config.json'
        if (-not (Test-Path $config)) { throw "$($folder.Name) has no urdeck-config.json" }
        Copy-Item $config (Join-Path $stage 'urdeck-config.json') -Force

        $png = Join-Path $out ($folder.Name + '.png')
        if (Test-Path $png) { Remove-Item $png }
        Write-Host "Rendering $($folder.Name)"
        # Piped, so that the host's output shows in the console and a crash shows its message.
        & $exe --snapshot $png --size $Size 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) {
            $log = Join-Path $stage 'urdeck.log'
            if (Test-Path $log) { Get-Content $log -Tail 15 | Out-Host }
            throw "The snapshot of $($folder.Name) failed with exit code $LASTEXITCODE"
        }
        if (-not (Test-Path $png) -or (Get-Item $png).Length -eq 0) { throw "The snapshot of $($folder.Name) wrote no file" }
    }

    Write-Host "Done: $out. Look at each picture before committing it."
}
finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
