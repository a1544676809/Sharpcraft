// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Converts transforms written in the reference model frame into Sharpcraft's baked frame.
/// <para>
/// The two frames are related by exactly three operations, in this order:
/// </para>
/// <list type="number">
/// <item><description>mirror <c>M = diag(-1,-1,1)</c> — the reference renderer flips living entities at draw time
/// (<c>LivingEntityRenderer</c> calls <c>scale(-1,-1,1)</c>) and Sharpcraft bakes that flip into the geometry
/// (<see cref="VanillaMeshBake.FromVanilla"/> negates X and maps <c>y</c> to <c>modelHeight - y</c>);</description></item>
/// <item><description>uniform scale by the model scale, because Sharpcraft draws model pixels at
/// <c>modelScale / 16</c> blocks while the reference uses <c>1 / 16</c>;</description></item>
/// <item><description>a lift by the model height, because Sharpcraft stores geometry with the feet at zero while the
/// reference model origin sits at the top of the model.</description></item>
/// </list>
/// <para>
/// The relation is checked against measured data: for the zombie's adult right arm (reference pivot
/// <c>(-0.3125, 0.125, 0)</c>, model scale 0.9) it yields <c>(0.281, 1.237, 0)</c>, which is exactly what
/// <see cref="EntityModelMesher.PartWorldMatrix"/> returns for <c>right_arm</c>.
/// </para>
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.LivingEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/LivingEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Reference-frame to baked-frame conversion; the reference applies scale(-1,-1,1) at draw time, Sharpcraft bakes it into geometry")]
public static class ReferenceFrame
{
    /// <summary>Reference model height in pixels, from which Sharpcraft lifts geometry so the feet sit at zero.</summary>
    public const float ModelHeightPixels = 24f;

    /// <summary>The reference flip (<c>poseStack.scale(-1, -1, 1)</c>); it is its own inverse.</summary>
    public static Matrix4x4 Mirror => Matrix4x4.CreateScale(-1f, -1f, 1f);

    /// <summary>
    /// Maps a point from the reference model frame into Sharpcraft's baked frame.
    /// </summary>
    /// <param name="referencePoint">Point in reference model coordinates, in blocks.</param>
    /// <param name="modelScale">The model's <c>Scale</c> (0.9 for adult humanoids, 0.5 for babies, 1 for most mobs).</param>
    public static Vector3 ToSharpcraft(Vector3 referencePoint, float modelScale = 1f)
    {
        var mirrored = Vector3.Transform(referencePoint, Mirror);
        var scaled = mirrored * modelScale;
        return scaled + new Vector3(0f, ModelHeightPixels / 16f * modelScale, 0f);
    }

    /// <summary>
    /// Re-expresses a transform from the reference model frame in Sharpcraft's frame: <c>reference · K⁻¹</c> with
    /// <c>K = Mirror · S(modelScale) · T(0, 24/16·modelScale, 0)</c>.
    /// </summary>
    public static Matrix4x4 ToSharpcraftFrame(in Matrix4x4 referenceTransform, float modelScale = 1f) =>
        referenceTransform
        * Matrix4x4.CreateTranslation(0f, -(ModelHeightPixels / 16f * modelScale), 0f)
        * Matrix4x4.CreateScale(1f / modelScale)
        * Mirror;

    /// <summary>
    /// Held-item anchor, solved against the reference implementation rather than hand-derived.
    /// </summary>
    /// <remarks>
    /// The reference chain (<c>ItemInHandLayer.submitArmWithItem</c>: translateToHand → XP -90° → YP 180° →
    /// <c>T_off</c> → item display) was run through the shipped JOML build and its whole world matrix captured
    /// (tools/Sharpcraft.ItemRender.Audit/truth/MatrixDump.java, values in
    /// HeldItemPlacementComparisonTests). Converting that world matrix into Sharpcraft's frame and solving
    /// <c>D · anchor · H = W</c> yields exactly a translation plus a -90° X rotation:
    /// <code>
    ///   rotation rows : [1 0 0] [0 0 -1] [0 1 0]      == CreateRotationX(-90°)
    ///   translation   : (0.09375, -0.48750, -0.12500)  == (1.5, -7.8, -2)/16
    /// </code>
    /// Both values are for the adult model scale 0.9, so they scale with it.
    /// </remarks>
    public static Matrix4x4 HeldItemAnchor(float modelScale = 1f)
    {
        // Solved, not hand-derived: the reference world matrix was captured from JOML (tools/Sharpcraft.ItemRender.Audit/
        // truth/MatrixDump.java), mapped into Sharpcraft's frame with K and solved for D · anchor · H = W. The solution is
        //     anchor = S(s) · Rx(-90°) · T((1, -10, -2)/16 · s)
        // with s the model scale (0.9 adult, 0.45 baby). For s = 0.9 that is rotation 0.9·Rx(-90°) and translation
        // (0.05625, -0.56250, -0.11250), which is exactly what the solver prints; HeldItemPlacementComparisonTests pins
        // the resulting world points against the reference values.
        var translation = new Vector3(1f, -10f, -2f) * (modelScale / 16f);
        return Matrix4x4.CreateScale(modelScale)
            * Matrix4x4.CreateRotationX(-MathF.PI / 2f)
            * Matrix4x4.CreateTranslation(translation);
    }}
