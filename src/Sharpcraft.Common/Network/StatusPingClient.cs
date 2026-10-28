// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace Sharpcraft.Common.Network;

/// <summary>Result of a server list ping (docs/specs/network/protocol-phases.md).</summary>
/// <param name="ProtocolVersion">Protocol reported by the server.</param>
/// <param name="VersionName">Human readable version string.</param>
/// <param name="MaxPlayers">Advertised player cap.</param>
/// <param name="OnlinePlayers">Advertised online count.</param>
/// <param name="Description">Best-effort MOTD text.</param>
/// <param name="LatencyMilliseconds">Round trip time of the ping/pong pair.</param>
public sealed record StatusPingResult(
    int ProtocolVersion,
    string VersionName,
    int MaxPlayers,
    int OnlinePlayers,
    string Description,
    long LatencyMilliseconds);

/// <summary>
/// Minimal server list ping: handshake (intention 1), status request, status response and a
/// ping/pong round trip. This is the smallest end-to-end proof that the codecs match a real
/// vanilla server, so it doubles as the protocol smoke test.
/// </summary>
public static class StatusPingClient
{
    private const int ReceiveBufferSize = 4096;

    /// <summary>Queries a server and returns its status.</summary>
    /// <exception cref="SocketException">The connection failed.</exception>
    /// <exception cref="TimeoutException">The server did not answer in time.</exception>
    /// <exception cref="InvalidDataException">The server spoke something unexpected.</exception>
    public static StatusPingResult Query(string address, TimeSpan timeout, int protocolVersion = ProtocolConstants.Version)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);

        var resolved = ServerAddressResolver.Resolve(address);
        return Query(
            resolved.ConnectHost,
            resolved.ConnectPort,
            timeout,
            protocolVersion,
            resolved.HandshakeHost,
            resolved.HandshakePort);
    }

    /// <summary>Queries a server through an already resolved TCP endpoint.</summary>
    public static StatusPingResult Query(
        string host,
        int port,
        TimeSpan timeout,
        int protocolVersion = ProtocolConstants.Version) =>
        Query(host, port, timeout, protocolVersion, host, port);

    /// <summary>Queries a server, advertising a possibly different address in the handshake.</summary>
    public static StatusPingResult Query(
        string connectHost,
        int connectPort,
        TimeSpan timeout,
        int protocolVersion,
        string handshakeHost,
        int handshakePort)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectHost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connectPort);

        using var client = new TcpClient();
        client.ConnectAsync(connectHost, connectPort).WaitAsync(timeout).GetAwaiter().GetResult();

        using var stream = client.GetStream();
        stream.ReadTimeout = (int)timeout.TotalMilliseconds;
        stream.WriteTimeout = (int)timeout.TotalMilliseconds;

        SendHandshake(stream, handshakeHost, handshakePort, protocolVersion);
        SendFrame(stream, BuildStatusRequest());

        var (json, latency) = AwaitStatus(stream, timeout);
        return Parse(json, latency);
    }

    private static StatusPingResult Parse(string json, long latency)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var versionName = string.Empty;
        var reportedProtocol = 0;
        if (root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Object)
        {
            if (version.TryGetProperty("name", out var name))
            {
                versionName = name.GetString() ?? string.Empty;
            }

            if (version.TryGetProperty("protocol", out var protocol) && protocol.ValueKind == JsonValueKind.Number)
            {
                reportedProtocol = protocol.GetInt32();
            }
        }

        var maxPlayers = 0;
        var onlinePlayers = 0;
        if (root.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Object)
        {
            if (players.TryGetProperty("max", out var max) && max.ValueKind == JsonValueKind.Number)
            {
                maxPlayers = max.GetInt32();
            }

            if (players.TryGetProperty("online", out var online) && online.ValueKind == JsonValueKind.Number)
            {
                onlinePlayers = online.GetInt32();
            }
        }

        var description = root.TryGetProperty("description", out var motd) ? ReadMotd(motd) : string.Empty;

        return new StatusPingResult(reportedProtocol, versionName, maxPlayers, onlinePlayers, description, latency);
    }

    private static void SendHandshake(Stream stream, string host, int port, int protocolVersion)
    {
        var body = new byte[512];
        var written = HandshakePackets.WriteClientIntention(
            body, protocolVersion, host, (ushort)port, ProtocolConstants.IntentStatus);
        SendFrame(stream, body.AsSpan(0, written));
    }

    private static byte[] BuildStatusRequest()
    {
        var body = new byte[8];
        var written = HandshakePackets.WriteStatusRequest(body);
        return body.AsSpan(0, written).ToArray();
    }

    private static byte[] BuildPingPayload()
    {
        var body = new byte[16];
        var written = HandshakePackets.WritePingRequest(body, Environment.TickCount64);
        return body.AsSpan(0, written).ToArray();
    }

    private static void SendFrame(Stream stream, ReadOnlySpan<byte> body)
    {
        var frame = PacketFrameBuffer.Wrap(body);
        stream.Write(frame);
        stream.Flush();
    }

    private static (string Json, long LatencyMilliseconds) AwaitStatus(Stream stream, TimeSpan timeout)
    {
        var buffer = new PacketFrameBuffer();
        var receive = new byte[ReceiveBufferSize];
        var stopwatch = Stopwatch.StartNew();
        var deadline = Stopwatch.StartNew();
        var pingSentAt = -1L;
        string? json = null;

        while (deadline.Elapsed < timeout)
        {
            var status = buffer.TryReadFrame(out var body);
            if (status == DecodeStatus.Invalid)
            {
                throw new InvalidDataException("server sent a malformed frame");
            }

            if (status == DecodeStatus.Success)
            {
                var reader = new PayloadReader(body);
                if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
                {
                    throw new InvalidDataException("empty packet body");
                }

                if (packetId == PacketIds.StatusClientbound.StatusResponse)
                {
                    if (HandshakePackets.TryReadStatusResponse(body, out var received) != DecodeStatus.Success)
                    {
                        throw new InvalidDataException("malformed status response");
                    }

                    json = received;

                    // Send the ping only after the status arrived, so the measured round trip
                    // has no queued write in front of it.
                    stopwatch.Restart();
                    SendFrame(stream, BuildPingPayload());
                    pingSentAt = stopwatch.ElapsedTicks;
                    continue;
                }

                if (packetId == PacketIds.StatusClientbound.PongResponse)
                {
                    if (json is null)
                    {
                        throw new InvalidDataException("pong before status response");
                    }

                    if (HandshakePackets.TryReadPingPayload(body, out _) != DecodeStatus.Success)
                    {
                        throw new InvalidDataException("malformed pong payload");
                    }

                    var latency = pingSentAt < 0
                        ? stopwatch.ElapsedMilliseconds
                        : (long)((stopwatch.ElapsedTicks - pingSentAt) * 1000.0 / Stopwatch.Frequency);
                    return (json, latency);
                }
            }

            var read = stream.Read(receive, 0, receive.Length);
            if (read == 0)
            {
                throw new InvalidDataException("connection closed before the status handshake finished");
            }

            buffer.Append(receive.AsSpan(0, read));
        }

        throw new TimeoutException("no status response within the timeout");
    }

    private static string ReadMotd(JsonElement description) => description.ValueKind switch
    {
        JsonValueKind.String => description.GetString() ?? string.Empty,
        JsonValueKind.Object when description.TryGetProperty("text", out var text) => text.GetString() ?? string.Empty,
        _ => string.Empty
    };
}
