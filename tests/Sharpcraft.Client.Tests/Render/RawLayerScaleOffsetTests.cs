// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Reflection;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Live-path layer-scale regression for raw source-frame models (#141/R-034). The source layer bake applies
/// MeshTransformer.scaling(factor) to the root pose: yOffset = 24.016*(1-factor) pixels before the entity flip.
/// The reference sample is model-space (y-down); the live path flips y and applies the 24 px baseline, so
/// expected world = (-x, 24/16 - y, z) at modelScale 1.
/// </summary>
public sealed class RawLayerScaleOffsetTests
{
    private const float BaselineBlocks = 24f / 16f;
    private const float LayerPivotPx = 24.016f;

    [Fact]
    public void HappyGhastLayerScaleLiveWorldMatchesEverySourceVertex()
    {
        CheckFull("happy-ghast-raw-vectors.reference.txt", "HAPPY_GHAST", typeof(HappyGhastModelDefinition), "HappyGhastMesh", 4f);
    }

    [Fact]
    public void HappyGhastProductionDefinitionLiveWorldMatchesSourceVertices()
    {
        // task-104: exercise the REAL production definition through the living path (no isolated test def),
        // so a production definition missing raw flags/Scale cannot pass via a reconstructed twin. The
        // layer-scale root offset (24.016*(1-4) px) must be applied on this path too.
        var definition = HappyGhastModelDefinition.Definition;
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);
        Assert.Equal(4f, definition.Scale);

