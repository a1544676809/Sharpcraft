# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# AGENTS.md 3.5: every public Sharpcraft type must map to a reference (vanilla 26.3) class via
# [ReferenceClass(...)] or sit in docs/src/.classmap-baseline.txt (a shrinking ratchet). This is the same
# check enforced by tests/Sharpcraft.Architecture.Tests/ClassMapTests.cs.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $root 'tools/Sharpcraft.ClassMap') -- --check
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
