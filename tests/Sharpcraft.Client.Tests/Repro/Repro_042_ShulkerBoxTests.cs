// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 42: a shulker box is not a chest. The reference draws it from its own two box model, and
/// these are the vertices that model produces at three opening amounts.
/// </summary>
public sealed class Repro_042_ShulkerBoxTests
{
    /// <summary>The reference's model-to-world matrix for an upward facing box, row major.</summary>
    private static readonly float[] UpMatrix =
    [
        0.9995f, 0f, 0f, 0f,
        0f, -0.9995f, 0f, 0f,
        0f, 0f, -0.9995f, 0f,
        0.5f, 1.4995f, 0.5f, 1f
    ];

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void TheModelMatchesTheReferenceVertices(float progress)
    {
        var geometry = Build(progress);
        var actual = Vertices(geometry);
        var expected = GoldenVertices(progress)
            .Select(vertex => Transform(UpMatrix, vertex))
            .Select(Round)
            .OrderBy(vertex => vertex, Comparer<(float X, float Y, float Z)>.Create(Compare))
            .ToArray();

        Assert.Equal(48, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AnUpwardBoxStaysWithinOneBlock()
    {
        var vertices = Vertices(Build(0f));

        Assert.InRange(vertices.Min(v => v.X), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.X), 0.999f, 1.001f);
        Assert.InRange(vertices.Min(v => v.Y), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.Y), 0.999f, 1.001f);
        Assert.InRange(vertices.Min(v => v.Z), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.Z), 0.999f, 1.001f);
    }

    [Fact]
    public void ABoxIsNotDrawnAsAChest()
    {
        // The chest model reaches 14 of 16 pixels and carries a latch; the shulker box is a full
        // block wide and has no latch, so its vertex count differs from a chest's.
        var geometry = Build(0f);

        Assert.Equal(2 * 24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
    }

    private static DynamicGeometry Build(float progress)
    {
        var mesher = new ShulkerBoxMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.ShulkerBox, Open: progress, Facing: 1)
        ];

        return mesher.Build(entities, new SpriteRect(0, 0, 64, 64, 1));
    }

    private static (float X, float Y, float Z)[] Vertices(DynamicGeometry geometry)
    {
        var result = new (float X, float Y, float Z)[geometry.VertexFloats / ChunkMesher.FloatsPerVertex];
        for (var i = 0; i < result.Length; i++)
        {
            var at = i * ChunkMesher.FloatsPerVertex;
            result[i] = Round((geometry.Vertices[at], geometry.Vertices[at + 1], geometry.Vertices[at + 2]));
        }

        return result.OrderBy(vertex => vertex, Comparer<(float X, float Y, float Z)>.Create(Compare)).ToArray();
    }

    private static IEnumerable<(float X, float Y, float Z)> GoldenVertices(float progress)
    {
        var key = "shulker|" + progress.ToString("F6", CultureInfo.InvariantCulture);
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "shulker-box-vectors.golden.txt")))
        {
            if (!line.StartsWith(key + "|", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            yield return (
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture));
        }
    }

    private static (float X, float Y, float Z) Transform(float[] m, (float X, float Y, float Z) v) =>
        (
            (m[0] * v.X) + (m[1] * v.Y) + (m[2] * v.Z) + m[12],
            (m[4] * v.X) + (m[5] * v.Y) + (m[6] * v.Z) + m[13],
            (m[8] * v.X) + (m[9] * v.Y) + (m[10] * v.Z) + m[14]);

    private static (float X, float Y, float Z) Round((float X, float Y, float Z) value) =>
        (MathF.Round(value.X, 3), MathF.Round(value.Y, 3), MathF.Round(value.Z, 3));

    private static int Compare((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
        a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z);
}
