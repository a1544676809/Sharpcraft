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
/// Copper golem statue: our world vertices against the reference sample converted to world space.
/// </summary>
/// <remarks>
/// The reference harness records model space, where the block origin sits at the top of the block and
/// y grows downwards, so a model vertex maps to <c>1.5 - y</c> in world space (the block top is 1.5). Comparing the two frames
/// directly was what made an equivalent renderer look broken.
/// </remarks>
public sealed class CopperGolemStatueGoldenTests
{
    private static readonly Dictionary<string, CopperGolemPose> Poses = new(StringComparer.Ordinal)
    {
        ["standing"] = CopperGolemPose.Standing,
        ["running"] = CopperGolemPose.Running,
        ["sitting"] = CopperGolemPose.Sitting,
        ["star"] = CopperGolemPose.Star
    };

    private static readonly Dictionary<string, byte> Facings = new(StringComparer.Ordinal)
    {
        ["down"] = 0,
        ["up"] = 1,
        ["north"] = 2,
        ["south"] = 3,
        ["west"] = 4,
        ["east"] = 5
    };

    [Theory]
    [InlineData("standing", "north")]
    [InlineData("standing", "east")]
    [InlineData("standing", "down")]
    [InlineData("sitting", "up")]
    [InlineData("star", "north")]
    public void TheStatueMatchesTheReferenceInWorldSpace(string pose, string facing)
    {
        var expected = ReferenceWorld(pose, facing);
        var actual = Positions(Build(Poses[pose], Facings[facing]));

        Assert.Equal(expected.Length, actual.Length);
        var worst = 0f;
        for (var index = 0; index < expected.Length; index++)
        {
            worst = MathF.Max(worst, MathF.Abs(expected[index].Y - actual[index].Y));
            worst = MathF.Max(worst, MathF.Abs(expected[index].X - actual[index].X));
            worst = MathF.Max(worst, MathF.Abs(expected[index].Z - actual[index].Z));
        }

        Assert.True(worst <= 0.001f, $"{pose}/{facing}: worst vertex error {worst}");
    }

    private static (float X, float Y, float Z)[] Positions(DynamicGeometry geometry)
    {
        var result = new List<(float X, float Y, float Z)>(geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var vertex = 0; vertex + 2 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            result.Add((
                MathF.Round(geometry.Vertices[vertex], 4),
                MathF.Round(geometry.Vertices[vertex + 1], 4),
                MathF.Round(geometry.Vertices[vertex + 2], 4)));
        }

        result.Sort();
        return [.. result];
    }

    private static (float X, float Y, float Z)[] ReferenceWorld(string pose, string facing)
    {
        var prefix = "statue|" + pose + "|" + facing + "|";
        var result = new List<(float, float, float)>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "copper-golem-statue-vectors.golden.txt")))
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            var x = float.Parse(parts[3], CultureInfo.InvariantCulture);
            var y = float.Parse(parts[4], CultureInfo.InvariantCulture);
            var z = float.Parse(parts[5], CultureInfo.InvariantCulture);
            result.Add((MathF.Round(x, 4), MathF.Round(1.5f - y, 4), MathF.Round(z, 4)));
        }

        result.Sort();
        return [.. result];
    }

    private static DynamicGeometry Build(CopperGolemPose pose, byte facing)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in ChestAtlas.CopperGolemStatueTextures)
        {
            textures[path] = new TextureData(256, 256, new byte[256 * 256 * 4]);
        }

        return new CopperGolemStatueMesher().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, Pose: pose)],
            ItemAtlas.FromTextures(textures));
    }
}
