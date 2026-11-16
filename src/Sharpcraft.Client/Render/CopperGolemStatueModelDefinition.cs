// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Common.Reference;

/// <summary>Reference <c>CopperGolemStatueBlock.Pose</c> ordinal order.</summary>
/// <remarks>
/// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/CopperGolemStatueBlock.java:174-196</c>.
/// The four ordinals are also the block-state <c>pose</c> names: standing, sitting, running, star.
/// </remarks>
[ReferenceClass("net.minecraft.world.level.block.CopperGolemStatueBlock.Pose", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/CopperGolemStatueBlock.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Full)]
public enum CopperGolemPose : byte
{
    Standing = 0,
    Sitting = 1,
    Running = 2,
    Star = 3
}

/// <summary>
/// The four CopperGolemStatue model layers. Each definition supplies the same 64 by 64 texture and
/// its pose-specific part tree; <c>CopperGolemStatueMesher</c> applies the statue root pose and the
/// oxidation sprite on top.
/// </summary>
/// <remarks>
/// Sources:
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java:54-95</c>
/// (standing), <c>:97-145</c> (running), <c>:146-206</c> (sitting), <c>:207-246</c> (star), and
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/statue/CopperGolemStatueModel.java:13-20</c>
/// (<c>root.y = 0</c>, <c>root.zRot = π</c>).
    /// <para>
    /// Part offsets are the source values verbatim, with no model height baked in: the statue asks
    /// <see cref="EntityModelDefinition"/> for <c>modelHeight: 0</c>. The source applies
    /// <c>root.zRot = PI</c> and zeroes the root pivot, which nets out to the y-down to y-up flip the
    /// bakery already performs, so nothing of it is repeated here. Writing it again renders the statue
    /// upside down, and the mesh pipeline never reads a root rotation from a definition anyway: it
    /// takes the root frame from the entity pose.
    /// </para>
