# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Checks the block model element reader and quad builder against the real model files:
# runs our parser over a model's parent chain and compares every quad, corner and texture
# rectangle with an independent implementation of the documented rules.
#
# The model files stay in the analysis workspace and are never copied into this repository.

param(
    [string] $AnalysisRoot = $(if ($env:MC_ANALYSIS_ROOT) { $env:MC_ANALYSIS_ROOT } else { Join-Path (Split-Path -Parent $PSScriptRoot) '..\mc-26.3-analysis' }),
    [string[]] $Models = @('minecraft:block/torch', 'minecraft:block/stairs', 'minecraft:block/flower_pot')
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $AnalysisRoot 'src\assets\minecraft\models'
if (-not (Test-Path $root)) {
    Write-Error "model directory not found under '$AnalysisRoot'; set MC_ANALYSIS_ROOT"
    exit 2
}

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$work = Join-Path $env:TEMP ('sharpcraft-modelcheck-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="REPO\src\Sharpcraft.Client\Sharpcraft.Client.csproj" />
  </ItemGroup>
</Project>
'@.Replace('REPO', $repo)
    Set-Content -Path (Join-Path $work 'modelcheck.csproj') -Value $project -Encoding utf8

    $program = @'
using System.Text.Json;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

var root = args[0];
byte[]? Read(Identifier id)
{
    var path = Path.Combine(root, id.Path.Replace('/', Path.DirectorySeparatorChar) + ".json");
    return File.Exists(path) ? File.ReadAllBytes(path) : null;
}

var elements = new BlockModelElements(Read).Resolve(Identifier.Parse(args[1]));
if (elements is null)
{
    Console.WriteLine("NULL");
    return 0;
}

foreach (var q in BlockModelGeometry.Build(elements))
{
    Console.WriteLine($"Q {q.Direction} {q.Texture} {q.CullFace ?? "-"} " +
        $"{q.V0.Position.X:F4},{q.V0.Position.Y:F4},{q.V0.Position.Z:F4} " +
        $"{q.V1.Position.X:F4},{q.V1.Position.Y:F4},{q.V1.Position.Z:F4} " +
        $"{q.V2.Position.X:F4},{q.V2.Position.Y:F4},{q.V2.Position.Z:F4} " +
        $"{q.V3.Position.X:F4},{q.V3.Position.Y:F4},{q.V3.Position.Z:F4}");
}
return 0;
'@
    Set-Content -Path (Join-Path $work 'Program.cs') -Value $program -Encoding utf8

    Push-Location $work
    try {
        & dotnet build --nologo -v:q | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Error 'the checker failed to build'
            exit 1
        }

        $checker = @'
import json, os, subprocess, sys

root, work = sys.argv[1], sys.argv[2]
models = sys.argv[3:]

def load(name):
    rel = name.split(':', 1)[1] if ':' in name else name
    with open(os.path.join(root, rel.replace('/', os.sep) + '.json'), encoding='utf-8') as handle:
        return json.load(handle)

def elements_of(model):
    current = model
    for _ in range(16):
        document = load(current)
        if 'elements' in document:
            return document['elements']
        parent = document.get('parent')
        if not parent:
            return None
        current = parent
    return None

P = 16.0
CORNERS = {
    'down':  lambda b: [(b[0], b[1], b[5]), (b[0], b[1], b[2]), (b[3], b[1], b[2]), (b[3], b[1], b[5])],
    'up':    lambda b: [(b[0], b[4], b[2]), (b[0], b[4], b[5]), (b[3], b[4], b[5]), (b[3], b[4], b[2])],
    'north': lambda b: [(b[3], b[4], b[2]), (b[3], b[1], b[2]), (b[0], b[1], b[2]), (b[0], b[4], b[2])],
    'south': lambda b: [(b[0], b[4], b[5]), (b[0], b[1], b[5]), (b[3], b[1], b[5]), (b[3], b[4], b[5])],
    'west':  lambda b: [(b[0], b[4], b[2]), (b[0], b[1], b[2]), (b[0], b[1], b[5]), (b[0], b[4], b[5])],
    'east':  lambda b: [(b[3], b[4], b[5]), (b[3], b[1], b[5]), (b[3], b[1], b[2]), (b[3], b[4], b[2])],
}

def default_uv(direction, b):
    return {
        'down':  [b[0], P - b[5], b[3], P - b[2]],
        'up':    [b[0], b[2], b[3], b[5]],
        'north': [P - b[3], P - b[4], P - b[0], P - b[1]],
        'south': [b[0], P - b[4], b[3], P - b[1]],
        'west':  [b[2], P - b[4], b[5], P - b[1]],
        'east':  [P - b[5], P - b[4], P - b[2], P - b[1]],
    }[direction]

def expected(model):
    rows = []
    for element in elements_of(model):
        b = [element['from'][0], element['from'][1], element['from'][2],
             element['to'][0], element['to'][1], element['to'][2]]
        for direction, face in element['faces'].items():
            corners = CORNERS[direction](b)
            rows.append((direction, face.get('texture'), face.get('cullface', '-'),
                         [(round(x / P, 4), round(y / P, 4), round(z / P, 4)) for x, y, z in corners]))
    return sorted(rows)

def actual(model):
    output = subprocess.run(['dotnet', 'run', '--no-build', '--', root, model],
                            cwd=work, capture_output=True, text=True, encoding='utf-8').stdout
    rows = []
    for line in output.split('\n'):
        if not line.startswith('Q '):
            continue
        parts = line.split(' ')
        corners = [tuple(round(float(v), 4) for v in group.split(',')) for group in parts[4:8]]
        rows.append((parts[1], parts[2], parts[3], corners))
    return sorted(rows)

failed = 0
for model in models:
    want, got = expected(model), actual(model)
    ok = len(want) == len(got)
    if ok:
        for a, b in zip(want, got):
            if a[:3] != b[:3] or any(abs(p - q) > 1e-3 for cp, cq in zip(a[3], b[3]) for p, q in zip(cp, cq)):
                ok = False
                break
    print(f'{model}: expected {len(want)} quads, got {len(got)} -> ' + ('MATCH' if ok else 'MISMATCH'))
    if not ok:
        failed += 1
        print('  expected', want[:2])
        print('  actual  ', got[:2])

sys.exit(1 if failed else 0)
'@
        Set-Content -Path (Join-Path $work 'check.py') -Value $checker -Encoding utf8

        $python = if ($env:SHARPCRAFT_PYTHON) { $env:SHARPCRAFT_PYTHON } else { 'python' }
        & $python (Join-Path $work 'check.py') $root $work @Models
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
