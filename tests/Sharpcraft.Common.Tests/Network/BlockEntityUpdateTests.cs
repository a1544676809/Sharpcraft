// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// The single block entity update packet (docs/specs/quality/vanilla-equivalence-matrix.md row 6).
/// </summary>
/// <remarks>
/// The position is packed into one long: X in the top twenty six bits, Z in the next twenty six, Y in the low twelve,
/// all sign extended. The test uses a negative Z and a negative Y so a missing sign extension shows up.
/// </remarks>
public sealed class BlockEntityUpdateTests
{
    [Fact]
    public void ThePackedPositionIsUnpackedWithSigns()
    {
        const int x = 100;
        const int y = -20;
        const int z = -200;

        var body = Build(x, y, z, typeId: 3);

        Assert.Equal(DecodeStatus.Success, BlockEntityPackets.TryReadBlockEntityData(body, out var update));
        Assert.Equal(100, update.X);
        Assert.Equal(-20, update.Y);
        Assert.Equal(-200, update.Z);
        Assert.Equal(3, update.TypeId);
        Assert.NotNull(update.Tag);
        Assert.Equal(100, update.Tag!.GetInt("X"));
    }

    [Fact]
    public void VanillaBlockEntityTypesMatch26_3AndAddBlockEntityReplacesAtSamePosition()
    {
        Assert.Equal(49, VanillaBlockEntityTypes.Count);
        Assert.Equal(0, VanillaBlockEntityTypes.IdOf("furnace"));
        Assert.Equal(1, VanillaBlockEntityTypes.IdOf("chest"));
        Assert.Equal(24, VanillaBlockEntityTypes.IdOf("shulker_box"));
        Assert.Equal(48, VanillaBlockEntityTypes.IdOf("potent_sulfur"));
        Assert.True(VanillaBlockEntityTypes.IsChestLike(1));

        Assert.Equal(DecodeStatus.Success, BlockEntityPackets.TryReadBlockEntityData(Build(0, 0, 0, 1, 10), out var first));
        Assert.Equal(DecodeStatus.Success, BlockEntityPackets.TryReadBlockEntityData(Build(0, 0, 0, 24, 99), out var second));

        var chunk = new LevelChunk(0, 0, -4, 24);
        chunk.AddBlockEntity(new BlockEntityRecord(5, 64, 7, first.TypeId, first.Tag));
        chunk.AddBlockEntity(new BlockEntityRecord(5, 64, 7, second.TypeId, second.Tag));

        Assert.Single(chunk.BlockEntities);
        Assert.Equal(24, chunk.BlockEntities[0].TypeId);
        Assert.Equal(99, chunk.BlockEntities[0].Tag!.GetInt("X"));
    }

    [Fact]
    public void ANonBlockEntityPacketIsRejected()
    {
        var body = Build(0, 0, 0, 3);
        body[0] = 45;   // keep alive

        Assert.Equal(DecodeStatus.Invalid, BlockEntityPackets.TryReadBlockEntityData(body, out _));
    }

    private static byte[] Build(int x, int y, int z, int typeId, int tagValue = 100)
    {
        var packed = ((long)(x & 0x3FFFFFF) << 38) | ((long)(z & 0x3FFFFFF) << 12) | (uint)(y & 0xFFF);
        var buffer = new byte[64];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayClientbound.BlockEntityData);
        writer.WriteLong(packed);
        writer.WriteVarInt(typeId);

        // An unnamed compound with a single int entry, then End.
        var tag = new byte[]
        {
            0x0A, 0x03, 0x00, 0x01, (byte)'X',
            (byte)(tagValue >> 24), (byte)(tagValue >> 16), (byte)(tagValue >> 8), (byte)tagValue,
            0x00
        };
        writer.WriteRaw(tag);

        return buffer.AsSpan(0, writer.Position).ToArray();
    }
}
