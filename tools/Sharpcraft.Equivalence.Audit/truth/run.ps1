# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
param(
    [Parameter(Mandatory = $true)][string]$Jar,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Jar)) { throw "version jar not found: $Jar" }

$versionDir = Split-Path -Parent $Jar
$versionJson = Get-ChildItem -Path $versionDir -Filter '*.json' | Select-Object -First 1
if (-not $versionJson) { throw "version json not found next to $Jar" }

$libraries = Join-Path (Split-Path -Parent (Split-Path -Parent $versionDir)) 'libraries'
$version = Get-Content $versionJson.FullName -Raw | ConvertFrom-Json

$classpath = [System.Collections.Generic.List[string]]::new()
$classpath.Add((Resolve-Path $Jar).Path)
foreach ($library in $version.libraries) {
    $relative = $library.downloads.artifact.path
    if (-not $relative) { continue }
    $candidate = Join-Path $libraries ($relative -replace '/', '\')
    if (Test-Path $candidate) { $classpath.Add($candidate) }
}

$build = Join-Path ([System.IO.Path]::GetTempPath()) ('equiv-probe-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $build | Out-Null
try {
    $source = Join-Path $PSScriptRoot 'ModelGeometryProbe.java'
    & javac -cp ($classpath -join ';') -d $build $source
    if ($LASTEXITCODE -ne 0) { throw "javac failed with exit code $LASTEXITCODE" }
    & java -cp ($build + ';' + ($classpath -join ';')) ModelGeometryProbe $Out
    if ($LASTEXITCODE -ne 0) { throw "java failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item -Recurse -Force $build -ErrorAction SilentlyContinue
}
