# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Patrick Bigler

<#
.SYNOPSIS
Starts a disposable Home Assistant in Docker for the Home Assistant provider's end-to-end tests.

.DESCRIPTION
Runs the homeassistant/home-assistant image with the demo integration (made-up lights, sensors, a lock, media players) in a
container named urdeck-ha-demo, onboards a user, makes a long-lived access token and sets, for this PowerShell session:

  URDECK_HA_E2E=1
  URDECK_HA_URL=http://localhost:<port>
  URDECK_SECRET_HOMEASSISTANT_TOKEN=<the token>

Then run the opt-in tests (dotnet test tests/UrDeck.Engine.Tests -c Release --filter HomeAssistantEndToEnd), or a deck or
urdeck-cli against it: the provider reads the token from that variable. The token is printed only with -ShowToken. The
container and its configuration are thrown away by -Stop. Never run by CI. Needs Docker (Docker Desktop on Windows).

.PARAMETER Port
The port Home Assistant answers on. Default: 8123.

.PARAMETER Image
The image to run. Default: a pinned release of homeassistant/home-assistant.

.PARAMETER ShowToken
Print the token too (it is a test installation's, on this machine only).

.PARAMETER Stop
Remove the container and its configuration folder, and clear the variables.
#>
[CmdletBinding()]
param(
    [int]$Port = 8123,
    [string]$Image = 'homeassistant/home-assistant:2026.10.3',
    [switch]$ShowToken,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$container = 'urdeck-ha-demo'
$configDir = Join-Path ([System.IO.Path]::GetTempPath()) 'urdeck-ha-demo'
$url = "http://localhost:$Port"
$clientId = "$url/"

function Clear-DemoVariables {
    Remove-Item Env:URDECK_HA_E2E, Env:URDECK_HA_URL, Env:URDECK_SECRET_HOMEASSISTANT_TOKEN -ErrorAction SilentlyContinue
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is not installed (or not on PATH). Install Docker Desktop and start it.'
}

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is not running. Start Docker Desktop and try again.'
}

if ($Stop) {
    docker rm --force $container *> $null
    if (Test-Path $configDir) { Remove-Item -Recurse -Force $configDir }
    Clear-DemoVariables
    Write-Host "Removed $container and its configuration."
    return
}

# A fresh installation every time: onboarding only works once per configuration.
docker rm --force $container *> $null
if (Test-Path $configDir) { Remove-Item -Recurse -Force $configDir }
New-Item -ItemType Directory -Force $configDir | Out-Null
@'
# A throwaway installation for UrDeck's end-to-end tests.
homeassistant:
  name: UrDeck demo
  unit_system: metric
  time_zone: UTC
http:
demo:
'@ | Set-Content -Encoding utf8 (Join-Path $configDir 'configuration.yaml')

Write-Host "Starting $Image as $container on port $Port ..."
docker run --detach --name $container --publish "${Port}:8123" --volume "${configDir}:/config" $Image | Out-Null
if ($LASTEXITCODE -ne 0) { throw "docker run failed (exit code $LASTEXITCODE)" }

# Onboarding answers once the HTTP server is up (the first start can take a minute or two).
$deadline = (Get-Date).AddMinutes(4)
while ($true) {
    try {
        $steps = Invoke-RestMethod -Uri "$url/api/onboarding" -TimeoutSec 5
        if ($steps) { break }
    }
    catch {
        if ((Get-Date) -gt $deadline) { throw "Home Assistant did not start within 4 minutes; see: docker logs $container" }
        Start-Sleep -Seconds 3
    }
}

Write-Host 'Onboarding a user ...'
$user = Invoke-RestMethod -Method Post -Uri "$url/api/onboarding/users" -ContentType 'application/json' -Body (@{
        client_id = $clientId; name = 'UrDeck'; username = 'urdeck'; password = 'urdeck-demo-only'; language = 'en'
    } | ConvertTo-Json)
$tokens = Invoke-RestMethod -Method Post -Uri "$url/auth/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
    grant_type = 'authorization_code'; code = $user.auth_code; client_id = $clientId
}
$headers = @{ Authorization = "Bearer $($tokens.access_token)" }
foreach ($step in @(
        @{ path = 'core_config'; body = @{} },
        @{ path = 'analytics'; body = @{} },
        @{ path = 'integration'; body = @{ client_id = $clientId; redirect_uri = "$($clientId)?auth_callback=1" } })) {
    try {
        Invoke-RestMethod -Method Post -Uri "$url/api/onboarding/$($step.path)" -Headers $headers -ContentType 'application/json' -Body ($step.body | ConvertTo-Json) | Out-Null
    }
    catch {
        Write-Verbose "Onboarding step $($step.path): $($_.Exception.Message)"
    }
}

# A long-lived token is only made over the WebSocket API.
Write-Host 'Making a long-lived access token ...'
$socket = [System.Net.WebSockets.ClientWebSocket]::new()
$cancel = [System.Threading.CancellationToken]::None
function Send-Message([object]$message) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($message | ConvertTo-Json -Compress))
    $socket.SendAsync([ArraySegment[byte]]::new($bytes), 'Text', $true, $cancel).GetAwaiter().GetResult() | Out-Null
}
function Receive-Message {
    $buffer = [byte[]]::new(65536)
    $stream = [System.IO.MemoryStream]::new()
    do {
        $result = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $cancel).GetAwaiter().GetResult()
        $stream.Write($buffer, 0, $result.Count)
    } while (-not $result.EndOfMessage)
    [System.Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
}
try {
    $socket.ConnectAsync([Uri]"ws://localhost:$Port/api/websocket", $cancel).GetAwaiter().GetResult() | Out-Null
    Receive-Message | Out-Null
    Send-Message @{ type = 'auth'; access_token = $tokens.access_token }
    $auth = Receive-Message
    if ($auth.type -ne 'auth_ok') { throw "signing in over the WebSocket failed: $($auth.type)" }
    Send-Message @{ id = 1; type = 'auth/long_lived_access_token'; client_name = 'UrDeck end-to-end tests'; lifespan = 30 }
    $answer = Receive-Message
    if (-not $answer.success) { throw "no long-lived token: $($answer.error.message)" }
    $token = $answer.result
}
finally {
    $socket.Dispose()
}

$env:URDECK_HA_E2E = '1'
$env:URDECK_HA_URL = $url
$env:URDECK_SECRET_HOMEASSISTANT_TOKEN = $token

Write-Host ''
Write-Host "Home Assistant $((Invoke-RestMethod -Uri "$url/api/config" -Headers @{ Authorization = "Bearer $token" }).version) is at $url (user urdeck, password urdeck-demo-only)."
Write-Host 'Set for this session: URDECK_HA_E2E, URDECK_HA_URL and URDECK_SECRET_HOMEASSISTANT_TOKEN.'
if ($ShowToken) { Write-Host "Token: $token" }
Write-Host 'Run: dotnet test tests/UrDeck.Engine.Tests -c Release --filter HomeAssistantEndToEnd'
Write-Host "Remove it with: pwsh scripts/Start-HomeAssistantDemo.ps1 -Stop"
