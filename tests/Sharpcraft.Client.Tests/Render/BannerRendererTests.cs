// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Banner static base slice: standing/wall body and flag transforms, dye tint, and the banner
/// texture remaining separate from the chest-only atlas list.
/// </summary>
public sealed class BannerRendererTests
{
    private readonly ItemAtlas _atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
    {
        [BannerModelDefinition.TexturePath] = new(64, 64, new byte[64 * 64 * 4])
    });

    [Fact]
    public void BannerTextureStaysOutsideTheChestOnlyList()
    {
        Assert.Equal(22, ChestAtlas.Textures.Length);
        Assert.Equal("entity/banner/banner_base", ChestAtlas.BannerTexture);
    }

    [Fact]
    public void HandlesBannerOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Banner, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    [Fact]
    public void GroundAndWallProduceStaticBodyAndFlagGeometry()
    {
        var renderer = new BannerRenderer();
        var ground = build(renderer, BannerAttachment.Ground, rotation: 0, color: 0, x: 0f, y: 0f, z: 0f);
        var wall = build(renderer, BannerAttachment.Wall, rotation: 2, color: 14, x: 0f, y: 0f, z: 0f);

        Assert.Equal(1, renderer.LastEntityCount);
        Assert.True(ground.VertexFloats > 0);
        Assert.True(ground.IndexCount > 0);
        Assert.True(wall.VertexFloats > 0);
        Assert.True(wall.IndexCount > 0);
    }

    [Fact]
    public void TintAppliesToFlagButNotBody()
    {
        var renderer = new BannerRenderer();
        var red = build(renderer, BannerAttachment.Ground, rotation: 0, color: 14, x: 0f, y: 0f, z: 0f);

        var body = VertexFieldReader.Colour(red.Vertices, 0);
        Assert.Equal(1f, body.R, 3);
        Assert.Equal(1f, body.G, 3);
        Assert.Equal(1f, body.B, 3);

        // Ground body = pole + bar (48 vertices); the flag starts at index 48.
        var flag = VertexFieldReader.Colour(red.Vertices, 48);
        Assert.Equal(11546150 >> 16 & 0xFF, (int)MathF.Round(flag.R * 255f));
        Assert.Equal(11546150 >> 8 & 0xFF, (int)MathF.Round(flag.G * 255f));
        Assert.Equal(11546150 & 0xFF, (int)MathF.Round(flag.B * 255f));
    }

    [Theory]
    [InlineData(BannerAttachment.Ground, (byte)0, 0.45833f, 0f, 0.45833f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)1, 0.47745f, 0f, 0.44556f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)2, 0.5f, 0f, 0.44107f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)3, 0.52255f, 0f, 0.44556f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)4, 0.54167f, 0f, 0.45833f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)5, 0.55444f, 0f, 0.47745f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)6, 0.55893f, 0f, 0.5f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)7, 0.55444f, 0f, 0.52255f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)8, 0.54167f, 0f, 0.54167f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)9, 0.52255f, 0f, 0.55444f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)10, 0.5f, 0f, 0.55893f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)11, 0.47745f, 0f, 0.55444f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)12, 0.45833f, 0f, 0.54167f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)13, 0.44556f, 0f, 0.52255f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)14, 0.44107f, 0f, 0.5f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Ground, (byte)15, 0.44556f, 0f, 0.47745f, 0.75f, 0f)]
    [InlineData(BannerAttachment.Wall, (byte)2, 0.91667f, 0.77083f, 0.97917f, 0.34375f, 0.65625f)]
    [InlineData(BannerAttachment.Wall, (byte)3, 0.08333f, 0.77083f, 0.02083f, 0.34375f, 0.65625f)]
    [InlineData(BannerAttachment.Wall, (byte)4, 0.97917f, 0.77083f, 0.08333f, 0.34375f, 0.65625f)]
    [InlineData(BannerAttachment.Wall, (byte)5, 0.02083f, 0.77083f, 0.91667f, 0.34375f, 0.65625f)]
    public void GroundAndWallFinalVertexSamples(
        BannerAttachment attachment,
        byte rotation,
        float x,
        float y,
        float z,
        float u,
        float v)
    {
        var renderer = new BannerRenderer();
        var geometry = build(renderer, attachment, rotation, color: 0, x: 0f, y: 0f, z: 0f);
        var position = VertexFieldReader.Position(geometry.Vertices, 0);
        var uv = VertexFieldReader.Uv(geometry.Vertices, 0);

        Assert.Equal(x, position.X, 4);
        Assert.Equal(y, position.Y, 4);
        Assert.Equal(z, position.Z, 4);
        Assert.Equal(u, uv.U, 4);
        Assert.Equal(v, uv.V, 4);
    }

    [Fact]
    public void InvalidBannerFieldsFallBackToBlockDefaults()
    {
        var renderer = new BannerRenderer();
        var ground = build(renderer, BannerAttachment.Ground, rotation: 0, color: 0, x: 0f, y: 0f, z: 0f);
        var invalidAttachment = build(renderer, (BannerAttachment)99, rotation: 0, color: 0, x: 0f, y: 0f, z: 0f);
        var invalidRotation = build(renderer, BannerAttachment.Ground, rotation: 16, color: 16, x: 0f, y: 0f, z: 0f);

        Assert.Equal(VertexFieldReader.Position(ground.Vertices, 0), VertexFieldReader.Position(invalidAttachment.Vertices, 0));
        Assert.Equal(VertexFieldReader.Position(ground.Vertices, 0), VertexFieldReader.Position(invalidRotation.Vertices, 0));
        Assert.Equal(VertexFieldReader.Colour(ground.Vertices, 48), VertexFieldReader.Colour(invalidRotation.Vertices, 48));
    }

    [Theory]
    [InlineData(BannerAttachment.Ground, 0f, 0.08333f, 0.16639f, 0.52858f)]
    [InlineData(BannerAttachment.Ground, 0.25f, 0.08333f, 0.16632f, 0.4762f)]
    [InlineData(BannerAttachment.Ground, 0.5f, 0.08333f, 0.16789f, 0.42385f)]
    [InlineData(BannerAttachment.Ground, 0.75f, 0.08333f, 0.16632f, 0.4762f)]
    [InlineData(BannerAttachment.Wall, 0f, 0.91667f, -0.81278f, 0.90892f)]
    [InlineData(BannerAttachment.Wall, 0.25f, 0.91667f, -0.81285f, 0.9613f)]
    [InlineData(BannerAttachment.Wall, 0.5f, 0.91667f, -0.81128f, 1.01365f)]
    [InlineData(BannerAttachment.Wall, 0.75f, 0.91667f, -0.81285f, 0.9613f)]
    public void FlagPhaseMovesOnlyTheFlag(BannerAttachment attachment, float phase, float x, float y, float z)
    {
        var renderer = new BannerRenderer();
        var bodyVertices = attachment == BannerAttachment.Ground ? 48 : 24;
        var geometry = build(renderer, attachment, rotation: 0, color: 0, x: 0f, y: 0f, z: 0f, phase);
        var flag = VertexFieldReader.Position(geometry.Vertices, bodyVertices);
        var uv = VertexFieldReader.Uv(geometry.Vertices, bodyVertices);
        var body = VertexFieldReader.Position(geometry.Vertices, 0);
        var expectedBody = attachment == BannerAttachment.Ground
            ? (0.45833f, 0f, 0.45833f)
            : (0.91667f, 0.77083f, 0.97917f);

        Assert.Equal(x, flag.X, 3);
        Assert.Equal(y, flag.Y, 3);
        Assert.Equal(z, flag.Z, 3);
        Assert.Equal(0.32813f, uv.U, 4);
        Assert.Equal(0f, uv.V, 4);
        Assert.Equal(expectedBody.Item1, body.X, 4);
        Assert.Equal(expectedBody.Item2, body.Y, 4);
        Assert.Equal(expectedBody.Item3, body.Z, 4);
    }

    [Theory]
    [InlineData(0, 0, 0, 0L, 0f, 0f)]
    [InlineData(0, 0, 0, 0L, 0.25f, 0.0025f)]
    [InlineData(1, 2, 3, 7L, 0f, 0.71f)]
    [InlineData(-1, -2, -3, -100L, 0f, 0.36f)]
    [InlineData(10, 64, -5, 123L, 0.5f, 0.045f)]
    public void BannerPhaseUsesTheSourceFloorModFormula(
        int x,
        int y,
        int z,
        long gameTime,
        float partialTick,
        float expected)
    {
        Assert.Equal(expected, FrameCommand.BannerPhase(x, y, z, gameTime, partialTick), 5);
    }

    [Fact]
    public void PhaseRotationHasTheSourceQuarterWaveValues()
    {
        Assert.Equal((-0.0125f + 0.01f) * MathF.PI, BannerRenderer.PhaseRotation(0f), 5);
        Assert.Equal((-0.0125f + 0f) * MathF.PI, BannerRenderer.PhaseRotation(0.25f), 5);
        Assert.Equal((-0.0125f - 0.01f) * MathF.PI, BannerRenderer.PhaseRotation(0.5f), 5);
        Assert.Equal((-0.0125f + 0f) * MathF.PI, BannerRenderer.PhaseRotation(0.75f), 5);
    }

    private DynamicGeometry build(
        BannerRenderer renderer,
        BannerAttachment attachment,
        byte rotation,
        byte color,
        float x,
        float y,
        float z,
        float phase = 0f) =>
        renderer.Build(
            [new BlockEntityRenderState(x, y, z, VanillaBlockEntityTypes.Banner, BannerAttachment: attachment, BannerRotation: rotation, BannerColor: color, BannerPhase: phase)],
            _atlas);
}
