<#
.SYNOPSIS
Runs the chunk-mesh benchmark across several client instances on the same spot.
.DESCRIPTION
Every bot is teleported to the same coordinates first, so the server sends the same chunk set to each, and only then
does measurement begin. The mcpfabric token is read from the local config and never printed.
.PARAMETER Bots
How many client instances to start.
.PARAMETER BufferSeconds
How long to buffer incoming chunks before measuring (dev switch MC_BENCH_BUFFER).
.PARAMETER Teleport
Coordinates for every bot, for example "-83 120 46".
#>
param(
    [int]$Bots = 2,
    [int]$BufferSeconds = 5,
    [string]$Teleport = "-83 120 46",
    [string]$Server = "127.0.0.1:25565",
    [string]$TokenConfig = "E:\Games\minecraft\.minecraft\versions\26.3-snapshot-8-Fabric 0.19.3\config\mcpfabric.config.json"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assets = Join-Path $env:TEMP "mcs-assets-pack"

if (-not (Test-Path $TokenConfig)) {
    throw "mcpfabric config not found: $TokenConfig"
}

$token = (Get-Content $TokenConfig -Raw | ConvertFrom-Json).token
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "mcpfabric token missing from the config"
}

function Send-Command([string]$command) {
    $body = @{ method = "chat.send"; params = @{ message = $command; asCommand = $true } } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "http://127.0.0.1:25599/rpc" -Method Post -Body $body `
        -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" } | Out-Null
}

$logs = @()
$names = @()

for ($i = 1; $i -le $Bots; $i++) {
    $name = "BenchBot$i"
    $names += $name
    $log = Join-Path $env:TEMP "bench_$name.log"

    $env:MC_ASSETS_OUT = $assets
    $env:MC_LOGIN_NAME = $name
    $env:MC_WINDOW_SECONDS = "120"
    $env:MC_DEV_BENCH = "1"
    $env:MC_BENCH_BUFFER = "$BufferSeconds"

    Start-Process -FilePath "dotnet" -ArgumentList @(
        "run", "--project", "src\Sharpcraft.Launcher", "--no-build", "--", "window", $Server
    ) -RedirectStandardOutput $log -RedirectStandardError "$log.err" | Out-Null

    $logs += $log
}

Start-Sleep -Seconds 25

# Everyone to the same spot, then let the chunk stream arrive before measuring.
foreach ($name in $names) {
    Send-Command "/tp $name $Teleport"
    Start-Sleep -Milliseconds 500
}

Start-Sleep -Seconds ($BufferSeconds + 20)

$rows = @()
for ($i = 0; $i -lt $Bots; $i++) {
    $name = $names[$i]
    $line = Select-String -Path $logs[$i] -Pattern "bench:" | Select-Object -Last 1
    $rows += [pscustomobject]@{ Bot = $name; Measurement = ($line.Line -replace "^\s+", "") }
}

Get-Process -Name "Sharpcraft.Launcher" -ErrorAction SilentlyContinue | ForEach-Object { taskkill /PID $_.Id /F 2>$null | Out-Null }

$rows | Format-Table -AutoSize | Out-String -Width 220
