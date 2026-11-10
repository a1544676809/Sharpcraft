# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Frame time attribution (docs/specs/performance-baseline.md 12).
#
# Runs the window four times at the same camera and resolution: the plain frame, then once with each pass group
# skipped (MC_SKIP_OIT, MC_SKIP_CLOUDS, MC_SKIP_TRANSLUCENT). The per stage CPU numbers come from MC_FRAME_CPU, so the
# subtraction between rows is what the skipped GPU work cost.
#
# Each row is a separate process, run back to back in the same machine state; absolute numbers drift with the power
# state, so compare rows against each other, never against a previous session.
param(
    [string]$Address = '127.0.0.1:25565',
    [int]$Width = 854,
    [int]$Height = 480,
    [int]$ViewDistance = 32,
    [int]$SettleSeconds = 45,
    [int]$SampleSeconds = 15,
    [string]$Username = 'Perf',
    [string]$TeleportCommand = 'tp Perf -74.098 121.0 37.303 180 1.5',
    [string]$Only = '',
    [switch]$SkipTeleport,
    [switch]$KeepLogs
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$client = Join-Path $root 'src\Sharpcraft.Client\bin\Debug\net10.0\Sharpcraft.Client.exe'
if (-not (Test-Path $client)) { "FAIL frame-attribution reason=no-client path=$client"; exit 2 }

$pack = (Get-ChildItem "$env:LOCALAPPDATA\Sharpcraft\packs" -Filter 'minecraft-26.3-*.pack.zip' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
if (-not $pack) { "FAIL frame-attribution reason=no-pack"; exit 2 }

$rcon = Join-Path $env:TEMP '__rcon.ps1'

$configs = @(
    @{ Name = 'default'; SkipOit = $false; SkipClouds = $false; SkipTranslucent = $false },
    @{ Name = 'skip-oit'; SkipOit = $true; SkipClouds = $false; SkipTranslucent = $false },
    @{ Name = 'skip-clouds'; SkipOit = $false; SkipClouds = $true; SkipTranslucent = $false },
    @{ Name = 'skip-translucent'; SkipOit = $false; SkipClouds = $false; SkipTranslucent = $true }
)

if ($Only) {
    $configs = $configs | Where-Object { $_.Name -eq $Only }
    if (-not $configs) { "FAIL frame-attribution reason=unknown-config name=$Only"; exit 2 }
}

$stages = @('frame', 'updates', 'acquire', 'camera', 'scene', 'oit', 'text', 'clouds', 'ui', 'present-rec', 'submit', 'present')
$rows = @()

function Stop-Clients {
    Get-Process -Name 'Sharpcraft.Client' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

function Measure-Config($config) {
    Stop-Clients

    $stamp = [guid]::NewGuid().ToString('N').Substring(0, 6)
    $log = Join-Path $env:TEMP "frame-attribution-$($config.Name)-$stamp.log"
    $options = [ordered]@{
        MC_CULL_STATS = '1'
        MC_FRAME_CPU = '1'
        MC_VIEW_DISTANCE = "$ViewDistance"
        MC_PRESENT_MODE = 'immediate'
        MC_WINDOW_SECONDS = "$($SettleSeconds + $SampleSeconds + 30)"
    }

    if ($config.SkipOit) { $options['MC_SKIP_OIT'] = '1' }
    if ($config.SkipClouds) { $options['MC_SKIP_CLOUDS'] = '1' }
    if ($config.SkipTranslucent) { $options['MC_SKIP_TRANSLUCENT'] = '1' }

    $optionFile = Join-Path $env:TEMP "frame-attribution-$stamp.json"
    ($options | ConvertTo-Json -Compress) | Set-Content -Path $optionFile -Encoding UTF8

    $arguments = @(
        '--version', '26.3',
        '--gameDir', "$env:APPDATA\.sharpcraft",
        '--pack', $pack,
        '--offlineDeveloperMode',
        '--username', $Username,
        '--quickPlayMultiplayer', $Address,
        '--width', "$Width",
        '--height', "$Height",
        '--debugOptionsFile', $optionFile
    )

    $process = Start-Process -FilePath $client -ArgumentList $arguments -RedirectStandardOutput $log `
        -RedirectStandardError "$log.err" -NoNewWindow -PassThru

    try {
        $firstFrame = $false
        for ($i = 0; $i -lt 240; $i++) {
            Start-Sleep -Milliseconds 500
            if (Select-String -Path $log -Pattern 'first frame presented' -Quiet -ErrorAction SilentlyContinue) {
                $firstFrame = $true
                break
            }

            if ($process.HasExited) { break }
        }

        if (-not $firstFrame) { return @{ Name = $config.Name; Error = 'no-first-frame'; Log = $log } }

        if (-not $SkipTeleport -and (Test-Path $rcon)) {
            & pwsh -NoProfile -ExecutionPolicy Bypass -File $rcon -Command $TeleportCommand *> $null
        }

        Start-Sleep -Seconds $SettleSeconds

        $marker = (Get-Content -Path $log | Measure-Object -Line).Lines
        Start-Sleep -Seconds $SampleSeconds

        $sampled = Get-Content -Path $log | Select-Object -Skip $marker
    }
    finally {
        Get-Process -Id $process.Id -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 800
        if (-not $KeepLogs) { Remove-Item $optionFile -ErrorAction SilentlyContinue }
    }

    $frames = @()
    $cullMeans = @()
    $chunks = 0
    $skipped = 'unknown'

    foreach ($line in $sampled) {
        if ($line -match '^frame-cpu:') {
            $fields = @{}
            foreach ($match in [regex]::Matches($line, '([a-zA-Z][\w-]*)=([\d.]+)')) {
                $fields[$match.Groups[1].Value] = [double]$match.Groups[2].Value
            }

            $frames += $fields
            if ($line -match 'skip=([\w+-]+)') { $skipped = $matches[1] }
        }
        elseif ($line -match 'cull: chunks=(\d+).*meanMs=([\d.]+)') {
            $chunks = [int]$matches[1]
            $cullMeans += [double]$matches[2]
        }
    }

    if ($frames.Count -eq 0) { return @{ Name = $config.Name; Error = 'no-cpu-lines'; Log = $log } }

    $row = @{ Name = $config.Name; Skip = $skipped; Samples = $frames.Count; Chunks = $chunks; Log = $log }
    foreach ($stage in $stages) {
        $values = $frames | Where-Object { $_.ContainsKey($stage) } | ForEach-Object { $_[$stage] }
        $row[$stage] = if ($values) { ($values | Measure-Object -Average).Average } else { 0 }
    }

    $row['meanMs'] = if ($cullMeans) { ($cullMeans | Measure-Object -Average).Average } else { $row['frame'] }
    $row['fps'] = if ($row['meanMs'] -gt 0) { 1000 / $row['meanMs'] } else { 0 }
    return $row
}

"INFO frame-attribution size=${Width}x${Height} viewDistance=$ViewDistance settle=${SettleSeconds}s sample=${SampleSeconds}s address=$Address"
"INFO every row is its own process, run back to back; compare rows, not sessions"

foreach ($config in $configs) {
    $current = Measure-Config $config
    if ($current.ContainsKey('Error')) {
        "FAIL frame-attribution config=$($current.Name) reason=$($current.Error) log=$($current.Log)"
        continue
    }

    $rows += $current
}

if ($rows.Count -eq 0) { "FAIL frame-attribution reason=no-rows"; exit 1 }

$header = "config,skip,lines,chunks,meanMs,fps," + ($stages -join ',')
$header
foreach ($row in $rows) {
    $cells = @(
        $row.Name,
        $row.Skip,
        $row.Samples,
        $row.Chunks,
        ('{0:F3}' -f $row['meanMs']),
        ('{0:F1}' -f $row['fps'])
    )
    foreach ($stage in $stages) { $cells += ('{0:F3}' -f $row[$stage]) }
    ($cells -join ',')
}

$baseline = $rows | Where-Object { $_.Name -eq 'default' } | Select-Object -First 1
if ($baseline) {
    foreach ($row in $rows) {
        if ($row.Name -eq 'default') { continue }
        $delta = $baseline['meanMs'] - $row['meanMs']
        $percent = 100 * $delta / [Math]::Max($baseline['meanMs'], 0.001)
        "INFO saved-by-$($row.Name)={0:F3}ms ({1:F1}% of the plain frame)" -f $delta, $percent
    }
}

"PASS frame-attribution rows=$($rows.Count) size=${Width}x${Height}"
