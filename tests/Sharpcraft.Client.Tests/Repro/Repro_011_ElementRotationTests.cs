// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 11: element rotation with <c>rescale</c> has to match the reference transform rather than
/// the plain rotation, which shrinks the element. The expected values are the 4x4 matrices the
/// vanilla client builds, sampled from the 26.3 jar.
/// </summary>
public sealed class Repro_011_ElementRotationTests
{
    private static readonly Vector3 Origin = new(0.5f, 0.5f, 0.5f);

    private static readonly Vector3[] Samples =
    [
        new(1f, 0f, 0f),
        new(0f, 1f, 0f),
        new(0f, 0f, 1f),
        new(2f, 3f, 5f),
        new(-1.5f, 0.25f, 4f)
    ];

    public static TheoryData<string, float, string, bool> Cases()
    {
        var data = new TheoryData<string, float, string, bool>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "element-rotation-vectors.golden.txt")))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts[0] != "rotate")
            {
                continue;
            }

            data.Add($"{parts[1]}|{parts[2]}", float.Parse(parts[2], CultureInfo.InvariantCulture), parts[1], parts[3] == "rescale");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheRescaledRotationMatchesTheReferenceMatrix(string key, float angle, string axis, bool rescale)
    {
        var matrix = GoldenMatrix("rotate|" + key + "|" + (rescale ? "rescale" : "plain"));

        foreach (var sample in Samples)
        {
            var expected = Vector3.TransformNormal(sample, matrix);
            var actual = ElementRotation.Apply(Origin + sample, Origin, axis, angle, rescale) - Origin;

            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Y, actual.Y, 3);
            Assert.Equal(expected.Z, actual.Z, 3);
        }
    }

    [Fact]
    public void RescalingIsWhatStopsTheElementShrinking()
    {
        var sample = new Vector3(1f, 0f, 0f);
        var plain = ElementRotation.Apply(Origin + sample, Origin, "y", 45f, rescale: false) - Origin;
        var rescaled = ElementRotation.Apply(Origin + sample, Origin, "y", 45f, rescale: true) - Origin;

        Assert.Equal(0.70710678f, plain.X, 4);
        Assert.Equal(1f, rescaled.X, 4);
        Assert.Equal(1f, rescaled.Z, 4);
    }

    private static Matrix4x4 GoldenMatrix(string key)
    {
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "element-rotation-vectors.golden.txt")))
        {
            if (!line.StartsWith(key + "|", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            var m = new float[16];
            for (var i = 0; i < 16; i++)
            {
                m[i] = float.Parse(parts[i + 4], CultureInfo.InvariantCulture);
            }

            // Row major in the file; System.Numerics is row vector (translation in M41..M43), so the
            // linear part is the transpose of the reference row-major matrix.
            return new Matrix4x4(
                m[0], m[4], m[8], 0f,
                m[1], m[5], m[9], 0f,
                m[2], m[6], m[10], 0f,
                0f, 0f, 0f, 1f);
        }

        throw new InvalidOperationException("no golden matrix for " + key);
    }
}
