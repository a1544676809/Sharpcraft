// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Transform constants and formulas for dropped items, ported value-for-value from the reference
/// <c>ItemEntityRenderer</c> and <c>ItemEntity</c>.
/// </summary>
/// <remarks>
/// The bob phase is <c>bobOffs = random.nextFloat() * 2π</c> on the reference entity, taken from a client-local random
/// source and never synced, so two vanilla clients already show different phases for the same drop. We therefore derive
/// the phase the same way instead of trying to match a value that does not exist on the wire.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.ItemEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/ItemEntityRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "bob/spin/ground offset and stack layout maths only; submission path lives in the renderer")]
public static class ItemEntityRenderer
{
    /// <summary>Reference <c>bob</c> amplitude factor.</summary>
    public const float BobAmplitude = 0.1f;

    /// <summary>Reference <c>bob</c> age divisor.</summary>
    public const float BobAgeDivisor = 10f;

    /// <summary>Reference vertical clearance above the model's lowest point.</summary>
    public const float GroundClearance = 0.0625f;

    /// <summary>Reference <c>getSpin</c> age divisor.</summary>
    public const float SpinAgeDivisor = 20f;

    /// <summary>Reference scatter spread for the thick-model branch.</summary>
    public const float ScatterSpread = 0.15f;

    /// <summary>Reference step factor between items of a thin stack.</summary>
    public const float BulkZFactor = 1.5f;

    /// <summary>Number of values a <c>bobOffs</c> draw covers: <c>random.nextFloat() * 2π</c>.</summary>
    public const float BobPhasePeriod = MathF.PI * 2f;

    /// <summary>Reference <c>bob = sin(age / 10 + bobOffset) * 0.1 + 0.1</c>.</summary>
    public static float BobHeight(float ageInTicks, float bobPhase) =>
        (MathF.Sin((ageInTicks / BobAgeDivisor) + bobPhase) * BobAmplitude) + BobAmplitude;

    /// <summary>Reference <c>ItemEntity.getSpin(ageInTicks, bobOffset) = ageInTicks / 20 + bobOffset</c>, in radians.</summary>
    public static float Spin(float ageInTicks, float bobPhase) => (ageInTicks / SpinAgeDivisor) + bobPhase;

    /// <summary>Reference <c>minOffsetY = -(boundingBox.minY) + 0.0625</c>.</summary>
    public static float GroundOffset(ItemModelExtents extents) => -extents.Min.Y + GroundClearance;

    /// <summary>
    /// True when the reference takes the scattered branch: <c>modelDepth &gt; 0.0625</c>. Flat items sit exactly at the
    /// threshold, so only genuinely three-dimensional models scatter.
    /// </summary>
    public static bool UsesScatteredLayout(ItemModelExtents extents) => extents.IsDeeperThanFlatItem;

    /// <summary>Reference <c>offsetZ = modelDepth * 1.5</c> between items of a thin stack.</summary>
    public static float BulkZStep(ItemModelExtents extents) => extents.Depth * BulkZFactor;

    /// <summary>Reference first-item shift for a thin stack: <c>-(offsetZ * (amount - 1) / 2)</c>.</summary>
    public static float BulkStartZ(ItemModelExtents extents, int count) =>
        -(BulkZStep(extents) * (count - 1) / 2f);

    /// <summary>Reference scatter offset in [-spread, spread): <c>(random.nextFloat() * 2 - 1) * spread</c>.</summary>
    public static float ScatterOffset(float randomUnit, float spread = ScatterSpread) =>
        ((randomUnit * 2f) - 1f) * spread;

    /// <summary>
    /// Reference submission for one item entity: <c>translate(0, bob + minOffsetY, 0)</c> then
    /// <c>rotate(YP, spin)</c>, in that order, so the row-vector matrix is translation × rotation.
    /// </summary>
    public static System.Numerics.Matrix4x4 RootTransform(float ageInTicks, float bobPhase, ItemModelExtents extents) =>
        System.Numerics.Matrix4x4.CreateTranslation(0f, BobHeight(ageInTicks, bobPhase) + GroundOffset(extents), 0f)
        * System.Numerics.Matrix4x4.CreateRotationY(Spin(ageInTicks, bobPhase));

    /// <summary>
    /// Offset of one item inside a thin stack: the column starts at <see cref="BulkStartZ"/> and advances by
    /// <see cref="BulkZStep"/> per item, with the reference's half-amplitude jitter on X and Y.
    /// </summary>
    public static System.Numerics.Vector3 ThinItemOffset(
        ItemModelExtents extents,
        int index,
        int count,
        float randomX,
        float randomY)
    {
        var z = BulkStartZ(extents, count) + (BulkZStep(extents) * index);
        var jitter = ScatterSpread * 0.5f;
        return new System.Numerics.Vector3(
            ScatterOffset(randomX, jitter),
            ScatterOffset(randomY, jitter),
            z);
    }

    /// <summary>Offset of one item inside a thick stack: every item after the first is scattered in all three axes.</summary>
    public static System.Numerics.Vector3 ThickItemOffset(float randomX, float randomY, float randomZ) =>
        new(ScatterOffset(randomX), ScatterOffset(randomY), ScatterOffset(randomZ));
}
