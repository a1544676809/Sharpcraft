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
/// task-269: CreeperPowerLayer is EnergySwirlLayer geometry with a per-frame texture offset. The geometry stays
/// static; only the half-UV tile coordinates move by Creeper.xOffset(t) and t * 0.01.
/// </summary>
public sealed class CreeperPowerUvTests
{
    private static readonly string SamplePath = Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", "creeper-energy-swirl-uv.reference.txt");

    [Fact]
    public void ProductionPowerLayerUvsMatchEverySourceTick()
    {
        var sample = File.ReadAllLines(SamplePath)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (
                T: float.Parse(parts[0], CultureInfo.InvariantCulture),
                U: float.Parse(parts[1], CultureInfo.InvariantCulture),
                V: float.Parse(parts[2], CultureInfo.InvariantCulture)))
            .ToArray();
        Assert.Equal(4, sample.Length);

        var baseline = ReadGeometry(0f, static (geometry, vertex) => VertexFieldReader.Position(geometry.Vertices, vertex));
        var baselineUvs = ReadGeometry(0f, static (geometry, vertex) => VertexFieldReader.Uv(geometry.Vertices, vertex));
        Assert.True(baselineUvs.Count > 0);
        Assert.Equal(-0.5f, EnergySwirlUv.Creeper(-50f).U, 5); // Java remainder keeps the dividend's sign.
        Assert.Equal(-0.5f, EnergySwirlUv.Creeper(-50f).V, 5);

        foreach (var (t, expectedU, expectedV) in sample)
        {
            var actualUvs = ReadGeometry(t, static (geometry, vertex) => VertexFieldReader.Uv(geometry.Vertices, vertex));
            var positions = ReadGeometry(t, static (geometry, vertex) => VertexFieldReader.Position(geometry.Vertices, vertex));
            Assert.Equal(baselineUvs.Count, actualUvs.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                Assert.Equal(baseline[i], positions[i]);
            }

            Assert.Equal(expectedU, EnergySwirlUv.Creeper(t).U, 5);
            Assert.Equal(expectedV, EnergySwirlUv.Creeper(t).V, 5);
            for (var i = 0; i < actualUvs.Count; i++)
            {
                Assert.Equal((float)(Half)(baselineUvs[i].U + expectedU), actualUvs[i].U, 3);
                Assert.Equal((float)(Half)(baselineUvs[i].V + expectedV), actualUvs[i].V, 3);
            }
        }
    }

    private static List<T> ReadGeometry<T>(float ageInTicks, Func<DynamicGeometry, int, T> read)
    {
        var state = new EntityRenderState(
            1, CreeperModelDefinition.CreeperEntityTypeId, 0f, 0f, 0f, 0f, 0f,
            IsPowered: true,
            AgeInTicks: ageInTicks);
        var layers = new EntityModelMesher().BuildLayers(
            [state],
            new SpriteRect(0, 0, 64, 64, 1),
            64,
            64);
        var power = layers.SingleOrDefault(layer => layer.Name == "creeper_power");
        Assert.True(power is not null, string.Join(",", layers.Select(layer => layer.Name)));
        var geometry = power!.Geometry;
        var values = new List<T>();
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            values.Add(read(geometry, vertex));
        }

        return values;
    }
}
