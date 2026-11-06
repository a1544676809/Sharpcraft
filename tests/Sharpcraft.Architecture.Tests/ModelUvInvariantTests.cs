// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Architecture.Tests;

/// <summary>
/// Registry-wide model UV invariant gate (T-665). Guards the bug class behind T-662/T-663: the baby elytra wing keeps the
/// reference 10×20×2 UV sampling even though its geometry is scaled to 5×10×1. The mesher derives the six face UV
/// rectangles from <c>UvWidth/UvHeight/UvDepth</c> (falling back to the box size), so every registered part must sample
/// a positive-area region that fits inside its texture, and adult/baby twins sharing a texture must share that span.
/// </summary>
public sealed class ModelUvInvariantTests
{
    private const int ZombieTypeId = 154;
    private const int ElytraWingTextureWidth = 64;
    private const int ElytraWingTextureHeight = 32;

    /// <summary>The mesher's per-part effective UV box: explicit overrides when set, else the physical box size.</summary>
    private static (float D, float W, float H) EffectiveUv(in EntityModelPart part) => (
        part.UvDepth > 0 ? part.UvDepth : part.Depth,
        part.UvWidth > 0 ? part.UvWidth : part.Width,
        part.UvHeight > 0 ? part.UvHeight : part.Height);

    /// <summary>The eight u/v coordinates of the reference 2·(d+w) × (d+h) box unwrap, in texture pixels.</summary>
    private static (float U0, float U1, float U2, float U3, float U4, float V0, float V1, float V2) UvSpan(in EntityModelPart part)
    {
        var (d, w, h) = EffectiveUv(part);
        var u0 = part.TextureX;
        var u1 = u0 + d;
        var u2 = u1 + w;
        var u3 = u2 + d;
        var u4 = u3 + w;
        var v0 = part.TextureY;
        var v1 = v0 + d;
        var v2 = v1 + h;
        return (u0, u1, u2, u3, u4, v0, v1, v2);
    }

    /// <summary>
    /// Pre-existing, reference-faithful UV overshoots that are documented rather than silently dropped. Reference
    /// <c>BabyLlamaModel.createBodyLayer</c> itself declares its two chest cubes at tex(45,28)/(45,41) inside a 64×64
    /// layer, so the unwrap reaches u = 45 + 2·(8+3) = 67 &gt; 64. Vanilla tolerates it because baby llamas cannot wear
    /// a chest, so those cubes never render. Kept as an explicit baseline: any other registry part must stay in bounds,
    /// and removing one of these (e.g. if the reference data changes) fails until the baseline is updated.
    /// </summary>
    private static readonly HashSet<string> ExpectedReferenceUvOvershoots = new(StringComparer.Ordinal)
    {
        "baby_llama/right_chest",
        "baby_llama/left_chest",
        "baby_llama_creamy/right_chest",
        "baby_llama_creamy/left_chest",
        "baby_llama_white/right_chest",
        "baby_llama_white/left_chest",
        "baby_llama_gray/right_chest",
        "baby_llama_gray/left_chest"
    };

