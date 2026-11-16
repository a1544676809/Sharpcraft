// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World.Blocks;

/// <summary>
/// The algorithmic side of a block: what it does on a tick, when a neighbour changes, when it is
/// placed or broken. Declarative facts (does it carry a block entity, how much light it emits, how it
/// occludes) live in the block registry data instead; this interface is only for behaviour that
/// cannot be expressed as data.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps both halves on one <c>Block</c> object. This project keeps the data in the
/// registry and reserves the behaviour for the server side, which is out of scope today
/// (AGENTS.md principle 4). The seam exists so that adding behaviour later does not change any
/// caller: everything asks <see cref="BlockBehaviours"/>.
/// </para>
/// <para>
/// <b>Provisional.</b> Only what a plain neighbour change needs is modelled. The reference passes an
/// <c>Orientation</c> and a <c>movedByPiston</c> flag, a <c>RandomSource</c> to the tick callbacks,
/// and lets a block query neighbour signals; none of that is here yet. A piston cannot be written
/// against this interface, so it must not be treated as a frozen contract — see Issue #97 before
/// implementing the first real behaviour.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.Block", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/Block.java", Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "behaviour half only, and only its simple neighbour and tick callbacks; data lives in the block registry")]
public interface IBlockBehaviour
{
    /// <summary>Runs when a random tick selects this block.</summary>
    /// <param name="context">World access, position and state.</param>
    void RandomTick(in BlockTickContext context)
    {
    }

    /// <summary>Runs when a scheduled tick reaches this block.</summary>
    /// <param name="context">World access, position and state.</param>
    void ScheduledTick(in BlockTickContext context)
    {
    }

    /// <summary>Runs when a neighbouring block changes, after the change has been applied.</summary>
    /// <param name="context">World access, position and state.</param>
    /// <param name="direction">
    /// Direction of the neighbour that changed, using the reference's direction ids
    /// (<c>0</c> down … <c>5</c> east), or <c>-1</c> when the source is unknown.
    /// </param>
    /// <param name="movedByPiston">True when a piston moved the neighbour rather than a player or tick.</param>
    void NeighbourChanged(in BlockTickContext context, int direction, bool movedByPiston)
    {
    }

    /// <summary>Runs after this block was placed.</summary>
    /// <param name="context">World access, position and the new state.</param>
    /// <param name="previousState">State that was there before, or <c>0</c>.</param>
    void Placed(in BlockTickContext context, int previousState)
    {
    }

    /// <summary>Runs when this block is about to be replaced by another state.</summary>
    /// <param name="context">World access, position and the state being removed.</param>
    /// <param name="newState">State that replaces it, or <c>0</c> for air.</param>
    /// <param name="movedByPiston">True when a piston is what removes the block.</param>
    void Broken(in BlockTickContext context, int newState, bool movedByPiston)
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
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.LevelReader", "ref://mc-26.3-analysis/src/net/minecraft/world/level/LevelReader.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "read and write of block states only; no signal queries yet")]
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
/// <remarks>
/// Two kinds of registration, because the two owners have opposite lifetimes. A mod assembly lives
/// in a collectible context and must be able to unload (AGENTS.md principle 2), so
/// <see cref="Register"/> holds its behaviour weakly: once the mod is gone the entry stops being
/// found, which is exactly what an unloaded mod should look like. The server's own behaviours must
/// outlive any collection, so <see cref="RegisterStrong"/> holds them strongly; register a server
/// behaviour weakly and a collection would silently turn it into a no-op.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.core.registries.BuiltInRegistries", "ref://mc-26.3-analysis/src/net/minecraft/core/registries/BuiltInRegistries.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "lookup of the behaviour half of a block, keyed by block id")]
public static class BlockBehaviours
{
    /// <summary>Behaviour used by every block that does not override anything.</summary>
    public static IBlockBehaviour Default { get; } = new NoopBehaviour();

    private static readonly System.Collections.Generic.Dictionary<int, Entry> Registered = [];

    /// <summary>Registers a behaviour that is allowed to disappear when its owner unloads.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    /// <param name="behaviour">Behaviour to run for it. Held weakly.</param>
    public static void Register(int blockId, IBlockBehaviour behaviour) =>
        Registered[blockId] = Entry.Weak(behaviour);

    /// <summary>Registers a behaviour that must outlive any collection, such as the server's own.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    /// <param name="behaviour">Behaviour to run for it. Held strongly.</param>
    public static void RegisterStrong(int blockId, IBlockBehaviour behaviour) =>
        Registered[blockId] = Entry.Strong(behaviour);

    /// <summary>Drops the behaviour of one block id, if any.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    /// <returns>True when an entry was removed.</returns>
    public static bool Unregister(int blockId) => Registered.Remove(blockId);

    /// <summary>Whether a block id has a live registration of either kind.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    public static bool IsRegistered(int blockId) => Registered.TryGetValue(blockId, out var entry) && entry.IsAlive;

    /// <summary>Behaviour for a block id, or <see cref="Default"/> when none is registered or it was collected.</summary>
    /// <param name="blockId">Block id from the registry layout.</param>
    public static IBlockBehaviour For(int blockId) =>
        Registered.TryGetValue(blockId, out var entry) && entry.TryGetTarget(out var behaviour)
            ? behaviour
            : Default;

    /// <summary>How many blocks have a registration; zero until the server side starts.</summary>
    public static int Count => Registered.Count;

    /// <summary>Drops every registration whose weak behaviour was collected.</summary>
    /// <returns>How many entries were dropped.</returns>
    public static int RemoveCollected()
    {
        int[] keys = [.. Registered.Keys];
        var dropped = 0;
        foreach (var key in keys)
        {
            if (!Registered[key].IsAlive)
            {
                Registered.Remove(key);
                dropped++;
            }
        }

        return dropped;
    }

    private readonly struct Entry
    {
        private readonly System.WeakReference<IBlockBehaviour>? _weak;
        private readonly IBlockBehaviour? _strong;

        private Entry(System.WeakReference<IBlockBehaviour>? weak, IBlockBehaviour? strong)
        {
            _weak = weak;
            _strong = strong;
        }

        public static Entry Weak(IBlockBehaviour behaviour) => new(new System.WeakReference<IBlockBehaviour>(behaviour), null);

        public static Entry Strong(IBlockBehaviour behaviour) => new(null, behaviour);

        public bool IsAlive => _strong is not null || (_weak is not null && _weak.TryGetTarget(out _));

        public bool TryGetTarget(out IBlockBehaviour behaviour)
        {
            if (_strong is not null)
            {
                behaviour = _strong;
                return true;
            }

            if (_weak is not null && _weak.TryGetTarget(out var target))
            {
                behaviour = target;
                return true;
            }

            behaviour = Default;
            return false;
        }
    }

    private sealed class NoopBehaviour : IBlockBehaviour
    {
    }
}
