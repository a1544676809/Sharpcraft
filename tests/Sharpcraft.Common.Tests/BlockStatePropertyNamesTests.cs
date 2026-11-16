// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Every block state property name we read has to exist in the reference, otherwise the lookup misses
/// and the caller silently falls back to a default.
/// </summary>
public sealed class BlockStatePropertyNamesTests
{
    [Fact]
    public void TheReferenceDeclaresEveryNameWeRead()
    {
        Assert.NotEmpty(BlockStatePropertyNames.All);
        foreach (var name in BlockStatePropertyNames.All)
        {
            Assert.True(
                VanillaBlockStatePropertyNames.Contains(name),
                $"block state property \"{name}\" is not declared by the reference");
        }
    }

    [Fact]
    public void TheGeneratedTableIsNotEmptyAndSorted()
    {
        Assert.Equal(VanillaBlockStatePropertyNames.Count, VanillaBlockStatePropertyNames.Names.Count);
        var names = VanillaBlockStatePropertyNames.Names;
        for (var index = 1; index < names.Count; index++)
        {
            Assert.True(
                string.CompareOrdinal(names[index - 1], names[index]) < 0,
                $"the table is not sorted at {index}: {names[index - 1]} then {names[index]}");
        }
    }

    [Fact]
    public void ThePoseNameMatchesTheStatueBlock()
    {
        Assert.Equal("copper_golem_pose", BlockStatePropertyNames.CopperGolemPose);
        Assert.True(VanillaBlockStatePropertyNames.Contains(BlockStatePropertyNames.CopperGolemPose));
    }
}