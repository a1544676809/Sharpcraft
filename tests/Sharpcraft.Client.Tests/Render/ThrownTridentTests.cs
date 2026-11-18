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
        Assert.Equal(139, ThrownTridentModelDefinition.EntityTypeId);
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
            var expected = sample
                .Select(vertex => SourceWorld(vertex.X, vertex.Y, vertex.Z, state))
                .ToArray();
            var actualPositions = ReadPositions(baseLayer.Geometry);
            Assert.Equal(expected.Length, actualPositions.Count);
            AssertVertexSet(expected, actualPositions);
            var baseUvs = ReadUvs(baseLayer.Geometry);
            Assert.Equal(sample.Count, baseUvs.Count);
            var sampleUvs = sample.Select(v => (v.U, v.V)).OrderBy(v => v.U).ThenBy(v => v.V).ToArray();
            var actualUvs = baseUvs.OrderBy(v => v.U).ThenBy(v => v.V).ToArray();
            for (var i = 0; i < sampleUvs.Length; i++)
            {
                Assert.True(MathF.Abs(sampleUvs[i].U - actualUvs[i].U) <= 0.001f, $"U i={i} sample={sampleUvs[i].U} actual={actualUvs[i].U}");
                Assert.True(MathF.Abs(sampleUvs[i].V - actualUvs[i].V) <= 0.001f, $"V i={i} sample={sampleUvs[i].V} actual={actualUvs[i].V}");
            }
            AssertVertexSet(ReadPositions(baseLayer.Geometry), ReadPositions(glint.Geometry));
            var glintUvs = ReadUvs(glint.Geometry);
            for (var i = 0; i < glintUvs.Count; i++)
            {
                var (u, v) = VertexFieldReader.Uv(baseLayer.Geometry.Vertices, i);
                var expectedUv = GlintTextureTransform.Apply(u, v, 55000, GlintTextureTransform.EntityScale);
                Assert.Equal((float)(Half)expectedUv.U, glintUvs[i].U, 3);
                Assert.Equal((float)(Half)expectedUv.V, glintUvs[i].V, 3);
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

    private static Vector3 SourceWorld(float x, float y, float z, EntityRenderState state)
    {
        var degrees = MathF.PI / 180f;
        var rotation = Matrix4x4.CreateRotationZ((state.Pitch + 90f) * degrees)
            * Matrix4x4.CreateRotationY((state.Yaw - 90f) * degrees);
        // The production entity mesher applies its established model pixel scale before the entity transform.
        return Vector3.Transform(new Vector3(x, y, z) * EntityPlaceholderMesher.ModelScale, rotation)
            + new Vector3(state.X, state.Y, state.Z);
    }

    private static void AssertVertexSet(IReadOnlyList<Vector3> expected, IReadOnlyList<Vector3> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var unmatched = actual.ToList();
        foreach (var point in expected)
        {
            var index = unmatched.FindIndex(candidate => Vector3.Distance(candidate, point) <= 1e-5f);
            Assert.True(index >= 0, $"missing expected vertex {point} actual0={actual[0]} actual1={actual[1]}");
            unmatched.RemoveAt(index);
        }
    }

    private static List<(float X, float Y, float Z, float U, float V)> ReadSample() =>
        File.ReadAllLines(SamplePath)
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length >= 10)
            .Select(parts => (
                X: float.Parse(parts[5], CultureInfo.InvariantCulture),
                Y: float.Parse(parts[6], CultureInfo.InvariantCulture),
                Z: float.Parse(parts[7], CultureInfo.InvariantCulture),
                U: float.Parse(parts[8], CultureInfo.InvariantCulture),
                V: float.Parse(parts[9], CultureInfo.InvariantCulture)))
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
