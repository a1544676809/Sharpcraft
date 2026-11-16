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
/// 8 phases x ground/wall = 36 groups). The sample is stored in SOURCE-DECLARATION vertex order
/// (pole then bar; flag alone) with stable part|cube|polygon|vertex keys, matching the order our
/// mesher emits. Every vertex is compared to the sample row at the same index, position and UV
/// strictly: no OrderBy, no rounding, no per-position grouping or set comparison.
/// </summary>
/// <remarks>
/// Sample provenance is recorded in the file header: 4 jar-verified anchor groups plus 32 groups
/// derived by documented source formulas. <see cref="BannerSourceFormulaConsistencyTests"/> checks
/// that every derived group equals the formula applied to the corresponding anchor, and
/// <see cref="BannerGoldenTests"/> here checks the renderer against the sample. Two independent
/// evidence paths, no implementation reverse-engineering.
/// </remarks>
public sealed class BannerGoldenTests
{
    /// <summary>Max per-component difference: sample stores 6 decimals; half-float UV rounds ~1e-3.</summary>
    private const float Tolerance = 0.002f;

    internal static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "banner-vectors.golden.txt");

    private static readonly string[] GroundGroups =
        Enumerable.Range(0, 16).Select(segment => "ground_" + segment).ToArray();

    private static readonly string[] WallGroups =
        Enumerable.Range(2, 4).Select(facing => "wall_" + facing).ToArray();

    internal static readonly float[] Phases =
        [0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f];

    internal static readonly string[] FlagGroups =
        Phases.SelectMany(phase => new[]
        {
            "flag_ground_" + phase.ToString("0.000", CultureInfo.InvariantCulture),
            "flag_wall_" + phase.ToString("0.000", CultureInfo.InvariantCulture)
        }).ToArray();

    internal static readonly string[] AllGroups = GroundGroups.Concat(WallGroups).Concat(FlagGroups).ToArray();

    public static IEnumerable<object[]> AllGroupRows => AllGroups.Select(group => new object[] { group });

    /// <summary>Every group of the full sample is covered by one strict theory row.</summary>
    [Theory]
    [MemberData(nameof(AllGroupRows))]
    public void EveryVertexMatchesTheReferenceByIndex(string group)
    {
        var expected = BannerSampleReader.Rows(group);

        var geometry = Build(group);
        var actual = OurVertices(geometry, group);

        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            var e = expected[index];
            var a = actual[index];
            Assert.True(
                MathF.Abs(e.X - a.X) <= Tolerance && MathF.Abs(e.Y - a.Y) <= Tolerance
                    && MathF.Abs(e.Z - a.Z) <= Tolerance
                    && MathF.Abs(e.U - a.U) <= Tolerance && MathF.Abs(e.V - a.V) <= Tolerance,
                $"{group} vertex {index} (sample key {e.Part}|{e.Cube}|{e.Polygon}|{e.Vertex}):"
                    + $" ours ({a.X:F4},{a.Y:F4},{a.Z:F4}) uv ({a.U:F4},{a.V:F4})"
                    + $" but sample says ({e.X:F4},{e.Y:F4},{e.Z:F4}) uv ({e.U:F4},{e.V:F4})");
        }
    }

    /// <summary>Every group present in the sample is asserted; the full 16+4+16 matrix is present.</summary>
    [Fact]
    public void EveryGroupInTheSampleIsAccountedFor()
    {
        var groups = BannerSampleReader.AllGroups().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(36, groups.Count);
        Assert.Equal(36, AllGroups.Length);
        Assert.All(groups, group => Assert.Contains(group, AllGroups));
    }

    private static List<(float X, float Y, float Z, float U, float V)> OurVertices(DynamicGeometry geometry, string group)
    {
        // Renderer emits body (pole 24 + bar 24 for standing; bar 24 for wall) then flag (24).
        var isFlag = group.StartsWith("flag_", StringComparison.Ordinal);
        var isWall = group.StartsWith("wall") || group.StartsWith("flag_wall");
        var bodyVertices = isWall ? 24 : 48;
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

    internal static DynamicGeometry Build(string group)
    {
        var isFlag = group.StartsWith("flag_", StringComparison.Ordinal);
        var isWall = group.StartsWith("wall") || group.StartsWith("flag_wall");
        var attachment = isWall ? BannerAttachment.Wall : BannerAttachment.Ground;

        byte rotation;
        float phase;
        if (isFlag)
        {
            rotation = isWall ? (byte)3 : (byte)0;
            var phaseText = group[(group.LastIndexOf('_') + 1)..];
            phase = float.Parse(phaseText, CultureInfo.InvariantCulture);
        }
        else
        {
            rotation = byte.Parse(group[(group.LastIndexOf('_') + 1)..], CultureInfo.InvariantCulture);
            phase = 0f;
        }

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [BannerModelDefinition.TexturePath] = new(64, 64, new byte[64 * 64 * 4])
        });
        return new BannerRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Banner,
                BannerAttachment: attachment, BannerRotation: rotation, BannerColor: 0, BannerPhase: phase)],
            atlas);
    }
}
