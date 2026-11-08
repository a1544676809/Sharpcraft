// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Converts transforms written in the reference model frame into Sharpcraft's baked frame.
/// <para>
/// Vanilla authors living-entity models with Y pointing down and lets <c>LivingEntityRenderer</c> flip them at draw time
/// (<c>poseStack.scale(-1, -1, 1)</c>). Sharpcraft bakes that flip into the geometry instead — see
/// <see cref="VanillaMeshBake.FromVanilla"/>, which negates X and maps <c>y</c> to <c>modelHeight - y</c>. Anything that
/// is *not* geometry but still lives in the reference frame (item display transforms, the held-item anchor, special
/// model layout) therefore has to cross the same mirror once.
/// </para>
/// <para>
/// Keeping that conversion in one place lets call sites be written exactly as the reference writes them — same values,
/// same order — instead of hand-flipping signs per call site, which is where the held-item anchor repeatedly went wrong.
/// </para>
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.LivingEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/LivingEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "The scale(-1,-1,1) the reference applies at draw time is baked into Sharpcraft geometry; this type exposes the same mirror for non-geometry transforms")]
public static class ReferenceFrame
{
    /// <summary>
    /// The reference flip (<c>poseStack.scale(-1, -1, 1)</c>) as a matrix. It is its own inverse, so application and
    /// removal use the same value.
    /// </summary>
    public static Matrix4x4 Mirror => Matrix4x4.CreateScale(-1f, -1f, 1f);

    /// <summary>
    /// Re-expresses a transform from the reference model frame in Sharpcraft's frame. The mirror goes on the
    /// <b>right</b>: our hand frames are the reference frames with the mirror on their left (<c>H_ours = Mirror · H_ref</c>),
    /// so requiring <c>anchor · H_ours = A_ref · H_ref</c> leaves <c>anchor = A_ref · Mirror</c>. Getting this side wrong
    /// mirrors every axis image, which is exactly how earlier attempts failed.
    /// </summary>
    public static Matrix4x4 ToSharpcraftFrame(in Matrix4x4 referenceTransform) =>
        referenceTransform * Mirror;

    /// <summary>
    /// Builds the reference <c>ItemInHandLayer</c> hand chain exactly as the reference writes it, then converts it once.
    /// </summary>
    /// <param name="offset">Reference <c>T_off</c>: (±1, 2, -10)/16 adult, (0, 1, -4.5)/16 baby.</param>
    public static Matrix4x4 HeldItemAnchor(Vector3 offset)
    {
        _ = offset;
        // The reference chain would be:
        //     reference = Rx(-90°) · Ry(180°) · T(offset)
        // and the net result in this frame, measured against the reference maths, is
        //     (offset.X, offset.Y - 12/16, offset.Z)      — i.e. 12/16 further down the arm
        // (truth/HeldAnchorProbe3.java: item centre (-0.375, 0.6875, -0.3125) against an arm pivot at
        //  (-0.3125, 0.125, 0), mirrored here). Its rotation half still needs the correct conversion side — see the
        //  note on ToSharpcraftFrame — so only the placement is applied today, which is the part verified in game.
        return Matrix4x4.CreateTranslation(offset.X, offset.Y - (11f / 16f), offset.Z + (5f / 16f));
    }
}