        var expected = LoadRows("happy-ghast-raw-vectors.reference.txt", "HAPPY_GHAST")
            .Select(row => new Vector3(
                -float.Parse(row[5], CultureInfo.InvariantCulture),
                BaselineBlocks - float.Parse(row[6], CultureInfo.InvariantCulture),
                float.Parse(row[7], CultureInfo.InvariantCulture)))
            .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).ToArray();
        var geometry = BuildLive(definition, 64, 64, "entity/ghast/happy_ghast");
        AssertFull(geometry, expected, "HAPPY_GHAST/production-live");
    }

    [Fact]
    public void HumanoidHatsLayerScaleLiveRootVertexMatchesSource()
    {
        // The hat-rim geometry is a separate known DSL gap (#134/R-033); this regression pins the layer-scale
        // root offset/sign at the first source vertex, not the unrelated hat shape.
        CheckFirst("villager-raw-vectors.reference.txt", "VILLAGER", typeof(VillagerModelDefinition), "VillagerMesh", 0.9375f);
        CheckFirst("pillager-raw-vectors.reference.txt", "PILLAGER", typeof(IllagerModelDefinition), "IllagerMesh", 0.9375f);
    }

    [Fact]
    public void CaveSpiderLayerScaleLiveRootVertexMatchesTheSpiderSourceSample()
    {
        // Full CaveSpider geometry still carries the known raw-mirror gap (R-027); this pins the layer-scale
        // root offset/sign using the first non-mirrored source vertex.
        var row = LoadRows("spider-raw-vectors.reference.txt", "SPIDER")[0];
        var source = new Vector3(
            float.Parse(row[5], CultureInfo.InvariantCulture),
            float.Parse(row[6], CultureInfo.InvariantCulture),
            float.Parse(row[7], CultureInfo.InvariantCulture));
        var expected = new Vector3(
            -source.X * 0.7f,
            BaselineBlocks - (LayerPivotPx * (1f - 0.7f) / 16f + source.Y * 0.7f),
            source.Z * 0.7f);

        var geometry = BuildLive(CaveSpiderModelDefinition.Definition, 64, 32, "entity/spider/cave_spider");
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.True(
            MathF.Abs(expected.X - actual.X) < 1e-5f
                && MathF.Abs(expected.Y - actual.Y) < 1e-5f
                && MathF.Abs(expected.Z - actual.Z) < 1e-5f,
            $"CAVE_SPIDER scale=0.7 first vertex: expected=({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                + $" actual=({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
    }

    [Fact]
    public void BuildModelDoesNotApplyTheLivingOffsetTwice()
    {
        var mesh = GetMesh(typeof(HappyGhastModelDefinition), "HappyGhastMesh");
        var definition = RawDefinition("HAPPY_GHAST", mesh, 4f, "probe/happy_ghast");
        var rows = LoadRows("happy-ghast-raw-vectors.reference.txt", "HAPPY_GHAST");
        var expected = rows.Select(row => new Vector3(
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture),
                float.Parse(row[7], CultureInfo.InvariantCulture)))
            .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).ToArray();
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            Atlas(definition.TexturePath!, 64, 64).Get(definition.TexturePath!)!,
            64, 64, modelScale: 1f);
        AssertFull(geometry, expected, "HAPPY_GHAST/BuildModel");
    }

    private static void CheckFull(string sampleFile, string layer, Type definitionType, string meshField, float scale)
    {
        var rows = LoadRows(sampleFile, layer);
        var expected = rows.Select(row => new Vector3(
                -float.Parse(row[5], CultureInfo.InvariantCulture),
                BaselineBlocks - float.Parse(row[6], CultureInfo.InvariantCulture),
                float.Parse(row[7], CultureInfo.InvariantCulture)))
            .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).ToArray();
        var mesh = GetMesh(definitionType, meshField);
        var definition = RawDefinition(layer, mesh, scale, "probe/" + layer.ToLowerInvariant());
        var geometry = BuildLive(definition, 64, 64, definition.TexturePath!);
        AssertFull(geometry, expected, layer);
    }

    private static void CheckFirst(string sampleFile, string layer, Type definitionType, string meshField, float scale)
    {
        var row = LoadRows(sampleFile, layer)[0];
        var source = new Vector3(
            float.Parse(row[5], CultureInfo.InvariantCulture),
            float.Parse(row[6], CultureInfo.InvariantCulture),
            float.Parse(row[7], CultureInfo.InvariantCulture));
        var expected = new Vector3(-source.X, BaselineBlocks - source.Y, source.Z);
        var mesh = GetMesh(definitionType, meshField);
        var definition = RawDefinition(layer, mesh, scale, "probe/" + layer.ToLowerInvariant());
        var geometry = BuildLive(definition, 64, 64, definition.TexturePath!);
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.True(
            MathF.Abs(expected.X - actual.X) < 1e-5f
                && MathF.Abs(expected.Y - actual.Y) < 1e-5f
                && MathF.Abs(expected.Z - actual.Z) < 1e-5f,
            $"{layer} scale={scale} first vertex: expected=({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                + $" actual=({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
    }

    private static DynamicGeometry BuildLive(EntityModelDefinition definition, int width, int height, string texturePath)
    {
        var atlas = Atlas(texturePath, width, height);
        return new EntityModelMesher().BuildInto(
            new DynamicGeometry(), null,
            [new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f)],
            new SpriteRect(0, 0, width, height, 1), width, height,
            skinAtlas: atlas,
            modelOverride: definition,
            modelScale: 1f);
    }

    private static void AssertFull(DynamicGeometry geometry, Vector3[] expected, string label)
    {
        var actual = Enumerable.Range(0, geometry.VertexFloats / ChunkMesher.FloatsPerVertex)
            .Select(i => VertexFieldReader.Position(geometry.Vertices, i))
            .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z)
            .ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                MathF.Abs(expected[i].X - actual[i].X) < 1e-5f
                    && MathF.Abs(expected[i].Y - actual[i].Y) < 1e-5f
                    && MathF.Abs(expected[i].Z - actual[i].Z) < 1e-5f,
                $"{label} vertex {i}: expected=({expected[i].X:F6},{expected[i].Y:F6},{expected[i].Z:F6})"
                    + $" actual=({actual[i].X:F6},{actual[i].Y:F6},{actual[i].Z:F6})");
        }
    }

    private static EntityModelDefinition RawDefinition(string name, VanillaMeshDefinition mesh, float scale, string texturePath) =>
        EntityModelDefinition.FromTree(name, 64, 64, mesh, texturePath: texturePath, scale: scale, baselinePx: 0f, rawSourceFrame: true);

    private static VanillaMeshDefinition GetMesh(Type type, string field) =>
        (VanillaMeshDefinition)type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static string[][] LoadRows(string file, string layer) =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", file))
            .Where(line => line.StartsWith(layer + "\t", StringComparison.Ordinal))
            .Select(line => line.Split('\t'))
            .ToArray();

    private static ItemAtlas Atlas(string texturePath, int width, int height) =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new TextureData(width, height, new byte[width * height * 4])
        });
}
