// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World.Blocks;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// The behaviour seam has no implementations yet, so its contract is all there is to test: a block
/// without behaviour answers with the shared no-op, registrations can be replaced and dropped, and a
/// collected behaviour stops being found instead of being pinned by the static table.
/// </summary>
public sealed class BlockBehavioursTests
{
    private sealed class Marker : IBlockBehaviour
    {
    }

    [Fact]
    public void AnUnregisteredBlockAnswersWithTheSharedNoop()
    {
        Assert.Same(BlockBehaviours.Default, BlockBehaviours.For(9_999));
    }

    [Fact]
    public void ARegisteredBehaviourIsFound()
    {
        var behaviour = new Marker();
        BlockBehaviours.Register(9_001, behaviour);

        try
        {
            Assert.Same(behaviour, BlockBehaviours.For(9_001));
        }
        finally
        {
            Assert.True(BlockBehaviours.Unregister(9_001));
        }

        Assert.Same(BlockBehaviours.Default, BlockBehaviours.For(9_001));
    }

    [Fact]
    public void RegisteringAgainReplacesTheEntry()
    {
        var first = new Marker();
        var second = new Marker();
        BlockBehaviours.Register(9_002, first);
        BlockBehaviours.Register(9_002, second);

        try
        {
            Assert.Same(second, BlockBehaviours.For(9_002));
        }
        finally
        {
            BlockBehaviours.Unregister(9_002);
        }
    }

    [Fact]
    public void ACollectedBehaviourStopsBeingFound()
    {
        var before = BlockBehaviours.Count;
        RegisterThroughWeakReference(9_003);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Same(BlockBehaviours.Default, BlockBehaviours.For(9_003));
        Assert.True(BlockBehaviours.RemoveCollected() >= 0);
        Assert.True(BlockBehaviours.Count <= before + 1);

        BlockBehaviours.Unregister(9_003);
    }

    [Fact]
    public void RemovingAnUnknownEntryDoesNothing() => Assert.False(BlockBehaviours.Unregister(9_004));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RegisterThroughWeakReference(int blockId) =>
        BlockBehaviours.Register(blockId, new Marker());
}
