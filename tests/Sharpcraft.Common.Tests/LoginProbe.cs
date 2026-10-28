// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net.Sockets;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Drives a real login handshake far enough to prove the crypto layer works: handshake,
/// login start, encryption request, encryption response, then decrypt whatever the server
/// sends next. No credentials are involved and no session is established.
/// </summary>
internal static class LoginProbe
{
    internal sealed record Result(
        bool EncryptionEnabled,
        bool LoggedIn,
        string Reason,
        string ServerId,
        int PublicKeyBytes,
        int ChallengeBytes,
        bool ShouldAuthenticate,
        string ServerHash);

    internal static Result Run(string address, string playerName, TimeSpan timeout)
    {
        var resolved = ServerAddressResolver.Resolve(address);

        using var tcp = new TcpClient();
        tcp.ConnectAsync(resolved.ConnectHost, resolved.ConnectPort).WaitAsync(timeout).GetAwaiter().GetResult();

        using var stream = tcp.GetStream();
        stream.ReadTimeout = (int)timeout.TotalMilliseconds;
        stream.WriteTimeout = (int)timeout.TotalMilliseconds;

        var frames = new PacketFrameBuffer();
        var receive = new byte[4096];
        AesCfb8? outbound = null;
        AesCfb8? inbound = null;
        var deadline = DateTime.UtcNow + timeout;

        void Send(ReadOnlySpan<byte> body)
        {
            var frame = PacketFrameBuffer.Wrap(body);
            outbound?.TransformInPlace(frame);
            stream.Write(frame);
            stream.Flush();
        }

        var handshake = new byte[512];
        var handshakeLength = HandshakePackets.WriteClientIntention(
            handshake, ProtocolConstants.Version, resolved.HandshakeHost, (ushort)resolved.HandshakePort, ProtocolConstants.IntentLogin);
        Send(handshake.AsSpan(0, handshakeLength));

        var loginStart = new byte[64];
        var loginStartLength = LoginPackets.WriteLoginStart(
            loginStart, playerName, LoginPackets.OfflineProfileId(playerName));
        Send(loginStart.AsSpan(0, loginStartLength));

        while (DateTime.UtcNow < deadline)
        {
            var status = frames.TryReadFrame(out var body);
            if (status == DecodeStatus.Invalid)
            {
                throw new InvalidDataException("malformed frame during login");
            }

            if (status == DecodeStatus.Success)
            {
                var reader = new PayloadReader(body);
                reader.TryReadVarInt(out var packetId);

                switch (packetId)
                {
                    case PacketIds.LoginClientbound.Hello:
                    {
                        if (LoginPackets.TryReadHello(body, out var request) != DecodeStatus.Success)
                        {
                            throw new InvalidDataException("malformed encryption request");
                        }

                        var secret = MinecraftLoginCrypto.GenerateSharedSecret();
                        var hash = MinecraftLoginCrypto.ServerHash(request.ServerId, secret, request.PublicKey);
                        var encryptedSecret = MinecraftLoginCrypto.EncryptRsa(request.PublicKey, secret);
                        var encryptedChallenge = MinecraftLoginCrypto.EncryptRsa(request.PublicKey, request.Challenge);

                        var response = new byte[encryptedSecret.Length + encryptedChallenge.Length + 16];
                        var responseLength = LoginPackets.WriteKey(response, encryptedSecret, encryptedChallenge);

                        // The key packet itself is sent in the clear; encryption starts right after.
                        Send(response.AsSpan(0, responseLength));

                        outbound = new AesCfb8(secret, decrypt: false);
                        inbound = new AesCfb8(secret, decrypt: true);

                        continue;
                    }

                    case PacketIds.LoginClientbound.LoginDisconnect:
                    {
                        if (LoginPackets.TryReadLoginDisconnect(body, out var reason) != DecodeStatus.Success)
                        {
                            throw new InvalidDataException("malformed login disconnect");
                        }

                        return new Result(inbound is not null, false, reason, string.Empty, 0, 0, false, string.Empty);
                    }

                    case PacketIds.LoginClientbound.LoginFinished:
                        return new Result(inbound is not null, true, string.Empty, string.Empty, 0, 0, false, string.Empty);

                    case PacketIds.LoginClientbound.LoginCompression:
                        // Abort here: the probe intentionally does not chase the whole session.
                        LoginPackets.TryReadCompression(body, out var threshold);
                        return new Result(inbound is not null, false, $"compression={threshold}", string.Empty, 0, 0, false, string.Empty);
                }
            }

            var read = stream.Read(receive, 0, receive.Length);
            if (read == 0)
            {
                throw new InvalidDataException("connection closed during login");
            }

            var chunk = receive.AsSpan(0, read);
            inbound?.TransformInPlace(chunk);
            frames.Append(chunk);
        }

        throw new TimeoutException("login probe timed out");
    }
}
