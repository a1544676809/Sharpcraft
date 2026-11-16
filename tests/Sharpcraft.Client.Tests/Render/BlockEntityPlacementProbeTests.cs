// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Issue 100: a block entity's geometry must sit inside its own block. The per renderer golden
/// samples check shape and relative placement only, so a model that floats a block above the ground
/// or sinks half a block into it still passes them. These probes assert the absolute vertical range.
/// </summary>
/// <remarks>
/// The copper golem statue is not listed: reference sampling shows it is 1.5 blocks tall by design,
/// so a one block bound is wrong for it. Its reference vertices are recorded in
/// <c>Repro/Data/copper-golem-statue-vectors.golden.txt</c> and the comparison is tracked by issue 101.
/// </remarks>
public sealed class BlockEntityPlacementProbeTests
{
    [Fact]
    public void ASkullSitsInsideItsBlock()
    {
        var atlas = Atlas(SkullModelDefinition.TextureFor(SkullType.Skeleton)!);
        var geometry = new SkullRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Skeleton)],
            atlas);

        AssertInsideOneBlock(geometry, "skull");
    }

    [Fact]
    public void AConduitSitsInsideItsBlock()
    {
        var atlas = Atlas(ConduitModelDefinition.TexturePath);
        var geometry = new ConduitRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit)],
            atlas);

        AssertInsideOneBlock(geometry, "conduit");
    }

    private static void AssertInsideOneBlock(DynamicGeometry geometry, string label)
    {
        Assert.True(geometry.VertexFloats > 0, $"{label}: no geometry");

        var min = float.MaxValue;
        var max = float.MinValue;
        for (var vertex = 0; vertex + 2 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var y = geometry.Vertices[vertex + 1];
            min = System.MathF.Min(min, y);
            max = System.MathF.Max(max, y);
        }

        Assert.True(
            min >= -0.001f && max <= 1.001f,
            $"{label}: vertical range [{min}, {max}] leaves the block it belongs to (expected 0..1)");
    }

    private static ItemAtlas Atlas(params string[] paths)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            textures[path] = new TextureData(256, 256, new byte[256 * 256 * 4]);
        }

        return ItemAtlas.FromTextures(textures);
    }
}
