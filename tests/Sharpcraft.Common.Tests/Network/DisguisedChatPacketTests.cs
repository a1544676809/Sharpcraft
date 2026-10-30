// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// The two chat packets that are not a player message: a server message on someone's behalf and an instruction to
/// delete a line (docs/specs/network/protocol-phases.md). Both reuse the chat type binding, so the shapes stay in one
/// place instead of drifting apart.
/// </summary>
public sealed class DisguisedChatPacketTests
{
    [Fact]
    public void ADeletionCarriesTheSignatureOfTheMessageItRemoves()
    {
        // VarInt length then the bytes.
        byte[] body = [3, 0xAA, 0xBB, 0xCC];

        Assert.Equal(DecodeStatus.Success, DeleteChatPackets.TryRead(body, out var signature));
        Assert.Equal([0xAA, 0xBB, 0xCC], signature);
    }

    [Fact]
    public void ATruncatedDeletionIsRejectedRatherThanGuessed()
    {
        byte[] body = [3, 0xAA];

        Assert.Equal(DecodeStatus.NeedMoreData, DeleteChatPackets.TryRead(body, out _));
    }

    [Fact]
    public void ADisguisedMessageIsReadWithItsChatTypeBinding()
    {
        var body = BuildDisguised(
            message: "waves",
            chatTypeId: 2,
            name: "Alice",
            target: null);

        Assert.Equal(
            DecodeStatus.Success,
            DisguisedChatPackets.TryRead(body, out var message, out var chatTypeId, out var sender, out var target));

        Assert.Equal("waves", string.Concat(message.Select(run => run.Text)));
        Assert.Equal(2, chatTypeId);
        Assert.Equal("Alice", string.Concat(sender.Select(run => run.Text)));
        Assert.Empty(target);
    }

    [Fact]
    public void ADisguisedDirectMessageKeepsItsTarget()
    {
        var body = BuildDisguised(message: "hi there", chatTypeId: 4, name: "Alice", target: "Bob");

        Assert.Equal(
            DecodeStatus.Success,
            DisguisedChatPackets.TryRead(body, out _, out _, out _, out var target));

        Assert.Equal("Bob", string.Concat(target.Select(run => run.Text)));
    }

    private static byte[] BuildDisguised(string message, int chatTypeId, string name, string? target)
    {
        var stream = new MemoryStream();

        WriteComponent(stream, message);
        WriteVarInt(stream, chatTypeId);
        WriteComponent(stream, name);
        stream.WriteByte(target is null ? (byte)0 : (byte)1);

        if (target is not null)
        {
            WriteComponent(stream, target);
        }

        return stream.ToArray();
    }

    private static void WriteComponent(Stream stream, string text)
    {
        var compound = new Nbt.NbtCompound();
        compound.Set("text", new Nbt.NbtString(text));

        stream.Write(Nbt.NbtWriter.WriteNetworkRoot(compound));
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var remaining = (uint)value;

        while (remaining >= 0x80)
        {
            stream.WriteByte((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte)remaining);
    }
}
