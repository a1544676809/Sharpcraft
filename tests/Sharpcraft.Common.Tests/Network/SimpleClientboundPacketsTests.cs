// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Decoders for small play clientbound packets (block_changed_ack, set_held_slot, set_experience).</summary>
public sealed class SimpleClientboundPacketsTests
{
    [Fact]
    public void BlockChangedAckReadsSequence()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadBlockChangedAck(new byte[] { 0x05 }, out var seq));
        Assert.Equal(5, seq);
    }

    [Fact]
    public void SetHeldSlotReadsSlot()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadSetHeldSlot(new byte[] { 0x03 }, out var slot));
        Assert.Equal(3, slot);
    }

    [Fact]
    public void SetExperienceReadsProgressTotalLevel()
    {
        // float 0.5 = 0x3F000000 (big-endian), total VarInt 100 = 0x64, level 7 = 0x07.
        var body = new byte[] { 0x3F, 0x00, 0x00, 0x00, 0x64, 0x07 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadSetExperience(body, out var progress, out var total, out var level));
        Assert.Equal(0.5f, progress);
        Assert.Equal(100, total);
        Assert.Equal(7, level);
    }

    [Fact]
    public void PlayerAbilitiesReadsFlagsAndSpeeds()
    {
        // flags 0x07 (invulnerable|flying|mayfly), flySpeed 0.05f, walkSpeed 0.1f.
        var body = new byte[] { 0x07, 0x3D, 0x4C, 0xCC, 0xCD, 0x3D, 0xCC, 0xCC, 0xCD };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadPlayerAbilities(body, out var flags, out var fly, out var walk));
        Assert.Equal(0x07, flags);
        Assert.Equal(0.05f, fly, 4);
        Assert.Equal(0.1f, walk, 4);
    }

    [Fact]
    public void SetSimulationDistanceReadsVarInt()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadSetSimulationDistance(new byte[] { 0x0A }, out var distance));
        Assert.Equal(10, distance);
    }

    [Fact]
    public void CooldownReadsGroupAndDuration()
    {
        // Identifier string "abc" (len 3) then VarInt duration 20.
        var body = new byte[] { 0x03, (byte)'a', (byte)'b', (byte)'c', 0x14 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadCooldown(body, out var group, out var duration));
        Assert.Equal("abc", group);
        Assert.Equal(20, duration);
    }

    [Fact]
    public void DamageEventReadsIdsWithoutPosition()
    {
        var body = new byte[] { 0x0A, 0x02, 0x00, 0x00, 0x00 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadDamageEvent(body, out var id, out var type, out var cause, out var direct, out var hasPos, out _, out _, out _));
        Assert.Equal(10, id);
        Assert.Equal(2, type);
        Assert.Equal(-1, cause);
        Assert.Equal(-1, direct);
        Assert.False(hasPos);
    }

    [Fact]
    public void HurtAnimationReadsIdAndYaw()
    {
        var body = new byte[] { 0x05, 0x42, 0xB4, 0x00, 0x00 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadHurtAnimation(body, out var id, out var yaw));
        Assert.Equal(5, id);
        Assert.Equal(90f, yaw);
    }

    [Fact]
    public void UpdateMobEffectReadsFields()
    {
        // entity 3, effect 1, amplifier 2, duration 200 (VarInt C8 01), flags 1.
        var body = new byte[] { 0x03, 0x01, 0x02, 0xC8, 0x01, 0x01 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadUpdateMobEffect(body, out var entity, out var effect, out var amp, out var duration, out var flags));
        Assert.Equal(3, entity);
        Assert.Equal(1, effect);
        Assert.Equal(2, amp);
        Assert.Equal(200, duration);
        Assert.Equal(1, flags);
    }

    [Fact]
    public void RemoveMobEffectReadsIds()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadRemoveMobEffect(new byte[] { 0x03, 0x01 }, out var entity, out var effect));
        Assert.Equal(3, entity);
        Assert.Equal(1, effect);
    }
}
