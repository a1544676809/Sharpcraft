// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers.Binary;
using System.Text;

namespace MinecraftCSharp.Common.Network;

/// <summary>Parsed encryption request (clientbound Hello).</summary>
/// <param name="ServerId">Server id string used in the session hash.</param>
/// <param name="PublicKey">DER-encoded RSA public key, exactly as received.</param>
/// <param name="Challenge">Random challenge to prove key possession.</param>
/// <param name="ShouldAuthenticate">False for offline-mode servers.</param>
public sealed record EncryptionRequest(string ServerId, byte[] PublicKey, byte[] Challenge, bool ShouldAuthenticate);

/// <summary>
/// Login-phase packets (docs/specs/network/encryption.md).
/// </summary>
public static class LoginPackets
{
    /// <summary>Maximum player name length on the wire.</summary>
    public const int MaxNameLength = 16;

    /// <summary>Maximum server id length on the wire.</summary>
    public const int MaxServerIdLength = 20;

    /// <summary>Largest public key we accept (2048-bit DER is 294 bytes).</summary>
    public const int MaxPublicKeyBytes = 512;

    /// <summary>Largest challenge we accept.</summary>
    public const int MaxChallengeBytes = 64;

    /// <summary>Encodes a login start body.</summary>
    public static int WriteLoginStart(Span<byte> destination, string name, Guid profileId)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.LoginServerbound.LoginStart);
        writer.WriteString(name, MaxNameLength);
        writer.WriteUuid(profileId);
        return writer.Position;
    }

    /// <summary>Decodes a login start body.</summary>
    public static DecodeStatus TryReadLoginStart(ReadOnlySpan<byte> body, out string name, out Guid profileId)
    {
        name = string.Empty;
        profileId = Guid.Empty;

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var id);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (id != PacketIds.LoginServerbound.LoginStart)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadString(out name, MaxNameLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        return reader.TryReadUuid(out profileId);
    }

    /// <summary>Encodes an encryption response body.</summary>
    public static int WriteKey(Span<byte> destination, ReadOnlySpan<byte> encryptedSecret, ReadOnlySpan<byte> encryptedChallenge)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.LoginServerbound.Key);
        writer.WriteByteArray(encryptedSecret);
        writer.WriteByteArray(encryptedChallenge);
        return writer.Position;
    }

    /// <summary>Decodes an encryption response body.</summary>
    public static DecodeStatus TryReadKey(
        ReadOnlySpan<byte> body,
        out byte[] encryptedSecret,
        out byte[] encryptedChallenge)
    {
        encryptedSecret = [];
        encryptedChallenge = [];

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var id);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (id != PacketIds.LoginServerbound.Key)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadByteArray(out encryptedSecret, MaxPublicKeyBytes);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        return reader.TryReadByteArray(out encryptedChallenge, MaxPublicKeyBytes);
    }

    /// <summary>Decodes an encryption request (clientbound Hello).</summary>
    public static DecodeStatus TryReadHello(ReadOnlySpan<byte> body, out EncryptionRequest request)
    {
        request = new EncryptionRequest(string.Empty, [], [], false);

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var id);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (id != PacketIds.LoginClientbound.Hello)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadString(out var serverId, MaxServerIdLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadByteArray(out var publicKey, MaxPublicKeyBytes);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadByteArray(out var challenge, MaxChallengeBytes);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadBool(out var shouldAuthenticate);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        request = new EncryptionRequest(serverId, publicKey, challenge, shouldAuthenticate);
        return DecodeStatus.Success;
    }

    /// <summary>Reads the JSON reason of a login disconnect.</summary>
    public static DecodeStatus TryReadLoginDisconnect(ReadOnlySpan<byte> body, out string reason)
    {
        reason = string.Empty;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success || id != PacketIds.LoginClientbound.LoginDisconnect)
        {
            return DecodeStatus.Invalid;
        }

        return reader.TryReadString(out reason, 262144);
    }

    /// <summary>Reads the compression threshold advertisement.</summary>
    public static DecodeStatus TryReadCompression(ReadOnlySpan<byte> body, out int threshold)
    {
        threshold = 0;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success || id != PacketIds.LoginClientbound.LoginCompression)
        {
            return DecodeStatus.Invalid;
        }

        return reader.TryReadVarInt(out threshold);
    }

    /// <summary>Builds an offline-mode UUID the way vanilla derives it from the name.</summary>
    public static Guid OfflineProfileId(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);  // version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);  // RFC 4122 variant
        return new Guid(hash, bigEndian: true);
    }
}
