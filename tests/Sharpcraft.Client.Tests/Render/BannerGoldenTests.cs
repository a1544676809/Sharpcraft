// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Banner vertices against the 26.3 reference sample. Body groups are pinned exactly; flag groups are
/// pinned to a known, quantified defect (issue #111).
/// </summary>
/// <remarks>
/// <para>
/// The sample in <c>Repro/Data/banner-vectors.golden.txt</c> was recorded with ModelProbe against the
/// official client jar. Its header cites the source line for every value. The body specs use the
/// literal <c>MODEL_TRANSLATION</c> and <c>MODEL_SCALE</c>; the flag specs add
/// <c>part=flag:X:-0.45</c>, the literal phase-zero idle tilt from BannerFlagModel.java:34. All four
/// groups sample angle zero, so rotateY(-angle) is the identity.
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
public sealed class BannerGoldenTests
{
    /// <summary>Largest disagreement tolerated for a matched position or texture coordinate, in blocks.</summary>
    private const float Tolerance = 0.002f;

    /// <summary>
    /// Measured per-vertex distance between the reference flag and our inverted flag. Both flag groups
    /// measure 0.026213; the assertion below locks that magnitude so a change in either direction fails.
    /// </summary>
    private const float FlagTiltGap = 0.026213f;

    private static readonly string[] BodyGroupNames = ["standing", "wall"];
    private static readonly string[] FlagGroupNames = ["standing_flag", "wall_flag"];

    /// <summary>One row per body group, so the theory and the coverage guard cannot drift apart.</summary>
    public static IEnumerable<object[]> BodyGroupRows =>
        BodyGroupNames.Select(group => new object[] { group });

    /// <summary>One row per flag group, so the theory and the coverage guard cannot drift apart.</summary>
    public static IEnumerable<object[]> FlagGroupRows =>
        FlagGroupNames.Select(group => new object[] { group });

    /// <summary>
    /// Body geometry (standing pole/bar and wall bar) must match the reference point for point.
    /// </summary>
    [Theory]
    [MemberData(nameof(BodyGroupRows))]
    public void EveryBodyVertexMatchesTheReference(string group)
    {
        var expected = ReferenceByPosition(group);
        var actual = OurByPosition(group);

        Assert.Equal(expected.Count, actual.Count);

        foreach (var (key, expectedUvs) in expected)
        {
            List<(float U, float V)>? matched = null;
            foreach (var (otherKey, otherUvs) in actual)
            {
                if (MathF.Abs(otherKey.X - key.X) <= Tolerance
                    && MathF.Abs(otherKey.Y - key.Y) <= Tolerance
                    && MathF.Abs(otherKey.Z - key.Z) <= Tolerance)
                {
                    matched = otherUvs;
                    break;
                }
            }

            Assert.True(
                matched is not null,
                $"{group}: the sample position ({key.X / 1000f:F3}, {key.Y / 1000f:F3}, {key.Z / 1000f:F3}) is missing");

            var actualUvs = matched!;
            Assert.True(
                expectedUvs.Count == actualUvs.Count,
                $"{group}: position ({key.X}, {key.Y}, {key.Z}) carries"
                    + $" {actualUvs.Count} vertices but the sample has {expectedUvs.Count}");

            expectedUvs.Sort();
            actualUvs.Sort();
            for (var corner = 0; corner < expectedUvs.Count; corner++)
            {
                var du = MathF.Abs(expectedUvs[corner].U - actualUvs[corner].U);
                var dv = MathF.Abs(expectedUvs[corner].V - actualUvs[corner].V);
                Assert.True(
                    du <= Tolerance && dv <= Tolerance,
                    $"{group}: position ({key.X}, {key.Y}, {key.Z}) vertex {corner}"
                        + $" is u {actualUvs[corner].U:F4} v {actualUvs[corner].V:F4}"
                        + $" but the sample says u {expectedUvs[corner].U:F4} v {expectedUvs[corner].V:F4}");
            }
        }
    }

