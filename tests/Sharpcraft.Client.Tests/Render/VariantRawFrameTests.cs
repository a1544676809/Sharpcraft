// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// B-1 migration of the 83 raw source-frame variants (issue #228): every definition must render in the
/// source frame, the two available world-frame samples must match production <c>BuildLayers</c> output,
/// and those samples must equal the source living formula <c>scale(-1,-1,1) * translate(0,-1.501,0)</c>.
/// </summary>
/// <remarks>
/// World samples: <c>parrot-blue-world-frame.reference.txt</c> (PARROT, blue is a texture-only variant)
/// and <c>dolphin-world-frame.reference.txt</c>. Both were generated from the 26.3 jar by
/// <c>ref://mc-26.3-analysis/vectors/b-class-source/LivingWorldFrameProbe.java</c> with the entity
/// yaw/translation/scale excluded. The samples are in world blocks; production <c>BuildLayers</c> uses
/// <see cref="EntityPlaceholderMesher.ModelScale"/> for living entities (0.9), so the comparison is
/// performed after dividing the production output by that scale. That division is exact: the source
/// frame is linear and the scale is applied uniformly, so the 1e-5 tolerance still pins every vertex.
/// </remarks>
public sealed class VariantRawFrameTests
{
    /// <summary>Strict per-vertex tolerance for the keyed world/UV comparisons.</summary>
    private const float Tolerance = 1e-5f;

    /// <summary><c>LivingEntityRenderer.submit</c>: scale(-1,-1,1) then translate(0,-1.501,0).</summary>
    private const float SourceFlipBaselineBlocks = 1.501f;

    /// <summary>Reference pivot height in pixels; equals 1.501 blocks and is the raw living baseline.</summary>
    private const float SourceFlipBaselinePx = VanillaMeshBake.ReferencePivotHeight;

    public static TheoryData<string, int, int, string, int, int> WorldCases => new()
    {
        // world sample, entity type id, variant value, texture path, width, height
        { "parrot-blue-world-frame.reference.txt", 99, 1, "entity/parrot/parrot_blue", 32, 32 },
        { "dolphin-world-frame.reference.txt", 36, 0, "entity/dolphin/dolphin", 64, 64 }
    };

    public static TheoryData<string, string> FormulaCases => new()
    {
        // world sample, model-space sample of the same source model layer
        { "parrot-blue-world-frame.reference.txt", "parrot-raw-vectors.reference.txt" },
        { "dolphin-world-frame.reference.txt", "dolphin-raw-vectors.reference.txt" }
    };

