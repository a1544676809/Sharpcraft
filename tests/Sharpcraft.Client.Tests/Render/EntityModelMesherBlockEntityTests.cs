// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Tests for <see cref="EntityModelMesher.BuildModel"/>, the block entity entry point added for Issue #59.
/// The API must reuse the existing entity geometry path byte for byte, so the expected streams here are
/// produced by <see cref="EntityModelMesher.Build"/> with the same fake state and model scale.
/// </summary>
public sealed class EntityModelMesherBlockEntityTests
{
    private const float EntityModelScale = EntityPlaceholderMesher.ModelScale;

    private static readonly int[] ModelTypes =
    [
        CopperGolemModelDefinition.CopperGolemEntityTypeId,
        ShulkerModelDefinition.ShulkerEntityTypeId,
        GhastModelDefinition.GhastEntityTypeId
    ];

    /// <summary>
    /// BuildModel must equal the existing entity path byte for byte for multiple positions and both
    /// declared texture sizes (64x64 and 64x32). State yaw 180 makes the entity path's identity
    /// orientation explicit; BuildModel applies the same zero rotation about the block centre.
    /// </summary>
    [Theory]
    [InlineData(0, 64, 64, 0f, 0f, 0f)]
    [InlineData(1, 64, 64, 3f, 2f, -4f)]
    [InlineData(2, 64, 32, -2f, 1f, 5f)]
    [InlineData(0, 64, 64, 7f, -1f, 2f)]
    [InlineData(2, 64, 32, 1f, 0.5f, -3f)]
    public void BuildModelMatchesTheExistingBuildStreams(int modelIndex, int textureWidth, int textureHeight, float x, float y, float z)
    {
        var typeId = ModelTypes[modelIndex];
        var model = EntityModelResolver.Resolve(typeId, false);
        var state = new EntityRenderState(1, typeId, x, y, z, Yaw: 180f, Pitch: 0f);
        var sprite = new SpriteRect(0, 0, textureWidth, textureHeight, 1);

        var expected = new EntityModelMesher().Build([state], sprite, textureWidth, textureHeight);
        var actual = new EntityModelMesher().BuildModel(
            model,
            state,
            sprite,
            textureWidth,
            textureHeight);

        Assert.Equal(expected.VertexFloats, actual.VertexFloats);
        Assert.Equal(expected.IndexCount, actual.IndexCount);
        Assert.Equal(
            expected.Vertices.AsSpan(0, expected.VertexFloats).ToArray(),
            actual.Vertices.AsSpan(0, actual.VertexFloats).ToArray());
        Assert.Equal(
            expected.Indices.AsSpan(0, expected.IndexCount).ToArray(),
            actual.Indices.AsSpan(0, actual.IndexCount).ToArray());
    }

