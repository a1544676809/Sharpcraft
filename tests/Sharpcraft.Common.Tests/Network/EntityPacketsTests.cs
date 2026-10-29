// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Entity packet parsing (docs/specs/network/entity-sync.md 3 and 4). Every case here pins one of the places
/// where 26.3 differs from older releases, because guessing any of them shifts every following field.
/// </summary>
public sealed class EntityPacketsTests
{
    [Fact]
    public void AddEntityParsesThreeAnglesAndACompressedVelocity()
    {
        Span<byte> buffer = stackalloc byte[128];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(42);
        writer.WriteUuid(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        writer.WriteVarInt(7);
        writer.WriteDouble(10.5);
        writer.WriteDouble(64.0);
        writer.WriteDouble(-3.25);
        WriteLpVec3(ref writer, 0.5f, 0f, 0f);
        writer.WriteByte(64);   // pitch, 90 degrees
        writer.WriteByte(32);   // yaw, 45 degrees
        writer.WriteByte(128);  // head yaw
        writer.WriteVarInt(3);

        var status = EntityPackets.TryReadAddEntity(buffer[..writer.Position], out var entity);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.NotNull(entity);
        Assert.Equal(42, entity!.Id);
        Assert.Equal(7, entity.TypeId);
        Assert.Equal(10.5, entity.X);
        Assert.Equal(64.0, entity.Y);
        Assert.Equal(-3.25, entity.Z);
        Assert.Equal(0.5f, entity.VelocityX, 2);
        Assert.Equal(90f, entity.Pitch, 2);
        Assert.Equal(45f, entity.Yaw, 2);
        Assert.Equal(180f, entity.HeadYaw, 2);
        Assert.Equal(3, entity.Data);
    }

    [Fact]
    public void RemoveEntitiesParsesEveryId()
    {
        Span<byte> buffer = stackalloc byte[16];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(3);
        writer.WriteVarInt(11);
        writer.WriteVarInt(12);
        writer.WriteVarInt(300);

        var status = EntityPackets.TryReadRemoveEntities(buffer[..writer.Position], out var ids);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal([11, 12, 300], ids);
    }

    [Fact]
    public void MoveEntityPosReadsThePackedPropertiesAndAFixedPointDelta()
    {
        Span<byte> buffer = stackalloc byte[32];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(5);
        writer.WriteVarInt(1);          // onGround, stepCount 0
        writer.WriteUnsignedShort(4096);   // +1.0 block on x
        writer.WriteUnsignedShort(0);      // keep y
        writer.WriteUnsignedShort(unchecked((ushort)(short)-2048)); // -0.5 on z

        var status = EntityPackets.TryReadMoveEntity(buffer[..writer.Position], hasPosition: true, hasRotation: false, out var move);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(5, move.Id);
        Assert.True(move.OnGround);
        Assert.True(move.HasPosition);
        Assert.False(move.HasRotation);
        Assert.Equal(1.0, move.DeltaX, 4);
        Assert.Equal(0.0, move.DeltaY, 4);
        Assert.Equal(-0.5, move.DeltaZ, 4);
    }

    [Fact]
    public void MoveEntityPosRotCarriesTheDeltaThenTheAngles()
    {
        Span<byte> buffer = stackalloc byte[32];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(6);
        writer.WriteVarInt(0);          // not on ground, stepCount 0
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(2048);   // +0.5 on y
        writer.WriteUnsignedShort(0);
        writer.WriteByte(128);          // yaw 180
        writer.WriteByte(64);           // pitch 90

        var status = EntityPackets.TryReadMoveEntity(buffer[..writer.Position], hasPosition: true, hasRotation: true, out var move);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(0.5, move.DeltaY, 4);
        Assert.True(move.HasRotation);
        Assert.Equal(180f, move.Yaw, 2);
        Assert.Equal(90f, move.Pitch, 2);
    }

    [Fact]
    public void MoveEntityRotUsesAPlainBooleanInsteadOfPackedProperties()
    {
        // The odd one out: no delta, no packed word. Reading properties here would consume the yaw byte.
        Span<byte> buffer = stackalloc byte[16];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(9);
        writer.WriteBool(true);
        writer.WriteByte(64);
        writer.WriteByte(0);

        var status = EntityPackets.TryReadMoveEntity(buffer[..writer.Position], hasPosition: false, hasRotation: true, out var move);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(9, move.Id);
        Assert.True(move.OnGround);
        Assert.False(move.HasPosition);
        Assert.Equal(90f, move.Yaw, 2);
    }

    [Fact]
    public void ASteppedDeltaSumsItsSteps()
    {
        Span<byte> buffer = stackalloc byte[48];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(10);
        writer.WriteVarInt((2 << 1) | 1);   // onGround, two steps
        writer.WriteVarInt(1);
        writer.WriteUnsignedShort(2048);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);
        writer.WriteVarInt(2);
        writer.WriteUnsignedShort(2048);
        writer.WriteUnsignedShort(0);
        writer.WriteUnsignedShort(0);

        var status = EntityPackets.TryReadMoveEntity(buffer[..writer.Position], hasPosition: true, hasRotation: false, out var move);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(1.0, move.DeltaX, 4);
    }

