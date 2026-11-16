// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Banner vertices against the 26.3 reference sample. Both body and flag groups are pinned exactly;
/// the former issue #111 flag-tilt defect is fixed by the raw source frame and this test now asserts
/// strict point-for-point equivalence.
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
    /// Flag geometry (standing/wall) must match the reference point for point now that the raw
    /// source frame applies the source <c>BannerFlagModel.setupAnim</c> X rotation literally.
    /// This is the strict replacement for the former issue #111 defect-pin assertion: the phase-zero
    /// idle tilt from the reference sample (<c>part=flag:X:-0.45</c>, i.e. the source radian value)
    /// is now rendered verbatim, not inverted. Uses the same position-grouped, UV-per-group
    /// comparison as the body test so a half-float corner rounding cannot measure opposite corners.
    /// </summary>
    [Theory]
    [MemberData(nameof(FlagGroupRows))]
    public void EveryFlagVertexMatchesTheReference(string group)
    {
        var expected = ReferenceByPosition(group);
        var actual = OurFlagByPosition(group);

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

    private static Dictionary<(int X, int Y, int Z), List<(float U, float V)>> OurFlagByPosition(string group)
    {
        var geometry = Build(group);
        var body = BodyVertexCount(group);
        var result = new Dictionary<(int X, int Y, int Z), List<(float U, float V)>>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var index = vertex / ChunkMesher.FloatsPerVertex;
            if (index < body)
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
