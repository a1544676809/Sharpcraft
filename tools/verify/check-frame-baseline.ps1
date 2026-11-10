<#
.SYNOPSIS
    Renders the same offscreen frame twice and checks the output is byte-identical.

.DESCRIPTION
    The offscreen path is the only part of the project that can produce a repeatable image, which makes it the closest
    thing to a pixel regression baseline. Two conditions have to hold for that to be true:

      1. The world state is pinned. Cloud sampling follows the live world clock, so two runs a second apart differ in a
         small patch of cloud and any hash comparison fails at random. `MC_FORCE_DAYTIME` and `MC_FORCE_PARTIAL` pin it,
         and they only reach the game through `--debugOptionsFile`; the game assemblies deliberately do not read the
         environment (AGENTS.md principle 13), so setting them as environment variables silently does nothing.
      2. It is the same build. This script checks exactly that, by running the current build twice.

    When a baseline file is supplied it also compares against it, which is how an intentional rendering change is
    distinguished from an accidental one. That file has to be refreshed deliberately when the picture is meant to
    change, so it is a separate argument rather than a committed constant.

    This is a verification tool, not a unit test: each run loads worlds and meshes chunks, so it takes tens of seconds.

.PARAMETER Client
    Path to Sharpcraft.Client.exe. Defaults to the debug build in this repository.

.PARAMETER Pack
    Asset pack to render with. Defaults to the one the launcher produces for 26.3.

.PARAMETER Server
    Server to load the world from. Defaults to the offline development server.

.PARAMETER Baseline
    Optional file holding a previous hash, as written by `-WriteBaseline`.

.PARAMETER WriteBaseline
    Write the observed hash to this path instead of comparing against it.

.EXAMPLE
    ./check-frame-baseline.ps1
    Renders twice and reports whether the two runs agree.

.EXAMPLE
    ./check-frame-baseline.ps1 -WriteBaseline artifacts/frame-baseline.txt
    Records the current picture as the baseline.

.EXAMPLE
    ./check-frame-baseline.ps1 -Baseline artifacts/frame-baseline.txt
    Fails when the picture has moved away from the recorded one.
#>
[CmdletBinding()]
param(
    [string] $Client = 'src/Sharpcraft.Client/bin/Debug/net10.0/Sharpcraft.Client.exe',
    [string] $Pack = "$env:LOCALAPPDATA\Sharpcraft\packs\minecraft-26.3-a117f327285a.pack.zip",
    [string] $Server = '127.0.0.1:25565',
    [string] $Baseline,
    [string] $WriteBaseline,
    [int] $Width = 640,
    [int] $Height = 360,
    [int] $TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $Client)) { throw "client not found: $Client" }
if (-not (Test-Path $Pack)) { throw "asset pack not found: $Pack" }

$frame = Join-Path $env:TEMP 'sharpcraft-frame.png'
$options = Join-Path $env:TEMP 'sharpcraft-frame-baseline.json'

# Noon, half a tick in: enough that lighting and celestial angles stop moving between runs.
'{"MC_FORCE_DAYTIME":"6000","MC_FORCE_PARTIAL":"0.5"}' | Set-Content -LiteralPath $options -Encoding UTF8

function Invoke-Frame([string] $tag)
{
    Remove-Item -LiteralPath $frame -Force -ErrorAction SilentlyContinue

    $stdout = Join-Path $env:TEMP "frame-$tag.out"
    $stderr = Join-Path $env:TEMP "frame-$tag.err"

    $arguments = @(
        '--version', '26.3',
        '--gameDir', "$env:APPDATA\.sharpcraft",
        '--pack', $Pack,
        '--offlineDeveloperMode', '--username', 'FrameBaseline',
        '--frame', '--quickPlayMultiplayer', $Server,
        '--width', "$Width", '--height', "$Height",
        '--debugOptionsFile', $options
    )

    $process = Start-Process -FilePath $Client -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (-not $process.WaitForExit($TimeoutSeconds * 1000))
    {
        $process.Kill()
        throw "the frame run '$tag' did not finish within $TimeoutSeconds seconds"
    }

    if (-not (Test-Path $frame)) { throw "the frame run '$tag' wrote no image; see $stderr" }

    $errorText = Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue
    if ($errorText -match 'Validation|panic') { throw "the frame run '$tag' reported a gpu error; see $stderr" }

    $bytes = (Get-Item -LiteralPath $frame).Length
    $hash = (Get-FileHash -LiteralPath $frame -Algorithm SHA256).Hash
    Write-Host "  $tag : $($hash.Substring(0, 16))… / $bytes bytes"
    return [pscustomobject]@{ Hash = $hash; Bytes = $bytes }
}

Write-Host 'rendering the same frame twice'
$first = Invoke-Frame 'a'
$second = Invoke-Frame 'b'

if ($first.Hash -ne $second.Hash)
{
    Write-Error "the two runs differ ($($first.Bytes) vs $($second.Bytes) bytes) even with the world state pinned; something in the frame is not deterministic"
    exit 1
}

Write-Host "  identical across runs"

if ($WriteBaseline)
{
    $directory = Split-Path -Parent $WriteBaseline
    if ($directory -and -not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory | Out-Null }
    "$($first.Hash) $($first.Bytes)" | Set-Content -LiteralPath $WriteBaseline -Encoding UTF8
    Write-Host "  baseline written to $WriteBaseline"
    exit 0
}

if ($Baseline)
{
    if (-not (Test-Path $Baseline)) { throw "baseline not found: $Baseline" }

    $recorded = (Get-Content -LiteralPath $Baseline -Raw).Trim().Split(' ')
    if ($recorded[0] -ne $first.Hash)
    {
        Write-Error "the picture moved away from the recorded baseline ($($recorded[0].Substring(0, 16))… -> $($first.Hash.Substring(0, 16))…); if this change was intended, rewrite the baseline"
        exit 1
    }

    Write-Host "  matches the recorded baseline"
}

exit 0
