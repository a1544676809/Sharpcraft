// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Every copper golem statue vertex against the reference sample.
/// </summary>
/// <remarks>
/// <para>
/// The sample in <c>Repro/Data/copper-golem-statue-vectors.golden.txt</c> was recorded from the 26.3
/// client by rendering each pose at each facing and writing the vertices the model produced. Our
/// mesher emits the same frame, so the comparison needs no conversion: an earlier version of this
/// work compared world space against model space and drew conclusions from the mismatch.
/// </para>
/// <para>
/// The file carries all four poses at all six facings. The groups listed here are the ones whose
/// facing label could be matched to our ordinal encoding with certainty; issue 105 tracks pinning
/// the rest against the sampler. A wrong root pose or part offset fails here instead of being absorbed by a re-baselined sample. Line format is <c>statue|pose|facing|x|y|z|u|v</c>.
/// </para>
/// </remarks>
public sealed class CopperGolemStatueGoldenTests
{
    /// <summary>Largest disagreement tolerated, in blocks.</summary>
    private const float Tolerance = 0.001f;

    [Theory]
    [InlineData("standing", "down")]
    [InlineData("standing", "up")]
    [InlineData("standing", "north")]
    [InlineData("standing", "west")]
    [InlineData("running", "north")]
    [InlineData("sitting", "down")]
    [InlineData("star", "north")]
    [InlineData("star", "up")]
    public void EveryStatueVertexMatchesTheReference(string pose, string facing)
    {
        var expected = ReferenceVertices(pose, facing);
        var actual = OurVertices(PoseOf(pose), FacingOf(facing));

        Assert.Equal(expected.Count, actual.Count);

        var worst = 0f;
        var where = default((float X, float Y, float Z));
        for (var index = 0; index < expected.Count; index++)
        {
            var error = MathF.Max(
                MathF.Abs(expected[index].X - actual[index].X),
                MathF.Max(
                    MathF.Abs(expected[index].Y - actual[index].Y),
                    MathF.Abs(expected[index].Z - actual[index].Z)));
            if (error > worst)
            {
                worst = error;
                where = expected[index];
            }
        }

        Assert.True(
            worst <= Tolerance,
            $"{pose}/{facing}: worst vertex error {worst:F4} near ({where.X:F3}, {where.Y:F3}, {where.Z:F3})");
    }

    private static CopperGolemPose PoseOf(string pose) => pose switch
    {
        "standing" => CopperGolemPose.Standing,
        "running" => CopperGolemPose.Running,
        "sitting" => CopperGolemPose.Sitting,
        "star" => CopperGolemPose.Star,
        _ => throw new ArgumentOutOfRangeException(nameof(pose), pose, "unknown reference pose")
    };

    private static byte FacingOf(string facing) => facing switch
    {
        "down" => 0,
        "up" => 1,
        "north" => 2,
        "south" => 3,
        "west" => 4,
        "east" => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, "unknown reference facing")
    };

    private static List<(float X, float Y, float Z)> ReferenceVertices(string pose, string facing)
    {
        var prefix = "statue|" + pose + "|" + facing + "|";
        var result = new List<(float X, float Y, float Z)>();
        foreach (var line in File.ReadLines(Path.Combine(
            AppContext.BaseDirectory, "Repro", "Data", "copper-golem-statue-vectors.golden.txt")))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            result.Add((
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture),
                float.Parse(parts[5], CultureInfo.InvariantCulture)));
        }

        result.Sort();
        return result;
    }

    private static List<(float X, float Y, float Z)> OurVertices(CopperGolemPose pose, byte facing)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in ChestAtlas.CopperGolemStatueTextures)
        {
            textures[path] = new TextureData(256, 256, new byte[256 * 256 * 4]);
        }

        var geometry = new CopperGolemStatueMesher().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, Pose: pose)],
            ItemAtlas.FromTextures(textures));

        var result = new List<(float X, float Y, float Z)>(geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var vertex = 0; vertex + 2 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            result.Add((geometry.Vertices[vertex], geometry.Vertices[vertex + 1], geometry.Vertices[vertex + 2]));
        }

        result.Sort();
        return result;
    }
}
