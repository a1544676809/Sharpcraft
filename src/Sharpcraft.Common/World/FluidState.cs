// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.World;

/// <summary>Which fluid a state carries.</summary>
public enum FluidKind
{
    /// <summary>No fluid (the default for a non-fluid block state).</summary>
    None = 0,
    Water = 1,
    Lava = 2,
}

/// <summary>
/// Derived fluid state of a block: the amount (1..8), whether it is falling, and the resulting surface height.
/// </summary>
/// <remarks>
/// Behaviour copied from the reference fluid model (docs/modules/block-state.md, docs/specs/render/fluid-rendering.md):
/// the block's <c>level</c> property (0..15) is the legacy encoding (source = 0, flowing 1..7, falling 8..15); the
/// internal amount is 1..8 and the surface <see cref="OwnHeight"/> is <c>amount / 9</c>, so a full source is 8/9, not 1.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.material.FluidState", "ref://mc-26.3-analysis/src/net/minecraft/world/level/material/FluidState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public readonly record struct FluidState(FluidKind Kind, int Amount, bool Falling)
{
    /// <summary>True when this state carries no fluid.</summary>
    public bool IsEmpty => Kind == FluidKind.None;

    /// <summary>True for a still source block (full amount, not falling).</summary>
    public bool IsSource => Kind != FluidKind.None && Amount == FluidStateCodec.FullAmount && !Falling;

    /// <summary>Surface height in block units: 0 when empty, otherwise <c>amount / 9</c> (source = 8/9).</summary>
    public float OwnHeight => IsEmpty ? 0f : Amount / 9f;
}

/// <summary>Decodes the fluid carried by a global block-state id (docs/modules/block-state.md step A).</summary>
public static class FluidStateCodec
{
    /// <summary>A source/full fluid amount (the reference <c>AMOUNT_FULL</c>).</summary>
    public const int FullAmount = 8;

    /// <summary>Number of states a fluid block owns (single integer property <c>level</c> 0..15).</summary>
    public const int FluidStateCount = 16;

    private static readonly Identifier WaterId = new("minecraft", "water");
    private static readonly Identifier LavaId = new("minecraft", "lava");

    /// <summary>Fluid carried by <paramref name="stateId"/>, or the empty fluid when it is not water/lava.</summary>
    public static FluidState FromBlockState(int stateId, BlockStateLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.TryGetBaseId(WaterId, out var waterBase) && stateId >= waterBase && stateId < waterBase + FluidStateCount)
        {
            return Decode(FluidKind.Water, stateId - waterBase);
        }

        if (layout.TryGetBaseId(LavaId, out var lavaBase) && stateId >= lavaBase && stateId < lavaBase + FluidStateCount)
        {
            return Decode(FluidKind.Lava, stateId - lavaBase);
        }

        return default;
    }

    /// <summary>Decodes a legacy <c>level</c> (0..15) into an amount/falling pair.</summary>
    public static FluidState Decode(FluidKind kind, int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(level, FluidStateCount);

        // level 0 = source (full, not falling); 1..7 = flowing with amount 8-level; 8..15 = falling with amount 16-level.
        if (level == 0)
        {
            return new FluidState(kind, FullAmount, Falling: false);
        }

        return level < FullAmount
            ? new FluidState(kind, FullAmount - level, Falling: false)
            : new FluidState(kind, FluidStateCount - level, Falling: true);
    }
}