    /// <summary>
    /// Locks the KNOWN DEFECT of issue #111: the flag idle tilt is inverted by roughly 0.9 degrees.
    /// This test pins the defect, not correctness. If it fails because the sign was corrected, delete
    /// this test and close issue #111.
    /// </summary>
    /// <remarks>
    /// Measured values (name | sample z range | ours z range | mirror sum | max per-vertex distance):
    /// <list type="bullet">
    /// <item>standing_flag: [0.541665, 0.596421] | [0.528576, 0.583331] | 1.124996 (= 2*0.5625) | 0.026213</item>
    /// <item>wall_flag:     [0.104165, 0.158921] | [0.091076, 0.145831] | 0.249996 (= 2*0.125)  | 0.026213</item>
    /// </list>
    /// Source truth is part=flag:X:-0.45 (BannerFlagModel.java:34). Our engine renders it as +0.45:
    /// BakeChild negates RotationX (VanillaMeshBake.cs:249) and the phase pose delta negates it
    /// again, so the two sign flips do not cancel. Effect limited to X-rotated parts; see issue #111
    /// and docs/modules/render.md. If this assertion fails because the sign was corrected, delete
    /// this test and close issue #111.
    /// </remarks>
    [Theory]
    [MemberData(nameof(FlagGroupRows))]
    public void TheFlagTiltIsInvertedUntilIssue111IsFixed(string group)
    {
        var expected = ReferencePositions(group);
        var actual = OurPositions(group);

        var expectedMin = expected.Min(p => p.Z);
        var expectedMax = expected.Max(p => p.Z);
        var actualMin = actual.Min(p => p.Z);
        var actualMax = actual.Max(p => p.Z);

        // Each group mirrors about its own measured centre. These are two independent per-group
        // equations, not a shared formula: a change to either the reference or our geometry breaks them.
        var mirrorSum = group == "standing_flag" ? 1.125f : 0.25f; // 2*0.5625 or 2*0.125
        Assert.True(
            MathF.Abs((expectedMin + actualMax) - mirrorSum) <= Tolerance,
            $"{group}: expectedMin+actualMax={expectedMin + actualMax:F6}, want {mirrorSum:F6}."
                + " If this fails because the X-rotation sign was corrected, delete this test and close #111.");
        Assert.True(
            MathF.Abs((expectedMax + actualMin) - mirrorSum) <= Tolerance,
            $"{group}: expectedMax+actualMin={expectedMax + actualMin:F6}, want {mirrorSum:F6}."
                + " If this fails because the X-rotation sign was corrected, delete this test and close #111.");

        // The spans stay equal - the tilt is mirrored, not resized.
        Assert.True(
            MathF.Abs((expectedMax - expectedMin) - (actualMax - actualMin)) <= Tolerance,
            $"{group}: z spans differ - sample {expectedMax - expectedMin:F6}, ours {actualMax - actualMin:F6}."
                + " If this fails because the X-rotation sign was corrected, delete this test and close #111.");

        // Nearest-vertex distance locks the magnitude. Tolerated on both sides so a correction or a
        // drift in the reference each turn it red.
        var maxDistance = 0f;
        foreach (var expectedPoint in expected)
        {
            var nearest = float.MaxValue;
            foreach (var actualPoint in actual)
            {
                var dx = expectedPoint.X - actualPoint.X;
                var dy = expectedPoint.Y - actualPoint.Y;
                var dz = expectedPoint.Z - actualPoint.Z;
                nearest = MathF.Min(nearest, MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)));
            }

            maxDistance = MathF.Max(maxDistance, nearest);
        }

        Assert.True(
            MathF.Abs(maxDistance - FlagTiltGap) <= Tolerance,
            $"{group}: max nearest distance {maxDistance:F6}, want {FlagTiltGap:F6}."
                + " If this fails because the X-rotation sign was corrected, delete this test and close #111.");
    }

    /// <summary>Every group present in the sample must be asserted by one of the two theories.</summary>
    [Fact]
    public void EveryGroupInTheSampleIsAccountedFor()
    {
        var groups = new HashSet<string>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            groups.Add(line.Split('|')[0]);
        }

        var known = new HashSet<string>(BodyGroupNames.Concat(FlagGroupNames));
        Assert.Equal(4, groups.Count);
        Assert.All(groups, group => Assert.Contains(group, known));
    }

    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "banner-vectors.golden.txt");

    private static BannerAttachment AttachmentOf(string group) =>
        group.StartsWith("standing", StringComparison.Ordinal) ? BannerAttachment.Ground : BannerAttachment.Wall;

    private static byte RotationOf(string group) =>
        group.StartsWith("standing", StringComparison.Ordinal) ? (byte)0 : (byte)3;

    private static int BodyVertexCount(string group) =>
        group.StartsWith("standing", StringComparison.Ordinal) ? 48 : 24;

    private static List<(float X, float Y, float Z)> ReferencePositions(string group) =>
        ReferenceRows(group)
            .Select(row => (Parse(row[1]), Parse(row[2]), Parse(row[3])))
            .ToList();

    private static Dictionary<(int X, int Y, int Z), List<(float U, float V)>> ReferenceByPosition(string group)
    {
        var result = new Dictionary<(int X, int Y, int Z), List<(float U, float V)>>();
        foreach (var row in ReferenceRows(group))
        {
            Group(result, Parse(row[1]), Parse(row[2]), Parse(row[3]), Parse(row[4]), Parse(row[5]));
        }

        return result;
    }

    private static IEnumerable<string[]> ReferenceRows(string group)
    {
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts[0] == group)
            {
                yield return parts;
            }
        }
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);

    private static List<(float X, float Y, float Z)> OurPositions(string group)
    {
        var geometry = Build(group);
        var body = BodyVertexCount(group);
        var isFlag = group.EndsWith("_flag", StringComparison.Ordinal);
        var result = new List<(float X, float Y, float Z)>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var index = vertex / ChunkMesher.FloatsPerVertex;
            if (isFlag ? index < body : index >= body)
            {
                continue;
            }

            result.Add((geometry.Vertices[vertex], geometry.Vertices[vertex + 1], geometry.Vertices[vertex + 2]));
        }

        return result;
    }

    private static Dictionary<(int X, int Y, int Z), List<(float U, float V)>> OurByPosition(string group)
    {
        var geometry = Build(group);
        var body = BodyVertexCount(group);
        var result = new Dictionary<(int X, int Y, int Z), List<(float U, float V)>>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            if (vertex / ChunkMesher.FloatsPerVertex >= body)
            {
                continue;
            }

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

    private static DynamicGeometry Build(string group)
    {
        var baseName = group.EndsWith("_flag", StringComparison.Ordinal)
            ? group[..^5]
            : group;
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [BannerModelDefinition.TexturePath] = new(64, 64, new byte[64 * 64 * 4])
        });
        return new BannerRenderer().Build(
            [new BlockEntityRenderState(
                0f, 0f, 0f, VanillaBlockEntityTypes.Banner,
                BannerAttachment: AttachmentOf(baseName),
                BannerRotation: RotationOf(baseName),
                BannerColor: 0,
                BannerPhase: 0f)],
            atlas);
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
