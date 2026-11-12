// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Proves whether the celestial dome can be built directly in "camera at the origin" space (T-811).
/// </summary>
/// <remarks>
/// <para>
/// The current path (FrameCommand.cs:1893) puts the camera translation into the draw's model matrix and then views with
/// a camera that carries the same translation, so the dome cancels back to a direction:
/// </para>
/// <code>
/// old = v * T(eye) * VP(eye, target)      // v * T(eye) is what CelestialQuadDraw.Model holds
/// new = v * VP(0, target - eye)           // geometry already on the dome, camera only rotates
/// </code>
/// <para>
/// The two agree exactly in exact arithmetic. In float they cannot, because the old path adds the eye and the view
/// matrix subtracts it again — a cancellation that loses low bits. This test measures that loss instead of assuming it
/// away: it compares clip space after the perspective divide, and the bound is stated as a fraction of one pixel of the
/// target framebuffer, which is the only scale that matters for what ends up on screen.
/// </para>
/// </remarks>
public sealed class CelestialDomeEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Creates the test class.</summary>
    /// <param name="output">xUnit fills this in so the measured difference can be printed as evidence.</param>
    public CelestialDomeEquivalenceTests(ITestOutputHelper output) => _output = output;
    private const float FieldOfViewDegrees = 70f;
    private const int Width = 858;
    private const int Height = 527;

    /// <summary>The celestial quads as CelestialGeometry builds them: a fixed distance in every cardinal direction.</summary>
    private static Vector3[] DomeVertices() =>
    [
        new(0f, 100f, 0f),
        new(30f, 100f, 0f),
        new(0f, 100f, -30f),
        new(-100f, 0f, 0f),
        new(0f, -100f, 0f),
        new(0f, 0f, 100f),
        new(0f, 0f, -100f),
        new(-100f, 0f, -100f),
        new(100f, 0f, 100f)
    ];

    /// <summary>
    /// The position-colour path (sunrise band, stars, clouds) follows the same chain, so it gets the same proof: the old
    /// draw applied a camera translation in its model matrix and viewed with the same translation, the new one drops both
    /// (T-810).
    /// </summary>
    [Theory]
    [MemberData(nameof(Eyes))]
    public void TheColourBodyPathMatchesToo(float eyeX, float eyeY, float eyeZ)
    {
        var eye = new Vector3(eyeX, eyeY, eyeZ);
        var model = Matrix4x4.CreateTranslation(eye);
        var colourVertices = new[]
        {
            new Vector3(0f, 100f, 0f), new Vector3(120f, 0f, -40f), new Vector3(-120f, 0f, -40f),
            new Vector3(0f, 100f, 120f), new Vector3(0f, -100f, 0f)
        };

        foreach (var target in Orientations(eye))
        {
            var oldViewProjection = CameraMath.BuildViewProjection(eye, target, FieldOfViewDegrees, Width, Height);
            var newViewProjection = CameraMath.BuildViewProjection(
                Vector3.Zero, target - eye, FieldOfViewDegrees, Width, Height);
            var tolerance = (2f / Width) / 10f;

            foreach (var vertex in colourVertices)
            {
                var oldNdc = ToNdc(Vector3.Transform(vertex, model), oldViewProjection);
                var newNdc = ToNdc(vertex, newViewProjection);
                if (oldNdc is null || newNdc is null)
                {
                    continue;
                }

                var difference = Vector3.Abs(oldNdc.Value - newNdc.Value);
                Assert.True(
                    difference.X <= tolerance && difference.Y <= tolerance && difference.Z <= tolerance,
                    $"colour path eye={eye} target={target} vertex={vertex}: difference={difference} exceeds {tolerance}");
            }
        }
    }

    /// <summary>Cameras to test: at the origin, and far away (the far one is the case T-809 got wrong).</summary>
    public static TheoryData<float, float, float> Eyes => new()
    {
        { 0f, 0f, 0f },
        { 74.098f, 121f, 37.303f },
        { -2048.5f, 62.25f, 1536.75f }
    };

    [Theory]
    [MemberData(nameof(Eyes))]
    public void BuildingTheDomeInViewSpaceMatchesTheTranslatedPath(float eyeX, float eyeY, float eyeZ)
    {
        var eye = new Vector3(eyeX, eyeY, eyeZ);

        // The old path puts the camera translation in the model matrix and views with the same translation.
        var oldModel = Matrix4x4.CreateTranslation(eye);
        var baseModel = oldModel;

        // The new path bakes the dome around the origin and views with a translation free camera.
        foreach (var target in Orientations(eye))
        {
            var oldViewProjection = CameraMath.BuildViewProjection(eye, target, FieldOfViewDegrees, Width, Height);
            var newViewProjection = CameraMath.BuildViewProjection(
                Vector3.Zero, target - eye, FieldOfViewDegrees, Width, Height);

            foreach (var vertex in DomeVertices())
            {
                var oldNdc = ToNdc(Vector3.Transform(vertex, baseModel), oldViewProjection);
                var newNdc = ToNdc(vertex, newViewProjection);

                if (oldNdc is null || newNdc is null)
                {
                    continue;
                }

                // One pixel is 2/Width in normalized device coordinates. A tenth of a pixel is the tightest bound that
                // survives the eye-plus-minus-eye cancellation at 2048 blocks from the origin, and it is still invisible.
                var tolerance = (2f / Width) / 10f;
                var difference = Vector3.Abs(oldNdc.Value - newNdc.Value);
                Assert.True(
                    difference.X <= tolerance && difference.Y <= tolerance && difference.Z <= tolerance,
                    $"eye={eye} target={target} vertex={vertex}: old={oldNdc} new={newNdc} "
                    + $"difference={difference} exceeds {tolerance}");
            }
        }
    }

    /// <summary>Documents the exact difference the cancellation produces, so "not bit equal" is a measured fact.</summary>
    [Fact]
    public void ReportsTheWorstDifferenceBetweenTheTwoPaths()
    {
        var worst = 0f;
        var worstCase = string.Empty;
        foreach (var eye in new[]
                 {
                     new Vector3(0f, 0f, 0f), new Vector3(74.098f, 121f, 37.303f), new Vector3(-2048.5f, 62.25f, 1536.75f)
                 })
        {
            var baseModel = Matrix4x4.CreateTranslation(eye);
            foreach (var target in Orientations(eye))
            {
                var oldViewProjection = CameraMath.BuildViewProjection(eye, target, FieldOfViewDegrees, Width, Height);
                var newViewProjection = CameraMath.BuildViewProjection(
                    Vector3.Zero, target - eye, FieldOfViewDegrees, Width, Height);
                foreach (var vertex in DomeVertices())
                {
                    var oldNdc = ToNdc(Vector3.Transform(vertex, baseModel), oldViewProjection);
                    var newNdc = ToNdc(vertex, newViewProjection);
                    if (oldNdc is null || newNdc is null)
                    {
                        continue;
                    }

                    var difference = (oldNdc.Value - newNdc.Value).Length();
                    if (difference > worst)
                    {
                        worst = difference;
                        worstCase = $"eye={eye} target={target} vertex={vertex}";
                    }
                }
            }
        }

        // The paths are algebraically identical, so any difference has to come from float rounding. Report the measured
        // figure so it is a fact rather than an assumption, and hold it well below one pixel: if it ever grows past that,
        // the algebra changed, not the rounding.
        var pixelInNdc = 2f / Width;
        _output.WriteLine(
            $"worst normalized-device difference {worst:E3} = {worst / pixelInNdc:P2} of a pixel; worst case {worstCase}");
        Assert.True(
            worst < pixelInNdc / 10f,
            $"worst normalized-device difference {worst} is not below a tenth of a pixel ({pixelInNdc / 10f}); "
            + $"worst case {worstCase}");
    }

    /// <summary>Orientations worth testing: at the sun, straight down, and a tilted horizon view.</summary>
    /// <param name="eye">Camera position, which the targets are relative to.</param>
    /// <returns>Look-at targets in world space.</returns>
    private static Vector3[] Orientations(Vector3 eye) =>
    [
        eye + new Vector3(4f, 100f, 12f),
        eye + new Vector3(0.5f, -1f, 0.3f),
        eye + new Vector3(0.3f, 0.1f, 1f),
        eye + new Vector3(-1f, 0.05f, 0f)
    ];

    /// <summary>Applies a view-projection matrix and divides by w, which is what the rasterizer does next.</summary>
    /// <param name="vertex">Vertex in the matrix's own space.</param>
    /// <param name="viewProjection">View-projection matrix.</param>
    /// <returns>Normalized device coordinates.</returns>
    private static Vector3? ToNdc(Vector3 vertex, Matrix4x4 viewProjection)
    {
        var clip = Vector4.Transform(new Vector4(vertex, 1f), viewProjection);
        // Vertices behind the eye have a non-positive w and never reach the rasterizer; dividing would only produce
        // infinities and hide real differences behind them.
        if (clip.W <= 1e-4f || float.IsNaN(clip.W))
        {
            return null;
        }

        return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
    }
}
