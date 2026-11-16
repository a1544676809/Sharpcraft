// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Inactive conduit shell slice: source bounds, UV and registration; the active cage/wind/eye
/// branches are intentionally absent.
/// </summary>
public sealed class ConduitRendererTests
{
    private readonly ItemAtlas _atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
    {
        [ConduitModelDefinition.TexturePath] = new(32, 16, new byte[32 * 16 * 4])
    });

    [Fact]
    public void ConduitTextureStaysOutsideTheChestOnlyList()
    {
        Assert.Equal(22, ChestAtlas.Textures.Length);
        Assert.Equal("entity/conduit/base", ChestAtlas.ConduitTexture);
    }

    [Fact]
    public void HandlesConduitOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Conduit, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    [Fact]
    public void ShellHasSourceBoundsAndUv()
    {
        var renderer = new ConduitRenderer();
        var geometry = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit)],
            _atlas);

        var min = (X: float.MaxValue, Y: float.MaxValue, Z: float.MaxValue);
        var max = (X: float.MinValue, Y: float.MinValue, Z: float.MinValue);
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            var position = VertexFieldReader.Position(geometry.Vertices, vertex);
            min = (MathF.Min(min.X, position.X), MathF.Min(min.Y, position.Y), MathF.Min(min.Z, position.Z));
            max = (MathF.Max(max.X, position.X), MathF.Max(max.Y, position.Y), MathF.Max(max.Z, position.Z));
        }

        var first = VertexFieldReader.Position(geometry.Vertices, 0);
        var uv = VertexFieldReader.Uv(geometry.Vertices, 0);

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        Assert.Equal(0.3125f, min.X, 4);
        Assert.Equal(0.3125f, min.Y, 4);
        Assert.Equal(0.3125f, min.Z, 4);
        Assert.Equal(0.6875f, max.X, 4);
        Assert.Equal(0.6875f, max.Y, 4);
        Assert.Equal(0.6875f, max.Z, 4);
        Assert.Equal(0.6875f, first.X, 4);
        Assert.Equal(0.3125f, first.Y, 4);
        Assert.Equal(0.6875f, first.Z, 4);
        Assert.Equal(0.375f, uv.U, 4);
        Assert.Equal(0f, uv.V, 4);
        Assert.Equal(1, renderer.LastEntityCount);
    }

    [Fact]
    public void ActiveConduitStillProducesTheStaticShell()
    {
        var renderer = new ConduitRenderer();
        var geometry = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit, ConduitActive: true)],
            _atlas);

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        Assert.Equal(1, renderer.LastEntityCount);
    }

    [Fact]
    public void DispatcherKeepsConduitShellForBothActivityStates()
    {
        var dispatcher = new Sharpcraft.Client.Render.BlockEntityRenderDispatcher([new ConduitRenderer()]);
        var atlas = _atlas;
        var inactive = dispatcher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit, ConduitActive: false)],
            atlas);
        var active = dispatcher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit, ConduitActive: true)],
            atlas);

        Assert.NotNull(inactive);
        Assert.NotNull(active);
        Assert.True(inactive.SolidIndexCount > 0);
        Assert.True(active.SolidIndexCount > 0);
        Assert.Equal(inactive.SolidIndexCount, active.SolidIndexCount);
    }

    [Fact]
    public void ProductionStateDefaultsToInactiveZeroRotation()
    {
        var state = new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit);
        Assert.False(state.ConduitActive);
        Assert.Equal(0f, state.ConduitRotation);
    }

    [Fact]
    public void ConduitRotationIsCarriedIntoTheRenderState()
    {
        var renderer = new ConduitRenderer();
        var unrotated = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit)],
            _atlas);
        var unrotatedFirst = VertexFieldReader.Position(unrotated.Vertices, 0);
        var rotated = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit, ConduitRotation: 90f)],
            _atlas);

        Assert.Equal(24, rotated.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, rotated.IndexCount);
        Assert.Equal(1, renderer.LastEntityCount);
        Assert.NotEqual(unrotatedFirst, VertexFieldReader.Position(rotated.Vertices, 0));
    }
}
