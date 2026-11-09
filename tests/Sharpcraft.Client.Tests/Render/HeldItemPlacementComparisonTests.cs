// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pins the held-item placement against reference world matrices captured from the shipped JOML build
/// (tools/Sharpcraft.ItemRender.Audit/truth/PoseVariantProbe.java), for two arm poses and two item display transforms.
/// <para>
/// The four cases are what makes the check meaningful: a single anchor has to satisfy all of them at once, so an anchor
/// fitted to one display transform (or one pose) fails here even though it looks right for that case alone.
/// </para>
/// </summary>
public sealed class HeldItemPlacementComparisonTests
{
    private static readonly ItemDisplayTransform GeneratedDisplay =
        new(Vector3.Zero, new Vector3(0f, 3f / 16f, 1f / 16f), new Vector3(0.55f));

    // Shield thirdperson_righthand: rotation [0, 90, 0], translation [10, 6, -4] pixels, scale 1.
    private static readonly ItemDisplayTransform ShieldDisplay =
        new(new Vector3(0f, 90f, 0f), new Vector3(10f / 16f, 6f / 16f, -4f / 16f), Vector3.One);

    private static readonly Vector3 ReferencePivot = new(-5f / 16f, 2f / 16f, 0f);

    [Fact]
    public void TheFrameMapCarriesTheReferencePivotOntoOurHandFrame()
    {
        var hand = Hand(pose: null);
        var expected = ReferenceFrame.ToSharpcraft(ReferencePivot, 0.9f);

        Assert.True(Close(expected, hand.Translation), $"{Show(hand.Translation)} expected {Show(expected)}");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TheHeldItemMatchesTheReferenceForEveryPoseAndDisplay(bool raised, bool shield)
    {
        var pose = raised ? RaisedArm() : null;
        var display = shield ? ShieldDisplay : GeneratedDisplay;
        var reference = Reference(raised, shield);

        var world = EntityModelResolver.AnchorFor(Model(display), offhand: false, baby: false) * Hand(pose);
        var expected = reference * ReferenceFrame.Frame(0.9f);

        foreach (var probe in new[] { new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            var want = Vector3.Transform(probe, expected);
            var got = Vector3.Transform(probe, world);
            Assert.True(Close(want, got), $"{probe}: {Show(got)} expected {Show(want)}");
        }
    }

    /// <summary>Reference world matrices captured from JOML for the adult right hand.</summary>
    private static Matrix4x4 Reference(bool raised, bool shield) => (raised, shield) switch
    {
        (false, false) => new Matrix4x4(-0.55f, 0f, 0f, 0f, 0f, 0f, -0.55f, 0f, 0f, -0.55f, 0f, 0f, -0.1f, 0.9625f, -0.0375f, 1f),
        (false, true) => new Matrix4x4(0f, 1f, 0f, 0f, 0f, 0f, -1f, 0f, -1f, 0f, 0f, 0f, -0.5f, 0.5f, 0f, 1f),
        (true, false) => new Matrix4x4(-0.55f, 0f, 0f, 0f, 0f, -0.55f, 0f, 0f, 0f, 0f, 0.55f, 0f, -0.1f, 0.0875f, -0.8375f, 1f),
        _ => new Matrix4x4(0f, 0f, -1f, 0f, 0f, -1f, 0f, 0f, -1f, 0f, 0f, 0f, -0.5f, 0.125f, -0.375f, 1f),
    };

    private static EntityPose RaisedArm()
    {
        var pose = new EntityPose();
        pose.Set("right_arm", new PoseOffset(Vector3.Zero, new Vector3(-MathF.PI / 2f, 0f, 0f), Vector3.Zero));
        return pose;
    }

    private static Matrix4x4 Hand(EntityPose? pose) =>
        EntityModelMesher.PartWorldMatrix(Definition(), Entity(), "right_arm", pose);

    private static EntityModelDefinition Definition() =>
        EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);

    private static EntityRenderState Entity() =>
        new(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f);

    private static ItemModelResolution Model(ItemDisplayTransform display) =>
        new(
            "minecraft:item/probe",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.ThirdPersonRightHand] = display
            });

    private static bool Close(Vector3 a, Vector3 b) => (a - b).Length() < 1e-3f;

    private static string Show(Vector3 v) =>
        string.Create(CultureInfo.InvariantCulture, $"({v.X:F5},{v.Y:F5},{v.Z:F5})");
}
