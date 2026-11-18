// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// task-270: glint TextureTransform. The CPU builds the reference setupGlintTexturing matrix once per frame for
/// glint layers; tests inject a fixed source millis and verify the production layer UVs, not just the helper.
/// </summary>
public sealed class GlintTextureTransformTests
{
    private static readonly string SamplePath = Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", "glint-texture-transform.reference.txt");

    [Fact]
    public void SourceMatrixSamplesMatchForEveryGlintScale()
    {
        var sample = ReadSample();
        Assert.Equal(12, sample.Length);
        foreach (var row in sample)
        {
            var (u, v) = GlintTextureTransform.Apply(row.InU, row.InV, row.Millis, row.Scale);
            Assert.Equal(row.OutU, u, 5);
            Assert.Equal(row.OutV, v, 5);
        }

        // Scales stay distinct: the same millis and UV must not produce the same result for item/entity/armor.
        Assert.NotEqual(
            GlintTextureTransform.Apply(0.25f, 0.75f, 55000, GlintTextureTransform.ItemScale),
            GlintTextureTransform.Apply(0.25f, 0.75f, 55000, GlintTextureTransform.EntityScale));
        Assert.NotEqual(
            GlintTextureTransform.Apply(0.25f, 0.75f, 55000, GlintTextureTransform.EntityScale),
            GlintTextureTransform.Apply(0.25f, 0.75f, 55000, GlintTextureTransform.ArmorScale));
    }

    [Fact]
    public void ProductionArmorGlintLayerUsesTheSourceMatrix()
    {
        var helmet = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:iron_helmet"));
        Assert.True(helmet >= 0);
        var glints = new bool?[8];
        glints[5] = true;
        var state = new EntityRenderState(
            1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, -1, helmet, -1, -1),
            EquipmentGlintOverrides: glints);
        var model = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var glintLayer = model.RenderLayers!.Single(layer => layer.Name == "glint_HumanoidArmor_5");
        var glintModel = glintLayer.Model!;
        var texture = new SpriteRect(0, 0, 64, 64, 1);
        var baseGeometry = new EntityModelMesher().BuildModel(glintModel, state, texture, 64, 64, modelScale: 1f);
        var baseUvs = ReadUvs(baseGeometry);
        List<(float X, float Y, float Z)>? firstPositions = null;

        try
        {
            foreach (var row in ReadSample().Where(row => row.Scale == GlintTextureTransform.ArmorScale))
            {
                GlintClock.SetProvider(() => row.Millis);
                var layers = new EntityModelMesher().BuildLayers(
                    [state], texture, 64, 64, variants: EntityVariantTable.Builtin);
                var built = layers.Single(layer => layer.Name == "glint_HumanoidArmor_5");
                var actualUvs = ReadUvs(built.Geometry);
                var positions = ReadPositions(built.Geometry);
                Assert.Equal(baseUvs.Count, actualUvs.Count);
                firstPositions ??= positions;
                Assert.Equal(firstPositions, positions);
                for (var i = 0; i < baseUvs.Count; i++)
                {
                    var expected = GlintTextureTransform.Apply(baseUvs[i].U, baseUvs[i].V, row.Millis, row.Scale);
                    Assert.Equal((float)(Half)expected.U, actualUvs[i].U, 3);
                    Assert.Equal((float)(Half)expected.V, actualUvs[i].V, 3);
                }
            }
        }
        finally
        {
            GlintClock.SetProvider(null);
        }
    }

    private static List<(float U, float V)> ReadUvs(DynamicGeometry geometry)
    {
        var values = new List<(float U, float V)>();
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            values.Add(VertexFieldReader.Uv(geometry.Vertices, vertex));
        }

        return values;
    }

    private static List<(float X, float Y, float Z)> ReadPositions(DynamicGeometry geometry)
    {
        var values = new List<(float X, float Y, float Z)>();
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            values.Add(VertexFieldReader.Position(geometry.Vertices, vertex));
        }

        return values;
    }

    private static (long Millis, float Scale, float InU, float InV, float OutU, float OutV)[] ReadSample() =>
        File.ReadAllLines(SamplePath)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (
                Millis: long.Parse(parts[0], CultureInfo.InvariantCulture),
                Scale: float.Parse(parts[1], CultureInfo.InvariantCulture),
                InU: float.Parse(parts[2], CultureInfo.InvariantCulture),
                InV: float.Parse(parts[3], CultureInfo.InvariantCulture),
                OutU: float.Parse(parts[4], CultureInfo.InvariantCulture),
                OutV: float.Parse(parts[5], CultureInfo.InvariantCulture)))
            .ToArray();
}
