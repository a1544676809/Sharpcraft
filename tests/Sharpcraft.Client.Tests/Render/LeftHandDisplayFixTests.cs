// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The left-hand display fix, pinned against the reference maths.
/// <para>
/// The reference <c>LayerRenderState.applyTransform</c> calls
/// <c>itemTransform.apply(displayContext.leftHand(), pose)</c>, and <c>ItemTransform.apply</c> with a true flag negates
/// <c>translation.x</c> plus <c>rotation.y</c>/<c>rotation.z</c> on top of the per-context JSON entry. Skipping it leaves
/// offhand items unmirrored (a bow lies flat instead of upright).
/// </para>
/// <para>
/// Reference values: <c>tools/Sharpcraft.ItemRender.Audit/truth/BowLeftProbe.java</c> replays the bow's
/// <c>thirdperson_lefthand</c> entry — rotation <c>[-80, -280, 40]</c>, translation <c>[-1, -2, 2.5]</c> px, scale 1 —
/// through JOML with the fix applied.
/// </para>
/// </summary>
public sealed class LeftHandDisplayFixTests
{
    private static readonly ItemDisplayTransform BowLeftHand = new(
        new Vector3(-80f, -280f, 40f),
        new Vector3(-1f / 16f, -2f / 16f, 2.5f / 16f),
        Vector3.One);

    [Fact]
    public void TheLeftHandFixReproducesTheReferenceMatrix()
    {
        var actual = BowLeftHand.ToMatrix(applyLeftHandFix: true);

        var expected = new Matrix4x4(
            0.133022f, 0.631326f, 0.764024f, 0f,
            0.111619f, 0.756427f, -0.644483f, 0f,
            -0.984808f, 0.171010f, 0.030154f, 0f,
            0.432583f, -0.904382f, 0.081403f, 1f);

        foreach (var probe in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.5f, 0.5f, 0.5f) })
        {
            var want = Vector3.Transform(probe, expected);
            var got = Vector3.Transform(probe, actual);
            Assert.True((want - got).Length() < 1e-4f, $"{probe}: {Show(got)} expected {Show(want)}");
        }
    }

    [Fact]
    public void TheFixChangesTheTransform()
    {
        // Guard against the flag silently doing nothing.
        var fixedMatrix = BowLeftHand.ToMatrix(applyLeftHandFix: true);
        var plain = BowLeftHand.ToMatrix();
        Assert.NotEqual(plain.Translation, fixedMatrix.Translation);
        Assert.NotEqual(plain.M21, fixedMatrix.M21);
    }

    [Fact]
    public void TheOffHandLookupAppliesTheFix()
    {
        var resolution = new ItemModelResolution(
            "minecraft:item/bow",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.ThirdPersonLeftHand] = BowLeftHand
            });

        var offhand = EntityModelResolver.DisplayFor(resolution, offhand: true);
        var expected = BowLeftHand.ToMatrix(applyLeftHandFix: true);

        Assert.Equal(expected.Translation, offhand.Translation);
        Assert.Equal(expected.M21, offhand.M21, 5);
    }

    private static string Show(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:F5},{v.Y:F5},{v.Z:F5})");
}
