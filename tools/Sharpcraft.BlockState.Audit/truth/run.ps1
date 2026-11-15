# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Dumps every block state's property values from a Minecraft version jar.

.DESCRIPTION
    Builds the version's classpath from the libraries listed next to the jar, compiles
    BlockStateDump.java into a temporary directory, and runs it. Nothing is written into the
    repository: the output goes wherever -Out points.

.PARAMETER Jar
    Path to the version jar, for example <versions>/26.3/26.3.jar.

.PARAMETER Out
    Path of the TSV to write.

.EXAMPLE
    ./run.ps1 -Jar "$env:APPDATA/.minecraft/versions/26.3/26.3.jar" -Out "$env:TEMP/block-states.tsv"
#>
param(
    [Parameter(Mandatory = $true)][string]$Jar,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Jar)) {
    throw "version jar not found: $Jar"
}

$versionDir = Split-Path -Parent $Jar
$versionJson = Get-ChildItem -Path $versionDir -Filter '*.json' | Select-Object -First 1
if (-not $versionJson) {
    throw "version json not found next to $Jar"
}

# The libraries live in the launcher's .minecraft/libraries, three levels above the version folder.
$libraries = Join-Path (Split-Path -Parent (Split-Path -Parent $versionDir)) 'libraries'
$version = Get-Content $versionJson.FullName -Raw | ConvertFrom-Json

$classpath = [System.Collections.Generic.List[string]]::new()
$classpath.Add((Resolve-Path $Jar).Path)
foreach ($library in $version.libraries) {
    $relative = $library.downloads.artifact.path
    if (-not $relative) {
        continue
    }

    $candidate = Join-Path $libraries ($relative -replace '/', '\')
    if (Test-Path $candidate) {
        $classpath.Add($candidate)
    }
}

$build = Join-Path ([System.IO.Path]::GetTempPath()) ('block-state-dump-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $build | Out-Null
try {
    $source = Join-Path $PSScriptRoot 'BlockStateDump.java'
    & javac -cp ($classpath -join ';') -d $build $source
    if ($LASTEXITCODE -ne 0) {
        throw "javac failed with exit code $LASTEXITCODE"
    }

    & java -cp ($build + ';' + ($classpath -join ';')) BlockStateDump $Out
    if ($LASTEXITCODE -ne 0) {
        throw "java failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -Recurse -Force $build -ErrorAction SilentlyContinue
}

Write-Host "wrote $Out"
