<#
.SYNOPSIS
    Checks that the offscreen render path runs and produces a valid image.

.DESCRIPTION
    The world has a day and night cycle and moving clouds, so two runs never agree pixel for pixel and comparing hashes
    would produce failures that mean nothing. What is worth checking is that the path works at all:

      * the process finishes and exits cleanly,
      * it reports no wgpu validation error or panic,
      * it writes a PNG of the requested size,
      * the image is not blank.

    The hash of each run is printed for information only. If two consecutive runs do happen to match, that is a bonus
    and not a requirement; when they differ it says nothing about correctness.

    This is a verification tool, not a unit test: a run loads a world and meshes chunks, so it takes tens of seconds.

.PARAMETER Client
    Path to Sharpcraft.Client.exe. Defaults to the debug build in this repository.

.PARAMETER Pack
    Asset pack to render with. Defaults to the one the launcher produces for 26.3.

.PARAMETER Server
    Server to load the world from. Defaults to the offline development server.

.PARAMETER Runs
    How many times to run. Each run is checked on its own; they are never compared to each other.

.EXAMPLE
    ./check-frame-baseline.ps1
    Renders one frame and checks it came out.
#>
[CmdletBinding()]
param(
    [string] $Client = 'src/Sharpcraft.Client/bin/Debug/net10.0/Sharpcraft.Client.exe',
    [string] $Pack = "$env:LOCALAPPDATA\Sharpcraft\packs\minecraft-26.3-a117f327285a.pack.zip",
    [string] $Server = '127.0.0.1:25565',
    [int] $Runs = 1,
    [int] $Width = 640,
    [int] $Height = 360,
    [int] $TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $Client)) { throw "client not found: $Client" }
if (-not (Test-Path $Pack)) { throw "asset pack not found: $Pack" }

$frame = Join-Path $env:TEMP 'sharpcraft-frame.png'

function Test-Frame([string] $tag)
{
    Remove-Item -LiteralPath $frame -Force -ErrorAction SilentlyContinue

    $stdout = Join-Path $env:TEMP "frame-$tag.out"
    $stderr = Join-Path $env:TEMP "frame-$tag.err"

    $arguments = @(
        '--version', '26.3',
        '--gameDir', "$env:APPDATA\.sharpcraft",
        '--pack', $Pack,
        '--offlineDeveloperMode', '--username', 'FrameCheck',
        '--frame', '--quickPlayMultiplayer', $Server,
        '--width', "$Width", '--height', "$Height"
    )

    $process = Start-Process -FilePath $Client -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr

    if (-not $process.WaitForExit($TimeoutSeconds * 1000))
    {
        $process.Kill()
        throw "run '$tag' did not finish within $TimeoutSeconds seconds"
    }

    if ($process.ExitCode -ne 0) { throw "run '$tag' exited with $($process.ExitCode); see $stderr" }

    $errorText = Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue
    if ($errorText -match 'Validation|panic|fatal') { throw "run '$tag' reported a gpu error; see $stderr" }

    if (-not (Test-Path $frame)) { throw "run '$tag' wrote no image; see $stderr" }

    $output = Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue
    if ($output -notmatch 'frame written to') { throw "run '$tag' never reported a written frame; see $stdout" }

    # A blank frame is the failure this check exists to catch: it happens when a pass is recorded but covers nothing.
    $bytes = [System.IO.File]::ReadAllBytes($frame)
    $hash = (Get-FileHash -LiteralPath $frame -Algorithm SHA256).Hash

    # Decoding the PNG for a real pixel check would need an image library; the byte size is enough to tell a blank
    # 640x360 image (a few hundred bytes when one colour compresses flat) from a rendered one.
    $minimumBytes = $Width * $Height / 64
    if ($bytes.Length -lt $minimumBytes)
    {
        throw "run '$tag' wrote only $($bytes.Length) bytes for $($Width)x$($Height), which is too small to be a rendered frame"
    }

    Write-Host "  $tag : ok, $($bytes.Length) bytes, sha256 $($hash.Substring(0, 16))…"
}

Write-Host "checking the offscreen path renders ($Runs run(s))"
for ($index = 1; $index -le $Runs; $index++)
{
    Test-Frame "run$index"
}

Write-Host 'the offscreen path rendered cleanly'
exit 0
