// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Login packet encodings (docs/specs/network/encryption.md).
/// </summary>
public sealed class LoginPacketTests
{
    [Fact]
    public void LoginStartRoundTrips()
    {
        var profileId = LoginPackets.OfflineProfileId("Steve");
        var buffer = new byte[64];

        var written = LoginPackets.WriteLoginStart(buffer, "Steve", profileId);

        Assert.Equal(
            DecodeStatus.Success,
            LoginPackets.TryReadLoginStart(buffer.AsSpan(0, written), out var name, out var decodedId));

        Assert.Equal("Steve", name);
        Assert.Equal(profileId, decodedId);
    }

    [Fact]
    public void UuidIsSixteenRawBytesWithoutALengthPrefix()
    {
        var buffer = new byte[64];
        var written = LoginPackets.WriteLoginStart(buffer, "abc", Guid.Empty);

        // id (1) + string length prefix (1) + 3 name bytes + 16 raw uuid bytes.
        Assert.Equal(21, written);
    }

    [Fact]
    public void LoginStartRejectsNamesAboveSixteenCharacters()
    {
        var buffer = new byte[128];
        var tooLong = new string('a', LoginPackets.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(
            () => LoginPackets.WriteLoginStart(buffer, tooLong, Guid.Empty));
    }

    [Theory]
    [InlineData("Notch", "b50ad385-829d-3141-a216-7e7d7539ba7f")]
    [InlineData("MCSProbe", "57045e05-95ce-3f40-acbe-ae90d3f74f5b")]
    public void OfflineProfileIdMatchesTheVanillaDerivation(string name, string expected)
    {
        Assert.Equal(Guid.Parse(expected), LoginPackets.OfflineProfileId(name));
    }

    [Fact]
    public void KeyPacketRoundTrips()
    {
        var encryptedSecret = new byte[128];
        var encryptedChallenge = new byte[128];
        System.Random.Shared.NextBytes(encryptedSecret);
        System.Random.Shared.NextBytes(encryptedChallenge);

        var buffer = new byte[512];
        var written = LoginPackets.WriteKey(buffer, encryptedSecret, encryptedChallenge);

        Assert.Equal(
            DecodeStatus.Success,
            LoginPackets.TryReadKey(buffer.AsSpan(0, written), out var secret, out var challenge));

        Assert.Equal(encryptedSecret, secret);
        Assert.Equal(encryptedChallenge, challenge);
    }

    [Fact]
    public void HelloPacketParsesLikeTheServerSendsIt()
    {
        // Synthesise a hello body: id, serverId, byte array key, byte array challenge, bool.
        var body = new List<byte>();
        var writerBuffer = new byte[512];
        var writer = new PayloadWriter(writerBuffer);
        writer.WriteVarInt(PacketIds.LoginClientbound.Hello);
        writer.WriteString(string.Empty, LoginPackets.MaxServerIdLength);
        writer.WriteByteArray(new byte[162]);
        writer.WriteByteArray([0x11, 0x22, 0x33, 0x44]);
        writer.WriteBool(true);
        body.AddRange(writerBuffer.AsSpan(0, writer.Position).ToArray());

        Assert.Equal(DecodeStatus.Success, LoginPackets.TryReadHello(body.ToArray(), out var request));
        Assert.Equal(string.Empty, request.ServerId);
        Assert.Equal(162, request.PublicKey.Length);
        Assert.Equal([0x11, 0x22, 0x33, 0x44], request.Challenge);
        Assert.True(request.ShouldAuthenticate);
    }

    [Fact]
    public void HelloRejectsAnOversizedPublicKey()
    {
        var writerBuffer = new byte[2048];
        var writer = new PayloadWriter(writerBuffer);
        writer.WriteVarInt(PacketIds.LoginClientbound.Hello);
        writer.WriteString("", LoginPackets.MaxServerIdLength);
        writer.WriteByteArray(new byte[LoginPackets.MaxPublicKeyBytes + 1]);
        writer.WriteByteArray([0x00]);
        writer.WriteBool(true);

        Assert.Equal(
            DecodeStatus.Invalid,
            LoginPackets.TryReadHello(writerBuffer.AsSpan(0, writer.Position), out _));
    }

    [Fact]
    public void LoginDisconnectCarriesAJsonReason()
    {
        const string reason = """{"text":"Failed to verify username!"}""";
        var buffer = new byte[128];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.LoginClientbound.LoginDisconnect);
        writer.WriteString(reason, 262144);

        Assert.Equal(
            DecodeStatus.Success,
            LoginPackets.TryReadLoginDisconnect(buffer.AsSpan(0, writer.Position), out var decoded));
        Assert.Equal(reason, decoded);
    }

    [Fact]
    public void CompressionThresholdParses()
    {
        var buffer = new byte[8];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.LoginClientbound.LoginCompression);
        writer.WriteVarInt(256);

        Assert.Equal(
            DecodeStatus.Success,
            LoginPackets.TryReadCompression(buffer.AsSpan(0, writer.Position), out var threshold));
        Assert.Equal(256, threshold);
    }

    [Fact]
    public void WrongPacketIdIsRejected()
    {
        var buffer = new byte[8];
        var written = VarInt.Write(buffer, PacketIds.LoginClientbound.Hello);

        Assert.Equal(
            DecodeStatus.Invalid,
            LoginPackets.TryReadLoginDisconnect(buffer.AsSpan(0, written), out _));
    }
}
