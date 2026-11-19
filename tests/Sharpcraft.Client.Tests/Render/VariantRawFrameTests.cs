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
/// B-1 migration of the 83 raw source-frame variants (issue #228) and Issue #247/R-040 world-frame sample
/// expansion: every definition must render in the source frame; world-frame samples must match production
/// <c>BuildLayers</c> output per source key (single-cube families), and every world sample must equal the
/// source living formula <c>scale(-1,-1,1) * translate(0,-1.501,0)</c> applied to the independent model-space
/// probe of the same source model layer.
/// </summary>
/// <remarks>
/// World samples were generated from the 26.3 jar by
/// <c>ref://mc-26.3-analysis/vectors/b-class-source/LivingWorldFrameProbe.java</c> with the entity
/// yaw/translation/scale excluded. They are in world blocks; production <c>BuildLayers</c> uses
/// <see cref="EntityPlaceholderMesher.ModelScale"/> for living entities (0.9), so the comparison divides
/// production output by that scale — exact because the source frame is linear and the scale is uniform.
/// <para>
/// Direct production <c>BuildLayers</c> keyed verification covers families whose jar model emits one cube
/// per part with no extra render layers (parrot, dolphin, shulker, bee, squid). Families whose source model
/// has multi-cube parts, degenerate cubes or additional layers (axolotl, tropical fish, salmon, frog, turtle,
/// the baby families) are covered by a two-step source-backed chain: <see cref="SourceWorldSampleEqualsTheLivingFlipOfTheModelSpaceSample"/>
/// pins the world sample to the flip of the independent <c>RawRenderUnitProbe</c> model-space sample per key,
/// and <c>LivingRawReferenceSampleTests.RawBakeMatchesSourceVertices</c> already verifies production against
/// that model-space sample with the required degenerate/mirror filtering. Their production world-frame keyed
/// coverage via a single atlas remains a scoped follow-up (R-040).
/// </para>
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
        { "dolphin-world-frame.reference.txt", 36, 0, "entity/dolphin/dolphin", 64, 64 },
        // Issue #247 / R-040: additional B-1 variant families. Production BuildLayers is verified per key for
        // families whose source model emits exactly one cube per part with no extra render layers (below);
        // the rest are covered by the source-to-source formula cross-check in FormulaCases + the existing
        // LivingRawReferenceSampleTests production keyed verification of the model-space samples.
        { "shulker-world-frame.reference.txt", 115, 0, "entity/shulker/shulker", 64, 64 },
        { "bee-world-frame.reference.txt", 11, 0, "entity/bee/bee", 64, 64 },
        { "squid-world-frame.reference.txt", 130, 0, "entity/squid/squid", 64, 32 }
    };

    public static TheoryData<string, string> FormulaCases => new()
    {
        // world sample, model-space sample of the same source model layer. Both come from independent jar probes
        // (LivingWorldFrameProbe applies the source living flip; RawRenderUnitProbe emits raw render units), so
        // the per-key equality below cross-validates the world samples against the already production-verified
        // model-space samples for every B-1 family added in Issue #247.
        { "parrot-blue-world-frame.reference.txt", "parrot-raw-vectors.reference.txt" },
        { "dolphin-world-frame.reference.txt", "dolphin-raw-vectors.reference.txt" },
        { "shulker-world-frame.reference.txt", "shulker-raw-vectors.reference.txt" },
        { "axolotl-world-frame.reference.txt", "axolotl-raw-vectors.reference.txt" },
        { "axolotl-baby-world-frame.reference.txt", "axolotl-baby-raw-vectors.reference.txt" },
        { "tropical-fish-small-world-frame.reference.txt", "tropical-fish-small-raw-vectors.reference.txt" },
        { "tropical-fish-large-world-frame.reference.txt", "tropical-fish-large-raw-vectors.reference.txt" },
        { "salmon-world-frame.reference.txt", "salmon-raw-vectors.reference.txt" },
        { "frog-world-frame.reference.txt", "frog-raw-vectors.reference.txt" },
        { "turtle-world-frame.reference.txt", "turtle-raw-vectors.reference.txt" },
        { "bee-world-frame.reference.txt", "bee-raw-vectors.reference.txt" },
        { "squid-world-frame.reference.txt", "squid-raw-vectors.reference.txt" },
        { "baby-cat-world-frame.reference.txt", "baby-cat-raw-vectors.reference.txt" },
        { "baby-wolf-world-frame.reference.txt", "baby-wolf-raw-vectors.reference.txt" },
        { "baby-fox-world-frame.reference.txt", "baby-fox-raw-vectors.reference.txt" },
        { "baby-rabbit-world-frame.reference.txt", "baby-rabbit-raw-vectors.reference.txt" },
        { "baby-horse-world-frame.reference.txt", "baby-horse-raw-vectors.reference.txt" },
        { "baby-panda-world-frame.reference.txt", "baby-panda-raw-vectors.reference.txt" }
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

        var atlas = Atlas(texturePath, width, height);
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f, VariantId: variantId);
        var sample = LoadSample(worldSample);

        // Full production layer first: the per-key walk below must consume exactly this vertex count, so
        // a part the mesher drops (or an unexpected extra part) fails even when every emitted vertex also
        // appears somewhere in the sample.
        var full = BuildCutout(state, width, height, atlas, onlyPart: null);
        Assert.Equal(sample.Count, VertexCount(full));

        // Per-key: group the sample by source part path (both samples have exactly one cube per part);
        // the production flat part is the path's last segment and each isolated part is meshed through
        // the real living path. Rows are ordered by (polygon, vertex), which is exactly the raw emit
        // order inside one cube, so the position AND the UV of the same key are compared together. A
        // vertex whose position matches one key but whose UV belongs to another fails here.
        var flatNames = new HashSet<string>(StringComparer.Ordinal);
        var consumed = 0;
        foreach (var group in sample.GroupBy(row => row.Key.Part, StringComparer.Ordinal))
        {
            var rows = group.ToArray();
            var flatName = group.Key.Split('/').Last();
            Assert.True(flatNames.Add(flatName),
                $"{worldSample}: sample parts '{group.Key}' and another part map to the same production part '{flatName}'");
            Assert.Single(rows.Select(row => row.Key.Cube).Distinct());
            Assert.All(rows, row => Assert.InRange(row.Key.Vertex, 0, 3));

            var geometry = BuildCutout(state, width, height, atlas, onlyPart: flatName);
            var ordered = rows.OrderBy(row => row.Key.Polygon).ThenBy(row => row.Key.Vertex).ToArray();
            AssertKeyedPartMatches(geometry, ordered, worldSample + ": " + group.Key);
            consumed += rows.Length;
        }

        Assert.Equal(sample.Count, consumed);
    }

    [Fact]
    public void KeyedPartCheckRejectsUvFromAnotherKey()
    {
        // Negative regression for the keyed production check above: a vertex whose position is correct
        // but whose UV was taken from a different sample key must fail. The helper is the same one the
        // production test calls, so this pins that the binding check cannot silently degrade into two
        // independent position/UV multiset checks.
        var definition = EntityVariantTable.Builtin.Resolve(99, 1);
        Assert.True(definition.RawSourceFrame);
        var atlas = Atlas("entity/parrot/parrot_blue", 32, 32);
        var state = new EntityRenderState(1, 99, 0f, 0f, 0f, 180f, 0f, VariantId: 1);
        var geometry = BuildCutout(state, 32, 32, atlas, onlyPart: "head");
        var rows = LoadSample("parrot-blue-world-frame.reference.txt")
            .Where(row => row.Key.Part == "/head")
            .OrderBy(row => row.Key.Polygon).ThenBy(row => row.Key.Vertex)
            .ToArray();

        // Control: the untampered sample rows pass, so the helper is not unconditionally throwing.
        AssertKeyedPartMatches(geometry, rows, "control");

        // Tamper UV only (position untouched): replace row 0's UV with a different key's UV.
        var victim = rows[0];
        var donor = rows.First(row => row.Key != victim.Key && (row.U != victim.U || row.V != victim.V));
        var tampered = rows.ToArray();
        tampered[0] = victim with { U = donor.U, V = donor.V };
        Assert.NotEqual(victim.U, donor.U);

        var exception = Record.Exception(() => AssertKeyedPartMatches(geometry, tampered, "tampered"));
        Assert.NotNull(exception);
        // It must fail on the UV binding, not on some unrelated position/count problem.
        Assert.Contains("UV expected", exception!.Message);
    }

    [Theory]
    [MemberData(nameof(FormulaCases))]
    public void SourceWorldSampleEqualsTheLivingFlipOfTheModelSpaceSample(string worldSample, string modelSpaceSample) =>
        AssertWorldIsLivingFlipOfModelPerKey(LoadSample(worldSample), LoadSample(modelSpaceSample), worldSample);

    /// <summary>
    /// Per-key two-step chain check: world = flip(model) in position, and world UV == model UV at half/1e-5
    /// (the living flip changes position only). Samples are matched by their (layer, part, cube, polygon, vertex)
    /// source key, not by an unordered multiset, so a vertex carrying one key's position and another's UV fails.
    /// </summary>
    internal static void AssertWorldIsLivingFlipOfModelPerKey(IReadOnlyList<SampleRow> world, IReadOnlyList<SampleRow> model, string label)
    {
        Assert.Equal(model.Count, world.Count);
        var keyedModel = model.ToDictionary(row => row.Key);
        Assert.Equal(model.Count, keyedModel.Count);
        foreach (var row in world)
        {
            Assert.True(keyedModel.TryGetValue(row.Key, out var modelRow), $"{label}: missing model row {row.Key}");
            var expected = new Vector3(-modelRow.Position.X, SourceFlipBaselineBlocks - modelRow.Position.Y, modelRow.Position.Z);
            Assert.True((expected - row.Position).Length() <= Tolerance,
                $"{label} {row.Key}: expected world {expected}, sample world {row.Position}");
            Assert.True(
                MathF.Abs(QuantizeHalf(row.U) - QuantizeHalf(modelRow.U)) <= Tolerance
                && MathF.Abs(QuantizeHalf(row.V) - QuantizeHalf(modelRow.V)) <= Tolerance,
                $"{label} {row.Key}: world UV ({row.U:F6},{row.V:F6}) must equal model-space UV ({modelRow.U:F6},{modelRow.V:F6}) at half/1e-5");
        }
    }

    [Fact]
    public void TwoStepChainRejectsWorldSampleWithWrongUv()
    {
        // Negative regression: a world sample whose position is correct but whose UV is corrupted must fail the
        // exact helper the 18 FormulaCases use — guards against the chain degrading into a position-only check.
        var world = LoadSample("shulker-world-frame.reference.txt");
        var model = LoadSample("shulker-raw-vectors.reference.txt");

        // Control: untouched samples pass through the real helper.
        AssertWorldIsLivingFlipOfModelPerKey(world, model, "control");

        // Tamper one world row's UV (position untouched) and require the real helper to reject it.
        var tampered = world.ToArray();
        var victim = tampered[0];
        var donor = tampered.First(r => r.U != victim.U || r.V != victim.V);
        tampered[0] = victim with { U = donor.U, V = donor.V };
        Assert.NotEqual(victim.U, donor.U);
        var exception = Record.Exception(() => AssertWorldIsLivingFlipOfModelPerKey(tampered, model, "tampered"));
        Assert.NotNull(exception);
        Assert.Contains("UV", exception!.Message);
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

    /// <summary>
    /// Keyed production check for one isolated part: vertex <c>i</c> (raw emit order) must match the
    /// sample row with the same (part, cube, polygon, vertex) at 1e-5 in position and UV together, so a
    /// vertex carrying the correct position but another key's UV fails. The negative unit test calls
    /// this directly with a tampered UV.
    /// </summary>
    internal static void AssertKeyedPartMatches(DynamicGeometry geometry, IReadOnlyList<SampleRow> expectedRows, string label)
    {
        Assert.Equal(expectedRows.Count, VertexCount(geometry));
        for (var i = 0; i < expectedRows.Count; i++)
        {
            var row = expectedRows[i];
            var rawPosition = VertexFieldReader.Position(geometry.Vertices, i);
            var actualPosition = new Vector3(rawPosition.X, rawPosition.Y, rawPosition.Z) / EntityPlaceholderMesher.ModelScale;
            Assert.True((actualPosition - row.Position).Length() <= Tolerance,
                $"{label}: {row.Key} world position expected {row.Position}, actual {actualPosition}");

            // The geometry buffer stores UVs as half floats; quantise the sample the same way.
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            var expectedU = QuantizeHalf(row.U);
            var expectedV = QuantizeHalf(row.V);
            Assert.True(MathF.Abs(uv.U - expectedU) <= Tolerance && MathF.Abs(uv.V - expectedV) <= Tolerance,
                $"{label}: {row.Key} UV expected ({expectedU:F6},{expectedV:F6}), actual ({uv.U:F6},{uv.V:F6})");
        }
    }
    private static ItemAtlas Atlas(string texturePath, int width, int height) =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new(width, height, new byte[width * height * 4])
        });

    /// <summary>
    /// Runs the real production layer path for one entity. <paramref name="onlyPart"/> isolates a single
    /// flat part (full ancestor frames preserved) so its vertices can be matched key-for-key against the
    /// source sample.
    /// </summary>
    private static DynamicGeometry BuildCutout(EntityRenderState state, int width, int height, ItemAtlas atlas, string? onlyPart)
    {
        // Bind pose: the jar world samples carry no animation, and yaw 180 makes EntityModelTransform's
        // 180 - yaw rotation the identity, leaving exactly the source flip + baseline.
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, width, height, 1), width, height,
            onlyPart: onlyPart, skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        return layers.Single(layer => layer.Kind == EntityGeometryLayerKind.Cutout).Geometry;
    }

    private static int VertexCount(DynamicGeometry geometry) => geometry.VertexFloats / ChunkMesher.FloatsPerVertex;

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

    internal readonly record struct SampleRow(
        (string Part, int Cube, int Polygon, int Vertex) Key,
        Vector3 Position,
        float U,
        float V);
}
