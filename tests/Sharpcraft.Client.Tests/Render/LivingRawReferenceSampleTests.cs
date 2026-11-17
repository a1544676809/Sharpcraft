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
/// Source-equivalence evidence for living-entity raw source-frame models. Three independent checks:
///
/// 1) <see cref="RawBakeMatchesSourceVertices"/>: raw bake + mesher model-space output is compared
///    STRICTLY, keyed by the sample part/cube/polygon/vertex columns, against the ModelProbe source
///    positions AND UVs (jar e877b6a0). No sorting/rounding/set-containment. Per-part isolation works
///    around vanilla's hash-map child order. This is the UV/position source oracle.
/// 2) <see cref="RawModelFinalWorldMatchesLegacyTwin"/>: the real Creeper/Spider/Wither models are
///    meshed through the actual living-entity geometry path (EntityModelMesher.BuildInto with a model
///    override and no block factory) in BOTH raw and legacy modes from the same DSL, and their FULL
///    final world-vertex position multisets are compared strictly. This pins the frame flip+baseline
///    on real geometry, not hand-written points. UV equivalence is NOT claimed here: raw uses source
///    face UVs (verified by check 1), legacy uses the legacy face calibration (a documented deviation).
/// 3) CaveSpider four-facing regression: real raw geometry at Scale=0.7 vs a source-sample-derived
///    live world frame (spider source sample × 0.7 + 24.016*(1-0.7) px root offset + living flip).
///
/// Baby: creeper/spider/wither have no distinct baby model - registered in docs/risks and render.md.
/// Blocker R-027: mirrored parts' min/max-X swap + polygon reversal is not reproduced by raw; those
/// part groups are explicitly excluded from check 1's source comparison, not silently.
/// </summary>
public sealed class LivingRawReferenceSampleTests
{
    private sealed record Sample(string Part, int Cube, int Polygon, int Vertex, float X, float Y, float Z, float U, float V);

