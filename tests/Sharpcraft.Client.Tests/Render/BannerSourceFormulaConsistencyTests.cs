// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Independent consistency check for the banner reference sample: the 4 jar-anchor groups are
/// treated as the oracle anchors, and all 36 sample groups are recomputed from those anchors with
/// the documented SOURCE formulas and compared row-by-row (by part|cube|polygon|vertex key, no
/// sorting). This validates the sample's anchor→derived chain without touching production meshing.
/// </summary>
/// <remarks>
/// Anchor mapping (sample header): standing→ground_0, wall→wall_3,
/// standing_flag→flag_ground_0.000, wall_flag→flag_wall_0.000.
/// Body formula (BannerRenderer.java:41-42,239-249): world = T + Ry(-angle)*(anchor-T).
/// Flag formula (BannerFlagModel.java:27,34): recover pixel=(anchor-T)*16/S, apply xRot delta
/// about the part pivot, map back. Pivots: standing (0,-44,0), wall (0,-20.5,10.5).
/// This is the test-side twin of the independent generator in the analysis workspace.
/// </remarks>
public sealed class BannerSourceFormulaConsistencyTests
{
    private const float Tolerance = 0.002f;
    private const float SX = 2f / 3f, SY = -2f / 3f, SZ = -2f / 3f;
    private static readonly Vector3 T = new(0.5f, 0f, 0.5f);

    private static readonly Dictionary<string, string> AnchorOf = new(StringComparer.Ordinal)
    {
        ["ground_0"] = "ground_0",
        ["wall_3"] = "wall_3",
        ["flag_ground_0.000"] = "flag_ground_0.000",
        ["flag_wall_0.000"] = "flag_wall_0.000"
    };

    /// <summary>The 4 anchors must exist in the sample (jar-verified reference points).</summary>
    [Fact]
    public void AllFourAnchorGroupsArePresent()
    {
        var groups = BannerSampleReader.AllGroups().ToHashSet(StringComparer.Ordinal);
        foreach (var anchor in AnchorOf.Values)
        {
            Assert.Contains(anchor, groups);
        }
    }

    /// <summary>Every one of the 36 sample groups must equal its source-formula value from the matching anchor.</summary>
    [Theory]
    [MemberData(nameof(BannerGoldenTests.AllGroupRows), MemberType = typeof(BannerGoldenTests))]
    public void EveryGroupEqualsItsAnchorDerivedFormula(string group)
    {
        var rows = BannerSampleReader.Rows(group);
        Assert.NotEmpty(rows);

        var anchorGroup = AnchorFor(group);
        var anchorRows = BannerSampleReader.Rows(anchorGroup);
        Assert.Equal(rows.Count, anchorRows.Count);

        var isFlag = group.StartsWith("flag_", StringComparison.Ordinal);
        var isWall = group.StartsWith("wall") || group.StartsWith("flag_wall");
        var angleDeg = AngleDegrees(group, isWall);
        var phase = isFlag
            ? float.Parse(group[(group.LastIndexOf('_') + 1)..], CultureInfo.InvariantCulture)
            : 0f;
        var pivotY = isWall ? -20.5f : -44f;
        var pivotZ = isWall ? 10.5f : 0f;

        // Match by stable key. Anchor and derived share keys (same geometry, transformed).
        var anchorByKey = anchorRows.ToDictionary(
            r => (r.Part, r.Cube, r.Polygon, r.Vertex), r => r);

        foreach (var row in rows)
        {
            var anchor = anchorByKey[(row.Part, row.Cube, row.Polygon, row.Vertex)];
            var expected = isFlag
                ? FlagFormula(anchor, phase, pivotY, pivotZ)
                : BodyFormula(anchor, angleDeg);

            // UV is source-defined and invariant under both the body rotation and the flag pivot rotation,
            // so the derived group's UV must equal the anchor's UV verbatim.
            Assert.True(
                MathF.Abs(expected.X - row.X) <= Tolerance && MathF.Abs(expected.Y - row.Y) <= Tolerance
                    && MathF.Abs(expected.Z - row.Z) <= Tolerance
                    && MathF.Abs(anchor.U - row.U) <= Tolerance && MathF.Abs(anchor.V - row.V) <= Tolerance,
                $"{group} key {row.Part}|{row.Cube}|{row.Polygon}|{row.Vertex}:"
                    + $" sample ({row.X:F4},{row.Y:F4},{row.Z:F4}) uv ({row.U:F4},{row.V:F4})"
                    + $" but anchor-derived formula says ({expected.X:F4},{expected.Y:F4},{expected.Z:F4}) uv ({anchor.U:F4},{anchor.V:F4})");
        }
    }

    private static string AnchorFor(string group)
    {
        if (group.StartsWith("ground_")) return "ground_0";
        if (group.StartsWith("wall_") && !group.StartsWith("wall_flag")) return "wall_3";
        if (group.StartsWith("flag_ground_")) return "flag_ground_0.000";
        if (group.StartsWith("flag_wall_")) return "flag_wall_0.000";
        throw new ArgumentOutOfRangeException(nameof(group), group, "unknown banner group");
    }

    private static float AngleDegrees(string group, bool isWall)
    {
        if (group.StartsWith("flag_")) return 0f;
        if (isWall)
        {
            var facing = int.Parse(group[(group.LastIndexOf('_') + 1)..], CultureInfo.InvariantCulture);
            return facing switch { 2 => 180f, 3 => 0f, 4 => 90f, 5 => 270f, _ => 0f };
        }

        var segment = int.Parse(group["ground_".Length..], CultureInfo.InvariantCulture);
        var angle = segment * 22.5f;
        if (angle >= 180f) angle -= 360f;
        return angle;
    }

    private static Vector3 BodyFormula(BannerSampleReader.Row anchor, float angleDeg)
    {
        var radians = -angleDeg * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var dx = anchor.X - T.X;
        var dz = anchor.Z - T.Z;
        return new Vector3(dx * cos + dz * sin + T.X, anchor.Y, -dx * sin + dz * cos + T.Z);
    }

    private static Vector3 FlagFormula(BannerSampleReader.Row anchor, float phase, float pivotY, float pivotZ)
    {
        // world -> pixel: (anchor - T) * 16 / S (flags have zero outer rotation)
        var px = (anchor.X - T.X) * 16f / SX;
        var py = (anchor.Y - T.Y) * 16f / SY;
        var pz = (anchor.Z - T.Z) * 16f / SZ;
        // xRot delta about the source part pivot; source: BannerFlagModel.java:34
        //   xRot(p) = (-0.0125 + 0.01*cos(2*pi*p)) * pi
        float FlagXRot(float p) => (-0.0125f + 0.01f * MathF.Cos(2f * MathF.PI * p)) * MathF.PI;
        var xRot = FlagXRot(phase) - FlagXRot(0f);
        var cos = MathF.Cos(xRot);
        var sin = MathF.Sin(xRot);
        var ry = py - pivotY;
        var rz = pz - pivotZ;
        var nry = ry * cos - rz * sin;
        var nrz = ry * sin + rz * cos;
        var npx = px;
        var npy = nry + pivotY;
        var npz = nrz + pivotZ;
        return new Vector3(T.X + SX * npx / 16f, T.Y + SY * npy / 16f, T.Z + SZ * npz / 16f);
    }
}
