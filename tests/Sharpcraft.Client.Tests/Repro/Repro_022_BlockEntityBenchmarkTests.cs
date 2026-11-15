// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 22 (B2): the block entity pass has to stay cheap as the number of chests grows. The
/// mesher is reused across frames, so a repeated build of the same size must not allocate.
/// </summary>
public sealed class Repro_022_BlockEntityBenchmarkTests
{
    private static ItemAtlas ChestAtlas()
    {
        var pixel = new byte[16 * 16 * 4];
        return ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/chest/normal"] = new(16, 16, pixel),
            ["entity/chest/normal_left"] = new(16, 16, pixel),
            ["entity/chest/normal_right"] = new(16, 16, pixel)
        });
    }

    private static BlockEntityRenderState[] Chests(int count)
    {
        var states = new BlockEntityRenderState[count];
        for (var i = 0; i < count; i++)
        {
            states[i] = new BlockEntityRenderState(
                i % 32,
                64,
                i / 32,
                VanillaBlockEntityTypes.Chest,
                ChestType: (byte)(i % 3));
        }

        return states;
    }

    [Fact]
    public void AThousandChestsBuildAndTheSecondBuildAllocatesNothing()
    {
        var atlas = ChestAtlas();
        var states = Chests(1000);
        var mesher = new ChestMesher();

        // The first build sizes the output buffers; the next one must reuse them.
        mesher.Build(states, atlas);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var geometry = mesher.Build(states, atlas);
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(geometry.VertexFloats >= 1000 * 72, $"expected at least 72000 vertex floats, got {geometry.VertexFloats}");
        Assert.Equal(0, allocated);

        // Recorded so a future regression is visible in the test log even when the assertion passes.
        Console.WriteLine($"1000 chests: {elapsed.TotalMilliseconds:F2} ms, {geometry.VertexFloats} floats, {geometry.IndexCount} indices");
    }
}
