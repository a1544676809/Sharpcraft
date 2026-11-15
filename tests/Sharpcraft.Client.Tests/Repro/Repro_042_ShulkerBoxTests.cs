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
/// these are the vertices and texture coordinates that model produces at three opening amounts.
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
    public void TheModelMatchesTheReferenceVerticesAndUvs(float progress)
    {
        var actual = Vertices(Build(progress));
        var expected = GoldenVertices(progress)
            .Select(vertex => (Position: Round(Transform(UpMatrix, vertex.Position)), U: MathF.Round(vertex.U, 3), V: MathF.Round(vertex.V, 3)))
            .Select(vertex => (vertex.Position.X, vertex.Position.Y, vertex.Position.Z, vertex.U, vertex.V))
            .OrderBy(vertex => vertex, Comparer<(float, float, float, float, float)>.Create(Compare))
            .ToArray();

        Assert.Equal(48, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheSixFacesDoNotShareOneTextureRectangle()
    {
        var uvs = GoldenVertices(0f).Select(vertex => (vertex.U, vertex.V)).Distinct().Count();

        // A box-unwrapped model reads many distinct spots of its sheet; one shared rectangle per
        // face would collapse to a handful of values.
        Assert.True(uvs >= 20, $"expected at least 20 distinct texture coordinates, got {uvs}");
    }

    [Fact]
    public void AnUpwardBoxStaysWithinOneBlock()
    {
        var vertices = Vertices(Build(0f));

        Assert.InRange(vertices.Min(v => v.Item1), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.Item1), 0.999f, 1.001f);
        Assert.InRange(vertices.Min(v => v.Item2), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.Item2), 0.999f, 1.001f);
        Assert.InRange(vertices.Min(v => v.Item3), -0.001f, 0.001f);
        Assert.InRange(vertices.Max(v => v.Item3), 0.999f, 1.001f);
    }

    private static DynamicGeometry Build(float progress, byte facing = 1)
    {
        var mesher = new ShulkerBoxMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.ShulkerBox, Open: progress, Facing: facing)
        ];

        return mesher.Build(entities, new SpriteRect(0, 0, 64, 64, 1));
    }

    private static (float, float, float, float, float)[] Vertices(DynamicGeometry geometry)
    {
        var result = new (float, float, float, float, float)[geometry.VertexFloats / ChunkMesher.FloatsPerVertex];
        for (var i = 0; i < result.Length; i++)
        {
            var at = i * ChunkMesher.FloatsPerVertex;
            var position = Round((geometry.Vertices[at], geometry.Vertices[at + 1], geometry.Vertices[at + 2]));
            result[i] = (
                position.X,
                position.Y,
                position.Z,
                MathF.Round(UnpackHalf(geometry.Vertices[at + 4], low: true), 3),
                MathF.Round(UnpackHalf(geometry.Vertices[at + 4], low: false), 3));
        }

        return result.OrderBy(vertex => vertex, Comparer<(float, float, float, float, float)>.Create(Compare)).ToArray();
    }

    private static float UnpackHalf(float packed, bool low)
    {
        var bits = BitConverter.SingleToUInt32Bits(packed);
        return low
            ? (float)BitConverter.UInt16BitsToHalf((ushort)(bits & 0xFFFF))
            : (float)BitConverter.UInt16BitsToHalf((ushort)(bits >> 16));
    }

    private static IEnumerable<(Vector3 Position, float U, float V)> GoldenVertices(float progress)
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
                new Vector3(
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture),
                    float.Parse(parts[4], CultureInfo.InvariantCulture)),
                float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture));
        }
    }

    private static (float X, float Y, float Z) Transform(float[] m, Vector3 v) =>
        (
            (m[0] * v.X) + (m[1] * v.Y) + (m[2] * v.Z) + m[12],
            (m[4] * v.X) + (m[5] * v.Y) + (m[6] * v.Z) + m[13],
            (m[8] * v.X) + (m[9] * v.Y) + (m[10] * v.Z) + m[14]);

    private static (float X, float Y, float Z) Round((float X, float Y, float Z) value) =>
        (MathF.Round(value.X, 3), MathF.Round(value.Y, 3), MathF.Round(value.Z, 3));

    private static int Compare((float X, float Y, float Z, float U, float V) a, (float X, float Y, float Z, float U, float V) b) =>
        a.X != b.X ? a.X.CompareTo(b.X)
        : a.Y != b.Y ? a.Y.CompareTo(b.Y)
        : a.Z != b.Z ? a.Z.CompareTo(b.Z)
        : a.U != b.U ? a.U.CompareTo(b.U)
        : a.V.CompareTo(b.V);

    /// <summary>Two boxes in one span must not disturb each other's indices.</summary>
    [Fact]
    public void TwoBoxesKeepIndependentWinding()
    {
        var mesher = new ShulkerBoxMesher();
        BlockEntityRenderState[] two =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.ShulkerBox, Facing: 1),
            new(4f, 0f, 0f, VanillaBlockEntityTypes.ShulkerBox, Facing: 1)
        ];
        var combined = mesher.Build(two, new SpriteRect(0, 0, 64, 64, 1));

        var single = new ShulkerBoxMesher();
        var one = single.Build([two[0]], new SpriteRect(0, 0, 64, 64, 1));

        Assert.Equal(2 * one.IndexCount, combined.IndexCount);
        for (var index = 0; index < one.IndexCount; index++)
        {
            Assert.Equal(one.Indices[index], combined.Indices[index]);
        }
    }

    /// <summary>A steady build must not allocate: the dispatcher calls this every frame.</summary>
    [Fact]
    public void BuildingSteadilyAllocatesNothing()
    {
        var mesher = new ShulkerBoxMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.ShulkerBox, Facing: 1)
        ];
        var sprite = new SpriteRect(0, 0, 64, 64, 1);

        // Warm up so the geometry buffers reach their steady size before measuring.
        for (var i = 0; i < 3; i++)
        {
            mesher.Build(entities, sprite);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        mesher.Build(entities, sprite);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    /// <summary>Direction ids the block state uses, in the reference's Direction order.</summary>
    private static readonly Dictionary<string, byte> FacingIds = new()
    {
        ["down"] = 0,
        ["up"] = 1,
        ["north"] = 2,
        ["south"] = 3,
        ["west"] = 4,
        ["east"] = 5
    };

    [Theory]
    [InlineData("down")]
    [InlineData("up")]
    [InlineData("north")]
    [InlineData("south")]
    [InlineData("west")]
    [InlineData("east")]
    public void EveryFacingMatchesTheReferenceMatrix(string direction)
    {
        var matrix = TransformMatrix(direction);
        var actual = Vertices(Build(0f, FacingIds[direction]))
            .Select(vertex => (vertex.Item1, vertex.Item2, vertex.Item3))
            .OrderBy(vertex => vertex, Comparer<(float, float, float)>.Create(
                (a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1)
                    : a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2)
                    : a.Item3.CompareTo(b.Item3)))
            .ToArray();
        var expected = GoldenVertices(0f)
            .Select(vertex => Round(Transform(matrix, vertex.Position)))
            .OrderBy(vertex => vertex, Comparer<(float X, float Y, float Z)>.Create(
                (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z)))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static float[] TransformMatrix(string direction)
    {
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "shulker-box-transforms.golden.txt")))
        {
            if (!line.StartsWith("transform|" + direction + "|", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            var matrix = new float[16];
            for (var i = 0; i < 16; i++)
            {
                matrix[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            }

            return matrix;
        }

        throw new InvalidOperationException("no transform for " + direction);
    }
}
