// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Source truth for the humanoid held-hand frame (task-268): the 12-state jar probe
/// (HumanoidHeldHandProbe.java) composed through the production source adapter.
/// </summary>
public sealed class HumanoidHeldHandSourceFrameTests
{
    private static readonly string SamplePath = Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", "humanoid-held-hand-source-vectors.reference.txt");

    [Fact]
    public void RestAndUseSourceFramesMatchTheJarHandFrame()
    {
        var lines = File.ReadAllLines(SamplePath);
        var matrices = lines.Where(line => line.StartsWith("#MATRIX ", StringComparison.Ordinal))
            .Select(ParseMatrix).ToDictionary(entry => entry.State, entry => entry.Matrix);
        Assert.Equal(12, matrices.Count);

        var definition = EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId);
        var checkedStates = 0;
        foreach (var (state, expected) in matrices)
        {
            var parts = state.Split('_');
            var left = parts[0] == "LEFT";
            var motion = parts[1];
            var baby = parts[2] == "baby";
            checkedStates++;
            var frame = default(EntityAnimationFrame) with
            {
                TypeId = EntityPlaceholderMesher.PlayerEntityTypeId,
                IsBaby = baby,
                SwingAnimation = motion == "attack" ? 0.15f : 0f,
                SwingType = 0,
                IsUsingItem = motion == "use",
                TicksUsingItem = motion == "use" ? 10f : 0f,
                UseItemHand = left ? 1 : 0,
            };
            var pose = new EntityPose();
            HumanoidModelPose.Apply(
                frame,
                pose,
                HumanoidVariant.Plain,
                left ? HumanoidArmPose.Item : PoseFor(motion),
                left ? PoseFor(motion) : HumanoidArmPose.Item,
                isHoldingBow: false,
                rawSourceFrame: true);

            var source = HumanoidHeldHandFrame.Source(
                definition, left ? "left_arm" : "right_arm", pose, offhand: left, baby);
            if (motion == "attack")
            {
                // Sample attack matrix is STAB * Source (row order), per trace.
                source = SpearAnimations.ThirdPersonAttackItemMatrix(0.15f, 0f) * source;
            }

            AssertMatrix(expected, source, state);
        }

        Assert.Equal(12, checkedStates);
    }

    private static HumanoidArmPose PoseFor(string motion) => motion switch
    {
        "attack" => HumanoidArmPose.ThrowTrident,
        "use" => HumanoidArmPose.Spear,
        _ => HumanoidArmPose.Item,
    };

    private static (string State, Matrix4x4 Matrix) ParseMatrix(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var values = parts.Skip(2).Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(16, values.Length);
        // File is column-major JOML; System.Numerics is row-vector.
        return (parts[1], new Matrix4x4(
            values[0], values[1], values[2], 0f,
            values[4], values[5], values[6], 0f,
            values[8], values[9], values[10], 0f,
            values[12], values[13], values[14], values[15]));
    }

    private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual, string label)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var want = Get(expected, row, column);
                var got = Get(actual, row, column);
                Assert.True(MathF.Abs(want - got) < 1e-5f, $"{label}[{row},{column}]: expected {want}, got {got}");
            }
        }
    }

    private static float Get(Matrix4x4 matrix, int row, int column) => (row, column) switch
    {
        (0, 0) => matrix.M11, (0, 1) => matrix.M12, (0, 2) => matrix.M13, (0, 3) => matrix.M14,
        (1, 0) => matrix.M21, (1, 1) => matrix.M22, (1, 2) => matrix.M23, (1, 3) => matrix.M24,
        (2, 0) => matrix.M31, (2, 1) => matrix.M32, (2, 2) => matrix.M33, (2, 3) => matrix.M34,
        (3, 0) => matrix.M41, (3, 1) => matrix.M42, (3, 2) => matrix.M43, _ => matrix.M44,
    };
}
