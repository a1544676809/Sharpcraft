// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Interpolates block entity animation values between game ticks, the way the reference does:
/// the block entity keeps the previous tick's value and the current one, and rendering asks for
/// the value at a partial tick instead of using the current tick's value directly.
/// </summary>
/// <remarks>
/// The reference expresses this as a function from partial tick to value
/// (<c>ChestBlock.opennessCombiner</c> returns a <c>Float2FloatFunction</c>), and a double chest
/// combines its two halves by taking the larger value. This is the same computation with the
/// partial tick passed in explicitly.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.LidBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/LidBlockEntity.java")]
public static class BlockEntityAnimation
{
    /// <summary>Value at a partial tick: the reference's <c>Mth.lerp(t, previous, current)</c>.</summary>
    /// <param name="previous">Value recorded at the end of the previous tick.</param>
    /// <param name="current">Value recorded at the end of the current tick.</param>
    /// <param name="partialTick">How far into the current tick the frame is, zero to one.</param>
    public static float Lerp(float previous, float current, float partialTick)
    {
        var t = Math.Clamp(partialTick, 0f, 1f);
        return previous + ((current - previous) * t);
    }

    /// <summary>
    /// Opening amount used by a chest. A single chest uses its own interpolated value; a double
    /// chest uses the larger of the two halves, matching the reference's combiner.
    /// </summary>
    /// <param name="previous">This half's value at the previous tick.</param>
    /// <param name="current">This half's value at the current tick.</param>
    /// <param name="partialTick">How far into the current tick the frame is.</param>
    /// <param name="otherPrevious">The other half's value at the previous tick.</param>
    /// <param name="otherCurrent">The other half's value at the current tick.</param>
    /// <param name="doubleChest">Whether the chest spans two blocks.</param>
    public static float Openness(
        float previous,
        float current,
        float partialTick,
        float otherPrevious,
        float otherCurrent,
        bool doubleChest)
    {
        var mine = Lerp(previous, current, partialTick);
        if (!doubleChest)
        {
            return mine;
        }

        return MathF.Max(mine, Lerp(otherPrevious, otherCurrent, partialTick));
    }
}