/// </remarks>
[ReferenceClass("net.minecraft.client.model.animal.golem.CopperGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
public static class CopperGolemStatueModelDefinition
{
    public const int TextureWidth = 64;
    public const int TextureHeight = 64;

    /// <summary>Standing layer, built from the source createBodyLayer values with no model height.</summary>
    public static readonly EntityModelDefinition Standing = Create("copper_golem_statue_standing", BuildStandingMesh());

    public static readonly EntityModelDefinition Sitting = Create("copper_golem_statue_sitting", BuildSittingMesh());
    public static readonly EntityModelDefinition Running = Create("copper_golem_statue_running", BuildRunningMesh());
    public static readonly EntityModelDefinition Star = Create("copper_golem_statue_star", BuildStarMesh());

    /// <summary>Definitions in reference <see cref="CopperGolemPose"/> ordinal order.</summary>
    public static readonly IReadOnlyList<EntityModelDefinition> ByPose = [Standing, Sitting, Running, Star];

    /// <summary>Returns the definition for one pose, defaulting invalid values to standing.</summary>
    public static EntityModelDefinition ForPose(CopperGolemPose pose) =>
        (uint)pose < (uint)ByPose.Count ? ByPose[(int)pose] : Standing;

    private static EntityModelDefinition Create(string name, VanillaMeshDefinition mesh)
    {
        var source = EntityModelDefinition.FromTree(
            name,
            TextureWidth,
            TextureHeight,
            mesh,
            PlayerModelDefinition.DefaultFaceTransforms,
            CopperGolemModelDefinition.Definition.TexturePath,
            emissiveTexturePath: CopperGolemModelDefinition.Definition.EmissiveTexturePath,
            additionalTexturePaths: CopperGolemModelDefinition.Definition.AdditionalTexturePaths,
            modelHeight: 0f);
        return WithRootPose(source, name);
    }

    private static EntityModelDefinition WithRootPose(EntityModelDefinition source, string name) =>
        new(
            name,
            source.TextureWidth,
            source.TextureHeight,
            source.Parts,
            source.FaceTransforms,
            source.TexturePath,
            source.Scale,
            source.EmissiveTexturePath,
            source.TextureSelector,
            source.AdditionalTexturePaths,
            source.Compiled,
            source.RenderLayers,
            // Kept for the compiled-root path even though the statue pose is all zeros; the
            // mesh pipeline reads the root frame from the entity pose, so this changes nothing.
            applyRootPose: true);

    /// <summary>
    /// Standing layer. Ref: CopperGolemModel.createBodyLayer lines 53-95, plus the statue pose that
    /// CopperGolemStatueModel.setupAnim applies to the model itself: <c>root.y = 0</c> and
    /// <c>root.zRot = PI</c>. That flip is what the shared y-up bakery already performs, so the root
    /// part below carries no rotation and the renderer adds no pose of its own.
    /// </summary>
    private static VanillaMeshDefinition BuildStandingMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild(
            "body",
            VanillaPartPose.Offset(0f, -5f, 0f),
            Cube("body", 0, 15, -4f, -6f, -3f, 8f, 6f, 6f));
        body.AddOrReplaceChild(
            "head",
            VanillaPartPose.Offset(0f, -6f, 0f),
            Cube("head", 0, 0, -4f, -5f, -5f, 8f, 5f, 10f, Grow: 0.015f),
            Cube("nose", 56, 0, -1f, -2f, -6f, 2f, 3f, 2f),
            Cube("antenna", 37, 8, -1f, -9f, -1f, 2f, 4f, 2f, Grow: -0.015f),
            Cube("antenna_tip", 37, 0, -2f, -13f, -2f, 4f, 4f, 4f, Grow: -0.015f));
        body.AddOrReplaceChild(
            "right_arm",
            VanillaPartPose.Offset(-4f, -6f, 0f),
            Cube("right_arm", 36, 16, -3f, -1f, -2f, 3f, 10f, 4f));
        body.AddOrReplaceChild(
            "left_arm",
            VanillaPartPose.Offset(4f, -6f, 0f),
            Cube("left_arm", 50, 16, 0f, -1f, -2f, 3f, 10f, 4f));
        root.AddOrReplaceChild(
            "right_leg",
            VanillaPartPose.Offset(0f, -5f, 0f),
            Cube("right_leg", 0, 27, -4f, 0f, -2f, 4f, 5f, 4f));
        root.AddOrReplaceChild(
            "left_leg",
            VanillaPartPose.Offset(0f, -5f, 0f),
            Cube("left_leg", 16, 27, 0f, 0f, -2f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>
    /// Running layer. Ref: CopperGolemModel.createRunningPoseBodyLayer lines 97-145. Every offset,
    /// rotation and cube UV below is the source PartPose/texOffs value in model pixels.
    /// </summary>
    private static VanillaMeshDefinition BuildRunningMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(-1.064f, -5f, 0f));
        body.AddOrReplaceChild(
            "body_r1",
            VanillaPartPose.OffsetAndRotation(1.1f, 0.1f, 0.7f, 0.1204f, -0.0064f, -0.0779f),
            Cube("body_r1", 0, 15, -4.02f, -6.116f, -3.5f, 8f, 6f, 6f));
        body.AddOrReplaceChild(
            "head",
            VanillaPartPose.Offset(0.7f, -5.6f, -1.8f),
            Cube("head", 0, 0, -4f, -5.1f, -5f, 8f, 5f, 10f),
            Cube("nose", 56, 0, -1.02f, -2.1f, -6f, 2f, 3f, 2f),
            Cube("antenna", 37, 8, -1.02f, -9.1f, -1f, 2f, 4f, 2f, Grow: -0.015f),
            Cube("antenna_tip", 37, 0, -2f, -13.1f, -2f, 4f, 4f, 4f, Grow: -0.015f));
        var rightArm = body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, -6f, 0f));
        rightArm.AddOrReplaceChild(
            "right_arm_r1",
            VanillaPartPose.OffsetAndRotation(0.7f, -0.248f, -1.62f, 1.0036f, 0f, 0f),
            Cube("right_arm_r1", 36, 16, -3.052f, -1.11f, -2.036f, 3f, 10f, 4f));
        var leftArm = body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, -6f, 0f));
        leftArm.AddOrReplaceChild(
            "left_arm_r1",
            VanillaPartPose.OffsetAndRotation(0.732f, 0f, 0f, -0.8715f, -0.0535f, -0.0449f),
            Cube("left_arm_r1", 50, 16, 0.032f, -1.1f, -2f, 3f, 10f, 4f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-3.064f, -5f, 0f));
        rightLeg.AddOrReplaceChild(
            "right_leg_r1",
            VanillaPartPose.OffsetAndRotation(1.048f, 0f, -0.9f, -0.8727f, 0f, 0f),
            Cube("right_leg_r1", 0, 27, -1.856f, -0.1f, -1.09f, 4f, 5f, 4f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(0.936f, -5f, 0f));
        leftLeg.AddOrReplaceChild(
            "left_leg_r1",
            VanillaPartPose.OffsetAndRotation(1f, 0f, 0f, 0.7854f, 0f, 0f),
            Cube("left_leg_r1", 16, 27, -2.088f, -0.1f, -2f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>
    /// Sitting layer. Ref: CopperGolemModel.createSittingPoseBodyLayer lines 146-206.
    /// </summary>
    private static VanillaMeshDefinition BuildSittingMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild(
            "body",
            VanillaPartPose.Offset(0f, -3f, 2.325f),
            Cube("body", 3, 19, -3f, -4f, -4.525f, 6f, 1f, 6f),
            Cube("body_main", 0, 15, -4f, -3f, -3.525f, 8f, 6f, 6f));
        body.AddOrReplaceChild(
            "body_r1",
            VanillaPartPose.OffsetAndRotation(0f, -1f, -4.325f, 0f, 0f, -3.1416f),
            Cube("body_r1", 3, 18, -4f, -3f, -2.2f, 8f, 6f, 3f));
        body.AddOrReplaceChild(
            "head",
            VanillaPartPose.Offset(0f, -6f, -0.2f),
            Cube("head", 37, 8, -1f, -7f, -3.3f, 2f, 4f, 2f, Grow: -0.015f),
            Cube("antenna_tip", 37, 0, -2f, -11f, -4.3f, 4f, 4f, 4f, Grow: -0.015f),
            Cube("head_main", 0, 0, -4f, -3f, -7.325f, 8f, 5f, 10f),
            Cube("nose", 56, 0, -1f, 0f, -8.325f, 2f, 3f, 2f));
        var rightArm = body.AddOrReplaceChild(
            "right_arm",
            VanillaPartPose.OffsetAndRotation(-4f, -5.6f, -1.8f, 0.4363f, 0f, 0f));
        rightArm.AddOrReplaceChild(
            "right_arm_r1",
            VanillaPartPose.OffsetAndRotation(0f, 0.0893f, 0.1198f, -1.0472f, 0f, 0f),
            Cube("right_arm_r1", 36, 16, -3.075f, -0.9733f, -1.9966f, 3f, 10f, 4f));
        var leftArm = body.AddOrReplaceChild(
            "left_arm",
            VanillaPartPose.OffsetAndRotation(4f, -5.6f, -1.7f, 0.4363f, 0f, 0f));
        leftArm.AddOrReplaceChild(
            "left_arm_r1",
            VanillaPartPose.OffsetAndRotation(0f, -0.0015f, -0.0808f, -1.0472f, 0f, 0f),
            Cube("left_arm_r1", 50, 16, 0.075f, -1.0443f, -1.8997f, 3f, 10f, 4f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2.1f, -2.1f, -2.075f));
        rightLeg.AddOrReplaceChild(
            "right_leg_r1",
            VanillaPartPose.OffsetAndRotation(0.05f, -1.9f, 1.075f, -1.5708f, 0f, 0f),
            Cube("right_leg_r1", 0, 27, -2f, 0.975f, 0f, 4f, 5f, 4f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, -2f, -2.075f));
        leftLeg.AddOrReplaceChild(
            "left_leg_r1",
            VanillaPartPose.OffsetAndRotation(0.05f, -2f, 1.075f, -1.5708f, 0f, 0f),
            Cube("left_leg_r1", 16, 27, -2f, 0.975f, 0f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>
    /// Star layer. Ref: CopperGolemModel.createStarPoseBodyLayer lines 207-246.
    /// </summary>
    private static VanillaMeshDefinition BuildStarMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild(
            "body",
            VanillaPartPose.Offset(0f, -5f, 0f),
            Cube("body", 0, 15, -4f, -6f, -3f, 8f, 6f, 6f));
        body.AddOrReplaceChild(
            "head",
            VanillaPartPose.Offset(0f, -6f, 0f),
            Cube("head", 0, 0, -4f, -5f, -5f, 8f, 5f, 10f),
            Cube("nose", 56, 0, -1f, -2f, -6f, 2f, 3f, 2f),
            Cube("antenna", 37, 8, -1f, -9f, -1f, 2f, 4f, 2f, Grow: -0.015f),
            Cube("antenna_tip", 37, 0, -2f, -13f, -2f, 4f, 4f, 4f, Grow: -0.015f));
        var rightArm = body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, -6f, 0f));
        rightArm.AddOrReplaceChild(
            "right_arm_r1",
            VanillaPartPose.OffsetAndRotation(1f, 1f, 0f, 0f, 0f, 1.9199f),
            Cube("right_arm_r1", 36, 16, -1.5f, -5f, -2f, 3f, 10f, 4f));
        rightArm.AddOrReplaceChild("rightItem", VanillaPartPose.Offset(-1f, 7.4f, -1f));
        var leftArm = body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, -6f, 0f));
        leftArm.AddOrReplaceChild(
            "left_arm_r1",
            VanillaPartPose.OffsetAndRotation(-1f, 1f, 0f, 0f, 0f, -1.9199f),
            Cube("left_arm_r1", 50, 16, -1.5f, -5f, -2f, 3f, 10f, 4f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-3f, -5f, 0f));
        rightLeg.AddOrReplaceChild(
            "right_leg_r1",
            VanillaPartPose.OffsetAndRotation(0.35f, 2f, 0.01f, 0f, 0f, 0.2618f),
            Cube("right_leg_r1", 0, 27, -2f, -2.5f, -2f, 4f, 5f, 4f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, -5f, 0f));
        leftLeg.AddOrReplaceChild(
            "left_leg_r1",
            VanillaPartPose.OffsetAndRotation(1.65f, 2f, 0f, 0f, 0f, -0.2618f),
            Cube("left_leg_r1", 16, 27, -2f, -2.5f, -2f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaCubeDefinition Cube(
        string name,
        int textureX,
        int textureY,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        float Grow = 0f) =>
        new(name, textureX, textureY, minX, minY, minZ, width, height, depth, Grow);
}
