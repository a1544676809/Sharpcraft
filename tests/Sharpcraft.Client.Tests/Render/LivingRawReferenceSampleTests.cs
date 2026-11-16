// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
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
/// 3) CaveSpider four-facing regression: real geometry vs a legacy spider twin at 0/90/180/270 yaw.
///
/// Baby: creeper/spider/wither have no distinct baby model - registered in docs/risks and render.md.
/// Blocker R-027: mirrored parts' min/max-X swap + polygon reversal is not reproduced by raw; those
/// part groups are explicitly excluded from check 1's source comparison, not silently.
/// </summary>
public sealed class LivingRawReferenceSampleTests
{
    private sealed record Sample(string Part, int Cube, int Polygon, int Vertex, float X, float Y, float Z, float U, float V);

    public static TheoryData<string, EntityModelDefinition, int, int, string> Families => new()
    {
        { "creeper-raw-vectors.reference.txt", CreeperModelDefinition.Definition, 64, 32, "entity/creeper/creeper" },
        { "spider-raw-vectors.reference.txt", SpiderModelDefinition.Definition, 64, 32, "entity/spider/spider" },
        { "wither-raw-vectors.reference.txt", WitherBossModelDefinition.Definition, 64, 64, "entity/wither/wither" }
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void RawBakeMatchesSourceVertices(string sampleFile, EntityModelDefinition definition, int width, int height, string texturePath)
    {
        var rows = LoadSample(sampleFile);
        var atlas = MakeAtlas(texturePath, width, height);

        var checkedVertices = 0;
        var expectedNonMirrorVertices = 0;
        foreach (var group in rows.GroupBy(r => (r.Part, r.Cube)))
        {
            var (part, cube) = group.Key;
            // Mirror exclusion is determined by the REAL baked part's Mirror flag (R-027), never by
            // name: Creeper left_*_leg and Wither left_head are NOT mirror and must be compared.
            EntityModelPart[] sourceParts = part == "/ribcage"
                ? new[] { "ribcage", "rib_1", "rib_2", "rib_3" }.Select(n => definition.Parts.Single(p => p.Name == n)).ToArray()
                : definition.Parts.Where(p => p.Name == part.TrimStart('/')).ToArray();
            Assert.NotEmpty(sourceParts);
            if (sourceParts[0].Mirror)
            {
                continue; // R-027 mirror blocker (actual Mirror flag, not name heuristic)
            }
            expectedNonMirrorVertices += group.Count();

            // The source WitherBoss ribcage is one ModelPart with four cubes; our DSL flattens them to
            // ribcage/rib_1..3. Mesh all four together so the parent frame composes, then compare the
            // per-cube vertex ranges strictly (96 vertices == source cube 0..3, verified).
            EntityModelPart[] ourParts = sourceParts;
            Assert.NotEmpty(ourParts);
            var single = new EntityModelDefinition(definition.Name, width, height, ourParts,
                texturePath: definition.TexturePath, baselinePx: 0f, rawSourceFrame: true);
            var geometry = BuildModelSpace(single, MakeLivingState(180f), width, height, atlas);
            var source = group.OrderBy(r => r.Polygon).ThenBy(r => r.Vertex).ToArray();
            // For the flattened ribcage, our geometry emits 4 cubes (96 verts) in DSL order; each source
            // cube group maps to its 24-vertex range (cube 0 -> ribcage, cube 1..3 -> rib_1..3).
            var vertexOffset = part == "/ribcage" ? cube * 24 : 0;
            Assert.Equal(24, source.Length);
            Assert.True(geometry.VertexFloats / ChunkMesher.FloatsPerVertex >= vertexOffset + 24);
            for (var i = 0; i < source.Length; i++)
            {
                var s = source[i];
                var p = VertexFieldReader.Position(geometry.Vertices, vertexOffset + i);
                var uv = VertexFieldReader.Uv(geometry.Vertices, vertexOffset + i);
                Assert.True(MathF.Abs(s.X - p.X) < 0.001f && MathF.Abs(s.Y - p.Y) < 0.001f && MathF.Abs(s.Z - p.Z) < 0.001f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
                Assert.True(MathF.Abs(s.U - uv.U) < 0.0001f && MathF.Abs(s.V - uv.V) < 0.0001f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex} UV: src=({s.U},{s.V}) our=({uv.U},{uv.V})");
                checkedVertices++;
            }
        }
        // Full coverage: every non-mirror source vertex must have been strictly compared. No silent
        // skipping by name; only parts whose real baked Mirror flag is true are excluded (R-027).
        Assert.Equal(expectedNonMirrorVertices, checkedVertices);
        Assert.True(checkedVertices > 0);
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
    public void CaveSpiderFacingMatchesLegacyTwinPerVertex(float yaw)
    {
        // CaveSpider reuses raw spider geometry at scale 0.7. Build a legacy spider twin from the same
        // DSL and compare final world positions across all four facings, strictly per-vertex set.
        var raw = CaveSpiderModelDefinition.Definition;
        Assert.True(raw.RawSourceFrame);
        Assert.Equal(0f, raw.BaselinePx);
        Assert.Null(raw.FaceTransforms is { Count: > 0 } ? raw.FaceTransforms : null);

        var legacy = LegacyEntityTwinFactory.Spider(0.7f, "entity/spider/cave_spider");
        var state = new EntityRenderState(1, CaveSpiderModelDefinition.CaveSpiderEntityTypeId, 0f, 0f, 0f, yaw, 0f);
        var atlas = MakeAtlas("entity/spider/cave_spider", 64, 32);
        AssertFullModelWorldPositions(raw, legacy, state, 64, 32, atlas);
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

    private static ItemAtlas MakeAtlas(string tex, int w, int h) =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal) { [tex] = new(w, h, new byte[w * h * 4]) });

    private static EntityRenderState MakeLivingState(float yaw) =>
        new(1, 0, 0f, 0f, 0f, yaw, 0f);

    private static DynamicGeometry BuildModelSpace(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas) =>
        new EntityModelMesher().BuildModel(def, state, atlas.Get(def.TexturePath)!, w, h, modelScale: 16f);

    private static DynamicGeometry BuildLivingWorld(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas)
    {
        // The internal BuildInto seam exposes the exact living-entity geometry path with a model override
        // and no block transform factory, so EntityModelTransform.ForRawSourceFrame is exercised on real parts.
        return new EntityModelMesher().BuildInto(new DynamicGeometry(), null, [state], new SpriteRect(0, 0, w, h, 1), w, h,
            skinAtlas: atlas, modelOverride: def);
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