    // Hard-coded expected counts from the jar probe samples (independent of the sample-loaded values):
    // total sample rows, rows belonging to genuinely-mirrored parts (Mirror=true), and rows that must be
    // strictly checked. Creeper/Wither have no mirrored parts; Spider has four mirrored left legs (4*24=96).
    public static TheoryData<string, EntityModelDefinition, int, int, string, int, int, int, int> Families => new()
    {
        { "creeper-raw-vectors.reference.txt", CreeperModelDefinition.Definition, 64, 32, "entity/creeper/creeper", 144, 0, 144, 0 },
        { "spider-raw-vectors.reference.txt", SpiderModelDefinition.Definition, 64, 32, "entity/spider/spider", 264, 96, 168, 0 },
        { "wither-raw-vectors.reference.txt", WitherBossModelDefinition.Definition, 64, 64, "entity/wither/wither", 216, 0, 216, 0 },
        { "pig-raw-vectors.reference.txt", PigModelDefinition.Definition, 64, 64, "entity/pig/pig_temperate", 168, 48, 120, 0 },
        { "cow-raw-vectors.reference.txt", CowModelDefinition.Definition, 64, 64, "entity/cow/cow_temperate", 240, 48, 192, 0 },
        { "chicken-raw-vectors.reference.txt", ChickenModelDefinition.Definition, 64, 32, "entity/chicken/chicken_temperate", 192, 24, 168, 0 },
        { "pig-baby-raw-vectors.reference.txt", BabyModelDefinitions.Pig, 32, 32, "entity/pig/pig_temperate_baby", 168, 0, 168, 0 },
        { "cow-baby-raw-vectors.reference.txt", BabyModelDefinitions.Cow, 64, 64, "entity/cow/cow_temperate_baby", 216, 24, 192, 0 },
        // Chicken-baby left/right legs + feet + wings are zero-area cubes (depth=0 or height=0); the
        // mesher culls degenerate faces, emitting 8 verts where the source probe emits all 24. This
        // pre-existing culling difference is registered as R-029 / Issue #126. The 144 degenerate
        // sample rows are excluded from BOTH strict source UV comparison AND from any world/legacy
        // twin check (WorldCases has no quadruped/baby cases). Their positions/UVs are therefore NOT
        // covered by an independent oracle - this is an explicit, audited gap, not a silent pass.
        { "chicken-baby-raw-vectors.reference.txt", BabyModelDefinitions.Chicken, 16, 16, "entity/chicken/chicken_temperate_baby", 192, 0, 48, 144 },
        // task-62 aquatic/flying batch. Counts = (total, mirror, checked, degenerate) from jar probe.
        { "cod-raw-vectors.reference.txt", CodModelDefinition.Definition, 32, 32, "entity/fish/cod", 168, 0, 72, 96 },
        { "salmon-raw-vectors.reference.txt", SalmonModelDefinition.Definition, 32, 32, "entity/fish/salmon", 192, 0, 72, 120 },
        { "tropical-fish-small-raw-vectors.reference.txt", TropicalFishModelDefinition.Definition, 32, 32, "entity/fish/tropical_a", 120, 0, 24, 96 },
        { "tropical-fish-large-raw-vectors.reference.txt", TropicalFishModelDefinition.Variants["flopper"], 32, 32, "entity/fish/tropical_b", 144, 0, 24, 120 },
        { "pufferfish-small-raw-vectors.reference.txt", PufferfishModelDefinition.Definition, 32, 32, "entity/fish/pufferfish", 144, 0, 72, 72 },
        { "pufferfish-medium-raw-vectors.reference.txt", PufferfishModelDefinition.Variants["medium"], 32, 32, "entity/fish/pufferfish", 264, 0, 48, 216 },
        { "pufferfish-large-raw-vectors.reference.txt", PufferfishModelDefinition.Variants["large"], 32, 32, "entity/fish/pufferfish", 312, 0, 96, 216 },
        { "tadpole-raw-vectors.reference.txt", TadpoleModelDefinition.Definition, 16, 16, "entity/tadpole/tadpole", 48, 0, 24, 24 },
        { "squid-raw-vectors.reference.txt", SquidModelDefinition.Definition, 64, 32, "entity/squid/squid", 216, 0, 216, 0 },
        { "glow-squid-raw-vectors.reference.txt", SquidModelDefinition.GlowSquid, 64, 32, "entity/squid/glow_squid", 216, 0, 216, 0 },
        { "dolphin-raw-vectors.reference.txt", DolphinModelDefinition.Definition, 64, 64, "entity/dolphin/dolphin", 192, 24, 168, 0 },
        { "axolotl-raw-vectors.reference.txt", AxolotlModelDefinition.Definition, 64, 64, "entity/axolotl/axolotl", 264, 0, 48, 216 },
        { "axolotl-baby-raw-vectors.reference.txt", BabyModelDefinitionsAquatic.Axolotl, 32, 32, "entity/axolotl/axolotl_baby", 264, 0, 48, 216 },
        { "turtle-raw-vectors.reference.txt", TurtleModelDefinition.Definition, 128, 64, "entity/turtle/turtle", 192, 0, 168, 24 },
        { "turtle-baby-raw-vectors.reference.txt", BabyModelDefinitionsMisc.Turtle, 16, 16, "entity/turtle/turtle_baby", 144, 0, 48, 96 },
        { "frog-raw-vectors.reference.txt", FrogModelDefinition.Definition, 48, 48, "entity/frog/frog_temperate", 384, 0, 216, 168 },
        { "bat-raw-vectors.reference.txt", BatModelDefinition.Definition, 32, 32, "entity/bat/bat", 216, 0, 48, 168 },
        { "bee-raw-vectors.reference.txt", BeeModelDefinition.Definition, 64, 64, "entity/bee/bee", 216, 0, 72, 144 },
        { "bee-baby-raw-vectors.reference.txt", BabyModelDefinitionsAquatic.Bee, 32, 32, "entity/bee/bee_baby", 216, 0, 72, 144 },
        { "parrot-raw-vectors.reference.txt", ParrotModelDefinition.Definition, 32, 32, "entity/parrot/parrot", 264, 0, 240, 24 },
        { "allay-raw-vectors.reference.txt", AllayModelDefinition.Definition, 32, 32, "entity/allay/allay", 168, 0, 120, 48 },
        // task-81: CopperGolem entity raw, nested part paths resolved via full parent chain.
        // Counts = (total, mirror, checked, degenerate) from jar probe: 216 rows, no mirrors/degenerate.
        { "copper-golem-raw-vectors.reference.txt", CopperGolemModelDefinition.Definition, 64, 64, "entity/copper_golem/copper_golem", 216, 0, 216, 0 },
        // task-94 special/large first group (total, mirror, checked, degenerate).
        { "shulker-raw-vectors.reference.txt", ShulkerModelDefinition.Definition, 64, 64, "entity/shulker/shulker", 72, 0, 72, 0 },
        { "blaze-raw-vectors.reference.txt", BlazeModelDefinition.Definition, 64, 32, "entity/blaze/blaze", 312, 0, 312, 0 },
        // breeze BLOCKED (special DSL/jar geometry gap, task-94)
        // creaking BLOCKED (special DSL/jar geometry gap, task-94)
        { "endermite-raw-vectors.reference.txt", EndermiteModelDefinition.Definition, 64, 32, "entity/endermite/endermite", 96, 0, 96, 0 },
        // ghast BLOCKED (special DSL/jar geometry gap, task-94)
        // task-104: HappyGhast (Scale=4) raw, enabled by the layer-scale root offset (#141).
        { "happy-ghast-raw-vectors.reference.txt", HappyGhastModelDefinition.Definition, 64, 64, "entity/ghast/happy_ghast", 240, 0, 240, 0 },
        // giant BLOCKED (special DSL/jar geometry gap, task-94)
        // guardian BLOCKED (special DSL/jar geometry gap, task-94)
        // iron-golem BLOCKED (special DSL/jar geometry gap, task-94)
        // task-95: illager hat as independent cube + layer scale 0.9375 (4 families share one mesh).
        { "evoker-raw-vectors.reference.txt", IllagerModelDefinition.Evoker, 64, 64, "entity/illager/evoker", 288, 72, 216, 0 },
        { "vindicator-raw-vectors.reference.txt", IllagerModelDefinition.Vindicator, 64, 64, "entity/illager/vindicator", 288, 72, 216, 0 },
        { "pillager-raw-vectors.reference.txt", IllagerModelDefinition.Pillager, 64, 64, "entity/illager/pillager", 288, 72, 216, 0 },
        { "illusioner-raw-vectors.reference.txt", IllagerModelDefinition.Illusioner, 64, 64, "entity/illager/illusioner", 288, 72, 216, 0 }
    };

