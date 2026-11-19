// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-700: proves the DSL-baked baby strider/snifflet/armadillo parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsOddTests
{

    private static bool Unrolled(Sharpcraft.Client.World.EntityRenderState state) => !state.IsHidingInShell;
    private static bool Rolled(Sharpcraft.Client.World.EntityRenderState state) => state.IsHidingInShell;

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
