// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

public sealed class PacketCompressionTests
{
    [Fact]
    public void CompressAndDecompressRoundTripAboveAndBelowThreshold()
    {
        const int threshold = 64;

        var small = new byte[] { 1, 2, 3, 4, 5 };
        var smallDest = new byte[128];
        var smallWritten = PacketCompression.Compress(smallDest, small, threshold);
        Assert.Equal(DecodeStatus.Success, PacketCompression.TryDecompress(smallDest.AsSpan(0, smallWritten), threshold, out var smallOut));
        Assert.Equal(small, smallOut);

        var large = new byte[512];
        for (var i = 0; i < large.Length; i++)
        {
            large[i] = (byte)(i % 17);
        }

        var largeDest = new byte[1024];
        var largeWritten = PacketCompression.Compress(largeDest, large, threshold);
        Assert.Equal(DecodeStatus.Success, PacketCompression.TryDecompress(largeDest.AsSpan(0, largeWritten), threshold, out var largeOut));
        Assert.Equal(large, largeOut);
    }

    [Fact]
    public async Task ConcurrentThreadsDoNotCorruptThreadStaticCompressStream()
    {
        const int threshold = 16;

        Task RunWorker(byte seed, int length) => Task.Run(() =>
        {
            var source = new byte[length];
            var dest = new byte[length + 128];

            for (var iter = 0; iter < 50; iter++)
            {
                for (var i = 0; i < source.Length; i++)
                {
                    source[i] = (byte)((seed + iter + i) & 0xFF);
                }

                var written = PacketCompression.Compress(dest, source, threshold);
                Assert.Equal(
                    DecodeStatus.Success,
                    PacketCompression.TryDecompress(dest.AsSpan(0, written), threshold, out var decoded));
                Assert.Equal(source, decoded);
            }
        });

        await Task.WhenAll(RunWorker(1, 128), RunWorker(97, 1024));
    }
}
