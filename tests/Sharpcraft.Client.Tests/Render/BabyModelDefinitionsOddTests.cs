// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-700: proves the DSL-baked baby strider/snifflet/armadillo parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsOddTests
{

    [Fact]
    public void BabyArmadilloPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("body", 0f, 20f, 0.5f, -2.5f, -2f, -3.5f, 5f, 4f, 7f, 0, 0, grow: 0.3f) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -2.5f, -2f, -3f, 5f, 4f, 6f, 0, 11) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanillaChild("tail", "body", 0f, 0f, 3.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanillaChild("tail_cube", "tail", 0f, 1.5f, 1f, -0.5f, -0.5f, -2f, 1f, 1f, 4f, 22, 11, rotationX: -1.0472f) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanillaChild("head", "body", 0f, 0f, -3.2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -1f, -2f, -4f, 2f, 2f, 4f, 20, 17, rotationX: 0.7417649f),
            RawModelPartFactory.FromVanillaChild("right_ear", "head_cube", -1f, -2f, -0.3f, -1.8f, -2f, 0f, 2f, 3f, 0f, 28, 8, mirror: true, rotationX: -0.4363f, rotationY: -0.1134f, rotationZ: 0.0524f),
            RawModelPartFactory.FromVanillaChild("left_ear", "head_cube", 1f, -2f, -0.3f, -0.2f, -2f, 0f, 2f, 3f, 0f, 28, 8, rotationX: -0.4363f, rotationY: 0.1134f, rotationZ: -0.0524f),
            RawModelPartFactory.FromVanilla("right_hind_leg", -1.5f, 22f, 2.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 27, mirror: true) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanilla("left_hind_leg", 1.5f, 22f, 2.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 27) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanilla("right_front_leg", 1.5f, 22f, -1.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 23) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanilla("left_front_leg", -1.5f, 22f, -1.5f, -1f, 0f, -1f, 2f, 2f, 2f, 24, 0, mirror: true) with { VisibleWhen = Unrolled },
            RawModelPartFactory.FromVanilla("cube", 0f, 20.7f, 0.5f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 25, grow: 0.3f) with { VisibleWhen = Rolled }
        };
        Assert.Same(BabyModelDefinitionsHumanoid.Zombie.Parts, BabyModelDefinitionsOdd.Husk.Parts);
    }

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
