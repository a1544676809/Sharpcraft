// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Placeholder entity boxes (docs/specs/network/entity-sync.md 6). The shapes only need to be visible and
/// obviously attached to a tracked entity, so the tests pin the counts, the bounds and the absence of
/// per-frame allocation rather than pixels.
/// </summary>
public sealed class EntityPlaceholderMesherTests
{
    [Fact]
    public void APlayerIsDrawnFromSixBoxes()
    {
        var mesher = new EntityPlaceholderMesher();

        var geometry = mesher.Build([State(159, 10f, 64f, -3f)], Sprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(EntityPlaceholderMesher.PlayerPartCount * 24 * 12, geometry.VertexFloats);
        Assert.Equal(EntityPlaceholderMesher.PlayerPartCount * 36, geometry.IndexCount);
    }

    [Fact]
    public void AMobIsDrawnFromOneBox()
    {
        var mesher = new EntityPlaceholderMesher();

        var geometry = mesher.Build([State(11, 10f, 64f, -3f)], Sprite());

        Assert.Equal(24 * 12, geometry.VertexFloats);
        Assert.Equal(36, geometry.IndexCount);
    }

    [Fact]
    public void TheBoxSitsOnTheFeetAndIsPlayerSized()
    {
        var mesher = new EntityPlaceholderMesher();
        const int playerTypeId = 159;

        var geometry = mesher.Build([State(playerTypeId, 5f, 70f, 5f)], Sprite());

        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry);

        // The arms reach past the collision box, as they do in the reference; the height is what has to match.
        Assert.InRange(maxX - minX, EntityPlaceholderMesher.PlayerWidth, 1.05f);
        Assert.Equal(EntityPlaceholderMesher.PlayerHeight, maxY - minY, 3);
        // Depth is the body depth; only the arms widen the silhouette.
        Assert.Equal(0.5f, maxZ - minZ, 3);
        Assert.Equal(70f, minY, 3);
        Assert.Equal(5f, (minX + maxX) / 2f, 3);
    }

    [Fact]
    public void AMobUsesTheGenericBox()
    {
        var mesher = new EntityPlaceholderMesher();

        var geometry = mesher.Build([State(11, 0f, 0f, 0f)], Sprite());

        var (minX, maxX, _, maxY, _, _) = Bounds(geometry);

        Assert.Equal(EntityPlaceholderMesher.MobWidth, maxX - minX, 3);
        Assert.Equal(EntityPlaceholderMesher.MobHeight, maxY, 3);
    }

    [Fact]
    public void EveryEntityAddsOneBox()
    {
        var mesher = new EntityPlaceholderMesher();

        var geometry = mesher.Build(
            [State(1, 0, 0, 0), State(2, 4, 0, 0), State(3, 8, 0, 0)],
            Sprite());

        Assert.Equal(3, mesher.LastEntityCount);
        Assert.Equal(3 * 24 * 12, geometry.VertexFloats);
        Assert.Equal(3 * 36, geometry.IndexCount);
    }

    [Fact]
    public void NoEntitiesProducesNoGeometry()
    {
        var mesher = new EntityPlaceholderMesher();

        var geometry = mesher.Build([], Sprite());

        Assert.Equal(0, mesher.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new EntityPlaceholderMesher();
        EntityRenderState[] states = [State(1, 0, 0, 0), State(2, 4, 0, 0)];

        var sprite = Sprite(); // hoisted: SpriteRect is a reference type, so building one per frame would allocate
        mesher.Build(states, sprite); // warm the buffers

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.Build(states, sprite);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static EntityRenderState State(int typeId, float x, float y, float z) =>
        new(Id: typeId, typeId, x, y, z, Yaw: 0f, Pitch: 0f);

    private static SpriteRect Sprite() =>
        new(X: 0, Y: 0, Width: 16, Height: 16, FrameCount: 1) { U0 = 0f, V0 = 0f, U1 = 0.015f, V1 = 0.015f };

    private static (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) Bounds(
        Framework.Rendering.DynamicGeometry geometry)
    {
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;

        for (var offset = 0; offset < geometry.VertexFloats; offset += 12)
        {
            var x = geometry.Vertices[offset];
            var y = geometry.Vertices[offset + 1];
            var z = geometry.Vertices[offset + 2];
            minX = MathF.Min(minX, x);
            maxX = MathF.Max(maxX, x);
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
            minZ = MathF.Min(minZ, z);
            maxZ = MathF.Max(maxZ, z);
        }

        return (minX, maxX, minY, maxY, minZ, maxZ);
    }
}
