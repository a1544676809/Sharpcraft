// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Static skull head slice: the shared SkullModel head box, four supported texture families, and the
/// source ground/wall transformations with final-vertex truth samples.
/// </summary>
public sealed class SkullRendererTests
{
    private static readonly SkullType[] SupportedTypes =
    [
        SkullType.Skeleton,
        SkullType.WitherSkeleton,
        SkullType.Zombie,
        SkullType.Creeper
    ];

    [Fact]
    public void SkullDefinitionHasTheReferenceHeadBox()
    {
        Assert.Single(SkullModelDefinition.Definition.Parts);
        Assert.Equal(64, SkullModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, SkullModelDefinition.Definition.TextureHeight);
        Assert.Equal("head", SkullModelDefinition.Definition.Parts[0].Name);
        Assert.Equal("entity/skeleton/skeleton", SkullModelDefinition.Definition.TexturePath);
        Assert.True(ChestAtlas.Textures.Length == 22);
        Assert.Equal(4, ChestAtlas.SkullTextures.Length);
    }

    [Fact]
    public void SkullTypeMapsBlockAndWallPaths()
    {
        Assert.Equal(SkullType.Skeleton, SkullModelDefinition.TypeOfPath("skeleton_skull"));
        Assert.Equal(SkullType.Skeleton, SkullModelDefinition.TypeOfPath("skeleton_wall_skull"));
        Assert.Equal(SkullType.WitherSkeleton, SkullModelDefinition.TypeOfPath("wither_skeleton_skull"));
        Assert.Equal(SkullType.Zombie, SkullModelDefinition.TypeOfPath("zombie_wall_head"));
        Assert.Equal(SkullType.Creeper, SkullModelDefinition.TypeOfPath("creeper_head"));
        Assert.Equal(SkullType.None, SkullModelDefinition.TypeOfPath("player_head"));
        Assert.Equal(SkullType.None, SkullModelDefinition.TypeOfPath(null));
    }

    /// <summary>Four head families times ground/wall; all use the shared head box but distinct textures.</summary>
    [Theory]
    [InlineData(SkullType.Skeleton, false, 5, 0)]
    [InlineData(SkullType.WitherSkeleton, false, 5, 0)]
    [InlineData(SkullType.Zombie, false, 5, 0)]
    [InlineData(SkullType.Creeper, false, 5, 0)]
    [InlineData(SkullType.Skeleton, true, 2, 0)]
    [InlineData(SkullType.WitherSkeleton, true, 2, 0)]
    [InlineData(SkullType.Zombie, true, 2, 0)]
    [InlineData(SkullType.Creeper, true, 2, 0)]
    public void SkullGroundAndWallFinalVertices(SkullType type, bool wall, byte facing, byte rotation)
    {
        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: type, SkullWall: wall, SkullFacing: facing, SkullRotation: rotation)],
            Atlas());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        if (wall)
        {
            AssertVertex(geometry, 0, 0.25f, 0.75f, 1f, 0.25f, 0f);
            AssertVertex(geometry, 1, 0.75f, 0.75f, 1f, 0.375f, 0f);
        }
        else
        {
            AssertVertex(geometry, 0, 0.25f, 0.5f, 0.75f, 0.25f, 0f);
            AssertVertex(geometry, 1, 0.75f, 0.5f, 0.75f, 0.375f, 0f);
        }
    }

    [Fact]
    public void GroundRotationChangesTheHeadYaw()
    {
        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Skeleton, SkullRotation: 4)],
            Atlas());

        AssertVertex(geometry, 0, 0.25f, 0.5f, 0.25f, 0.25f, 0f);
        AssertVertex(geometry, 1, 0.25f, 0.5f, 0.75f, 0.375f, 0f);
    }

    [Fact]
    public void WallFacingChangesTheHeadYaw()
    {
        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Skeleton, SkullWall: true, SkullFacing: 3)],
            Atlas());

        AssertVertex(geometry, 0, 0.75f, 0.75f, 0f, 0.25f, 0f);
        AssertVertex(geometry, 1, 0.25f, 0.75f, 0f, 0.375f, 0f);
    }

    [Fact]
    public void RendererHandlesSkullOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Skull, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    private static ItemAtlas Atlas()
    {
        var pixels = new byte[64 * 32 * 4];
        return ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SkullModelDefinition.SkeletonTexture] = new(64, 32, pixels),
            [SkullModelDefinition.WitherSkeletonTexture] = new(64, 32, pixels),
            [SkullModelDefinition.ZombieTexture] = new(64, 32, pixels),
            [SkullModelDefinition.CreeperTexture] = new(64, 32, pixels)
        });
    }

    private static void AssertVertex(DynamicGeometry geometry, int vertex, float x, float y, float z, float u, float v)
    {
        var position = VertexFieldReader.Position(geometry.Vertices, vertex);
        var uv = VertexFieldReader.Uv(geometry.Vertices, vertex);
        Assert.Equal(x, position.X, 3);
        Assert.Equal(y, position.Y, 3);
        Assert.Equal(z, position.Z, 3);
        Assert.Equal(u, uv.U, 3);
        Assert.Equal(v, uv.V, 3);
    }
}

