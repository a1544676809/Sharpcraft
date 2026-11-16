// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World.Blocks;

/// <summary>
/// The algorithmic side of a block: what it does once per tick, when a neighbour changes, when it is
/// placed or broken. Declarative facts (does it carry a block entity, how much light it emits, how it
/// occludes) live in the block registry data instead; this interface is only for behaviour that
/// cannot be expressed as data.
/// </summary>
/// <remarks>
/// The reference keeps both halves on one <c>Block</c> object. This project keeps the data in the
/// registry and reserves the behaviour for the server side, which is out of scope today
/// (AGENTS.md principle 4). The seam exists so that adding behaviour later does not change any
/// caller: everything asks <see cref="BlockBehaviours.For"/>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.Block", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/Block.java", Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "behaviour half only; data lives in the block registry")]
public interface IBlockBehaviour
{
    /// <summary>Runs when a random tick selects this block.</summary>
    /// <param name="context">World access and position.</param>
    void RandomTick(in BlockTickContext context)
    {
    }

    /// <summary>Runs when a scheduled tick reaches this block.</summary>
    /// <param name="context">World access and position.</param>
    void ScheduledTick(in BlockTickContext context)
    {
    }

    /// <summary>Runs when a neighbouring block changes.</summary>
    /// <param name="context">World access and position.</param>
    void NeighbourChanged(in BlockTickContext context)
    {
    }
}

/// <summary>What a block behaviour needs: the world, the position, and the state id it acts on.</summary>
/// <param name="World">World holding the block.</param>
/// <param name="X">Block X.</param>
/// <param name="Y">Block Y.</param>
/// <param name="Z">Block Z.</param>
/// <param name="StateId">Block state id at that position.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.Level", "ref://mc-26.3-analysis/src/net/minecraft/world/level/Level.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "the position and state a block method receives, without the whole level object")]
public readonly record struct BlockTickContext(IBlockWorldView World, int X, int Y, int Z, int StateId);

/// <summary>The small slice of the world a block behaviour may touch.</summary>
/// <remarks>Kept narrow on purpose: the server side does not exist yet, and this is the seam it will use.</remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.LevelReader", "ref://mc-26.3-analysis/src/net/minecraft/world/level/LevelReader.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "read and write of block states only")]
public interface IBlockWorldView
{
    /// <summary>Block state id at a position.</summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <returns>The state id, or zero for air outside the loaded world.</returns>
    int GetBlockState(int x, int y, int z);

    /// <summary>Replaces the block state at a position.</summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <param name="state">New state id.</param>
    /// <returns>True when the world changed.</returns>
    bool SetBlockState(int x, int y, int z, int state);
}

/// <summary>
/// Where a block's behaviour is looked up. Empty of implementations today; the server side fills it
/// per block id when it starts (docs/decisions/0031-block-behaviour-layering.md).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.core.registries.BuiltInRegistries", "ref://mc-26.3-analysis/src/net/minecraft/core/registries/BuiltInRegistries.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "lookup of the behaviour half of a block, keyed by block id")]
public static class BlockBehaviours
{
    /// <summary>Behaviour used by every block that does not override anything.</summary>
    public static IBlockBehaviour Default { get; } = new NoopBehaviour();

    private static readonly System.Collections.Generic.Dictionary<int, IBlockBehaviour> Registered = [];

    /// <summary>Registers the behaviour of one block id, replacing any previous entry.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    /// <param name="behaviour">Behaviour to run for it.</param>
    public static void Register(int blockId, IBlockBehaviour behaviour) => Registered[blockId] = behaviour;

    /// <summary>Behaviour for a block id, or <see cref="Default"/> when none is registered.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    public static IBlockBehaviour For(int blockId) =>
        Registered.TryGetValue(blockId, out var behaviour) ? behaviour : Default;

    /// <summary>How many blocks have behaviour of their own; zero until the server side starts.</summary>
    public static int Count => Registered.Count;

    private sealed class NoopBehaviour : IBlockBehaviour
    {
    }
}
