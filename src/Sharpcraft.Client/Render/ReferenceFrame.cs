// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Converts transforms written in the reference model frame into Sharpcraft's baked frame.
/// <para>
/// The reference flips living entities at draw time (<c>LivingEntityRenderer</c> applies <c>scale(-1,-1,1)</c>) and
/// Sharpcraft bakes that flip into geometry (<see cref="VanillaMeshBake.FromVanilla"/> negates X and maps <c>y</c> to
/// <c>modelHeight - y</c>), draws model pixels at the model scale instead of 1/16, and stores the feet at zero. Anything
/// authored in the reference frame therefore cannot be dropped into this engine unchanged.
/// </para>
/// <para>
/// The reference held-item chain is one composite inside the entity pose stack
/// (<c>ItemInHandLayer.submitArmWithItem</c>, then <c>ItemStackRenderState.LayerRenderState.applyTransform</c> for the
/// item display transform):
/// </para>
/// <code>
///   world = H · XP(-90°) · YP(180°) · T_off · [swing] · [use] · D_display
/// </code>
/// <para>
/// Rather than translate those factors by hand, the anchor below was <em>solved</em> from reference world matrices
/// captured with the shipped JOML build, for two poses and two display transforms; all four combinations agree on the
/// same matrix, so the result is both pose- and item-independent.
/// </para>
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.LivingEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/LivingEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Reference-frame to baked-frame conversion; the reference applies scale(-1,-1,1) at draw time, Sharpcraft bakes it into geometry")]
public static class ReferenceFrame
{
    /// <summary>Reference model height in pixels; Sharpcraft lifts geometry so the feet sit at zero.</summary>
    public const float ModelHeightPixels = 24f;

    /// <summary>Reference <c>T_off</c> for the right hand of an adult holder, in blocks.</summary>
    public static readonly Vector3 AdultOffset = new(1f / 16f, 2f / 16f, -10f / 16f);

    /// <summary>Reference <c>T_off</c> for a baby holder that is not an armor stand, in blocks.</summary>
    public static readonly Vector3 BabyOffset = new(0f, 1f / 16f, -4.5f / 16f);

    /// <summary>The reference flip (<c>poseStack.scale(-1, -1, 1)</c>); it is its own inverse.</summary>
    public static Matrix4x4 Mirror => Matrix4x4.CreateScale(-1f, -1f, 1f);

    /// <summary>The reference-to-baked model-space map, as a row-vector matrix (leftmost factor applies first).</summary>
    public static Matrix4x4 Frame(float modelScale) =>
        Mirror
        * Matrix4x4.CreateScale(modelScale)
        * Matrix4x4.CreateTranslation(0f, ModelHeightPixels / 16f * modelScale, 0f);

    /// <summary>Maps a point from the reference model frame into this one.</summary>
    public static Vector3 ToSharpcraft(Vector3 referencePoint, float modelScale = 1f) =>
        Vector3.Transform(referencePoint, Frame(modelScale));

    /// <summary>
    /// Held-item anchor, solved from reference world matrices rather than derived by hand.
    /// </summary>
    /// <remarks>
    /// Reference captures live in <c>tools/Sharpcraft.ItemRender.Audit/truth/</c> (<c>PoseVariantProbe.java</c> covers two
    /// poses times two display transforms). Solving <c>D · anchor · H_ours = W_ref · K</c> for each combination yields the
    /// same matrix every time:
    /// <code>
    ///   anchor = T(T_off) · S(modelScale) · Rx(+90°)
    ///   adult: linear 0.9·Rx(+90°), translation (0.05625, -0.56250, -0.11250)
    /// </code>
    /// No display term appears here, which is the point: an anchor fitted against one item's display transform renders
    /// every other item in the wrong place, which is exactly how shields broke while generated items looked correct.
    /// </remarks>
    /// <param name="referenceOffset">
    /// Reference <c>T_off</c> in blocks — see <see cref="AdultOffset"/> and <see cref="BabyOffset"/>. The left hand
    /// mirrors its X component.
    /// </param>
    /// <param name="modelScale">The holder's model scale.</param>
    public static Matrix4x4 HeldItemAnchor(Vector3 referenceOffset, float modelScale)
    {
        // Built from the solved numbers rather than by composing factors: the linear part spins one way and the offset
        // the other, and hand-writing that composition is where the sign kept flipping.
        var anchor = Matrix4x4.CreateScale(modelScale) * Matrix4x4.CreateRotationX(-MathF.PI / 2f);
        anchor.Translation = Vector3.TransformNormal(referenceOffset, Matrix4x4.CreateRotationX(-MathF.PI / 2f)) * modelScale;
        return anchor;
    }
}