    /// <summary>
    /// Guards against isolated-definition false greens: the production definitions migrated in the
    /// task-94 special batch must themselves carry the raw source-frame flags. The sample test below
    /// rebuilds an isolated definition for one part at a time, so a production definition missing these
    /// flags would still pass the model-space samples while the real living entity renders the legacy way.
    /// </summary>
    [Fact]
    public void Task94MigratedProductionDefinitionsCarryRawSourceFrameFlags()
    {
        // Guards isolated-definition false greens: the model-space sample test rebuilds a one-part
        // definition with raw flags, so a production definition missing them would still pass samples
        // while the real living entity renders the legacy way (the Blaze regression this fixes).
        // HappyGhast is Scale != 1 and stays legacy pending the layer-scale root-offset spec (#141).
        var migrated = new (string Name, EntityModelDefinition Def)[]
        {
            ("shulker", ShulkerModelDefinition.Definition),
            ("blaze", BlazeModelDefinition.Definition),
            ("endermite", EndermiteModelDefinition.Definition)
        };
        foreach (var (name, def) in migrated)
        {
            Assert.True(def.RawSourceFrame, $"{name}: production definition must be rawSourceFrame");
            Assert.Equal(0f, def.BaselinePx);
            Assert.Equal(24f, def.SourceFrameBaselinePx);
            Assert.Null(def.FaceTransforms is { Count: > 0 } ? def.FaceTransforms : null);
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void RawBakeMatchesSourceVertices(string sampleFile, EntityModelDefinition definition, int width, int height,
        string texturePath, int expectedTotal, int expectedMirror, int expectedChecked, int expectedDegenerate)
    {
        var rows = LoadSample(sampleFile);
        var atlas = MakeAtlas(texturePath, width, height);

        // Verify the raw sample file itself against hard-coded expectations, before any comparison.
        Assert.Equal(expectedTotal, rows.Count);

        var checkedVertices = 0;
        var mirrorVertices = 0;
        var degenerateVertices = 0;
        var expectedNonMirrorVertices = 0;
        foreach (var group in rows.GroupBy(r => (r.Part, r.Cube)))
        {
            var (part, cube) = group.Key;
            // Source ModelParts can own several cubes, while our DSL flattens each cube to its own part
            // (e.g. source /head has head+snout+horns). Map the sample (part,cube) to our flat part name.
            // Mirror exclusion is determined by the REAL baked part's Mirror flag (R-027), never by name.
            EntityModelPart[] sourceParts = FlatPartsForSampleCube(definition, part, cube);
            Assert.NotEmpty(sourceParts);
            if (sourceParts[0].Width == 0f || sourceParts[0].Height == 0f || sourceParts[0].Depth == 0f
                || EmittedVertCount(sourceParts[0], width, height, atlas, texturePath) == 0)
            {
                // Degenerate/hidden cube: mesher culls the faces the source probe emits. Pre-existing
                // behavior, registered as blocker (R-029/#126). Explicitly excluded, not silently passed.
                degenerateVertices += group.Count();
                continue;
            }
            if (sourceParts[0].Mirror)
            {
                mirrorVertices += group.Count();
                continue; // R-027 mirror blocker (actual Mirror flag, not name heuristic)
            }
            expectedNonMirrorVertices += group.Count();

            Assert.NotEmpty(sourceParts);
            // Include ancestor parts so child pivots (e.g. baby pig snout is a child of head) compose.
            // The target part is emitted first in definition.Parts order; ancestors follow.
            var targetName = sourceParts[0].Name;
            var sourceNames = sourceParts.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var partOrder = definition.Parts.Where(p => sourceNames.Contains(p.Name) || IsAncestorOf(p, targetName, definition)).ToArray();
            // The jar sample is bind-pose geometry with ALL parts emitted; strip runtime visibility
            // predicates (e.g. illager crossed-vs-open arms are mutually exclusive at runtime) so the
            // geometry test compares the stored cubes, not a particular animation state.
            partOrder = partOrder.Select(p => p with { VisibleWhen = null }).ToArray();
            var single = new EntityModelDefinition(definition.Name, width, height, partOrder,
                texturePath: texturePath, scale: definition.Scale, baselinePx: 0f, rawSourceFrame: true,
                sourceFrameBaselinePx: definition.SourceFrameBaselinePx);
            var geometry = BuildModelSpace(single, MakeLivingState(180f), width, height, atlas);
            var source = group.OrderBy(r => r.Polygon).ThenBy(r => r.Vertex).ToArray();
            // The target's 24 vertices are emitted after all preceding parts in partOrder; each part
            // emits a variable count (0 for fully-zero/hidden parts, 8 for single-plane parts, 24 for
            // full cubes), so sum actual emitted counts rather than assuming 24 each.
            var vertexOffset = 0;
            foreach (var before in partOrder)
            {
                if (before.Name == targetName) break;
                vertexOffset += EmittedVertCount(before, width, height, atlas, texturePath);
            }
            // For the flattened Wither ribcage, all four cubes were returned; map cube index to its part.
            if (part == "/ribcage") vertexOffset = cube * 24;
            Assert.Equal(24, source.Length);
            Assert.True(geometry.VertexFloats / ChunkMesher.FloatsPerVertex >= vertexOffset + 24,
                $"part={part} cube={cube} target={targetName} vertexOffset={vertexOffset} partOrder=[{string.Join(",", partOrder.Select(p => p.Name))}] geoVerts={geometry.VertexFloats / ChunkMesher.FloatsPerVertex}");
            for (var i = 0; i < source.Length; i++)
            {
                var s = source[i];
                var p = VertexFieldReader.Position(geometry.Vertices, vertexOffset + i);
                var uv = VertexFieldReader.Uv(geometry.Vertices, vertexOffset + i);
                // #128: samples are in normalized render units; production built with modelScale:1.
                // Tight 1e-5 tolerance (Book standard) for both position and UV. The sample UV is
                // quantized to half-float first, matching what the geometry buffer actually stores.
                Assert.True(MathF.Abs(s.X - p.X) < 1e-5f && MathF.Abs(s.Y - p.Y) < 1e-5f && MathF.Abs(s.Z - p.Z) < 1e-5f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
                var su = QuantizeHalf(s.U);
                var sv = QuantizeHalf(s.V);
                Assert.True(MathF.Abs(su - uv.U) < 1e-5f && MathF.Abs(sv - uv.V) < 1e-5f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex} UV: src=({su},{sv}) our=({uv.U},{uv.V})");
                checkedVertices++;
            }
        }
        // Counts pinned against hard-coded jar-probe expectations (no sample self-justification):
        //   total = mirror + degenerate + checked, each equals its explicit constant.
        Assert.Equal(expectedMirror, mirrorVertices);
        Assert.Equal(expectedDegenerate, degenerateVertices);
        Assert.Equal(expectedChecked, checkedVertices);
        Assert.Equal(expectedNonMirrorVertices, checkedVertices);
        Assert.Equal(expectedTotal, mirrorVertices + degenerateVertices + checkedVertices);
    }

    /// <summary>
    /// Real-model final-world equivalence. Both models come from the SAME declarative DSL mesh (read via
    /// reflection), one baked raw (current production definition), one baked legacy
    /// (rawSourceFrame:false, baselinePx:24). Both are meshed through the real living path and their
    /// complete world-vertex POSITION multisets compared strictly. UV is excluded here by design
    /// (legacy applies a face-UV calibration; raw uses source UVs verified in check 1).
    /// </summary>
    public static TheoryData<EntityModelDefinition, EntityModelDefinition, int, int, string, EntityRenderState> WorldCases => new()
    {
        { CreeperModelDefinition.Definition, LegacyEntityTwinFactory.Creeper(), 64, 32, "entity/creeper/creeper", new EntityRenderState(1, 32, 10f, 2f, -4f, 45f, 0f) },
        { SpiderModelDefinition.Definition, LegacyEntityTwinFactory.Spider(), 64, 32, "entity/spider/spider", new EntityRenderState(1, 127, -3f, 1f, 6f, 250f, 0f) },
        { WitherBossModelDefinition.Definition, LegacyEntityTwinFactory.Wither(), 64, 64, "entity/wither/wither", new EntityRenderState(1, 149, 0f, 5f, 0f, 90f, 0f) }
    };

    [Theory]
    [MemberData(nameof(WorldCases))]
    public void RawModelFinalWorldMatchesLegacyTwin(EntityModelDefinition rawDefinition, EntityModelDefinition legacyTwin, int width, int height, string texturePath, EntityRenderState state)
    {
        var atlas = MakeAtlas(texturePath, width, height);
        // Full-model final world comparison through the real living path. Raw and legacy emit faces in
        // different order (raw = source face order), so per-index equality is not expected; the strict
        // relation is a multiplicity-preserving bijection of world POSITION tuples. UVs are not compared
        // here because legacy carries a documented face-UV calibration while raw uses source UVs (which
        // are verified strictly against the jar probe in RawBakeMatchesSourceVertices).
        AssertFullModelWorldPositions(rawDefinition, legacyTwin, state, width, height, atlas);
    }

    private static void AssertFullModelWorldPositions(EntityModelDefinition raw, EntityModelDefinition legacy, EntityRenderState state,
        int width, int height, ItemAtlas atlas)
    {
        // Full-model strict world POSITION comparison through the real living path. Raw and legacy
        // emit faces/parts in different order, and rotated/mirrored parts can land in each other's
        // slots (R-027 mirror convention), so the strict relation is a multiplicity-preserving
        // bijection of ALL world position tuples - a sorted LIST at 1e-4, not a set and not bounds.
        // This detects any dropped/duplicated/moved vertex while absorbing only 1-ULP float noise.
        // Per-vertex UV source equivalence is proven separately, strictly per part/cube/polygon/vertex,
        // in RawBakeMatchesSourceVertices against the jar probe (legacy UV is a documented deviation).
        var rawGeo = BuildLivingWorld(raw, state, width, height, atlas);
        var legacyGeo = BuildLivingWorld(legacy, state, width, height, atlas);
        Assert.Equal(legacyGeo.VertexFloats / ChunkMesher.FloatsPerVertex,
                     rawGeo.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(WorldPositions(legacyGeo), WorldPositions(rawGeo));
    }

    /// <summary>
    /// World positions for every emitted vertex as a sorted list keyed at 1e-4 (4 decimals). Sorting a
    /// LIST (not a set) preserves multiplicity, so a duplicated/dropped/reordered vertex still fails;
    /// the 1e-4 key only absorbs the 1-ULP float difference between the two mathematically-equal bakes.
    /// </summary>
    private static (int, int, int)[] WorldPositions(DynamicGeometry geometry)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new (int, int, int)[count];
        for (var i = 0; i < count; i++)
        {
            var p = VertexFieldReader.Position(geometry.Vertices, i);
            result[i] = ((int)MathF.Round(p.X * 10000f), (int)MathF.Round(p.Y * 10000f), (int)MathF.Round(p.Z * 10000f));
        }
        Array.Sort(result);
        return result;
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(270f)]
    public void CaveSpiderLayerScaleRootFrameMatchesSpiderSource(float yaw)
    {
        // CaveSpider reuses the raw spider geometry at Scale=0.7. Its world frame is source-driven:
        // the spider model-space sample is scaled by 0.7, translated by 24.016*(1-0.7) px, then the
        // living flip/baseline is applied. This replaces the old legacy-twin oracle, which kept the
        // missing layer-scale root offset (R-034) and was therefore wrong for scaled raw models.
        var raw = CaveSpiderModelDefinition.Definition;
        Assert.True(raw.RawSourceFrame);
        Assert.Equal(0f, raw.BaselinePx);
        Assert.Null(raw.FaceTransforms is { Count: > 0 } ? raw.FaceTransforms : null);

        var source = LoadSample("spider-raw-vectors.reference.txt").First();
        var scaled = new Vector3(
            -source.X * 0.7f,
            24f / 16f - (24.016f * (1f - 0.7f) / 16f + source.Y * 0.7f),
            source.Z * 0.7f);
        var expected = Vector3.Transform(
            scaled,
            Matrix4x4.CreateRotationY((180f - yaw) * (MathF.PI / 180f)));

        var state = new EntityRenderState(1, CaveSpiderModelDefinition.CaveSpiderEntityTypeId, 0f, 0f, 0f, yaw, 0f);
        var atlas = MakeAtlas("entity/spider/cave_spider", 64, 32);
        var geometry = BuildLivingWorld(raw, state, 64, 32, atlas, modelScale: 1f);
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.True(
            MathF.Abs(expected.X - actual.X) < 1e-5f
                && MathF.Abs(expected.Y - actual.Y) < 1e-5f
                && MathF.Abs(expected.Z - actual.Z) < 1e-5f,
            $"yaw={yaw}: expected=({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                + $" actual=({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
    }

    /// <summary>
    /// Creeper/spider/wither have no distinct baby model definitions (baby mammals only). This is an
    /// explicit N/A registration; the mammal families' baby variants are covered by their own migrations.
    /// </summary>
    [Fact]
    public void BabyVariantsAreNotApplicableToTheseFamilies()
    {
        foreach (var typeId in new[] { CreeperModelDefinition.CreeperEntityTypeId, SpiderModelDefinition.SpiderEntityTypeId, WitherBossModelDefinition.WitherEntityTypeId })
        {
            var baby = EntityModelResolver.TryResolve(typeId, baby: true);
            Assert.True(baby is null || !baby.Name.StartsWith("baby_", StringComparison.Ordinal),
                $"type {typeId} unexpectedly has a distinct baby model");
        }
    }

    /// <summary>
    /// Maps a source sample (part path, cube index) to our DSL's flattened part records. Several source
    /// ModelParts own multiple cubes that our DSL declares as separate parts; the mapping reflects the
    /// CubeListBuilder.addBox order in the 26.3 source (PigModel/CowModel/ChickenModel/WitherBossModel).
    /// </summary>
    private static EntityModelPart[] FlatPartsForSampleCube(EntityModelDefinition definition, string part, int cube)
    {
        string flatName;
        switch (part)
        {
            // Baby variants: cube order follows the reference Baby*Model CubeListBuilder.addBox order.
            case "/head" when definition.Name.StartsWith("baby_pig", StringComparison.Ordinal):
                flatName = cube == 0 ? "head" : "snout"; break;
            case "/head" when definition.Name.StartsWith("baby_cow", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "ear_right", 2 => "ear_left", _ => "snout" }; break;
            case "/body" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "beak"; break;
            case "/left_leg" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "left_leg" : "left_foot"; break;
            case "/right_leg" when definition.Name.StartsWith("baby_chicken", StringComparison.Ordinal):
                flatName = cube == 0 ? "right_leg" : "right_foot"; break;
            case "/head" when definition.Name.StartsWith("pig", StringComparison.Ordinal):
                flatName = cube == 0 ? "head" : "snout"; break;
            case "/head" when definition.Name.StartsWith("cow", StringComparison.Ordinal) || definition.Name.StartsWith("mooshroom", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "snout", 2 => "right_horn", _ => "left_horn" }; break;
            case "/body" when definition.Name.StartsWith("cow", StringComparison.Ordinal) || definition.Name.StartsWith("mooshroom", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "udder"; break;
            // task-62 multi-cube source parts flattened into separate DSL parts (source addBox order).
            case "/body" when definition.Name.StartsWith("turtle", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "belly"; break;
            case "/body" when definition.Name.StartsWith("axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body" when definition.Name.StartsWith("baby_axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_fin"; break;
            case "/root/body" when definition.Name.StartsWith("axolotl", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body" when definition.Name.StartsWith("frog", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_layer"; break;
            case "/root/body/head" when definition.Name.StartsWith("frog", StringComparison.Ordinal):
                // Source CubeListBuilder order: head_layer (height 0) first, then head (height 3).
                flatName = cube == 0 ? "head_layer" : "head"; break;
            case "/root/body" when definition.Name.StartsWith("allay", StringComparison.Ordinal):
                flatName = cube == 0 ? "body" : "body_inner"; break;
            case "/bone" when definition.Name.StartsWith("baby_bee", StringComparison.Ordinal):
                flatName = cube == 0 ? "bone" : "antenna_left"; break;
            // task-81: CopperGolem source head is one ModelPart with 4 cubes (head/nose/antenna/
            // antenna_tip), flattened into separate DSL parts in addBox order.
            case "/body/head" when definition.Name.StartsWith("copper_golem", StringComparison.Ordinal):
                flatName = cube switch { 0 => "head", 1 => "nose", 2 => "antenna", _ => "antenna_tip" }; break;
            // task-95: illager multi-cube source parts. Source addBox order: /body -> body, body_outer;
            // /arms -> arms_right, arms_cross. Hat is now an independent child part named "hat".
            case "/body" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = cube == 0 ? "body" : "body_outer"; break;
            case "/arms" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = cube == 0 ? "arms_right" : "arms_cross"; break;
            case "/arms/left_shoulder" when definition.Name is "evoker" or "vindicator" or "pillager" or "illusioner":
                flatName = "arms_left"; break;
            case "/head/beak": flatName = "beak"; break;
            case "/head/red_thing": flatName = "red_thing"; break;
            case "/ribcage" when definition.Name.StartsWith("wither", StringComparison.Ordinal):
                // Wither ribcage is one source part with 4 cubes; meshed together below via the
                // multi-cube group handling in the caller (all 4 parts, offset by cube).
                return new[] { "ribcage", "rib_1", "rib_2", "rib_3" }.Select(n => definition.Parts.Single(p => p.Name == n)).ToArray();
            default:
                flatName = part.TrimStart('/').Split('/').Last(); break;
        }
        return [definition.Parts.Single(p => p.Name == flatName)];
    }

    /// <summary>Vertex count a single part emits when meshed (depends only on its cube dimensions/visibility).</summary>
    private static int EmittedVertCount(EntityModelPart part, int w, int h, ItemAtlas atlas, string texturePath)
    {
        var single = new EntityModelDefinition("count", w, h, [part with { VisibleWhen = null }], texturePath: texturePath, baselinePx: 0f, rawSourceFrame: true);
        var geo = BuildModelSpace(single, MakeLivingState(180f), w, h, atlas);
        return geo.VertexFloats / ChunkMesher.FloatsPerVertex;
    }

    /// <summary>True if part p is an ancestor (transitively) of targetName.</summary>
    private static bool IsAncestorOf(EntityModelPart p, string targetName, EntityModelDefinition def)
    {
        if (p.Name is null) return false;
        var cursor = def.Parts.FirstOrDefault(x => x.Name == targetName);
        var guard = 0;
        while (cursor.Name is not null && cursor.Parent is { } parent && guard++ < 32)
        {
            if (parent == p.Name) return true;
            cursor = def.Parts.FirstOrDefault(x => x.Name == parent);
        }
        return false;
    }

    private static ItemAtlas MakeAtlas(string tex, int w, int h) =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal) { [tex] = new(w, h, new byte[w * h * 4]) });

    private static EntityRenderState MakeLivingState(float yaw) =>
        new(1, 0, 0f, 0f, 0f, yaw, 0f);

    /// <summary>Quantize a full-float sample UV to the half-float value the geometry buffer stores.</summary>
    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

    private static DynamicGeometry BuildModelSpace(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas) =>
        // #128 render units: modelScale:1 gives px=1/16, so output matches normalized samples.
        new EntityModelMesher().BuildModel(def, state, atlas.Get(def.TexturePath)!, w, h, modelScale: 1f);

    private static DynamicGeometry BuildLivingWorld(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas, float modelScale = EntityPlaceholderMesher.ModelScale)
    {
        // The internal BuildInto seam exposes the exact living-entity geometry path with a model override
        // and no block transform factory, so EntityModelTransform.ForRawSourceFrame is exercised on real parts.
        return new EntityModelMesher().BuildInto(new DynamicGeometry(), null, [state], new SpriteRect(0, 0, w, h, 1), w, h,
            skinAtlas: atlas, modelOverride: def, modelScale: modelScale);
    }

    private static List<Sample> LoadSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var result = new List<Sample>();
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.StartsWith('#') || string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            result.Add(new Sample(f[1], int.Parse(f[2], CultureInfo.InvariantCulture), int.Parse(f[3], CultureInfo.InvariantCulture),
                int.Parse(f[4], CultureInfo.InvariantCulture), float.Parse(f[5], CultureInfo.InvariantCulture),
                float.Parse(f[6], CultureInfo.InvariantCulture), float.Parse(f[7], CultureInfo.InvariantCulture),
                float.Parse(f[8], CultureInfo.InvariantCulture), float.Parse(f[9], CultureInfo.InvariantCulture)));
        }
        return result;
    }
}

