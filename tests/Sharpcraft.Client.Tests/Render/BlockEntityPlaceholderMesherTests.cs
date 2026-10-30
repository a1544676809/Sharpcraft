// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Block entity placeholders (docs/specs/render/pipeline.md, block entity stage): one inset box per entity, sitting on the
/// floor of its block, with the chest proportion taken from the reference model.
/// </summary>
public sealed class BlockEntityPlaceholderMesherTests
{
    private static SpriteRect Sprite() => new(0, 0, 16, 16, 1);

    [Fact]
    public void OneEntityBecomesOneBox()
    {
        var mesher = new BlockEntityPlaceholderMesher();
        var geometry = mesher.Build([new BlockEntityRenderState(10f, 64f, -3f, 1)], Sprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(24 * 12, geometry.VertexFloats);
        Assert.Equal(36, geometry.IndexCount);
    }

    [Fact]
    public void TheBoxIsInsetAndSitsOnTheBlockFloor()
    {
        var mesher = new BlockEntityPlaceholderMesher();
        var geometry = mesher.Build([new BlockEntityRenderState(10f, 64f, -3f, 1)], Sprite());
        var (minX, maxX, minY, _, minZ, maxZ) = Bounds(geometry);

        Assert.Equal(BlockEntityPlaceholderMesher.ChestSize, maxX - minX, 3);
        Assert.Equal(BlockEntityPlaceholderMesher.ChestSize, maxZ - minZ, 3);
        // The reference chest model sits on the block floor.
        Assert.Equal(64f, minY, 4);
        Assert.Equal(10.5f, (minX + maxX) / 2f, 3);
        Assert.Equal(-2.5f, (minZ + maxZ) / 2f, 3);
    }

    [Fact]
    public void TheChestIsLargerThanTheGenericBox()
    {
        Assert.True(BlockEntityPlaceholderMesher.ChestSize > BlockEntityPlaceholderMesher.GenericSize);
    }

    [Fact]
    public void NoEntitiesProducesNoGeometry()
    {
        var mesher = new BlockEntityPlaceholderMesher();
        var geometry = mesher.Build([], Sprite());

        Assert.Equal(0, geometry.IndexCount);
        Assert.Equal(0, mesher.LastEntityCount);
    }

    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new BlockEntityPlaceholderMesher();
        var entities = new BlockEntityRenderState[4];

        for (var index = 0; index < entities.Length; index++)
        {
            entities[index] = new BlockEntityRenderState(index, 64f, 0f, 1);
        }

        // The sprite is created outside the measured region: it is a reference type, so making one would be counted.
        var sprite = Sprite();
        mesher.Build(entities, sprite);

        var before = System.GC.GetAllocatedBytesForCurrentThread();
        mesher.Build(entities, sprite);
        var after = System.GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private static (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) Bounds(Sharpcraft.Framework.Rendering.DynamicGeometry geometry)
    {
        var minX = float.MaxValue; var maxX = float.MinValue;
        var minY = float.MaxValue; var maxY = float.MinValue;
        var minZ = float.MaxValue; var maxZ = float.MinValue;

        for (var index = 0; index + 12 <= geometry.VertexFloats; index += 12)
        {
            var x = geometry.Vertices[index];
            var y = geometry.Vertices[index + 1];
            var z = geometry.Vertices[index + 2];

            minX = System.MathF.Min(minX, x); maxX = System.MathF.Max(maxX, x);
            minY = System.MathF.Min(minY, y); maxY = System.MathF.Max(maxY, y);
            minZ = System.MathF.Min(minZ, z); maxZ = System.MathF.Max(maxZ, z);
        }

        return (minX, maxX, minY, maxY, minZ, maxZ);
    }
}
