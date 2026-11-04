// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Collection for tests that are sensitive to parallel JIT/GC noise (steady-state allocation assertions) or to
/// ordering-sensitive static state. Running them serialized removes the intermittent failures seen under the default
/// parallel test run without changing what they assert.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollection
{
    public const string Name = "NonParallel";
}
