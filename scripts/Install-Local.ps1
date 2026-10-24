# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
Publishes UrDeck and installs it for the current user.

.DESCRIPTION
Publishes the host to a temporary folder, closes a running copy from the destination (UrDeck.Host.exe --quit), mirrors
the application's files into the destination and starts the copy again if it was running (or when -Start is given).
The installed copy keeps the user's files (configuration, log, themes/, backgrounds/, plugins the user added and cache/)
in the data folder, %APPDATA%\UrDeck, which this script does not touch. It writes no configuration: the first start
creates it from the sample page. It does not register the app to start with Windows: that is the "Start with Windows"
entry in the tray menu.

One-time move: a destination from before the data folder existed holds those files next to the executable. When it has a
urdeck-config.json and the data folder has none, the configuration, urdeck.log and urdeck.log.1, themes/, backgrounds/,
cache/ and the files in plugins/ that the publish did not produce are moved to the data folder first. When the data folder
already has a configuration nothing is moved: the old files stay where they are and are named. A move that fails is
undone as far as it went and stops the script before anything is replaced.

.PARAMETER Destination
Where to install. Default: %LOCALAPPDATA%\Programs\UrDeck.

.PARAMETER RoamingRoot
The folder that holds the data folder (UrDeck inside it). Default: %APPDATA%. Only a test of this script changes it.

.PARAMETER Start
Start the installed copy afterwards even if it was not running.
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\UrDeck'),
    [string]$RoamingRoot = $env:APPDATA,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$Destination = [System.IO.Path]::GetFullPath($Destination)
$dataFolder = Join-Path ([System.IO.Path]::GetFullPath($RoamingRoot)) 'UrDeck'
$exe = Join-Path $Destination 'UrDeck.Host.exe'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("urdeck-install-" + [guid]::NewGuid().ToString('N'))

# What a copy from before the data folder kept next to the executable.
$userFiles = @('urdeck-config.json', 'urdeck.log', 'urdeck.log.1')
$userDirs = @('themes', 'backgrounds', 'cache')

function Invoke-Robocopy {
    param([string[]]$Arguments)
    & robocopy @Arguments | Out-Null
    # Exit codes below 8 are success (bit flags: files copied, extras, mismatches); 8 and above are failures.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
}

# The files and folders in the destination that belong to the user: the known names, and the plugins the publish did not produce.
function Get-OldUserItems {
    param([string]$Published)
    $items = @()
    foreach ($name in $userFiles) {
        $path = Join-Path $Destination $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { $items += [pscustomobject]@{ Source = $path; Target = (Join-Path $dataFolder $name); Directory = $false } }
    }
    foreach ($name in $userDirs) {
        $path = Join-Path $Destination $name
        if (Test-Path -LiteralPath $path -PathType Container) { $items += [pscustomobject]@{ Source = $path; Target = (Join-Path $dataFolder $name); Directory = $true } }
    }
    $plugins = Join-Path $Destination 'plugins'
    if (Test-Path -LiteralPath $plugins -PathType Container) {
        $producedNames = @()
        if (Test-Path (Join-Path $Published 'plugins')) { $producedNames = @(Get-ChildItem (Join-Path $Published 'plugins') -File | ForEach-Object { $_.Name }) }
        foreach ($file in Get-ChildItem -LiteralPath $plugins -File) {
            if ($producedNames -notcontains $file.Name) {
                $items += [pscustomobject]@{ Source = $file.FullName; Target = (Join-Path (Join-Path $dataFolder 'plugins') $file.Name); Directory = $false }
            }
        }
        foreach ($folder in Get-ChildItem -LiteralPath $plugins -Directory) {
            $items += [pscustomobject]@{ Source = $folder.FullName; Target = (Join-Path (Join-Path $dataFolder 'plugins') $folder.Name); Directory = $true }
        }
    }
    return $items
}

# Moves the items, and puts back what it moved when one fails.
function Move-OldUserItems {
    param($Items)
    $done = @()
    try {
        New-Item -ItemType Directory -Force -Path $dataFolder | Out-Null
        foreach ($item in $Items) {
            if (Test-Path -LiteralPath $item.Target) { throw "$($item.Target) exists already; nothing was moved" }
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $item.Target) | Out-Null
            Move-Item -LiteralPath $item.Source -Destination $item.Target
            $done += $item
            Write-Host "Moved $($item.Source) -> $($item.Target)"
        }
    }
    catch {
        $reason = $_.Exception.Message
        [array]::Reverse($done)
        foreach ($item in $done) {
            try { Move-Item -LiteralPath $item.Target -Destination $item.Source } catch { Write-Warning "Could not put $($item.Target) back to $($item.Source): $($_.Exception.Message)" }
        }
        throw "Moving the user's files to $dataFolder failed ($reason); the files were put back and nothing was replaced"
    }
}

try {
    Write-Host "Publishing to $stage"
    & dotnet publish (Join-Path $repo 'src\UrDeck.Host') -c Release -o $stage
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path (Join-Path $stage 'UrDeck.Host.exe'))) { throw 'The publish did not produce UrDeck.Host.exe' }

    $wasRunning = $false
    if (Test-Path $exe) {
        $wasRunning = [bool](Get-Process -Name 'UrDeck.Host' -ErrorAction SilentlyContinue |
            Where-Object { try { $_.MainModule.FileName -ieq $exe } catch { $false } })
        if ($wasRunning) {
            Write-Host 'Closing the running copy'
            & $exe --quit
            if ($LASTEXITCODE -ne 0) { throw "The running copy did not close (--quit exit code $LASTEXITCODE); nothing was replaced" }
        }
    }

    # A destination from before the data folder: move its user files, once.
    $oldItems = @(Get-OldUserItems -Published $stage)
    if ($oldItems.Count -gt 0) {
        if (-not (Test-Path (Join-Path $Destination 'urdeck-config.json'))) {
            Write-Host "$Destination has user files but no urdeck-config.json; leaving them where they are"
        }
        elseif (Test-Path (Join-Path $dataFolder 'urdeck-config.json')) {
            Write-Warning "$dataFolder has a configuration already, so nothing was moved. These old files stay in ${Destination}:"
            $oldItems | ForEach-Object { Write-Warning "  $($_.Source)" }
        }
        else {
            Move-OldUserItems -Items $oldItems
            $oldItems = @()
        }
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Write-Host "Copying to $Destination"
    # The application's files are mirrored. Old user files that were not moved (see above) are excluded on both sides, so they are
    # neither overwritten nor purged; plugins/ is copied without purging for the same reason.
    $keepFiles = @($oldItems | Where-Object { -not $_.Directory -and (Split-Path -Parent $_.Source) -eq $Destination } | ForEach-Object { Split-Path -Leaf $_.Source })
    $keepDirs = @($userDirs + 'plugins')
    $mirror = @($stage, $Destination, '/MIR', '/NJH', '/NJS', '/NP', '/R:2', '/W:1', '/XD') + $keepDirs
    if ($keepFiles.Count -gt 0) { $mirror += @('/XF') + $keepFiles }
    Invoke-Robocopy $mirror
    if (Test-Path (Join-Path $stage 'plugins')) {
        Invoke-Robocopy @((Join-Path $stage 'plugins'), (Join-Path $Destination 'plugins'), '/E', '/NJH', '/NJS', '/NP', '/R:2', '/W:1')
    }

    if ($wasRunning -or $Start) {
        Write-Host 'Starting UrDeck'
        Start-Process -FilePath $exe -WorkingDirectory $Destination
    }

    Write-Host "Installed to $Destination (data folder: $dataFolder)"
}
finally {
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue }
}