    [Fact]
    public void RotateHeadParsesTheAngleByte()
    {
        Span<byte> buffer = stackalloc byte[8];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(12);
        writer.WriteByte(192);

        var status = EntityPackets.TryReadRotateHead(buffer[..writer.Position], out var id, out var headYaw);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(12, id);
        Assert.Equal(270f, headYaw, 2);
    }

    [Fact]
    public void SetEntityMotionReadsTheCompressedVector()
    {
        Span<byte> buffer = stackalloc byte[16];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(13);
        WriteLpVec3(ref writer, 0f, -0.25f, 0f);

        var status = EntityPackets.TryReadSetEntityMotion(buffer[..writer.Position], out var motion);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(13, motion.Id);
        Assert.Equal(0f, motion.X, 3);
        Assert.Equal(-0.25f, motion.Y, 2);
    }

    [Fact]
    public void AZeroVectorIsASingleByte()
    {
        Span<byte> buffer = stackalloc byte[8];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(14);
        writer.WriteByte(0);

        var status = EntityPackets.TryReadSetEntityMotion(buffer[..writer.Position], out var motion);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(0f, motion.X);
        Assert.Equal(0f, motion.Y);
        Assert.Equal(0f, motion.Z);
    }

    [Fact]
    public void TeleportEntityParsesTheWholeState()
    {
        Span<byte> buffer = stackalloc byte[96];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(15);
        writer.WriteDouble(1.5);
        writer.WriteDouble(70.0);
        writer.WriteDouble(-2.5);
        writer.WriteDouble(0.1);
        writer.WriteDouble(-0.2);
        writer.WriteDouble(0.3);
        writer.WriteFloat(45f);
        writer.WriteFloat(15f);
        writer.WriteInt(0b101);   // x and z relative
        writer.WriteBool(true);

        var status = EntityPackets.TryReadTeleportEntity(buffer[..writer.Position], out var teleport);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(15, teleport.Id);
        Assert.Equal(1.5, teleport.X);
        Assert.Equal(-0.2, teleport.VelocityY);
        Assert.Equal(45f, teleport.Yaw);
        Assert.Equal(0b101, teleport.Relatives);
        Assert.True(teleport.OnGround);
    }

    /// <summary>
    /// Builds the compressed vector exactly as the wire format defines it: fifteen bit components scaled by a
    /// power of two, with the continuation flag in bit two.
    /// </summary>
    private static void WriteLpVec3(ref PayloadWriter writer, float x, float y, float z)
    {
        var max = MathF.Max(MathF.Abs(x), MathF.Max(MathF.Abs(y), MathF.Abs(z)));
        if (max < 3.051944088384301E-5f)
        {
            writer.WriteByte(0);
            return;
        }

        long scale = 1;
        while (scale < max)
        {
            scale <<= 1;
        }

        var partial = (scale & 3) != scale;
        var markers = partial ? (scale & 3) | 4 : scale;
        var buffer = markers
            | (PackLp(x / scale) << 3)
            | (PackLp(y / scale) << 18)
            | (PackLp(z / scale) << 33);

        writer.WriteByte((byte)buffer);
        writer.WriteByte((byte)(buffer >> 8));
        writer.WriteInt((int)(buffer >> 16));

        if (partial)
        {
            writer.WriteVarInt((int)(scale >> 2));
        }
    }

    private static long PackLp(float value) => (long)Math.Round(((value * 0.5f) + 0.5f) * 32766f);
}