    [Fact]
    public void AllRegisteredModelPartsHaveUvInsideTheirTexture()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var violations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            foreach (var part in definition.Parts)
            {
                // Only solid cuboids have a full positive-area unwrap; zero-thickness parts (fins, layers, group
                // pivots) are degenerate and are not gated by this pixel-span invariant.
                if (part.Width <= 0f || part.Height <= 0f || part.Depth <= 0f)
                {
                    continue;
                }

                var textureWidth = part.TextureWidth > 0 ? part.TextureWidth : definition.TextureWidth;
                var textureHeight = part.TextureHeight > 0 ? part.TextureHeight : definition.TextureHeight;
                var key = $"{definition.Name}/{part.Name}";
                var span = UvSpan(part);

                if (textureWidth <= 0 || textureHeight <= 0)
                {
                    violations[key] = $"non-positive texture size {textureWidth}x{textureHeight}";
                    continue;
                }

                if (span.U0 < 0f || span.U0 > span.U4 + 1e-3f || span.U4 > textureWidth + 1e-3f
                    || span.V0 < 0f || span.V0 > span.V2 + 1e-3f || span.V2 > textureHeight + 1e-3f
                    || span.U4 - span.U0 <= 0f || span.V2 - span.V0 <= 0f)
                {
                    violations[key] =
                        $"uv {span.U0}..{span.U4} x {span.V0}..{span.V2} outside 0..{textureWidth} x 0..{textureHeight}";
                }
            }
        }

        var unexpected = violations.Where(pair => !ExpectedReferenceUvOvershoots.Contains(pair.Key)).ToArray();
        var missing = ExpectedReferenceUvOvershoots.Where(key => !violations.ContainsKey(key)).ToArray();
        Assert.True(
            unexpected.Length == 0 && missing.Length == 0,
            "Unexpected UV out-of-bounds:\n"
            + string.Join("\n", unexpected.Select(pair => $"{pair.Key}: {pair.Value}"))
            + (missing.Length > 0 ? "\nMissing known baseline entries: " + string.Join(", ", missing) : string.Empty));
    }

    [Fact]
    public void AdultAndBabyElytraShareTheSameUvSpan()
    {
        var adultWing = FindElytraLeftWing(EntityModelResolver.Resolve(ZombieTypeId, baby: false));
        var babyWing = FindElytraLeftWing(EntityModelResolver.Resolve(ZombieTypeId, baby: true));

        // The baby geometry is half-sized, which is exactly why explicit UV overrides are required.
        Assert.Equal(10f, adultWing.Width);
        Assert.Equal(20f, adultWing.Height);
        Assert.Equal(2f, adultWing.Depth);
        Assert.Equal(5f, babyWing.Width);
        Assert.Equal(10f, babyWing.Height);
        Assert.Equal(1f, babyWing.Depth);

        // Effective sampling box must stay the reference 10×20×2 for both.
        Assert.Equal((2f, 10f, 20f), EffectiveUv(adultWing));
        Assert.Equal((2f, 10f, 20f), EffectiveUv(babyWing));

        // Same texture origin and identical unwrap coordinates.
        Assert.Equal(22, adultWing.TextureX);
        Assert.Equal(0, adultWing.TextureY);
        Assert.Equal(22, babyWing.TextureX);
        Assert.Equal(0, babyWing.TextureY);
        Assert.Equal(ElytraWingTextureWidth, adultWing.TextureWidth > 0 ? adultWing.TextureWidth : ElytraWingTextureWidth);
        Assert.Equal(UvSpan(adultWing), UvSpan(babyWing));
    }

    [Fact]
    public void AdultAndBabyElytraMeshesEmitTheSameSixFaceUvArrays()
    {
        var adult = WingUvPixels(baby: false);
        var baby = WingUvPixels(baby: true);

        Assert.Equal(48, adult.Length); // 2 wings × 6 faces × 4 corners
        Assert.Equal(adult.Length, baby.Length);
        for (var i = 0; i < adult.Length; i++)
        {
            Assert.True(Vector2.Distance(adult[i], baby[i]) <= 1e-3f,
                $"vertex {i}: adult UV {adult[i]} != baby UV {baby[i]}");
        }
    }

    [Fact]
    public void ExplicitUvOverridesAgreeAcrossTheRegistry()
    {
        var overrides = new Dictionary<(string Name, string Texture), (int Depth, int Width, int Height)>();

        foreach (var definition in EntityModelResolver.Definitions)
        {
            foreach (var part in definition.Parts)
            {
                if (part.UvWidth <= 0 && part.UvHeight <= 0 && part.UvDepth <= 0)
                {
                    continue;
                }

                var key = (part.Name, part.TexturePath ?? definition.TexturePath ?? string.Empty);
                var triple = (part.UvDepth, part.UvWidth, part.UvHeight);
                if (overrides.TryGetValue(key, out var existing))
                {
                    Assert.True(existing == triple,
                        $"{definition.Name}/{part.Name}: UV override {triple} disagrees with earlier {existing} for the same part/texture");
                }
                else
                {
                    overrides[key] = triple;
                }
            }
        }
    }

    private static EntityModelPart FindElytraLeftWing(EntityModelDefinition definition) =>
        definition.Parts.Single(part => part.Name == "equipment_Elytra_4_left_wing");

    /// <summary>
    /// Mirrors the ElytraWingGeometryTests vertex helper: builds the elytra layer and returns the per-vertex texture-pixel
    /// UVs for both wings (2 × 6 faces × 4 corners).
    /// </summary>
    private static Vector2[] WingUvPixels(bool baby)
    {
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        Assert.True(elytraId >= 0, "elytra item id missing from the embedded registry");

        var state = new EntityRenderState(
            1,
            ZombieTypeId,
            0f,
            0f,
            0f,
            0f,
            0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1),
            IsBaby: baby);

        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 32, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var elytra = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
        var geometry = elytra.Geometry;

        var count = geometry.VertexFloats / 14;
        var result = new Vector2[count];
        for (var v = 0; v < count; v++)
        {
            var offset = v * 14;
            result[v] = new Vector2(
                geometry.Vertices[offset + 6] * ElytraWingTextureWidth,
                geometry.Vertices[offset + 7] * ElytraWingTextureHeight);
        }

        return result;
    }
}
