// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Reading <c>player_chat</c> (docs/modules/ui.md 8.4). Most of the packet is signing bookkeeping, but every field has
/// to be measured exactly: one wrong length and every following packet on the connection is misaligned.
/// </summary>
public sealed class PlayerChatPacketTests
{
    [Fact]
    public void TheSenderNameAndMessageAreReadFromASignedMessage()
    {
        var body = Body("hello there", unsignedContent: null, chatTypeName: "Alice");

        Assert.Equal(DecodeStatus.Success, PlayerChatPackets.TryReadPlayerChat(body, out var sender, out var text, out _, out _, out _));
        Assert.Equal("Alice", Join(sender));
        Assert.Equal("hello there", Join(text));
    }

    [Fact]
    public void AnUnsignedContentOverridesTheBody()
    {
        // The server rewrote the message, for example to filter it; what it put in the unsigned content is what it
        // wants shown.
        var body = Body("original", unsignedContent: "filtered", chatTypeName: "Alice");

        Assert.Equal(DecodeStatus.Success, PlayerChatPackets.TryReadPlayerChat(body, out _, out var text, out _, out _, out _));
        Assert.Equal("filtered", Join(text));
    }

    [Fact]
    public void AChatTypeWithATargetIsConsumedToo()
    {
        var body = Body("whisper", unsignedContent: null, chatTypeName: "Alice", target: "Bob");

        Assert.Equal(DecodeStatus.Success, PlayerChatPackets.TryReadPlayerChat(body, out var sender, out var text, out _, out _, out _));
        Assert.Equal("Alice", Join(sender));
        Assert.Equal("whisper", Join(text));
    }

    [Fact]
    public void APartiallyFilteredMessageCarriesItsMaskAndStillParses()
    {
        var body = Body("masked", unsignedContent: null, chatTypeName: "Alice", filterWords: 1);

        Assert.Equal(DecodeStatus.Success, PlayerChatPackets.TryReadPlayerChat(body, out _, out var text, out _, out _, out _));
        Assert.Equal("masked", Join(text));
    }

    [Fact]
    public void ATruncatedPacketReportsThatItNeedsMoreData()
    {
        var body = Body("hello", unsignedContent: null, chatTypeName: "Alice");

        Assert.Equal(
            DecodeStatus.NeedMoreData,
            PlayerChatPackets.TryReadPlayerChat(body[..^6], out _, out _, out _, out _, out _));
    }


    private static string Join(Sharpcraft.Common.Network.TextRun[] runs)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < runs.Length; index++)
        {
            builder.Append(runs[index].Text);
        }

        return builder.ToString();
    }

    private static byte[] Body(
        string content,
        string? unsignedContent,
        string chatTypeName,
        string? target = null,
        int filterWords = 0)
    {
        using var stream = new MemoryStream();
        WriteVarInt(stream, 7);                             // global index
        stream.Write(Guid.NewGuid().ToByteArray(bigEndian: true));
        WriteVarInt(stream, 3);                             // per sender index
        stream.WriteByte(0);                                // no signature
        WriteString(stream, content);
        WriteLong(stream, 1_700_000_000_000L);              // timestamp in milliseconds
        WriteLong(stream, 42L);                             // salt
        WriteVarInt(stream, 0);                             // no acknowledged signatures

        if (unsignedContent is null)
        {
            stream.WriteByte(0);
        }
        else
        {
            stream.WriteByte(1);
            stream.Write(Component(unsignedContent));
        }

        if (filterWords == 0)
        {
            WriteVarInt(stream, 0);                         // pass through
        }
        else
        {
            WriteVarInt(stream, 2);                         // partially filtered
            WriteVarInt(stream, filterWords);
            WriteLong(stream, 1L);
        }

        WriteVarInt(stream, 0);                             // chat type registry id
        stream.Write(Component(chatTypeName));

        if (target is null)
        {
            stream.WriteByte(0);
        }
        else
        {
            stream.WriteByte(1);
            stream.Write(Component(target));
        }

        return stream.ToArray();
    }

    /// <summary>A network NBT compound holding one string, which is the smallest usable component.</summary>
    private static byte[] Component(string text)
    {
        var compound = new Sharpcraft.Common.Nbt.NbtCompound();
        compound.Set("text", new Sharpcraft.Common.Nbt.NbtString(text));
        return Sharpcraft.Common.Nbt.NbtWriter.WriteNetworkRoot(compound);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
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

    private static void WriteLong(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
