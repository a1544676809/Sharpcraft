// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Locates the STAB offset layer by layer against the reference world matrix captured with the shipped JOML
/// (tools/Sharpcraft.ItemRender.Audit/truth/StabProbe.java, <c>swingAnimation = 0.5</c>):
/// <code>
///   reference: [[-0.55, 0, 0], [0, 0.51569, -0.19122], [0, -0.19122, -0.51569]]  t = (-0.1, 0.77750, 0.30341)
///   attack = 1.0, retract = 0.004922, so the rotation is 69.655495 deg about X around the pivot (0, -0.125, 0.125)
/// </code>
/// Each assertion isolates one composition step, so a failure names the step instead of only the final point.
/// </summary>
public sealed class SpearStabOffsetTests
{
    private const float SwingAnimation = 0.5f;

    private static readonly Matrix4x4 Reference = new(
        -0.55f, 0f, 0f, 0f,
        0f, 0.51569f, -0.19122f, 0f,
        0f, -0.19122f, -0.51569f, 0f,
        -0.1f, 0.77750f, 0.30341f, 1f);

    [Fact]
    public void TheStabMatrixIsTheSolvedRotationAboutTheHandPivot()
    {
        var stab = SpearAnimations.ThirdPersonAttackItemMatrix(SwingAnimation, jetForward: 0f);

        // Solved from the reference world matrix: Rx(-deg) around (0, -0.125, 0.125).
        Assert.Equal(0.34770f, stab.M22, 4);
        Assert.Equal(-0.93760f, stab.M23, 4);
        Assert.Equal(0.93760f, stab.M32, 4);
        Assert.Equal(0.34770f, stab.M33, 4);
        Assert.Equal(0f, stab.Translation.X, 5);
        Assert.Equal(-0.19874f, stab.Translation.Y, 4);
        Assert.Equal(-0.03566f, stab.Translation.Z, 4);
    }

    [Fact]
    public void EveryLayerOfTheCompositionMatchesTheReference()
    {
        var stab = SpearAnimations.ThirdPersonAttackItemMatrix(SwingAnimation, jetForward: 0f);
        var display = Display();
        var anchor = ReferenceFrame.HeldItemAnchor(ReferenceFrame.AdultOffset, 0.9f);
        var hand = Hand();
        var target = Reference * ReferenceFrame.Frame(0.9f);

        // Layer by layer: the reference chain is D · STAB · anchor · H once each piece is converted.
        var composed = display * stab * anchor * hand;
        foreach (var probe in Probes())
        {
            var want = Vector3.Transform(probe, target);
            var got = Vector3.Transform(probe, composed);
            Assert.True(Close(want, got), $"{probe}: {Show(got)} expected {Show(want)}");
        }
    }

    private static ItemDisplayTransform Generated =>
        new(Vector3.Zero, new Vector3(0f, 3f / 16f, 1f / 16f), new Vector3(0.55f));

    private static Matrix4x4 Display() => Generated.ToMatrix();

    private static Matrix4x4 Hand() =>
        EntityModelMesher.PartWorldMatrix(
            EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId),
            Entity(),
            "right_arm",
            null);

    private static EntityRenderState Entity() =>
        new(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f);

    private static Vector3[] Probes() => [new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitX, Vector3.UnitY];

    private static bool Close(Vector3 a, Vector3 b) => (a - b).Length() < 1e-3f;

    private static string Show(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:F5},{v.Y:F5},{v.Z:F5})");
}
