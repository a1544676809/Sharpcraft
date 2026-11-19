// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-672/T-673/T-675: proves the DSL-baked humanoid parts match the 26.3 source-frame part records
/// (raw pivot/min/size literals, independent inflated hat child). Skeleton variants keep shared parts.
/// Expected records are independent source literals (RawModelPartFactory), never the C# bake output.
/// </summary>
public sealed class HumanoidMobModelDefinitionTests
{
    // HumanoidModel.createMesh: head 8x8x8 at min(-4,-8,-4); hat = head inflated grow 0.5 at tex(32,0).
    private static EntityModelPart Hat(float grow = 0.5f, int tx = 32, int ty = 0) =>
        RawModelPartFactory.FromVanillaChild("hat", "head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, tx, ty, grow: grow);

    // Drowned's translucent outer layer copies (DrownedOuter in the DSL).
    private static EntityModelLayer DrownedOuter(int textureX, int textureY) =>
        new("drowned_outer", 0.25f, textureX, textureY, false, "entity/zombie/drowned_outer_layer", EntityGeometryLayerKind.Translucent);

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
