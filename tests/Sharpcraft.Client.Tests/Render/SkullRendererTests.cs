// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;
using System.Numerics;

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
        SkullType.Creeper,
        SkullType.Player,
        SkullType.Piglin,
        SkullType.Dragon
    ];

    [Fact]
    public void SkullDefinitionHasTheReferenceHeadBox()
    {
        Assert.Single(SkullModelDefinition.Definition.Parts);
        Assert.Equal(64, SkullModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, SkullModelDefinition.Definition.TextureHeight);
        Assert.Equal("head", SkullModelDefinition.Definition.Parts[0].Name);
        Assert.Equal("entity/skeleton/skeleton", SkullModelDefinition.Definition.TexturePath);
        var head = SkullModelDefinition.Definition.Parts[0];
        Assert.Equal(0f, head.CentreX);
        Assert.Equal(0f, head.Bottom);
        Assert.Equal(8f, head.Width);
        Assert.Equal(8f, head.Height);
        Assert.Equal(8f, head.Depth);
        Assert.Equal(0, head.TextureX);
        Assert.Equal(0, head.TextureY);
        Assert.Equal(1f, head.ScaleX);
        Assert.Equal(1f, head.ScaleY);
        Assert.Equal(1f, head.ScaleZ);
        Assert.True(ChestAtlas.Textures.Length == 22);
        Assert.Equal(7, ChestAtlas.SkullTextures.Length);
    }

    [Fact]
    public void DragonDefinitionHasSevenBoxHeadAndJaw()
    {
        Assert.Equal(256, SkullModelDefinition.DragonDefinition.TextureWidth);
        Assert.Equal(256, SkullModelDefinition.DragonDefinition.TextureHeight);
        Assert.Equal("entity/enderdragon/dragon", SkullModelDefinition.DragonDefinition.TexturePath);
        Assert.Equal(7, SkullModelDefinition.DragonDefinition.Parts.Count);
        Assert.Equal(1f, SkullModelDefinition.DragonDefinition.Parts[0].ScaleX);
        Assert.Equal(1f, SkullModelDefinition.DragonDefinition.Parts[0].ScaleY);
        Assert.Equal(1f, SkullModelDefinition.DragonDefinition.Parts[0].ScaleZ);
        Assert.Same(SkullModelDefinition.DragonDefinition, SkullModelDefinition.DefinitionFor(SkullType.Dragon));

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Dragon)],
            DragonAtlas());
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(168, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(252, geometry.IndexCount);
        var bounds = Bounds(geometry);
        Assert.Equal(0.125f, bounds.Min.X, 3);
        Assert.Equal(-0.0006f, bounds.Min.Y, 3);
        Assert.Equal(-0.625f, bounds.Min.Z, 3);
        Assert.Equal(0.875f, bounds.Max.X, 3);
        Assert.Equal(0.9369f, bounds.Max.Y, 3);
        Assert.Equal(0.7813f, bounds.Max.Z, 3);
        AssertVertex(geometry, 0, 0.21875f, 0.42125f, 0.125f, 0.76953f, 0.17188f);
        AssertVertex(geometry, 1, 0.78125f, 0.42125f, 0.125f, 0.80469f, 0.17188f);
        AssertVertex(geometry, 2, 0.78125f, 0.42125f, -0.625f, 0.80469f, 0.21875f);
        AssertVertex(geometry, 4, 0.21875f, 0.18688f, -0.625f, 0.73438f, 0.21875f);
        AssertVertex(geometry, 8, 0.78125f, 0.42125f, -0.625f, 0.76953f, 0.2334f);
    }

    [Fact]
    public void DragonTextureMapsToTheDragonSkinAtlas()
    {
        var atlas = DragonAtlas();
        Assert.Equal("entity/enderdragon/dragon", SkullModelDefinition.TextureFor(SkullType.Dragon));
        Assert.Equal("entity/enderdragon/dragon", ChestAtlas.SkullTextures[6]);
        var sprite = ChestAtlas.SpriteForSkull(atlas, SkullType.Dragon);
        Assert.NotNull(sprite);
        Assert.Equal(atlas.Get("entity/enderdragon/dragon"), sprite);
    }

    [Fact]
    public void PiglinDefinitionHasSixBoxHead()
    {
        Assert.Equal(64, PiglinModelDefinition.Head.TextureWidth);
        Assert.Equal(64, PiglinModelDefinition.Head.TextureHeight);
        Assert.Equal("entity/piglin/piglin", PiglinModelDefinition.Head.TexturePath);
        Assert.Same(PiglinModelDefinition.Head, SkullModelDefinition.DefinitionFor(SkullType.Piglin));

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Piglin)],
            PiglinAtlas());
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(144, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(216, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.1875f, 0.5f, 0.75f, 0.28125f, 0f);
        AssertVertex(geometry, 1, 0.8125f, 0.5f, 0.75f, 0.4375f, 0f);
        AssertVertex(geometry, 2, 0.8125f, 0.5f, 0.25f, 0.4375f, 0.125f);
        AssertVertex(geometry, 4, 0.1875f, 0f, 0.25f, 0.125f, 0.125f);
        var bounds = Bounds(geometry);
        Assert.Equal(-0.2416f, bounds.Min.X, 3);
        Assert.Equal(0f, bounds.Min.Y, 3);
        Assert.Equal(0.1875f, bounds.Min.Z, 3);
        Assert.Equal(1.2416f, bounds.Max.X, 3);
        Assert.Equal(0.5f, bounds.Max.Y, 3);
        Assert.Equal(0.75f, bounds.Max.Z, 3);
    }

    [Fact]
    public void PiglinTextureMapsToThePiglinSkinAtlas()
    {
        var atlas = PiglinAtlas();
        Assert.Equal("entity/piglin/piglin", SkullModelDefinition.TextureFor(SkullType.Piglin));
        Assert.Equal("entity/piglin/piglin", ChestAtlas.SkullTextures[5]);
        var sprite = ChestAtlas.SpriteForSkull(atlas, SkullType.Piglin);
        Assert.NotNull(sprite);
        Assert.Equal(atlas.Get("entity/piglin/piglin"), sprite);
    }

    [Fact]
    public void PlayerDefinitionHasHeadAndHatOverlay()
    {
        Assert.Equal(64, SkullModelDefinition.PlayerDefinition.TextureWidth);
        Assert.Equal(64, SkullModelDefinition.PlayerDefinition.TextureHeight);
        Assert.Equal(SkullModelDefinition.DefaultPlayerSkinTexture, SkullModelDefinition.PlayerDefinition.TexturePath);
        Assert.Same(SkullModelDefinition.PlayerDefinition, SkullModelDefinition.DefinitionFor(SkullType.Player));
        Assert.Same(SkullModelDefinition.Definition, SkullModelDefinition.DefinitionFor(SkullType.Skeleton));

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Player)],
            PlayerAtlas());
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(48, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(72, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 1, 0.75f, 0.5f, 0.75f, 0.375f, 0f);
        AssertVertex(geometry, 24, 0.23438f, 0.51563f, 0.76563f, 0.75f, 0f);
        AssertVertex(geometry, 25, 0.76563f, 0.51563f, 0.76563f, 0.875f, 0f);
        AssertVertex(geometry, 26, 0.76563f, 0.51563f, 0.23438f, 0.875f, 0.125f);
        AssertVertex(geometry, 28, 0.23438f, -0.01563f, 0.23438f, 0.625f, 0.125f);
        Assert.Equal(-0.015625f, geometry.Vertices[28 * ChunkMesher.FloatsPerVertex + 1], 4);
        Assert.Equal(0.234375f, geometry.Vertices[24 * ChunkMesher.FloatsPerVertex], 4);
        var bounds = Bounds(geometry);
        Assert.Equal(0.234375f, bounds.Min.X, 4);
        Assert.Equal(-0.015625f, bounds.Min.Y, 4);
        Assert.Equal(0.234375f, bounds.Min.Z, 4);
        Assert.Equal(0.765625f, bounds.Max.X, 4);
        Assert.Equal(0.515625f, bounds.Max.Y, 4);
        Assert.Equal(0.765625f, bounds.Max.Z, 4);
    }

    [Fact]
    public void PlayerTextureMapsToTheDefaultSlimSteveSprite()
    {
        var atlas = Atlas();
        Assert.Equal(DefaultPlayerSkin.Default.Path, SkullModelDefinition.TextureFor(SkullType.Player));
        Assert.Equal(DefaultPlayerSkin.Default.Path, ChestAtlas.SkullTextures[4]);
        var sprite = ChestAtlas.SpriteForSkull(atlas, SkullType.Player);
        Assert.NotNull(sprite);
        Assert.Equal(atlas.Get(SkullModelDefinition.DefaultPlayerSkinTexture), sprite);
    }

    [Fact]
    public void EverySupportedSkullTypeMapsToItsOwnTextureAndSprite()
    {
        var atlas = Atlas();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var rects = new HashSet<(int, int)>();
        foreach (var type in SupportedTypes)
        {
            var path = SkullModelDefinition.TextureFor(type);
            Assert.NotNull(path);
            Assert.True(paths.Add(path));
            var sprite = ChestAtlas.SpriteForSkull(atlas, type);
            Assert.NotNull(sprite);
            Assert.Equal(atlas.Get(path), sprite);
            Assert.True(rects.Add((sprite.RectId, sprite.Width)));
        }

        Assert.Null(SkullModelDefinition.TextureFor(SkullType.None));
        Assert.Equal(7, paths.Count);
        Assert.Equal(7, rects.Count);
    }

    [Fact]
    public void SkullTypeMapsBlockAndWallPaths()
    {
        Assert.Equal(SkullType.Skeleton, SkullModelDefinition.TypeOfPath("skeleton_skull"));
        Assert.Equal(SkullType.Skeleton, SkullModelDefinition.TypeOfPath("skeleton_wall_skull"));
        Assert.Equal(SkullType.WitherSkeleton, SkullModelDefinition.TypeOfPath("wither_skeleton_skull"));
        Assert.Equal(SkullType.Zombie, SkullModelDefinition.TypeOfPath("zombie_wall_head"));
        Assert.Equal(SkullType.Creeper, SkullModelDefinition.TypeOfPath("creeper_head"));
        Assert.Equal(SkullType.Player, SkullModelDefinition.TypeOfPath("player_head"));
        Assert.Equal(SkullType.Player, SkullModelDefinition.TypeOfPath("player_wall_head"));
        Assert.Equal(SkullType.Piglin, SkullModelDefinition.TypeOfPath("piglin_head"));
        Assert.Equal(SkullType.Piglin, SkullModelDefinition.TypeOfPath("piglin_wall_head"));
        Assert.Equal(SkullType.Dragon, SkullModelDefinition.TypeOfPath("dragon_head"));
        Assert.Equal(SkullType.Dragon, SkullModelDefinition.TypeOfPath("dragon_wall_head"));
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
    public void FortyPlayerSkullsKeepHeadAndHatVertices()
    {
        var states = new BlockEntityRenderState[40];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new BlockEntityRenderState(
                i, 0f, 0f, VanillaBlockEntityTypes.Skull,
                SkullType: SkullType.Player);
        }

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(states, PlayerAtlas());

        Assert.Equal(40, mesher.LastEntityCount);
        Assert.Equal(1920, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(2880, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 48, 1.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 960, 20.25f, 0.5f, 0.75f, 0.25f, 0f);
    }

    [Fact]
    public void FortyPiglinSkullsKeepEveryHeadBoxVertex()
    {
        var states = new BlockEntityRenderState[40];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new BlockEntityRenderState(
                i, 0f, 0f, VanillaBlockEntityTypes.Skull,
                SkullType: SkullType.Piglin);
        }

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(states, PiglinAtlas());

        Assert.Equal(40, mesher.LastEntityCount);
        Assert.Equal(5760, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(8640, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.1875f, 0.5f, 0.75f, 0.28125f, 0f);
        AssertVertex(geometry, 144, 1.1875f, 0.5f, 0.75f, 0.28125f, 0f);
        AssertVertex(geometry, 2880, 20.1875f, 0.5f, 0.75f, 0.28125f, 0f);
    }

    [Fact]
    public void FortyDragonSkullsKeepEveryHeadBoxVertex()
    {
        var states = new BlockEntityRenderState[40];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new BlockEntityRenderState(
                i, 0f, 0f, VanillaBlockEntityTypes.Skull,
                SkullType: SkullType.Dragon);
        }

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(states, DragonAtlas());

        Assert.Equal(40, mesher.LastEntityCount);
        Assert.Equal(6720, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(10080, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.21875f, 0.42125f, 0.125f, 0.76953f, 0.17188f);
        AssertVertex(geometry, 168, 1.21875f, 0.42125f, 0.125f, 0.76953f, 0.17188f);
        AssertVertex(geometry, 3360, 20.21875f, 0.42125f, 0.125f, 0.76953f, 0.17188f);
    }

    [Theory]
    [InlineData((byte)0, 0.25f, 0.5f, 0.75f)]
    [InlineData((byte)4, 0.25f, 0.5f, 0.25f)]
    [InlineData((byte)8, 0.75f, 0.5f, 0.25f)]
    [InlineData((byte)12, 0.75f, 0.5f, 0.75f)]
    [InlineData((byte)15, 0.3647f, 0.5f, 0.82664f)]
    public void GroundRotationUsesSegmentedSourceDegrees(byte rotation, float x, float y, float z)
    {
        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Skeleton, SkullRotation: rotation)],
            Atlas());

        AssertVertex(geometry, 0, x, y, z, 0.25f, 0f);
    }

    [Theory]
    [InlineData((byte)2, 0.25f, 0.75f, 1f)]
    [InlineData((byte)3, 0.75f, 0.75f, 0f)]
    [InlineData((byte)4, 1f, 0.75f, 0.75f)]
    [InlineData((byte)5, 0f, 0.75f, 0.25f)]
    public void WallFacingUsesTheOppositeDirectionYaw(byte facing, float x, float y, float z)
    {
        var mesher = new SkullRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Skull, SkullType: SkullType.Skeleton, SkullWall: true, SkullFacing: facing)],
            Atlas());

        AssertVertex(geometry, 0, x, y, z, 0.25f, 0f);
    }

    [Fact]
    public void FortySkullsKeepEveryEarlierVertexWhenTheBatchGrows()
    {
        var states = new BlockEntityRenderState[40];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new BlockEntityRenderState(
                i, 0f, 0f, VanillaBlockEntityTypes.Skull,
                SkullType: SkullType.Skeleton);
        }

        var mesher = new SkullRenderer();
        var geometry = mesher.Build(states, Atlas());

        Assert.Equal(40, mesher.LastEntityCount);
        Assert.Equal(960, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(1440, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 24, 1.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 648, 27.25f, 0.5f, 0.75f, 0.25f, 0f);
        AssertVertex(geometry, 672, 28.25f, 0.5f, 0.75f, 0.25f, 0f);
    }

    [Fact]
    public void RendererHandlesSkullOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Skull, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    private static ItemAtlas PlayerAtlas()
    {
        var pixels = new byte[64 * 64 * 4];
        return ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SkullModelDefinition.DefaultPlayerSkinTexture] = new(64, 64, pixels)
        });
    }

    private static ItemAtlas PiglinAtlas()
    {
        var pixels = new byte[64 * 64 * 4];
        return ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SkullModelDefinition.PiglinTexture] = new(64, 64, pixels)
        });
    }

    private static ItemAtlas DragonAtlas()
    {
        var pixels = new byte[256 * 256 * 4];
        return ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SkullModelDefinition.DragonTexture] = new(256, 256, pixels)
        });
    }

    private static ItemAtlas Atlas()
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        for (var i = 0; i < SupportedTypes.Length; i++)
        {
            var path = SkullModelDefinition.TextureFor(SupportedTypes[i]);
            Assert.NotNull(path);
            var height = SupportedTypes[i] switch
            {
                SkullType.Player or SkullType.Piglin => 64,
                SkullType.Dragon => 256,
                _ => 32
            };
            var pixels = new byte[64 * height * 4];
            pixels[0] = (byte)(i + 1);
            textures[path] = new TextureData(64, height, pixels);
        }

        return ItemAtlas.FromTextures(textures);
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

    private static (Vector3 Min, Vector3 Max) Bounds(DynamicGeometry geometry)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < geometry.VertexFloats; i += ChunkMesher.FloatsPerVertex)
        {
            var position = new Vector3(geometry.Vertices[i], geometry.Vertices[i + 1], geometry.Vertices[i + 2]);
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return (min, max);
    }
}

