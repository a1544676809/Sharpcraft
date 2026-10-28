// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;

namespace Sharpcraft.Common.Network;

/// <summary>Outcome of a login attempt (docs/specs/network/encryption.md 1).</summary>
/// <param name="Success">True when the server accepted the login.</param>
/// <param name="Encrypted">Whether the encryption handshake completed.</param>
/// <param name="CompressionThreshold">Negotiated threshold, or -1 when the server did not ask for compression.</param>
/// <param name="ServerHash">Signed server hash submitted to the session server.</param>
/// <param name="Reason">Disconnect reason, when the login failed.</param>
public sealed record LoginOutcome(
    bool Success,
    bool Encrypted,
    int CompressionThreshold,
    string ServerHash,
    string Reason);

/// <summary>
/// Login phase client: handshake, login start, encryption exchange, session registration, then
/// waits for the login finish so the caller can switch to the configuration phase.
/// </summary>
public static class MinecraftLoginClient
{
    /// <summary>Runs the login phase against a resolved endpoint.</summary>
    /// <param name="address">Server address as typed; SRV resolution is applied.</param>
    /// <param name="profile">Player name and profile id.</param>
    /// <param name="sessionToken">Minecraft session token.</param>
    /// <param name="joinAsync">Callback that registers the hash with the session server.</param>
    /// <param name="protocolVersion">Protocol version to advertise.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<(LoginOutcome Outcome, MinecraftConnection? Connection)> LoginAsync(
        string address,
        (string Name, string ProfileId) profile,
        string sessionToken,
        Func<string, string, CancellationToken, Task> joinAsync,
        int protocolVersion = ProtocolConstants.Version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);
        ArgumentNullException.ThrowIfNull(joinAsync);

        var resolved = ServerAddressResolver.Resolve(address);
        var connection = await MinecraftConnection
            .ConnectAsync(resolved.ConnectHost, resolved.ConnectPort, TimeSpan.FromSeconds(20), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var handshake = new byte[512];
            var handshakeLength = HandshakePackets.WriteClientIntention(
                handshake,
                protocolVersion,
                resolved.HandshakeHost,
                (ushort)resolved.HandshakePort,
                ProtocolConstants.IntentLogin);
            connection.Send(handshake.AsSpan(0, handshakeLength));

            var profileId = ParseProfileId(profile.ProfileId);
            var loginStart = new byte[64];
            var loginStartLength = LoginPackets.WriteLoginStart(loginStart, profile.Name, profileId);
            connection.Send(loginStart.AsSpan(0, loginStartLength));

            var serverHash = string.Empty;
            while (true)
            {
                var body = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                var reader = new PayloadReader(body);
                if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
                {
                    throw new InvalidDataException("empty packet during login");
                }

                if (packetId == PacketIds.LoginClientbound.Hello)
                {
                    if (LoginPackets.TryReadHello(body, out var request) != DecodeStatus.Success)
                    {
                        throw new InvalidDataException("malformed encryption request");
                    }

                    var secret = MinecraftLoginCrypto.GenerateSharedSecret();
                    serverHash = MinecraftLoginCrypto.ServerHash(request.ServerId, secret, request.PublicKey);

                    // Register the join before completing encryption, mirroring vanilla ordering.
                    if (request.ShouldAuthenticate)
                    {
                        await joinAsync(sessionToken, serverHash, cancellationToken).ConfigureAwait(false);
                    }

                    var encryptedSecret = MinecraftLoginCrypto.EncryptRsa(request.PublicKey, secret);
                    var encryptedChallenge = MinecraftLoginCrypto.EncryptRsa(request.PublicKey, request.Challenge);
                    var keyPacket = new byte[encryptedSecret.Length + encryptedChallenge.Length + 16];
                    var keyLength = LoginPackets.WriteKey(keyPacket, encryptedSecret, encryptedChallenge);

                    connection.Send(keyPacket.AsSpan(0, keyLength));
                    connection.EnableEncryption(secret);
                    continue;
                }

                if (packetId == PacketIds.LoginClientbound.LoginCompression)
                {
                    if (LoginPackets.TryReadCompression(body, out var threshold) != DecodeStatus.Success)
                    {
                        throw new InvalidDataException("malformed compression packet");
                    }

                    // The packet that carried the threshold was still uncompressed.
                    connection.EnableCompression(threshold);
                    continue;
                }

                if (packetId == PacketIds.LoginClientbound.LoginDisconnect)
                {
                    LoginPackets.TryReadLoginDisconnect(body, out var reason);
                    connection.Dispose();
                    return (new LoginOutcome(false, connection.Encrypted, connection.CompressionThreshold, serverHash, reason), null);
                }

                if (packetId == PacketIds.LoginClientbound.LoginFinished)
                {
                    var outcome = new LoginOutcome(
                        true,
                        connection.Encrypted,
                        connection.Compressed ? connection.CompressionThreshold : -1,
                        serverHash,
                        string.Empty);
                    return (outcome, connection);
                }

                if (packetId == PacketIds.LoginClientbound.CustomQuery || packetId == PacketIds.LoginClientbound.CookieRequest)
                {
                    // Not implemented yet; ignoring is safe because both are advisory.
                    continue;
                }
            }
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Converts a profile id (with or without dashes) into a Guid.</summary>
    public static Guid ParseProfileId(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        if (Guid.TryParse(profileId, out var parsed))
        {
            return parsed;
        }

        if (profileId.Length == 32 && Guid.TryParseExact(profileId, "N", out var compact))
        {
            return compact;
        }

        throw new FormatException($"profile id '{profileId}' is not a UUID");
    }

    /// <summary>Maps a failed join to a classified error.</summary>
    public static string Describe(HttpStatusCode status, string body) =>
        $"join failed ({(int)status}): {(string.IsNullOrWhiteSpace(body) ? "no body" : body)}";
}
