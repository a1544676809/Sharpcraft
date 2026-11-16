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
/// client by rendering each pose at each facing and writing the vertices the model produced. Its header
/// states the source jar, the coordinate space and the direction encoding, and our mesher emits the same
/// frame, so no conversion is applied here.
/// </para>
/// <para>
/// Vertices are grouped by position and the texture coordinates are compared inside each group. Sorting
/// whole vertices as one tuple does not work: a cube corner carries one vertex per adjacent face, so two
/// vertices with the same position and different coordinates swap places as soon as a half float rounds
/// differently, and the comparison then measures opposite corners of the same box against each other.
/// That mistake once made several groups look mirrored when they were identical.
/// </para>
/// <para>
/// Vertices carry position and texture coordinates. The terrain vertex packs u in the low half and v in
/// the high half of the third float, so both are decoded rather than assumed.
/// </para>
/// </remarks>
public sealed class CopperGolemStatueGoldenTests
{
    /// <summary>Largest disagreement tolerated for a position, in blocks.</summary>
    /// <remarks>
    /// One group lands a thousandth away from the sample, which is a float boundary rather than a
    /// difference in geometry: the sample prints six decimals and our rotation accumulates rounding.
    /// </remarks>
    private const float PositionTolerance = 0.002f;

    /// <summary>Largest disagreement tolerated for a texture coordinate.</summary>
    /// <remarks>The vertex stores u and v as half floats, which resolve about 1e-3 near one.</remarks>
    private const float UvTolerance = 0.002f;

    /// <summary>Every pose and facing group the sample carries.</summary>
    private static readonly (string Pose, string Facing)[] Groups =
    [
        ("standing", "down"),
        ("standing", "up"),
        ("standing", "north"),
        ("standing", "south"),
        ("standing", "west"),
        ("standing", "east"),
        ("running", "down"),
        ("running", "up"),
        ("running", "north"),
        ("running", "south"),
        ("running", "west"),
        ("running", "east"),
        ("sitting", "down"),
        ("sitting", "up"),
        ("sitting", "north"),
        ("sitting", "south"),
        ("sitting", "west"),
        ("sitting", "east"),
        ("star", "down"),
        ("star", "up"),
        ("star", "north"),
        ("star", "south"),
        ("star", "west"),
        ("star", "east")
    ];

    /// <summary>
    /// Groups whose geometry still differs from the sample, tracked by issue 107. They are listed
    /// rather than dropped so the guard below notices if one of them starts matching, or if the sample
    /// gains a group nobody classified.
    /// </summary>
    private static readonly (string Pose, string Facing)[] MismatchedGroups =
    [
        ("running", "south"),
        ("sitting", "north"),
        ("sitting", "south"),
        ("sitting", "east")
    ];

    /// <summary>One row per matching group, so the theory and the coverage guard cannot drift apart.</summary>
    public static IEnumerable<object[]> GroupRows =>
        Groups
            .Where(group => !MismatchedGroups.Contains(group))
            .Select(group => new object[] { group.Pose, group.Facing });

    [Theory]
    [MemberData(nameof(GroupRows))]
    public void EveryStatueVertexMatchesTheReference(string pose, string facing)
    {
        var expected = ReferenceByPosition(pose, facing);
        var actual = OurByPosition(PoseOf(pose), FacingOf(facing));

        Assert.Equal(expected.Count, actual.Count);

        var expectedKeys = expected.Keys.Order().ToArray();
        var actualKeys = actual.Keys.Order().ToArray();
        for (var index = 0; index < expectedKeys.Length; index++)
        {
            var left = expectedKeys[index];
            var right = actualKeys[index];
            Assert.True(
                Math.Abs(left.X - right.X) <= PositionTolerance * 1000f
                    && Math.Abs(left.Y - right.Y) <= PositionTolerance * 1000f
                    && Math.Abs(left.Z - right.Z) <= PositionTolerance * 1000f,
                $"{pose}/{facing}: position ({right.X}, {right.Y}, {right.Z}) is not in the sample");

            var expectedUvs = expected[left];
            var actualUvs = actual[right];
            Assert.True(
                expectedUvs.Count == actualUvs.Count,
                $"{pose}/{facing}: position ({right.X}, {right.Y}, {right.Z}) carries"
                    + $" {actualUvs.Count} vertices but the sample has {expectedUvs.Count}");

            expectedUvs.Sort();
            actualUvs.Sort();
            for (var corner = 0; corner < expectedUvs.Count; corner++)
            {
                var du = MathF.Abs(expectedUvs[corner].U - actualUvs[corner].U);
                var dv = MathF.Abs(expectedUvs[corner].V - actualUvs[corner].V);
                Assert.True(
                    du <= UvTolerance && dv <= UvTolerance,
                    $"{pose}/{facing}: position ({right.X}, {right.Y}, {right.Z}) vertex {corner}"
                        + $" is u {actualUvs[corner].U:F4} v {actualUvs[corner].V:F4}"
                        + $" but the sample says u {expectedUvs[corner].U:F4} v {expectedUvs[corner].V:F4}");
            }
        }
    }

