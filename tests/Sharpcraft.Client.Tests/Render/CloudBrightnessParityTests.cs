// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Text.RegularExpressions;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Keeps the per-direction cloud brightness in the shader and in the explicit geometry builder the same (T-827).
/// </summary>
/// <remarks>
/// Once clouds are generated on the GPU the shader owns the numbers, but the classic path still uses the CPU table.
/// Two copies of the same six constants will drift the first time someone touches one of them, and the symptom would
/// be clouds that look subtly wrong in one mode and right in the other. Reading the shader source here is the same
/// approach the shader organisation gate already takes.
/// </remarks>
public sealed partial class CloudBrightnessParityTests
{
    [Fact]
    public void TheShaderUsesTheSameBrightnessPerDirectionAsTheGeometryBuilder()
    {
        var source = ReadShader();
        var numbers = ParseBrightness(source);

        Assert.Equal(CloudGeometryBuilder.FaceBrightness.Length, numbers.Length);
        for (var index = 0; index < numbers.Length; index++)
        {
            Assert.Equal(CloudGeometryBuilder.FaceBrightness[index], numbers[index], 5);
        }
    }

    [Fact]
    public void TheShaderStillHasSixDirectionsAndTheQuadOrderIsSixVerticesPerFace()
    {
        var source = ReadShader();

        // A triangle list needs two triangles per quad, so the shader must be told six vertices and never four. This
        // caught the first attempt, which drew a sky full of triangles connecting unrelated faces.
        var quadOrder = QuadOrderPattern().Match(source);
        Assert.True(quadOrder.Success, "the shader no longer declares a quad order");
        Assert.Equal(6, quadOrder.Groups[1].Value.Split(',').Length);

        Assert.Matches(@"vertexIndex % 6u", source);
        Assert.Matches(@"vertexIndex / 6u", source);
    }

    private static string ReadShader()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sharpcraft.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(
            directory.FullName,
            "src",
            "Sharpcraft.Framework",
            "Rendering",
            "Shaders",
            "pipelines",
            "cloud_oit.wgsl");

        Assert.True(File.Exists(path), $"cloud shader not found at {path}");
        return File.ReadAllText(path);
    }

    private static float[] ParseBrightness(string source)
    {
        var match = BrightnessPattern().Match(source);
        Assert.True(match.Success, "the shader no longer declares a brightness table");

        return
        [
            .. match.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => float.Parse(value, CultureInfo.InvariantCulture))
        ];
    }

    [GeneratedRegex(@"const BRIGHTNESS = array<f32, \d+>\(([^)]*)\)")]
    private static partial Regex BrightnessPattern();

    [GeneratedRegex(@"const QUAD_ORDER = array<i32, \d+>\(([^)]*)\)")]
    private static partial Regex QuadOrderPattern();
}
