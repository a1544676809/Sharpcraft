// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-236: adult player armor raw bind pose, keyed per slot/source sample, 1e-5 + UV half.</summary>
public sealed class PlayerArmorRawReferenceSampleTests
{
    [Theory]
    [InlineData("player-armor-helmet-raw-vectors.reference.txt", "helmet")]
    [InlineData("player-armor-chestplate-raw-vectors.reference.txt", "chestplate")]
    [InlineData("player-armor-leggings-raw-vectors.reference.txt", "leggings")]
    [InlineData("player-armor-boots-raw-vectors.reference.txt", "boots")]
    [InlineData("zombie-armor-helmet-raw-vectors.reference.txt", "helmet")]
    [InlineData("zombie-armor-chestplate-raw-vectors.reference.txt", "chestplate")]
    [InlineData("zombie-armor-leggings-raw-vectors.reference.txt", "leggings")]
    [InlineData("zombie-armor-boots-raw-vectors.reference.txt", "boots")]
    [InlineData("skeleton-armor-helmet-raw-vectors.reference.txt", "helmet")]
    [InlineData("skeleton-armor-chestplate-raw-vectors.reference.txt", "chestplate")]
    [InlineData("skeleton-armor-leggings-raw-vectors.reference.txt", "leggings")]
    [InlineData("skeleton-armor-boots-raw-vectors.reference.txt", "boots")]
    public void AdultPlayerArmorRawGeometryMatchesSource(string sampleFile, string slot)
    {
        var rows = Load(sampleFile);
        var definition = slot switch
        {
            "helmet" => PlayerArmorModelDefinition.Helmet,
            "chestplate" => PlayerArmorModelDefinition.Chestplate,
            "leggings" => PlayerArmorModelDefinition.Leggings,
            _ => PlayerArmorModelDefinition.Boots
        };
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid/iron")!,
            64,
            32,
            modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{slot} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{slot} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{slot} {row.Part} v{i} z {z} vs {row.Z}");
            Assert.True(MathF.Abs(u - row.U) <= 1e-5f, $"{slot} {row.Part} v{i} u {u} vs {row.U}");
            Assert.True(MathF.Abs(v - row.V) <= 1e-5f, $"{slot} {row.Part} v{i} v {v} vs {row.V}");
        }
    }

    [Fact]
    public void PlayerArmorProductionUsesRawLayersWhileOtherHumanoidArmorStaysLegacy()
    {
        var player = EntityModelResolver.Resolve(Sharpcraft.Client.Render.EntityPlaceholderMesher.PlayerEntityTypeId);
        var expected = new (string Name, int Vertices)[]
        {
            ("equipment_HumanoidArmor_5", 48),
            ("equipment_HumanoidArmor_4", 72),
            ("equipment_HumanoidLeggings_3", 72),
            ("equipment_HumanoidArmor_2", 48)
        };
        foreach (var (name, vertices) in expected)
        {
            var layer = player.RenderLayers!.Single(layer => layer.Name == name);
            Assert.NotNull(layer.Model);
            Assert.True(layer.Model.RawSourceFrame, name);
            Assert.Equal(0f, layer.Model.BaselinePx);
            Assert.Equal(0f, layer.Model.SourceFrameBaselinePx);
            Assert.True(layer.Model.FaceTransforms is null || layer.Model.FaceTransforms.Count == 0);
            Assert.Equal(vertices, layer.Model.Compiled!.Cubes.Sum(cube => 24));
        }

        // Adult Zombie/Skeleton share the same source humanoid armor meshes, so they reuse the raw
        // definitions without a per-mob copy.
        foreach (var typeId in new[]
                 {
                     Sharpcraft.Client.Render.HumanoidMobModelDefinition.ZombieEntityTypeId,
                     Sharpcraft.Client.Render.HumanoidMobModelDefinition.SkeletonEntityTypeId
                 })
        {
            var humanoid = EntityModelResolver.Resolve(typeId);
            var armor = humanoid.RenderLayers!.First(layer => layer.Name == "equipment_HumanoidArmor_5").Model!;
            Assert.True(armor.RawSourceFrame);
            Assert.Equal(0f, armor.BaselinePx);
            Assert.Equal(48, armor.Compiled!.Cubes.Sum(cube => 24));
        }

        // Stray armor is source-identical to player armor (B-3 / #228) and reuses the raw definition.
        var stray = EntityModelResolver.Resolve(Sharpcraft.Client.Render.HumanoidMobModelDefinition.StrayEntityTypeId);
        var strayArmor = stray.RenderLayers!.First(layer => layer.Name == "equipment_HumanoidArmor_5").Model!;
        Assert.True(strayArmor.RawSourceFrame);
        Assert.Equal(0f, strayArmor.BaselinePx);
        Assert.True(strayArmor.FaceTransforms is null || strayArmor.FaceTransforms.Count == 0);
    }

    private static Row[] Load(string sampleFile)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", sampleFile);
        Assert.True(File.Exists(path), $"missing reference data: {path}");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var f = line.Split('\t');
            rows.Add(new Row(f[1], f[2], f[3], f[4],
                float.Parse(f[5], CultureInfo.InvariantCulture),
                float.Parse(f[6], CultureInfo.InvariantCulture),
                float.Parse(f[7], CultureInfo.InvariantCulture),
                float.Parse(f[8], CultureInfo.InvariantCulture),
                float.Parse(f[9], CultureInfo.InvariantCulture)));
        }

        return [.. rows];
    }

    private readonly record struct Row(string Part, string Cube, string Polygon, string Vertex,
        float X, float Y, float Z, float U, float V);
}
