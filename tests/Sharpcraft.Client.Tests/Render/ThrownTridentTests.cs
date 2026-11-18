// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// task-274: thrown trident (EntityTypes.TRIDENT = 139) raw base model plus entity-scale glint. The source renderer
/// applies rotateY(yRot - 90) then rotateZ(xRot + 90); the raw model sample supplies geometry and UVs.
/// </summary>
public sealed class ThrownTridentTests
{
    private static readonly string SamplePath = Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", "special-trident-raw-vectors.reference.txt");

    [Fact]
    public void RegistryTypeIdAndFoilMetadataAreWired()
    {
        var registryProbe = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "entity-type-registry.reference.txt");
        var registryRow = File.ReadAllLines(registryProbe).Single(line => line.StartsWith("minecraft:trident", StringComparison.Ordinal));
        var registryId = int.Parse(registryRow.Split('	')[1], CultureInfo.InvariantCulture);
        Assert.Equal(139, registryId);
        Assert.True(ThrownTridentModelDefinition.EntityTypeId == registryId);
        var tracker = new EntityTracker();
        Assert.True(tracker.Add(new AddedEntity(7, Guid.Empty, 139, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.True(tracker.ApplyEntityData(7, [new EntityDataItem(12, 8, 1)]));
        Assert.True(tracker.Interpolate(0f)[0].IsFoil);
    }

    [Fact]
    public void ProductionBaseAndGlintMatchTheSourceModel()
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SpecialItemModelDefinition.TridentTexture] = new(32, 32, new byte[32 * 32 * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4]),
        });
        var state = new EntityRenderState(
            1, ThrownTridentModelDefinition.EntityTypeId, 0.5f, 1.25f, -0.75f, 45f, 30f,
            IsFoil: true);
        try
        {
            GlintClock.SetFixedSourceMillis(55000);
            var glintDefinition = EntityModelResolver.Resolve(139).RenderLayers.Single(layer => layer.Name == "trident_glint");
            Assert.True(glintDefinition.VisibleWhen!(state));
            var layers = new EntityModelMesher().BuildLayers(
                [state], new SpriteRect(0, 0, 32, 32, 1), 32, 32, skinAtlas: atlas);
            var baseLayer = layers.Single(layer => layer.Name == "cutout");
            var glint = layers.Single(layer => layer.Name == "trident_glint");
            var sample = ReadSample();
            var basePositions = ReadPositions(baseLayer.Geometry);
            var baseUvs = ReadUvs(baseLayer.Geometry);
            Assert.Equal(sample.Count, basePositions.Count);
            Assert.Equal(sample.Count, baseUvs.Count);
            var keyToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var used = new bool[basePositions.Count];
            foreach (var vertex in sample)
            {
                var expectedPosition = SourceWorld(vertex.X, vertex.Y, vertex.Z, state);
                var index = -1;
                for (var candidate = 0; candidate < basePositions.Count; candidate++)
                {
                    if (used[candidate]
                        || Vector3.Distance(expectedPosition, basePositions[candidate]) > 1e-5f
                        || MathF.Abs(vertex.U - baseUvs[candidate].U) > 0.001f
                        || MathF.Abs(vertex.V - baseUvs[candidate].V) > 0.001f)
                    {
                        continue;
                    }

                    index = candidate;
                    break;
                }

                Assert.True(index >= 0, $"no base vertex for key {vertex.Key}");
                used[index] = true;
                keyToIndex.Add(vertex.Key, index);
            }

            Assert.Equal(sample.Count, keyToIndex.Count);
            var glintPositions = ReadPositions(glint.Geometry);
            var glintUvs = ReadUvs(glint.Geometry);
            Assert.Equal(basePositions.Count, glintPositions.Count);
            foreach (var vertex in sample)
            {
                var index = keyToIndex[vertex.Key];
                Assert.True(Vector3.Distance(basePositions[index], glintPositions[index]) <= 1e-5f,
                    $"position mismatch for key {vertex.Key}");
                var expectedUv = GlintTextureTransform.Apply(
                    vertex.U, vertex.V, 55000, GlintTextureTransform.EntityScale);
                Assert.Equal((float)(Half)expectedUv.U, glintUvs[index].U, 3);
                Assert.Equal((float)(Half)expectedUv.V, glintUvs[index].V, 3);
            }

            var plain = state with { IsFoil = false };
            var plainLayers = new EntityModelMesher().BuildLayers(
                [plain], new SpriteRect(0, 0, 32, 32, 1), 32, 32, skinAtlas: atlas);
            Assert.DoesNotContain(plainLayers, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal));
        }
        finally
        {
            GlintClock.SetProvider(null);
        }
    }

    [Fact]
    public void ModelGeometryMatchesSampleKeysInOrder()
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SpecialItemModelDefinition.TridentTexture] = new(32, 32, new byte[32 * 32 * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4]),
        });
        var state = new EntityRenderState(1, ThrownTridentModelDefinition.EntityTypeId, 0.5f, 1.25f, -0.75f, 45f, 30f);
        var sample = ReadSample();
        foreach (var cube in sample.GroupBy(vertex => vertex.Part.Split('/').Last()))
        {
            var rows = cube.ToArray();
            var geometry = new EntityModelMesher().BuildInto(
                new DynamicGeometry(), null, [state], new SpriteRect(0, 0, 32, 32, 1), 32, 32,
                skinAtlas: atlas, onlyPart: cube.Key, modelOverride: ThrownTridentModelDefinition.Definition);
            var actualPositions = ReadPositions(geometry);
            var actualUvs = ReadUvs(geometry);
            Assert.Equal(rows.Length, actualPositions.Count);
            for (var i = 0; i < rows.Length; i++)
            {
                var expected = SourceWorld(rows[i].X, rows[i].Y, rows[i].Z, state);
                Assert.True(Vector3.Distance(expected, actualPositions[i]) <= 1e-5f,
                    $"cube={cube.Key} key={rows[i].Key} expected={expected} actual={actualPositions[i]}");
                Assert.True(MathF.Abs(rows[i].U - actualUvs[i].U) <= 0.001f, $"U cube={cube.Key} key={rows[i].Key}");
                Assert.True(MathF.Abs(rows[i].V - actualUvs[i].V) <= 0.001f, $"V cube={cube.Key} key={rows[i].Key}");
            }
        }
    }
    private static Vector3 SourceWorld(float x, float y, float z, EntityRenderState state)
    {
        var degrees = MathF.PI / 180f;
        var rotation = Matrix4x4.CreateRotationZ((state.Pitch + 90f) * degrees)
            * Matrix4x4.CreateRotationY((state.Yaw - 90f) * degrees);
        // The production entity mesher applies its established model pixel scale before the entity transform.
        return Vector3.Transform(new Vector3(x, y, z) * EntityPlaceholderMesher.ModelScale, rotation)
            + new Vector3(state.X, state.Y, state.Z);
    }

    private sealed record SampleVertex(string Key, string Part, float X, float Y, float Z, float U, float V);

    private static List<SampleVertex> ReadSample() =>
        File.ReadAllLines(SamplePath)
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length >= 10)
            .Select(parts => new SampleVertex(
                string.Join('|', parts[0], parts[1], parts[2], parts[3], parts[4]),
                parts[1],
                float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture),
                float.Parse(parts[7], CultureInfo.InvariantCulture),
                float.Parse(parts[8], CultureInfo.InvariantCulture),
                float.Parse(parts[9], CultureInfo.InvariantCulture)))
            .ToList();

    private static List<Vector3> ReadPositions(DynamicGeometry geometry)
    {
        var result = new List<Vector3>();
        for (var i = 0; i < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            result.Add(new Vector3(x, y, z));
        }

        return result;
    }

    private static List<(float U, float V)> ReadUvs(DynamicGeometry geometry)
    {
        var result = new List<(float U, float V)>();
        for (var i = 0; i < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
        {
            result.Add(VertexFieldReader.Uv(geometry.Vertices, i));
        }

        return result;
    }
}
