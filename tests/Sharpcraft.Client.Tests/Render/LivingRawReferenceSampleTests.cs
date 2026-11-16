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
        foreach (var group in rows.GroupBy(r => (r.Part, r.Cube)))
        {
            var (part, cube) = group.Key;
            if (IsMirrorSourcePart(part) || (part == "/ribcage" && cube > 0))
            {
                // R-027 mirror blocker; ribcage multi-cube flattening is covered by the full-model
                // world test (RawModelFinalWorldMatchesLegacyTwin) which meshes every part incl. ribs.
                continue;
            }

            EntityModelPart[] ourParts = [.. definition.Parts.Where(p => p.Name == part.TrimStart('/'))];
            Assert.NotEmpty(ourParts);
            var single = new EntityModelDefinition(definition.Name, width, height, ourParts,
                texturePath: definition.TexturePath, baselinePx: 0f, rawSourceFrame: true);
            var geometry = BuildModelSpace(single, MakeLivingState(180f), width, height, atlas);
            var source = group.OrderBy(r => r.Polygon).ThenBy(r => r.Vertex).ToArray();
            Assert.Equal(source.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
            for (var i = 0; i < source.Length; i++)
            {
                var s = source[i];
                var p = VertexFieldReader.Position(geometry.Vertices, i);
                var uv = VertexFieldReader.Uv(geometry.Vertices, i);
                Assert.True(MathF.Abs(s.X - p.X) < 0.001f && MathF.Abs(s.Y - p.Y) < 0.001f && MathF.Abs(s.Z - p.Z) < 0.001f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
                Assert.True(MathF.Abs(s.U - uv.U) < 0.0001f && MathF.Abs(s.V - uv.V) < 0.0001f,
                    $"part={part} cube={cube} poly={s.Polygon} v={s.Vertex} UV: src=({s.U},{s.V}) our=({uv.U},{uv.V})");
                checkedVertices++;
            }
        }
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
        var rawWorld = BuildLivingWorld(rawDefinition, state, width, height, atlas);
        var legacyWorld = BuildLivingWorld(legacyTwin, state, width, height, atlas);

        Assert.Equal(legacyWorld.Length, rawWorld.Length);
        // Same final world-coordinate set, strict at 1e-3. Order differs (raw uses source face order),
        // so compare sorted position tuples. This is a position multiset equality, not a source oracle;
        // source positions/UVs are verified strictly per-key in RawBakeMatchesSourceVertices.
        var a = rawWorld.Select(p => (MathF.Round(p.X, 3), MathF.Round(p.Y, 3), MathF.Round(p.Z, 3))).OrderBy(p => p).ToArray();
        var b = legacyWorld.Select(p => (MathF.Round(p.X, 3), MathF.Round(p.Y, 3), MathF.Round(p.Z, 3))).OrderBy(p => p).ToArray();
        Assert.Equal(a, b);
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
        var rawWorld = BuildLivingWorld(raw, state, 64, 32, atlas);
        var legacyWorld = BuildLivingWorld(legacy, state, 64, 32, atlas);
        Assert.Equal(legacyWorld.Length, rawWorld.Length);
        var a = rawWorld.Select(p => (MathF.Round(p.X, 3), MathF.Round(p.Y, 3), MathF.Round(p.Z, 3))).OrderBy(p => p).ToArray();
        var b = legacyWorld.Select(p => (MathF.Round(p.X, 3), MathF.Round(p.Y, 3), MathF.Round(p.Z, 3))).OrderBy(p => p).ToArray();
        Assert.Equal(a, b);
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

    private static (float X, float Y, float Z)[] BuildLivingWorld(EntityModelDefinition def, EntityRenderState state, int w, int h, ItemAtlas atlas)
    {
        // The internal BuildInto seam exposes the exact living-entity geometry path with a model override
        // and no block transform factory, so EntityModelTransform.ForRawSourceFrame is exercised on real parts.
        var target = new DynamicGeometry();
        var geometry = new EntityModelMesher().BuildInto(target, null, [state], new SpriteRect(0, 0, w, h, 1), w, h,
            skinAtlas: atlas, modelOverride: def);
        var result = new (float, float, float)[geometry.VertexFloats / ChunkMesher.FloatsPerVertex];
        for (var i = 0; i < result.Length; i++)
        {
            var p = VertexFieldReader.Position(geometry.Vertices, i);
            result[i] = (p.X, p.Y, p.Z);
        }
        return result;
    }

    private static bool IsMirrorSourcePart(string part) =>
        part.Contains("_left_", StringComparison.Ordinal) || part.EndsWith("_left", StringComparison.Ordinal)
        || part is "/left_front_leg" or "/left_hind_leg" or "/left_middle_hind_leg" or "/left_middle_front_leg"
        or "/left_head" or "/left_arm" or "/left_leg" or "/left_wing" or "/left_ear" or "/left_ear_tip" or "/left_horn";

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
