// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-252: horse/nautilus/armor-stand armor raw layers.</summary>
public sealed class MountArmorRawReferenceSampleTests
{
    [Theory]
    [InlineData("horse-armor-raw-vectors.reference.txt", "horse", 5, 64, 64)]
    [InlineData("undead-horse-armor-raw-vectors.reference.txt", "undead", 5, 64, 64)]
    [InlineData("armor-stand-armor-helmet-raw-vectors.reference.txt", "stand", 5, 64, 32)]
    [InlineData("armor-stand-armor-chestplate-raw-vectors.reference.txt", "stand", 4, 64, 32)]
    [InlineData("armor-stand-armor-leggings-raw-vectors.reference.txt", "stand", 3, 64, 32)]
    [InlineData("armor-stand-armor-boots-raw-vectors.reference.txt", "stand", 2, 64, 32)]
    [InlineData("armor-stand-small-armor-helmet-raw-vectors.reference.txt", "small", 5, 64, 32)]
    [InlineData("armor-stand-small-armor-chestplate-raw-vectors.reference.txt", "small", 4, 64, 32)]
    [InlineData("armor-stand-small-armor-leggings-raw-vectors.reference.txt", "small", 3, 64, 32)]
    [InlineData("armor-stand-small-armor-boots-raw-vectors.reference.txt", "small", 2, 64, 32)]
    [InlineData("nautilus-armor-raw-vectors.reference.txt", "nautilus", 5, 128, 128)]
    public void MountArmorRawGeometryMatchesSource(string sampleFile, string mode, int slot, int textureWidth, int textureHeight)
    {
        var rows = Load(sampleFile);
        var definition = mode switch
        {
            "horse" => HorseArmorModelDefinition.Living,
            "undead" => HorseArmorModelDefinition.Undead,
            "stand" => ArmorStandArmorModelDefinition.ForSlot(slot, small: false),
            "small" => ArmorStandArmorModelDefinition.ForSlot(slot, small: true),
            _ => NautilusArmorModelDefinition.Definition
        };
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(textureWidth, textureHeight, new byte[textureWidth * textureHeight * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid/iron")!, textureWidth, textureHeight, modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            var expectedU = QuantizeHalf(row.U);
            var expectedV = QuantizeHalf(row.V);
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{sampleFile} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{sampleFile} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{sampleFile} {row.Part} v{i} z {z} vs {row.Z}");
            Assert.True(MathF.Abs(u - expectedU) <= 1e-5f, $"{sampleFile} {row.Part} v{i} u {u} vs {expectedU}");
            Assert.True(MathF.Abs(v - expectedV) <= 1e-5f, $"{sampleFile} {row.Part} v{i} v {v} vs {expectedV}");
        }
    }

    [Fact]
    public void ProductionHorseAndNautilusArmorUseRawLayers()
    {
        var horse = EntityModelResolver.Resolve(HorseModelDefinition.HorseEntityTypeId)
            .RenderLayers!.Single(layer => layer.Name == "equipment_HorseBody_6").Model!;
        Assert.True(horse.RawSourceFrame);
        Assert.Equal(0f, horse.BaselinePx);
        Assert.Equal(288, horse.Compiled!.Cubes.Sum(cube => 24));

        foreach (var typeId in new[]
                 {
                     HorseModelDefinition.ZombieHorseEntityTypeId,
                     HorseModelDefinition.SkeletonHorseEntityTypeId
                 })
        {
            var undead = EntityModelResolver.Resolve(typeId)
                .RenderLayers!.Single(layer => layer.Name == "equipment_HorseBody_6").Model!;
            Assert.True(undead.RawSourceFrame, typeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0f, undead.BaselinePx);
            Assert.Equal(288, undead.Compiled!.Cubes.Sum(cube => 24));
        }

        foreach (var typeId in new[]
                 {
                     NautilusModelDefinition.NautilusEntityTypeId,
                     ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId
                 })
        {
            var nautilus = EntityModelResolver.Resolve(typeId)
                .RenderLayers!.Single(layer => layer.Name == "equipment_NautilusBody_6").Model!;
            Assert.True(nautilus.RawSourceFrame, typeId.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0f, nautilus.BaselinePx);
            Assert.Equal(192, nautilus.Compiled!.Cubes.Sum(cube => 24));
        }
    }

    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

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
