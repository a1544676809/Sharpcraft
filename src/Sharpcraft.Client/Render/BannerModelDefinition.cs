// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Common.Reference;

/// <summary>Reference <c>BannerBlock.AttachmentType</c>.</summary>
/// <remarks>Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/BannerBlock.java:71-82</c>.</remarks>
[ReferenceClass("net.minecraft.world.level.block.BannerBlock.AttachmentType", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/BannerBlock.java", Kind = ReferenceKind.Mirror, Status = ReferenceStatus.Full)]
public enum BannerAttachment
{
    Wall = 0,
    Ground = 1
}

/// <summary>
/// The four static banner meshes: standing/wall body and standing/wall flag. The banner base texture
/// is 64 by 64 and shared by all four.
/// </summary>
/// <remarks>
/// Sources: <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/banner/BannerModel.java:16-48</c>
/// (<c>createBodyLayer</c>) and
/// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/object/banner/BannerFlagModel.java:17-40</c>
/// (<c>createFlagLayer</c>; <c>setupAnim(0)</c>). Part boxes and UVs are declared once here; the
/// mesher owns only the banner transformation.
/// </remarks>
[ReferenceClass("net.minecraft.client.model.object.banner.BannerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/banner/BannerModel.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
public static class BannerModelDefinition
{
    public const string TexturePath = "entity/banner/banner_base";
    public const int TextureWidth = 64;
    public const int TextureHeight = 64;

    /// <summary>
    /// Source <c>BannerFlagModel.setupAnim(0)</c>: <c>(-0.0125 + 0.01 * cos(0)) * π</c>. This is the
    /// base flag pose; the renderer adds the per-phase delta through <see cref="EntityPose"/>.
    /// </summary>
    public const float FlagPhaseZeroRotationX = (-0.0125f + 0.01f) * MathF.PI;

    public static readonly EntityModelDefinition StandingBody = CreateBody("banner_standing_body", standing: true);
    public static readonly EntityModelDefinition WallBody = CreateBody("banner_wall_body", standing: false);
    public static readonly EntityModelDefinition StandingFlag = CreateFlag("banner_standing_flag", standing: true);
    public static readonly EntityModelDefinition WallFlag = CreateFlag("banner_wall_flag", standing: false);

    /// <summary>Body definition for an attachment type.</summary>
    public static EntityModelDefinition BodyFor(BannerAttachment attachment) =>
        attachment == BannerAttachment.Ground ? StandingBody : WallBody;

    /// <summary>Flag definition for an attachment type.</summary>
    public static EntityModelDefinition FlagFor(BannerAttachment attachment) =>
        attachment == BannerAttachment.Ground ? StandingFlag : WallFlag;

    private static EntityModelDefinition CreateBody(string name, bool standing) =>
        Create(name, BuildBody(standing));

    private static EntityModelDefinition CreateFlag(string name, bool standing) =>
        Create(name, BuildFlag(standing));

    private static EntityModelDefinition Create(string name, VanillaMeshDefinition mesh) =>
        EntityModelDefinition.FromTree(
            name,
            TextureWidth,
            TextureHeight,
            mesh,
            PlayerModelDefinition.DefaultFaceTransforms,
            TexturePath);

    /// <summary>
    /// Source <c>BannerModel.createBodyLayer(standing)</c>. Ground adds the pole at (-1,-42,-1) size
    /// (2,42,2) with UV (44,0); the bar is (20,2,2) at UV (0,42), at y -44 ground or y -20.5 and
    /// z 9.5 for a wall banner.
    /// </summary>
    private static VanillaMeshDefinition BuildBody(bool standing)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (standing)
        {
            root.AddOrReplaceChild(
                "pole",
                VanillaPartPose.Zero,
                new VanillaCubeDefinition("pole", 44, 0, -1f, -42f, -1f, 2f, 42f, 2f));
        }

        root.AddOrReplaceChild(
            "bar",
            VanillaPartPose.Zero,
            standing
                ? new VanillaCubeDefinition("bar", 0, 42, -10f, -44f, -1f, 20f, 2f, 2f)
                : new VanillaCubeDefinition("bar", 0, 42, -10f, -20.5f, 9.5f, 20f, 2f, 2f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>
    /// Source <c>BannerFlagModel.createFlagLayer(standing)</c>. Flag box is (20,40,1) at UV (0,0);
    /// its source pose is (0,-44,0) standing or (0,-20.5,10.5) wall, with the phase-zero X rotation
    /// as the baked base pose.
    /// </summary>
    private static VanillaMeshDefinition BuildFlag(bool standing)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "flag",
            VanillaPartPose.OffsetAndRotation(
                0f,
                standing ? -44f : -20.5f,
                standing ? 0f : 10.5f,
                FlagPhaseZeroRotationX,
                0f,
                0f),
            new VanillaCubeDefinition("flag", 0, 0, -10f, 0f, -2f, 20f, 40f, 1f));
        return new VanillaMeshDefinition(root);
    }
}
