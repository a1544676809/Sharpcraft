// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World.Blocks;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// The behaviour seam has no implementations yet, so its contract is all there is to test: the two
/// registration kinds differ in lifetime, replacement and removal work, and only weak entries
/// disappear when their owner is collected.
/// </summary>
public sealed class BlockBehavioursTests
{
    private sealed class Marker : IBlockBehaviour
    {
    }

    [Fact]
    public void AnUnregisteredBlockAnswersWithTheSharedNoop()
    {
        Assert.Same(BlockBehaviours.Default, BlockBehaviours.For(9_099));
        Assert.False(BlockBehaviours.IsRegistered(9_099));
    }

    [Fact]
    public void ARegisteredBehaviourIsFound()
    {
        var behaviour = new Marker();
        BlockBehaviours.Register(9_001, behaviour);

        try
        {
            Assert.Same(behaviour, BlockBehaviours.For(9_001));
            Assert.True(BlockBehaviours.IsRegistered(9_001));
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
    public void AStronglyRegisteredBehaviourSurvivesCollection()
    {
        BlockBehaviours.RegisterStrong(9_005, new Marker());

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.True(BlockBehaviours.IsRegistered(9_005));
            Assert.NotSame(BlockBehaviours.Default, BlockBehaviours.For(9_005));
        }
        finally
        {
            BlockBehaviours.Unregister(9_005);
        }
    }

    [Fact]
    public void AWeaklyRegisteredBehaviourIsFoundWhileItsOwnerLives()
    {
        var owner = new Marker();
        BlockBehaviours.Register(9_006, owner);

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // The local still holds it, so collection cannot have happened yet.
            Assert.Same(owner, BlockBehaviours.For(9_006));
        }
        finally
        {
            BlockBehaviours.Unregister(9_006);
        }
    }

    [Fact]
    public void CollectedWeakEntriesAreDroppedAndLiveOnesKept()
    {
        RegisterWeakly(9_007);
        BlockBehaviours.RegisterStrong(9_008, new Marker());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        BlockBehaviours.RemoveCollected();

        try
        {
            Assert.False(BlockBehaviours.IsRegistered(9_007));
            Assert.True(BlockBehaviours.IsRegistered(9_008));
        }
        finally
        {
            BlockBehaviours.Unregister(9_007);
            BlockBehaviours.Unregister(9_008);
        }
    }

    [Fact]
    public void RemovingAnUnknownEntryDoesNothing() => Assert.False(BlockBehaviours.Unregister(9_004));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RegisterWeakly(int blockId) => BlockBehaviours.Register(blockId, new Marker());
}
