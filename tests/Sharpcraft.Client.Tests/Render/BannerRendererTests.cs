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

    /// <summary>Every ground segment / wall facing must contain the corner predicted by the source formula.</summary>
    [Theory]
    [InlineData(BannerAttachment.Ground, (byte)0)]
    [InlineData(BannerAttachment.Ground, (byte)1)]
    [InlineData(BannerAttachment.Ground, (byte)2)]
    [InlineData(BannerAttachment.Ground, (byte)3)]
    [InlineData(BannerAttachment.Ground, (byte)4)]
    [InlineData(BannerAttachment.Ground, (byte)5)]
    [InlineData(BannerAttachment.Ground, (byte)6)]
    [InlineData(BannerAttachment.Ground, (byte)7)]
    [InlineData(BannerAttachment.Ground, (byte)8)]
    [InlineData(BannerAttachment.Ground, (byte)9)]
    [InlineData(BannerAttachment.Ground, (byte)10)]
    [InlineData(BannerAttachment.Ground, (byte)11)]
    [InlineData(BannerAttachment.Ground, (byte)12)]
    [InlineData(BannerAttachment.Ground, (byte)13)]
    [InlineData(BannerAttachment.Ground, (byte)14)]
    [InlineData(BannerAttachment.Ground, (byte)15)]
    [InlineData(BannerAttachment.Wall, (byte)2)]
    [InlineData(BannerAttachment.Wall, (byte)3)]
    [InlineData(BannerAttachment.Wall, (byte)4)]
    [InlineData(BannerAttachment.Wall, (byte)5)]
    public void GroundAndWallFinalVertexSamples(BannerAttachment attachment, byte rotation)
    {
        var expected = attachment == BannerAttachment.Ground
            ? BannerSourceFormula.GroundBodyVertex(rotation)
            : BannerSourceFormula.WallBodyVertex(rotation);
        var expectedUv = attachment == BannerAttachment.Ground
            ? (0.75f, 0f)   // pole box texture offset (44,0), DOWN face first corner
            : (0.34375f, 0.65625f); // wall bar texture offset (0,42)

        var geometry = build(new BannerRenderer(), attachment, rotation, color: 0, x: 0f, y: 0f, z: 0f);
        Assert.True(ContainsVertex(geometry, expected, expectedUv, tolerance: 0.002f),
            $"{attachment}/{rotation}: source-formula vertex ({expected.X:F4},{expected.Y:F4},{expected.Z:F4})"
                + $" uv {expectedUv} is missing from the emitted body geometry");
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
    [InlineData(BannerAttachment.Ground, 0f)]
    [InlineData(BannerAttachment.Ground, 0.25f)]
    [InlineData(BannerAttachment.Ground, 0.5f)]
    [InlineData(BannerAttachment.Ground, 0.75f)]
    [InlineData(BannerAttachment.Wall, 0f)]
    [InlineData(BannerAttachment.Wall, 0.25f)]
    [InlineData(BannerAttachment.Wall, 0.5f)]
    [InlineData(BannerAttachment.Wall, 0.75f)]
    public void FlagPhaseMovesOnlyTheFlag(BannerAttachment attachment, float phase)
    {
        var bodyVertices = attachment == BannerAttachment.Ground ? 48 : 24;
        // Wall flag reference pose uses facing south (rotation 3 -> toYRot 0), matching banner-vectors.golden.txt.
        var rotation = attachment == BannerAttachment.Ground ? (byte)0 : (byte)3;
        var geometry = build(new BannerRenderer(), attachment, rotation: rotation, color: 0, x: 0f, y: 0f, z: 0f, phase);

        // The flag's source-formula DOWN-face corner at this phase must be present in the flag range.
        var expectedFlag = BannerSourceFormula.FlagVertex(attachment == BannerAttachment.Ground, phase);
        var flagFound = false;
        for (var vertex = bodyVertices; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            var p = VertexFieldReader.Position(geometry.Vertices, vertex);
            if (MathF.Abs(p.X - expectedFlag.X) <= 0.002f
                && MathF.Abs(p.Y - expectedFlag.Y) <= 0.002f
                && MathF.Abs(p.Z - expectedFlag.Z) <= 0.002f)
            {
                flagFound = true;
                break;
            }
        }

        Assert.True(flagFound,
            $"{attachment}/{phase}: source-formula flag corner ({expectedFlag.X:F4},{expectedFlag.Y:F4},{expectedFlag.Z:F4}) is missing");

        // The body is unaffected by phase: it still contains the source-formula rest corner.
        var expectedBody = attachment == BannerAttachment.Ground
            ? BannerSourceFormula.GroundBodyVertex(0)
            : BannerSourceFormula.WallBodyVertex(3);
        Assert.True(ContainsVertex(geometry, expectedBody, null, tolerance: 0.002f, vertexLimit: bodyVertices),
            $"{attachment}/{phase}: moving the flag changed the body");
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

    private static bool ContainsVertex(DynamicGeometry geometry, System.Numerics.Vector3 expected,
        (float U, float V)? uv, float tolerance, int vertexLimit = -1)
    {
        var count = vertexLimit < 0 ? geometry.VertexFloats / ChunkMesher.FloatsPerVertex : vertexLimit;
        for (var vertex = 0; vertex < count; vertex++)
        {
            var p = VertexFieldReader.Position(geometry.Vertices, vertex);
            if (MathF.Abs(p.X - expected.X) > tolerance
                || MathF.Abs(p.Y - expected.Y) > tolerance
                || MathF.Abs(p.Z - expected.Z) > tolerance)
            {
                continue;
            }

            if (uv is not { } want)
            {
                return true;
            }

            var got = VertexFieldReader.Uv(geometry.Vertices, vertex);
            if (MathF.Abs(got.U - want.U) <= tolerance && MathF.Abs(got.V - want.V) <= tolerance)
            {
                return true;
            }
        }

        return false;
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
