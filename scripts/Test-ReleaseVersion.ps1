# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
  Checks that a release version has the form the release process allows, is new and is higher than the latest release.

.DESCRIPTION
  The form is MAJOR.MINOR.PATCH with an optional -rc.N label (release-process spec, "Version Numbers"). "Higher" is
  SemVer 2.0 precedence: 0.2.0-rc.1 is higher than 0.1.0 and lower than 0.2.0. Exits 0 when the version is acceptable,
  1 with the reason on stderr when not. -Latest are the published versions (the highest counts), -Existing every version that
  already has a tag, a release or a draft. -SelfTest runs the script's own cases and exits 0 when all pass.

.EXAMPLE
  pwsh scripts/Test-ReleaseVersion.ps1 -Version 0.2.0 -Latest 0.1.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string[]]$Latest,
    [string[]]$Existing,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$pattern = '^(\d+)\.(\d+)\.(\d+)(?:-rc\.(\d+))?$'

function Get-VersionProblem([string]$Candidate, [string[]]$LatestVersions, [string[]]$ExistingVersions) {
    if ($Candidate -notmatch $pattern) {
        return "'$Candidate' is not a release version. Expected MAJOR.MINOR.PATCH with an optional -rc.N label, e.g. 0.2.0 or 0.2.0-rc.1 (no leading 'v')."
    }
    if ($ExistingVersions -contains $Candidate) {
        return "Version '$Candidate' already has a tag, a release or a draft."
    }

    # The latest release is the highest of the versions given; one that is not in the release form is ignored.
    $latest = $null
    foreach ($v in @($LatestVersions | Where-Object { $_ -match $pattern })) {
        if ($null -eq $latest -or (Compare-Version $v $latest) -gt 0) { $latest = $v }
    }
    if ($latest -and (Compare-Version $Candidate $latest) -le 0) {
        return "'$Candidate' is not higher than the latest release '$latest'."
    }
    return $null
}

# SemVer precedence for the forms above: the numbers first; with equal numbers a version with a label is lower than
# one without, and labels compare by their number.
function Compare-Version([string]$A, [string]$B) {
    $null = $A -match $pattern; $left = $Matches
    $null = $B -match $pattern; $right = $Matches
    foreach ($i in 1..3) {
        $d = [int]$left[$i] - [int]$right[$i]
        if ($d -ne 0) { return [math]::Sign($d) }
    }
    $leftRc = $left[4]; $rightRc = $right[4]
    if ($null -eq $leftRc -and $null -eq $rightRc) { return 0 }
    if ($null -eq $leftRc) { return 1 }
    if ($null -eq $rightRc) { return -1 }
    return [math]::Sign([int]$leftRc - [int]$rightRc)
}

if ($SelfTest) {
    $cases = @(
        @{ V = '0.1.0';      L = $null;         Ok = $true  },
        @{ V = '0.1';        L = $null;         Ok = $false },
        @{ V = 'v0.2.0';     L = '0.1.0';       Ok = $false },
        @{ V = '0.2.0-rc.1'; L = '0.1.0';       Ok = $true  },
        @{ V = '0.2.0';      L = '0.2.0-rc.1';  Ok = $true  },
        @{ V = '0.2.0-rc.1'; L = '0.2.0';       Ok = $false },
        @{ V = '0.2.0-rc.2'; L = '0.2.0-rc.1';  Ok = $true  },
        @{ V = '0.2.0';      L = '0.2.0';       Ok = $false },
        @{ V = '0.1.9';      L = '0.2.0';       Ok = $false },
        @{ V = '0.10.0';     L = '0.9.0';       Ok = $true  },
        @{ V = '0.2.0-beta'; L = '0.1.0';       Ok = $false },
        @{ V = '0.3.0';      L = @('0.1.0', '0.2.0'); E = @('0.2.0', '0.3.0'); Ok = $false },
        @{ V = '0.3.0';      L = @('0.2.0', '0.1.0'); E = @('0.4.0-rc.1'); Ok = $true },
        @{ V = '0.2.0';      L = @('0.1.0', '0.2.0-rc.1'); Ok = $true }
    )
    $failed = 0
    foreach ($c in $cases) {
        $problem = Get-VersionProblem $c.V $c.L $c.E
        if (($null -eq $problem) -ne $c.Ok) {
            Write-Host "FAIL: '$($c.V)' after '$($c.L)': expected ok=$($c.Ok), got '$problem'"
            $failed++
        }
    }
    if ($failed) { exit 1 }
    Write-Host "All $($cases.Count) cases passed."
    exit 0
}

$problem = Get-VersionProblem $Version $Latest $Existing
if ($problem) {
    [Console]::Error.WriteLine($problem)
    exit 1
}
Write-Host "Version $Version is acceptable$(if ($Latest) { " (published: $($Latest -join ', '))" })."
exit 0
