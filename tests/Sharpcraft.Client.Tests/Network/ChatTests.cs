// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Network;

/// <summary>
/// Chat wire format and prompt behaviour (docs/modules/ui.md 8.4; the reference's chat packet and prompt).
/// </summary>
public sealed class ChatTests
{
    [Fact]
    public void AChatMessageCarriesTheZerosThatKeepItWellFormed()
    {
        var packet = PlayPackets.BuildChatMessage("hello", timestamp: 1234);
        var reader = new PayloadReader(packet);

        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var id));
        Assert.Equal(PacketIds.PlayServerbound.Chat, id);

        Assert.Equal(DecodeStatus.Success, reader.TryReadString(out var message));
        Assert.Equal("hello", message);

        Assert.Equal(DecodeStatus.Success, reader.TryReadLong(out var timestamp));
        Assert.Equal(1234, timestamp);

        Assert.Equal(DecodeStatus.Success, reader.TryReadLong(out var salt));
        Assert.Equal(0, salt);

        // No signature, then an offset, twenty clear bits and a checksum of zero.
        Assert.Equal(DecodeStatus.Success, reader.TryReadBool(out var hasSignature));
        Assert.False(hasSignature);
        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var offset));
        Assert.Equal(0, offset);

        var bits = 0;

        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(DecodeStatus.Success, reader.TryReadByte(out var value));
            bits |= value;
        }

        Assert.Equal(0, bits);
        Assert.Equal(DecodeStatus.Success, reader.TryReadByte(out var checksum));
        Assert.Equal(0, checksum);
        Assert.Equal(packet.Length, reader.Position);
    }

    [Fact]
    public void ACommandGoesOnItsOwnPacketWithoutTheSlash()
    {
        // The slash is what makes the line a command for the player; the reference strips it before sending, and a
        // server that receives it answers that the command is unknown.
        var packet = PlayPackets.BuildChatCommand("time set day");
        var reader = new PayloadReader(packet);

        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var id));
        Assert.Equal(PacketIds.PlayServerbound.ChatCommand, id);

        Assert.Equal(DecodeStatus.Success, reader.TryReadString(out var command));
        Assert.Equal("time set day", command);
        Assert.Equal(packet.Length, reader.Position);
    }

    [Fact]
    public void ACommandThatStillCarriesItsSlashIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PlayPackets.BuildChatCommand("/time set day"));
    }

    [Fact]
    public void AnOverlongLineIsRefusedRatherThanTruncatedSilently()
    {
        var tooLong = new string('a', PlayPackets.MaxChatLength + 1);

        Assert.Throws<ArgumentException>(() => PlayPackets.BuildChatMessage(tooLong, 0));
        Assert.Throws<ArgumentException>(() => PlayPackets.BuildChatCommand(string.Empty));
    }

    [Fact]
    public void TheSlashKeyLeavesTheSlashInTheBox()
    {
        var chat = new ChatInput();
        chat.Open("/");

        Assert.True(chat.IsOpen);
        Assert.Equal("/", chat.Text);

        chat.Append("tp".AsSpan());
        Assert.Equal("/tp", chat.Text);

        Assert.True(chat.TrySubmit(out var message, out var isCommand));
        Assert.Equal("/tp", message);
        Assert.True(isCommand);
        Assert.False(chat.IsOpen);
    }

    [Fact]
    public void APlainLineIsChatNotACommand()
    {
        var chat = new ChatInput();
        chat.Open();
        chat.Append("hi all".AsSpan());

        Assert.True(chat.TrySubmit(out var message, out var isCommand));
        Assert.Equal("hi all", message);
        Assert.False(isCommand);
    }

    [Fact]
    public void BackspaceRemovesOneCharacterAndStopsAtEmpty()
    {
        var chat = new ChatInput();
        chat.Open("ab");

        chat.Backspace();
        Assert.Equal("a", chat.Text);

        chat.Backspace();
        chat.Backspace();
        Assert.Equal(string.Empty, chat.Text);
    }

    [Fact]
    public void TypingStopsAtTheReferenceLimit()
    {
        var chat = new ChatInput();
        chat.Open();
        chat.Append(new string('x', ChatInput.MaxLength + 50).AsSpan());

        Assert.Equal(ChatInput.MaxLength, chat.Length);
    }

    [Fact]
    public void ClosingForgetsTheLineAndSubmittingBlankSendsNothing()
    {
        var chat = new ChatInput();
        chat.Open("draft");
        chat.Close();

        Assert.False(chat.IsOpen);
        Assert.Equal(string.Empty, chat.Text);

        chat.Open();
        Assert.False(chat.TrySubmit(out _, out _));
        Assert.False(chat.IsOpen);
    }
}