    [Theory]
    [MemberData(nameof(WorldCases))]
    public void ProductionBuildLayersWorldMatchesTheSourceWorldSample(string worldSample, int typeId, int variantId,
        string texturePath, int width, int height)
    {
        var definition = EntityVariantTable.Builtin.Resolve(typeId, variantId);
        Assert.True(definition.RawSourceFrame, $"{definition.Name}: variant must be rawSourceFrame");
        Assert.Equal(0f, definition.BaselinePx);
        Assert.Equal(SourceFlipBaselinePx, definition.SourceFrameBaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new(width, height, new byte[width * height * 4])
        });
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f, VariantId: variantId);
        // Bind pose: the jar world samples carry no animation, and yaw 180 makes EntityModelTransform's
        // 180 - yaw rotation the identity, leaving exactly the source flip + baseline.
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, width, height, 1), width, height,
            skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        var cutout = layers.Single(layer => layer.Kind == EntityGeometryLayerKind.Cutout);
        var actual = Positions(cutout.Geometry, EntityPlaceholderMesher.ModelScale);

        var sample = LoadSample(worldSample);
        Assert.Equal(sample.Count, actual.Count);
        AssertMultisetMatches(actual, sample.Select(row => row.Position).ToArray(), "world position");

        // UVs are half-float in the geometry buffer; quantise the sample the same way before comparing.
        var expectedUvs = sample.Select(row => (QuantizeHalf(row.U), QuantizeHalf(row.V))).OrderBy(v => v.Item1).ThenBy(v => v.Item2).ToArray();
        var actualUvs = Uvs(cutout.Geometry).OrderBy(v => v.Item1).ThenBy(v => v.Item2).ToArray();
        for (var i = 0; i < expectedUvs.Length; i++)
        {
            Assert.True(MathF.Abs(expectedUvs[i].Item1 - actualUvs[i].Item1) <= Tolerance
                && MathF.Abs(expectedUvs[i].Item2 - actualUvs[i].Item2) <= Tolerance,
                $"{worldSample}: UV[{i}] expected {expectedUvs[i]}, actual {actualUvs[i]}");
        }
    }

    [Theory]
    [MemberData(nameof(FormulaCases))]
    public void SourceWorldSampleEqualsTheLivingFlipOfTheModelSpaceSample(string worldSample, string modelSpaceSample)
    {
        var world = LoadSample(worldSample);
        var model = LoadSample(modelSpaceSample);
        Assert.Equal(model.Count, world.Count);
        var keyedModel = model.ToDictionary(row => row.Key);
        Assert.Equal(model.Count, keyedModel.Count);
        // Source formula: world = (-x, 1.501 - y, z) of the y-down model-space vertex.
        foreach (var row in world)
        {
            Assert.True(keyedModel.TryGetValue(row.Key, out var modelRow), $"{worldSample}: missing model row {row.Key}");
            var expected = new Vector3(-modelRow.Position.X, SourceFlipBaselineBlocks - modelRow.Position.Y, modelRow.Position.Z);
            Assert.True((expected - row.Position).Length() <= Tolerance,
                $"{row.Key}: expected world {expected}, sample world {row.Position}");
        }
    }

    [Fact]
    public void NoDefinitionUsesTheLegacyBakedFrame()
    {
        // #228 B-1: the audit found 83 variants (shulker x16, baby x41, parrot x4, axolotl x4,
        // tropical fish x12, salmon x2, frog x2, dolphin, turtle) whose compiled geometry was raw-baked
        // but whose flag was omitted, so the living flip/baseline was skipped and the model rendered in
        // the wrong frame. All definitions must now be raw source-frame; a legacy definition without the
        // flag fails here instead of silently rendering reflected.
        var definitions = EntityModelResolver.Definitions;
        Assert.True(definitions.Count >= 250, $"unexpected definition count {definitions.Count}");
        var legacy = definitions.Where(definition => !definition.RawSourceFrame).Select(definition => definition.Name).ToArray();
        Assert.Empty(legacy);
        foreach (var definition in definitions)
        {
            Assert.Equal(0f, definition.BaselinePx);
            // Body definitions carry the source flip baseline; layers positioned relative to an already
            // placed model pass 0 explicitly.
            Assert.True(definition.SourceFrameBaselinePx is 0f or SourceFlipBaselinePx,
                $"{definition.Name}: unexpected source-frame baseline {definition.SourceFrameBaselinePx}");
        }
    }

    [Fact]
    public void ShulkerKeepsItsSixteenColourVariants()
    {
        // B-1 must not collapse the 26.3 dyeable-shulker texture variants: the reference
        // ShulkerRenderer.getTextureLocation(state.color) selects one texture per dye colour while the
        // model layer (and therefore the raw frame) stays the same.
        Assert.Equal(16, ShulkerModelDefinition.Variants.Count);
        foreach (var (key, definition) in ShulkerModelDefinition.Variants)
        {
            Assert.True(definition.RawSourceFrame, $"shulker_{key}: variant must be rawSourceFrame");
            Assert.Equal(0f, definition.BaselinePx);
            Assert.Equal(SourceFlipBaselinePx, definition.SourceFrameBaselinePx);
            Assert.Equal("entity/shulker/shulker_" + key, definition.TexturePath);
        }
    }

    private static List<Vector3> Positions(DynamicGeometry geometry, float scale)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new List<Vector3>(count);
        for (var i = 0; i < count; i++)
        {
            var position = VertexFieldReader.Position(geometry.Vertices, i);
            result.Add(new Vector3(position.X, position.Y, position.Z) / scale);
        }

        return result;
    }

    private static List<(float, float)> Uvs(DynamicGeometry geometry)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new List<(float, float)>(count);
        for (var i = 0; i < count; i++)
        {
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            result.Add((uv.U, uv.V));
        }

        return result;
    }

    /// <summary>
    /// Greedy one-to-one matching at <see cref="Tolerance"/>; both sides must be fully consumed, which
    /// preserves vertex multiplicity (a duplicated/dropped vertex still fails).
    /// </summary>
    private static void AssertMultisetMatches(IReadOnlyList<Vector3> actual, IReadOnlyList<Vector3> expected, string what)
    {
        Assert.Equal(expected.Count, actual.Count);
        var used = new bool[expected.Count];
        foreach (var position in actual)
        {
            var match = -1;
            for (var i = 0; i < expected.Count; i++)
            {
                if (used[i] || (expected[i] - position).Length() > Tolerance)
                {
                    continue;
                }

                match = i;
                break;
            }

            Assert.True(match >= 0, $"{what}: no source vertex within {Tolerance} of {position}");
            used[match] = true;
        }

        Assert.DoesNotContain(false, used);
    }

    private static List<SampleRow> LoadSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var rows = new List<SampleRow>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            rows.Add(new SampleRow(
                (fields[1], int.Parse(fields[2], CultureInfo.InvariantCulture), int.Parse(fields[3], CultureInfo.InvariantCulture),
                    int.Parse(fields[4], CultureInfo.InvariantCulture)),
                new Vector3(
                    float.Parse(fields[5], CultureInfo.InvariantCulture),
                    float.Parse(fields[6], CultureInfo.InvariantCulture),
                    float.Parse(fields[7], CultureInfo.InvariantCulture)),
                float.Parse(fields[8], CultureInfo.InvariantCulture),
                float.Parse(fields[9], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    private readonly record struct SampleRow(
        (string Part, int Cube, int Polygon, int Vertex) Key,
        Vector3 Position,
        float U,
        float V);
}