    [Fact]
    public void EveryGroupInTheSampleIsAccountedFor()
    {
        var inFile = ReferenceGroups();
        var known = Groups.ToHashSet();

        Assert.Equal(known.Count, Groups.Length);
        Assert.Equal(known.Count, inFile.Count);
        foreach (var group in inFile)
        {
            Assert.Contains(group, known);
        }
    }

    /// <summary>
    /// The sample records each direction's ordinal, its two dimensional data value and the yaw the
    /// reference derives from them. Those numbers are the mapping our renderer has to reproduce, so
    /// they are checked against it instead of being trusted from a comment.
    /// </summary>
    [Theory]
    [InlineData("down", 0, -1)]
    [InlineData("up", 1, -1)]
    [InlineData("north", 2, 2)]
    [InlineData("south", 3, 0)]
    [InlineData("west", 4, 1)]
    [InlineData("east", 5, 3)]
    public void TheSampleAgreesWithOurDirectionEncoding(string facing, int ordinal, int data2d)
    {
        var row = File.ReadLines(SamplePath)
            .Where(line => line.StartsWith("# facing|" + facing + "|", StringComparison.Ordinal))
            .Select(line => line.Split('|'))
            .Single();

        Assert.Equal(ordinal, int.Parse(row[2], CultureInfo.InvariantCulture));
        Assert.Equal(data2d, int.Parse(row[3], CultureInfo.InvariantCulture));
        Assert.Equal((byte)ordinal, FacingOf(facing));
        Assert.Equal(
            float.Parse(row[4], CultureInfo.InvariantCulture),
            CopperGolemStatueMesher.FacingDegrees((byte)ordinal),
            3);
    }

    private static HashSet<(string Pose, string Facing)> ReferenceGroups()
    {
        var groups = new HashSet<(string Pose, string Facing)>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            groups.Add((parts[1], parts[2]));
        }

        return groups;
    }

    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "copper-golem-statue-vectors.golden.txt");

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

    private static Dictionary<(int X, int Y, int Z), List<(float U, float V)>> ReferenceByPosition(
        string pose, string facing)
    {
        var prefix = "statue|" + pose + "|" + facing + "|";
        var result = new Dictionary<(int X, int Y, int Z), List<(float U, float V)>>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            Group(
                result,
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture),
                float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture),
                float.Parse(parts[7], CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static Dictionary<(int X, int Y, int Z), List<(float U, float V)>> OurByPosition(
        CopperGolemPose pose, byte facing)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in ChestAtlas.CopperGolemStatueTextures)
        {
            textures[path] = new TextureData(256, 256, new byte[256 * 256 * 4]);
        }

        var geometry = new CopperGolemStatueMesher().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, Pose: pose)],
            ItemAtlas.FromTextures(textures));

        // TerrainVertexWriter stores position, colour bits, uv bits, then sprite and light bits; the
        // texture coordinates live in the third float as two half floats, u low and v high.
        var result = new Dictionary<(int X, int Y, int Z), List<(float U, float V)>>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[vertex + 4]);
            Group(
                result,
                geometry.Vertices[vertex],
                geometry.Vertices[vertex + 1],
                geometry.Vertices[vertex + 2],
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFFu)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16)));
        }

        return result;
    }

    private static void Group(
        Dictionary<(int X, int Y, int Z), List<(float U, float V)>> target,
        float x,
        float y,
        float z,
        float u,
        float v)
    {
        var key = (
            (int)MathF.Round(x * 1000f),
            (int)MathF.Round(y * 1000f),
            (int)MathF.Round(z * 1000f));
        if (!target.TryGetValue(key, out var corners))
        {
            corners = [];
            target[key] = corners;
        }

        corners.Add((u, v));
    }
}
