// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Held-item hand frame for raw humanoids (#120/#134). Raw parts keep source pivots, but the fitted
/// held-item anchor was solved against the legacy baked hand frame; <see cref="EntityModelMesher.LegacyHandWorldMatrix"/>
/// replays the legacy root-pivot conjugation for the item-in-hand caller only.
/// </summary>
public sealed class LegacyHandFrameTests
{
    private static readonly EntityRenderState ZombieState =
        new(1, HumanoidMobModelDefinition.ZombieEntityTypeId, 0f, 0f, 0f, 0f, 0f);

    [Theory]
    [InlineData("ZombieMesh")]
    [InlineData("SkeletonMesh")]
    public void RawZombieAndSkeletonHandFrameMatchesTheLegacyBake(string meshField)
    {
        var type = typeof(HumanoidMobModelDefinition);
        var mesh = (VanillaMeshDefinition)type
            .GetField(meshField, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var raw = EntityModelDefinition.FromTree(
            "raw_" + meshField, 64, 64, mesh, texturePath: "probe/hand", baselinePx: 0f, rawSourceFrame: true);
        var legacy = EntityModelDefinition.FromTree(
            "legacy_" + meshField, 64, 64, mesh, texturePath: "probe/hand", baselinePx: 24f);

        foreach (var pose in new[] { null, RaisedArm() })
        {
            var rawHand = EntityModelMesher.LegacyHandWorldMatrix(raw, ZombieState, "right_arm", pose);
            var legacyHand = EntityModelMesher.PartWorldMatrix(legacy, ZombieState, "right_arm", pose);
            AssertMatrixEqual(legacyHand, rawHand, $"{meshField} pose={(pose is null ? "rest" : "raised")}");
        }

        // Jar hand origin for the adult right arm, after the legacy bake and model scale 0.9.
        var rest = EntityModelMesher.LegacyHandWorldMatrix(raw, ZombieState, "right_arm");
        Assert.Equal(0.28125f, rest.Translation.X, 4);
        Assert.Equal(1.2375f, rest.Translation.Y, 4);
        Assert.Equal(0f, rest.Translation.Z, 4);
    }

    [Fact]
    public void RawPlayerHandFrameMatchesTheLegacyBake()
    {
        var mesh = PlayerModelDefinition.AdultMeshDefinition;
        var raw = EntityModelDefinition.FromTree(
            "raw_player", 64, 64, mesh, texturePath: "probe/hand", baselinePx: 0f, rawSourceFrame: true);
        var legacy = EntityModelDefinition.FromTree(
            "legacy_player", 64, 64, mesh, texturePath: "probe/hand", baselinePx: 24f);
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);

        foreach (var pose in new[] { null, RaisedArm() })
        {
            var rawHand = EntityModelMesher.LegacyHandWorldMatrix(raw, state, "right_arm", pose);
            var legacyHand = EntityModelMesher.PartWorldMatrix(legacy, state, "right_arm", pose);
            AssertMatrixEqual(legacyHand, rawHand, pose is null ? "player rest" : "player raised");
        }
    }

    [Fact]
    public void RawChildArmFallsBackToTheExistingFrame()
    {
        // CopperGolem's arm is a child of body; its existing source-pivot frame is the jar-verified one
        // (HeldItemHandFrameProbe origin (-0.25,0.8125,0) * 0.9), so the helper must not rewrite it.
        var raw = CopperGolemModelDefinition.Definition;
        Assert.True(raw.RawSourceFrame);
        var state = new EntityRenderState(1, CopperGolemModelDefinition.CopperGolemEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var expected = EntityModelMesher.PartWorldMatrix(raw, state, "right_arm", rawSourceFrame: false);
        var actual = EntityModelMesher.LegacyHandWorldMatrix(raw, state, "right_arm");

        AssertMatrixEqual(expected, actual, "copper golem child arm");
        Assert.Equal(-0.225f, actual.Translation.X, 5);
        Assert.True(MathF.Abs(0.73125f - actual.Translation.Y) < 1e-5f,
            $"expected y 0.73125, actual {actual.Translation.Y:F8}");
    }

    private static EntityPose RaisedArm()
    {
        var pose = new EntityPose();
        pose.Set("right_arm", new PoseOffset(Vector3.Zero, new Vector3(-MathF.PI / 2f, 0f, 0f), Vector3.Zero));
        return pose;
    }

    private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual, string label)
    {
        Assert.True(
            MathF.Abs(expected.M11 - actual.M11) < 1e-5f
                && MathF.Abs(expected.M12 - actual.M12) < 1e-5f
                && MathF.Abs(expected.M13 - actual.M13) < 1e-5f
                && MathF.Abs(expected.M21 - actual.M21) < 1e-5f
                && MathF.Abs(expected.M22 - actual.M22) < 1e-5f
                && MathF.Abs(expected.M23 - actual.M23) < 1e-5f
                && MathF.Abs(expected.M31 - actual.M31) < 1e-5f
                && MathF.Abs(expected.M32 - actual.M32) < 1e-5f
                && MathF.Abs(expected.M33 - actual.M33) < 1e-5f
                && MathF.Abs(expected.Translation.X - actual.Translation.X) < 1e-5f
                && MathF.Abs(expected.Translation.Y - actual.Translation.Y) < 1e-5f
                && MathF.Abs(expected.Translation.Z - actual.Translation.Z) < 1e-5f,
            $"{label}: expected {expected}, actual {actual}");
    }
}
