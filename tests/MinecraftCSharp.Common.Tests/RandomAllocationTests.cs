// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Random;
using Xunit;

namespace MinecraftCSharp.Common.Tests;

/// <summary>
/// Every <c>[HotPath]</c> member needs an allocation test (AGENTS.md 3.6).
/// </summary>
public sealed class RandomAllocationTests
{
    [Fact]
    public void LegacyDrawsAllocateNothing()
    {
        var source = new LegacyRandomSource(42L);

        AllocationAssert.Zero(
            () =>
            {
                source.NextLong();
                source.NextInt();
                source.NextInt(100);
                source.NextBoolean();
                source.NextFloat();
                source.NextDouble();
            },
            "legacy draws run per tick and must not allocate");
    }

    [Fact]
    public void XoroshiroDrawsAllocateNothing()
    {
        var source = new XoroshiroRandomSource(42L);

        AllocationAssert.Zero(
            () =>
            {
                source.NextLong();
                source.NextInt();
                source.NextInt(100);
                source.NextBoolean();
                source.NextFloat();
                source.NextDouble();
                source.NextBits(53);
            },
            "xoroshiro draws run during world generation and must not allocate");
    }

    [Fact]
    public void SeedUpgradeAllocatesNothing()
    {
        AllocationAssert.Zero(
            () => _ = SeedSupport.UpgradeTo128Bit(1234L),
            "seed derivation happens per structure/chunk and must not allocate");
    }
}