    /// <summary>BuildModel normalises Ghast's UVs by its declared 64x32 texture, not the PNG resolution.</summary>
    [Fact]
    public void BuildModelUsesTheDeclaredTextureSize()
    {
        var state = new EntityRenderState(1, GhastModelDefinition.GhastEntityTypeId, 0f, 0f, 0f, Yaw: 180f, Pitch: 0f);
        var sprite = new SpriteRect(0, 0, 128, 64, 1);
        var geometry = new EntityModelMesher().BuildModel(
            GhastModelDefinition.Definition,
            state,
            sprite,
            textureWidth: 64,
            textureHeight: 32,
            modelScale: EntityModelScale);

        var maxU = 0f;
        var maxV = 0f;
        for (var vertex = 0; vertex < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[vertex + 4]);
            maxU = MathF.Max(maxU, (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFF)));
            maxV = MathF.Max(maxV, (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16)));
        }

        Assert.Equal(1f, maxU, 3);
        Assert.InRange(maxV, 0.9f, 1f);
    }

    /// <summary>A batch of states is built in one call with independent block positions.</summary>
    [Fact]
    public void BuildModelAcceptsABatchOfBlockEntities()
    {
        var model = CopperGolemModelDefinition.Definition;
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        EntityRenderState[] states =
        [
            new(1, CopperGolemModelDefinition.CopperGolemEntityTypeId, 0f, 0f, 0f, Yaw: 180f, Pitch: 0f),
            new(2, CopperGolemModelDefinition.CopperGolemEntityTypeId, 4f, 1f, -2f, Yaw: 180f, Pitch: 0f)
        ];

        var mesher = new EntityModelMesher();
        var single = mesher.BuildModel(model, states[0], sprite, 64, 64, modelScale: EntityModelScale);
        var singleVertexFloats = single.VertexFloats;
        var singleIndexCount = single.IndexCount;
        var batch = mesher.BuildModel(model, states, sprite, 64, 64, modelScale: EntityModelScale);

        Assert.Equal(2 * singleVertexFloats, batch.VertexFloats);
        Assert.Equal(2 * singleIndexCount, batch.IndexCount);
    }

    /// <summary>Facing rotates the model about the block's horizontal centre, not its origin.</summary>
    [Fact]
    public void BuildModelRotatesAboutTheBlockCentre()
    {
        var model = ShulkerModelDefinition.Definition;
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var identity = new EntityRenderState(1, ShulkerModelDefinition.ShulkerEntityTypeId, 2.5f, 3f, -0.5f, Yaw: 180f, Pitch: 0f);
        var rotated = new EntityRenderState(2, ShulkerModelDefinition.ShulkerEntityTypeId, 2.5f, 3f, -0.5f, Yaw: 90f, Pitch: 0f);
        var mesher = new EntityModelMesher();
        var identityBounds = Bounds(mesher.BuildModel(model, identity, sprite, 64, 64, modelScale: EntityModelScale));
        var rotatedBounds = Bounds(mesher.BuildModel(model, rotated, sprite, 64, 64, modelScale: EntityModelScale));

        var identityCentre = (identityBounds.Min + identityBounds.Max) * 0.5f;
        var rotatedCentre = (rotatedBounds.Min + rotatedBounds.Max) * 0.5f;
        Assert.Equal(identityCentre.X, rotatedCentre.X, 3);
        Assert.Equal(identityCentre.Z, rotatedCentre.Z, 3);
        Assert.Equal(identityCentre.Y, rotatedCentre.Y, 3);
    }

    private static (Vector3 Min, Vector3 Max) Bounds(DynamicGeometry geometry)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var vertex = 0; vertex < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var point = new Vector3(geometry.Vertices[vertex], geometry.Vertices[vertex + 1], geometry.Vertices[vertex + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }

    /// <summary>BuildModel validates declared texture dimensions and model scale at its boundary.</summary>
    [Theory]
    [InlineData(0, 64)]
    [InlineData(64, 0)]
    [InlineData(-1, 64)]
    [InlineData(64, -1)]
    public void BuildModelRejectsNonPositiveTextureSizes(int textureWidth, int textureHeight)
    {
        var state = new EntityRenderState(1, CopperGolemModelDefinition.CopperGolemEntityTypeId, 0f, 0f, 0f, Yaw: 180f, Pitch: 0f);
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EntityModelMesher().BuildModel(CopperGolemModelDefinition.Definition, state, sprite, textureWidth, textureHeight));
    }

    /// <summary>BuildModel reuses its DynamicGeometry and its state array for a steady batch.</summary>
    [Fact]
    public void BuildModelDoesNotAllocateOnceWarm()
    {
        var model = CopperGolemModelDefinition.Definition;
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        EntityRenderState[] states =
        [
            new(1, CopperGolemModelDefinition.CopperGolemEntityTypeId, 0f, 0f, 0f, Yaw: 180f, Pitch: 0f),
            new(2, CopperGolemModelDefinition.CopperGolemEntityTypeId, 4f, 1f, -2f, Yaw: 180f, Pitch: 0f)
        ];
        var mesher = new EntityModelMesher();
        mesher.BuildModel(model, states, sprite, 64, 64, modelScale: EntityModelScale);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.BuildModel(model, states, sprite, 64, 64, modelScale: EntityModelScale);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }
}
