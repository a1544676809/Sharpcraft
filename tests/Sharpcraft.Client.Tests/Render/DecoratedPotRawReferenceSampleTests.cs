// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-224: DecoratedPot raw bind-pose samples, keyed part/cube/polygon/vertex, 1e-5 + UV half.</summary>
public sealed class DecoratedPotRawReferenceSampleTests
{
    [Fact]
    public void BaseRawGeometryMatchesSource96()
    {
        var rows = Load("decorated-pot-base-raw-vectors.reference.txt");
        Assert.Equal(96, rows.Length);
        var geometry = Build(DecoratedPotModelDefinition.Base, 32,
            "entity/decorated_pot/decorated_pot_base");
        Assert.Equal(96, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        // Definition part order is neck (2 cubes), top, bottom; sample groups are bottom/neck/top.
        AssertRows(geometry, rows.Where(r => r.Part == "/neck").ToArray(), 0);
        AssertRows(geometry, rows.Where(r => r.Part == "/top").ToArray(), 48);
        AssertRows(geometry, rows.Where(r => r.Part == "/bottom").ToArray(), 72);
    }

    [Fact]
    public void SidesRawGeometryMatchesSource16()
    {
        var rows = Load("decorated-pot-sides-raw-vectors.reference.txt");
        Assert.Equal(16, rows.Length);
        var geometry = Build(DecoratedPotModelDefinition.Sides, 16,
            "entity/decorated_pot/decorated_pot_side");
        Assert.Equal(16, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        // Definition part order is back, left, right, front; sample groups are sorted by part path.
        AssertRows(geometry, rows.Where(r => r.Part == "/back").ToArray(), 0);
        AssertRows(geometry, rows.Where(r => r.Part == "/left").ToArray(), 4);
        AssertRows(geometry, rows.Where(r => r.Part == "/right").ToArray(), 8);
        AssertRows(geometry, rows.Where(r => r.Part == "/front").ToArray(), 12);
    }

    [Fact]
    public void AssembledPotKeepsSourceCountsAndFacing()
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ChestAtlas.DecoratedPotBaseTexture] = new(32, 32, new byte[32 * 32 * 4]),
            [ChestAtlas.DecoratedPotSideTexture] = new(16, 16, new byte[16 * 16 * 4])
        });
        var geometry = new DecoratedPotMesher().Build(
            [new(0f, 0f, 0f, Sharpcraft.Common.World.VanillaBlockEntityTypes.DecoratedPot, Facing: 2)], atlas);
        Assert.Equal(112, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(168, geometry.IndexCount);
    }

    private static DynamicGeometry Build(EntityModelDefinition definition, int textureSize, string texturePath)
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new(textureSize, textureSize, new byte[textureSize * textureSize * 4])
        });
        return new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get(texturePath)!,
            textureSize,
            textureSize,
            modelScale: 1f);
    }

    private static void AssertRows(DynamicGeometry geometry, Row[] expected, int startVertex)
    {
        Assert.True(geometry.VertexFloats / ChunkMesher.FloatsPerVertex >= startVertex + expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, startVertex + i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, startVertex + i);
            Assert.True(MathF.Abs(x - expected[i].X) <= 1e-5f, $"{expected[i].Part} vertex {i} x {x} vs {expected[i].X}");
            Assert.True(MathF.Abs(y - expected[i].Y) <= 1e-5f, $"{expected[i].Part} vertex {i} y {y} vs {expected[i].Y}");
            Assert.True(MathF.Abs(z - expected[i].Z) <= 1e-5f, $"{expected[i].Part} vertex {i} z {z} vs {expected[i].Z}");
            Assert.True(MathF.Abs(u - expected[i].U) <= 1e-5f, $"{expected[i].Part} vertex {i} u {u} vs {expected[i].U}");
            Assert.True(MathF.Abs(v - expected[i].V) <= 1e-5f, $"{expected[i].Part} vertex {i} v {v} vs {expected[i].V}");
        }
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
            rows.Add(new Row(f[1], f[2], f[3],
                float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[6], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[7], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[8], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return [.. rows];
    }

    private readonly record struct Row(string Part, string Cube, string Polygon, float X, float Y, float Z, float U, float V);
}
