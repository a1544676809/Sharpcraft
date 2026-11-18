// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// SPECIAL foil decal coordinates: the reference builds <c>foilDecalPose</c> (the item pose scaled by 0.5 in the GUI
/// and 0.75 in first person) and fills UV3 through <c>SheetedDecalTextureGenerator.setSheetedDecalUv</c>. Because the
/// probe pose is the same pose the quad is submitted with, its inverse cancels the pose and UV3 depends only on the
/// model-space vertex, the quad facing and that pose scale — which is why the CPU can bake it once.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.feature.ItemFeatureRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/feature/ItemFeatureRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "computeFoilDecalPose and the SPECIAL putBakedQuadWithGlint UV3 path; the frame-dependent pose cancels, so the decal is a model-space projection")]
public static class ItemFoilDecal
{
    /// <summary>Reference <c>SPECIAL_FOIL_TEXTURE_SCALE</c>: the decal position is divided by 128.</summary>
    public const float TextureScale = 0.0078125f;

    /// <summary>Reference <c>SPECIAL_FOIL_UI_SCALE</c>: the GUI foil decal pose is scaled by a half.</summary>
    public const float GuiPoseScale = 0.5f;

    /// <summary>Reference <c>SPECIAL_FOIL_FIRST_PERSON_SCALE</c>.</summary>
    public const float FirstPersonPoseScale = 0.75f;

    private static readonly float HalfPi = MathF.PI / 2f;

    // Direction.getRotation(): DOWN rotates X by pi, UP is the identity, NORTH/WEST/EAST are rotationXYZ accordingly.
    // JOML's rotationXYZ(x, y, z) composes like System.Numerics' qx * qy * qz (see ItemDisplayTransform.ToMatrix).
    private static readonly Quaternion DownRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
    private static readonly Quaternion UpRotation = Quaternion.Identity;
    private static readonly Quaternion NorthRotation = Compose(HalfPi, MathF.PI);
    private static readonly Quaternion SouthRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, HalfPi);
    private static readonly Quaternion WestRotation = Compose(HalfPi, HalfPi);
    private static readonly Quaternion EastRotation = Compose(HalfPi, -HalfPi);

    /// <summary>
    /// Reference <c>ItemFeatureRenderer.computeFoilDecalPose</c> pose scale for one display context.
    /// </summary>
    public static float PoseScale(ItemDisplayContext context) => context switch
    {
        ItemDisplayContext.Gui => GuiPoseScale,
        ItemDisplayContext.FirstPersonLeftHand or ItemDisplayContext.FirstPersonRightHand => FirstPersonPoseScale,
        _ => 1f
    };

    /// <summary>
    /// The model-space multiplier the decal pose applies: inverting the pose scaled by <c>k</c> multiplies the vertex
    /// by <c>1/k</c>. Third person (and every other context) keeps 1.
    /// </summary>
    public static float ScaleFor(ItemDisplayContext context) => 1f / PoseScale(context);

    /// <summary>
    /// Decal coordinate (UV3) of one model-space vertex on a face with the given facing. The result is frame
    /// independent: the reference pose cancels against the inverse decal pose.
    /// </summary>
    public static Vector2 Uv(Vector3 position, ModelFaceDirection direction, float decalScale)
    {
        var scaled = position * decalScale;
        // worldPos.rotateY(pi): (x, y, z) -> (-x, y, -z).
        var rotated = new Vector3(-scaled.X, scaled.Y, -scaled.Z);
        // worldPos.rotateX(-pi/2): (x, y, z) -> (x, z, -y).
        rotated = new Vector3(rotated.X, rotated.Z, -rotated.Y);
        rotated = Vector3.Transform(rotated, Rotation(direction));
        return new Vector2(-rotated.X * TextureScale, -rotated.Y * TextureScale);
    }

    /// <summary>Reference <c>Direction.getRotation</c>.</summary>
    public static Quaternion Rotation(ModelFaceDirection direction) => direction switch
    {
        ModelFaceDirection.Down => DownRotation,
        ModelFaceDirection.Up => UpRotation,
        ModelFaceDirection.North => NorthRotation,
        ModelFaceDirection.South => SouthRotation,
        ModelFaceDirection.West => WestRotation,
        _ => EastRotation
    };

    private static Quaternion Compose(float rotationX, float rotationZ) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, rotationX) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationZ);
}
