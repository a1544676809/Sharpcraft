// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Conduit shell vertices against the 26.3 reference sample. Positions are pinned exactly; the face UV
/// assignment is pinned to a known, quantified defect (issue #112).
/// </summary>
/// <remarks>
/// <para>
/// The sample in <c>Repro/Data/conduit-vectors.golden.txt</c> was recorded with the generic ModelProbe
/// sampler against the official client jar. Its header cites the source lines for every value: the
/// inactive shell layer (<c>createShellLayer</c> lines 72-77) and the inactive submit branch (lines
/// 106-119, <c>translate(0.5,0.5,0.5)</c> at 108). Only the inactive shell is sampled: this slice draws
/// only that branch (SIM-BE-CONDUIT-ACTIVE); the active cage/eye/wind branches depend on tick count,
/// animation phase and camera orientation, which the sampler's transform grammar cannot express.
/// </para>
/// <para>
/// Vertices are grouped by position and the texture coordinates are compared inside each group. Sorting
/// whole vertices as one tuple does not work: a cube corner carries one vertex per adjacent face, so two
/// vertices with the same position and different coordinates swap places as soon as a half float rounds
/// differently, and the comparison then measures opposite corners of the same box against each other.
/// </para>
/// <para>
/// Vertices carry position and texture coordinates. The terrain vertex packs u in the low half and v in
/// the high half of the third float, so both are decoded rather than assumed.
/// </para>
/// </remarks>
public sealed class ConduitGoldenTests
{
    /// <summary>Largest disagreement tolerated for a matched position or texture coordinate, in blocks.</summary>
    private const float Tolerance = 0.002f;

    /// <summary>Every sample group the golden file carries.</summary>
    private static readonly string[] Groups = ["rot0"];

    /// <summary>One row per group, so the theories and the coverage guard cannot drift apart.</summary>
    public static IEnumerable<object[]> GroupRows => Groups.Select(group => new object[] { group });

    /// <summary>All six shell faces in emit order: 0=DOWN, 1=UP, 2=WEST, 3=NORTH, 4=EAST, 5=SOUTH.</summary>
    private static readonly int[] UnaffectedFaces = [0, 1, 2, 3, 4, 5];

    /// <summary>One row per face.</summary>
    public static IEnumerable<object[]> UnaffectedFaceRows => UnaffectedFaces.Select(face => new object[] { face });

    /// <summary>
    /// Shell positions must match the reference point for point. Measured maximum per-axis error is 0.
    /// </summary>
    [Theory]
    [MemberData(nameof(GroupRows))]
    public void EveryVertexPositionMatchesTheReference(string group)
    {
        var expected = ReferenceVertices(group);
        var actual = OurVertices();
        Assert.Equal(expected.Count, actual.Count);

        for (var vertex = 0; vertex < expected.Count; vertex++)
        {
            var e = expected[vertex];
            var a = actual[vertex];
            Assert.True(
                MathF.Abs(e.X - a.X) <= Tolerance
                    && MathF.Abs(e.Y - a.Y) <= Tolerance
                    && MathF.Abs(e.Z - a.Z) <= Tolerance,
                $"{group}: vertex {vertex} position ({a.X:F4}, {a.Y:F4}, {a.Z:F4})"
                    + $" but the sample says ({e.X:F4}, {e.Y:F4}, {e.Z:F4})");
        }
    }

    /// <summary>All six shell faces must use the sample's matching face UV set (#112 fixed).</summary>
    [Theory]
    [MemberData(nameof(UnaffectedFaceRows))]
    public void EveryFaceUvMatchesTheReference(int face)
    {
        var expected = ReferenceFaceUvs("rot0", face);
        var actual = OurFaceUvs(face);

        AssertUvSetsEqual(expected, actual, face, face);
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

    private static void AssertUvSetsEqual(
        List<(float U, float V)> expected,
        List<(float U, float V)> actual,
        int face,
        int referenceFace)
    {
        Assert.True(
            expected.Count == actual.Count,
            $"face {face}: ours carries {actual.Count} UVs but the sample face {referenceFace} has {expected.Count}."
                + " If the face mapping was corrected, change this test to a strict comparison and close #112.");

        expected.Sort();
        actual.Sort();
        for (var corner = 0; corner < expected.Count; corner++)
        {
            var du = MathF.Abs(expected[corner].U - actual[corner].U);
            var dv = MathF.Abs(expected[corner].V - actual[corner].V);
            Assert.True(
                du <= Tolerance && dv <= Tolerance,
                $"face {face} vertex {corner}: ours is u {actual[corner].U:F4} v {actual[corner].V:F4}"
                    + $" but sample face {referenceFace} says u {expected[corner].U:F4} v {expected[corner].V:F4}."
                    + " If the face mapping was corrected, change this test to a strict comparison and close #112.");
        }
    }

    private static HashSet<string> ReferenceGroups()
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            groups.Add(line.Split('|')[1]);
        }

        return groups;
    }

    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "conduit-vectors.golden.txt");

    private static List<(float X, float Y, float Z, float U, float V)> ReferenceVertices(string group)
    {
        var prefix = "conduit|" + group + "|";
        var result = new List<(float X, float Y, float Z, float U, float V)>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            result.Add((
                Parse(parts[2]),
                Parse(parts[3]),
                Parse(parts[4]),
                Parse(parts[5]),
                Parse(parts[6])));
        }

        return result;
    }

    private static List<(float U, float V)> ReferenceFaceUvs(string group, int face)
    {
        var vertices = ReferenceVertices(group);
        var result = new List<(float U, float V)>();
        for (var index = face * 4; index < (face * 4) + 4; index++)
        {
            result.Add((vertices[index].U, vertices[index].V));
        }

        return result;
    }

    private static List<(float X, float Y, float Z, float U, float V)> OurVertices()
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ConduitModelDefinition.TexturePath] = new(
                ConduitModelDefinition.TextureWidth,
                ConduitModelDefinition.TextureHeight,
                new byte[ConduitModelDefinition.TextureWidth * ConduitModelDefinition.TextureHeight * 4])
        });
        var geometry = new ConduitRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Conduit)],
            atlas);

        var result = new List<(float X, float Y, float Z, float U, float V)>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[vertex + 4]);
            result.Add((
                geometry.Vertices[vertex],
                geometry.Vertices[vertex + 1],
                geometry.Vertices[vertex + 2],
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFFu)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16))));
        }

        return result;
    }

    private static List<(float U, float V)> OurFaceUvs(int face)
    {
        var vertices = OurVertices();
        var result = new List<(float U, float V)>();
        for (var index = face * 4; index < (face * 4) + 4; index++)
        {
            result.Add((vertices[index].U, vertices[index].V));
        }

        return result;
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
