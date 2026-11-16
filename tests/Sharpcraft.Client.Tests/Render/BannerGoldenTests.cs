// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Banner geometry against the full 26.3 reference sample (16 ground rotations, 4 wall facings,
/// 8 phases x ground/wall = 36 groups). Every vertex is compared strictly as a whole
/// (position, UV) tuple: the complete multiset is sorted canonically by all five components and
/// matched element by element. There is no per-position grouping, no separate UV sorting, and no
/// tolerance-based set membership, so a duplicated, missing or position/UV-mismatched vertex fails.
/// </summary>
/// <remarks>
/// The sample is produced by <c>ref://mc-26.3-analysis/vectors/BannerVectorProbe.java</c>, which
/// uses the jar's baked ModelPart layers and the real BannerFlagModel.setupAnim pose, with the
/// source literal <c>Transformation(T,R,S)</c> and 1/16 model scale. Its ground_0 group is
/// byte-identical to the previously verified standing group of the older sample.
/// The probe keys each row by part/polygon/vertex; those keys are retained in the sample for
/// auditability, while the assertion compares the whole (x,y,z,u,v) multiset because the runtime
/// DynamicGeometry does not carry part names.
/// </remarks>
public sealed class BannerGoldenTests
{
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "banner-vectors.golden.txt");

    private static readonly string[] GroundGroups =
        Enumerable.Range(0, 16).Select(segment => "ground_" + segment).ToArray();

    private static readonly string[] WallGroups =
        Enumerable.Range(2, 4).Select(facing => "wall_" + facing).ToArray();

    private static readonly string[] FlagGroups =
        new[] { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f }
            .SelectMany(phase => new[]
            {
                "flag_ground_" + phase.ToString("0.000", CultureInfo.InvariantCulture),
                "flag_wall_" + phase.ToString("0.000", CultureInfo.InvariantCulture)
            })
            .ToArray();

    private static readonly string[] AllGroups = GroundGroups.Concat(WallGroups).Concat(FlagGroups).ToArray();

    public static IEnumerable<object[]> AllGroupRows => AllGroups.Select(group => new object[] { group });

    /// <summary>Every group of the full sample is covered by one strict theory row.</summary>
    [Theory]
    [MemberData(nameof(AllGroupRows))]
    public void EveryVertexMatchesTheReference(string group)
    {
        var expected = ReferenceVertices(group);
        Assert.NotEmpty(expected);

        var geometry = Build(group);
        var actual = OurVertices(geometry, group);

        Assert.Equal(expected.Count, actual.Count);

        // Strict whole-tuple comparison: sort complete (x,y,z,u,v) tuples by every component,
        // then match element by element. No position grouping and no separate UV sorting.
        var expectedSorted = expected.Select(static v => (Round(v.X), Round(v.Y), Round(v.Z), Round(v.U), Round(v.V)))
            .OrderBy(v => v).ToList();
        var actualSorted = actual.Select(static v => (Round(v.X), Round(v.Y), Round(v.Z), Round(v.U), Round(v.V)))
            .OrderBy(v => v).ToList();

        for (var index = 0; index < expectedSorted.Count; index++)
        {
            var e = expectedSorted[index];
            var a = actualSorted[index];
            Assert.True(
                MathF.Abs(e.Item1 - a.Item1) <= 0.001f
                    && MathF.Abs(e.Item2 - a.Item2) <= 0.001f
                    && MathF.Abs(e.Item3 - a.Item3) <= 0.001f
                    && MathF.Abs(e.Item4 - a.Item4) <= 0.001f
                    && MathF.Abs(e.Item5 - a.Item5) <= 0.001f,
                $"{group} vertex {index}: ours ({a.Item1},{a.Item2},{a.Item3}) uv ({a.Item4},{a.Item5})"
                    + $" but sample says ({e.Item1},{e.Item2},{e.Item3}) uv ({e.Item4},{e.Item5})");
        }
    }

    /// <summary>Every group present in the sample is asserted; the full 16+4+16 matrix is present.</summary>
    [Fact]
    public void EveryGroupInTheSampleIsAccountedFor()
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            groups.Add(line.Split('|')[0]);
        }

        Assert.Equal(36, groups.Count); // 16 ground + 4 wall + (8 phases x 2 flags)
        Assert.All(groups, group => Assert.Contains(group, AllGroups));
        Assert.Equal(36, AllGroups.Length);
    }

    private static float Round(float value) => MathF.Round(value, 3);

    private static List<(float X, float Y, float Z, float U, float V)> ReferenceVertices(string group)
    {
        var result = new List<(float, float, float, float, float)>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts[0] == group)
            {
                // columns: group|vertex|x|y|z|u|v
                result.Add((Parse(parts[2]), Parse(parts[3]), Parse(parts[4]), Parse(parts[5]), Parse(parts[6])));
            }
        }

        return result;
    }

    private static List<(float X, float Y, float Z, float U, float V)> OurVertices(DynamicGeometry geometry, string group)
    {
        // The renderer emits body then flag in one geometry. A body sample group covers only the
        // body; a flag sample group covers only the flag. Select the matching vertex range by index.
        var isFlag = group.StartsWith("flag_", StringComparison.Ordinal);
        var wall = group.Contains("wall");
        var bodyVertices = wall ? 24 : 48; // wall bar only; standing pole(24) + bar(24)
        var from = isFlag ? bodyVertices : 0;
        var to = isFlag ? geometry.VertexFloats / ChunkMesher.FloatsPerVertex : bodyVertices;

        var result = new List<(float, float, float, float, float)>();
        for (var vertex = from; vertex < to; vertex++)
        {
            var offset = vertex * ChunkMesher.FloatsPerVertex;
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[offset + 4]);
            result.Add((
                geometry.Vertices[offset],
                geometry.Vertices[offset + 1],
                geometry.Vertices[offset + 2],
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFFu)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16))));
        }

        return result;
    }

    private static DynamicGeometry Build(string group)
    {
        var isFlag = group.StartsWith("flag_", StringComparison.Ordinal);
        var standing = group.StartsWith("ground", StringComparison.Ordinal) || group.Contains("ground_");
        var attachment = group.Contains("wall") ? BannerAttachment.Wall : BannerAttachment.Ground;

        byte rotation;
        float phase;
        if (isFlag)
        {
            rotation = attachment == BannerAttachment.Wall ? (byte)3 : (byte)0;
            var phaseText = group[(group.LastIndexOf('_') + 1)..];
            phase = float.Parse(phaseText, CultureInfo.InvariantCulture);
        }
        else
        {
            rotation = byte.Parse(group[(group.LastIndexOf('_') + 1)..], CultureInfo.InvariantCulture);
            phase = 0f;
        }

        _ = standing;
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [BannerModelDefinition.TexturePath] = new(64, 64, new byte[64 * 64 * 4])
        });
        return new BannerRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Banner,
                BannerAttachment: attachment, BannerRotation: rotation, BannerColor: 0, BannerPhase: phase)],
            atlas);
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
