# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
Publishes UrDeck and installs it for the current user, keeping the user's own files.

.DESCRIPTION
Publishes the host to a temporary folder, closes a running copy from the destination (UrDeck.Host.exe --quit), mirrors
the application's files into the destination and starts the copy again if it was running (or when -Start is given).
Never replaced or removed: urdeck-config.json once it exists, urdeck.log and urdeck.log.1, cache/, themes/, backgrounds/, and files in
plugins/ that the publish did not produce. A destination without urdeck-config.json gets the sample one. It does not
register the app to start with Windows: that is the "Start with Windows" entry in the tray menu.

.PARAMETER Destination
Where to install. Default: %LOCALAPPDATA%\Programs\UrDeck.

.PARAMETER Start
Start the installed copy afterwards even if it was not running.
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\UrDeck'),
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$Destination = [System.IO.Path]::GetFullPath($Destination)
$exe = Join-Path $Destination 'UrDeck.Host.exe'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("urdeck-install-" + [guid]::NewGuid().ToString('N'))

function Invoke-Robocopy {
    param([string[]]$Arguments)
    & robocopy @Arguments | Out-Null
    # Exit codes below 8 are success (bit flags: files copied, extras, mismatches); 8 and above are failures.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
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

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $userFiles = @('urdeck-config.json', 'urdeck.log', 'urdeck.log.1')
    $userDirs = @('plugins', 'themes', 'backgrounds', 'cache')

    Write-Host "Copying to $Destination"
    # The application's files are mirrored; what the user owns is excluded on both sides, so it is neither overwritten nor purged.
    Invoke-Robocopy (@($stage, $Destination, '/MIR', '/NJH', '/NJS', '/NP', '/R:2', '/W:1', '/XD') + $userDirs + @('/XF') + $userFiles)
    # plugins/ is copied without purging, so plugins the user added stay.
    if (Test-Path (Join-Path $stage 'plugins')) {
        Invoke-Robocopy @((Join-Path $stage 'plugins'), (Join-Path $Destination 'plugins'), '/E', '/NJH', '/NJS', '/NP', '/R:2', '/W:1')
    }

    $config = Join-Path $Destination 'urdeck-config.json'
    if (-not (Test-Path $config)) {
        Copy-Item (Join-Path $stage 'urdeck-config.json') $config
        Write-Host 'Installed the sample configuration'
    }

    if ($wasRunning -or $Start) {
        Write-Host 'Starting UrDeck'
        Start-Process -FilePath $exe -WorkingDirectory $Destination
    }

    Write-Host "Installed to $Destination"
}
finally {
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue }
}
