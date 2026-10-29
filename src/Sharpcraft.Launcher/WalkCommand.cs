// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Launcher;

/// <summary>
/// Walks a player on a live server without rendering, so the movement packets can be verified against a real
/// server: the evidence is whether the server answers, corrects us, or drops us for moving too quickly
/// (docs/specs/network/play-movement.md 3).
/// </summary>
internal static class WalkCommand
{
    /// <summary>Result of a walk run.</summary>
    /// <param name="TicksSent">Movement packets sent.</param>
    /// <param name="Corrections">Server position corrections received.</param>
    /// <param name="Distance">Distance the player believed it travelled.</param>
    /// <param name="DisconnectReason">Reason the server gave, or empty.</param>
    /// <param name="Survived">True when the connection was still open at the end.</param>
    internal sealed record Result(int TicksSent, int Corrections, double Distance, string DisconnectReason, bool Survived);

    /// <summary>Walks forward for a bounded time.</summary>
    /// <param name="connection">Play-phase connection.</param>
    /// <param name="seconds">How long to walk.</param>
    /// <param name="log">Trace sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<Result> RunAsync(
        MinecraftConnection connection,
        double seconds,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(log);

        // A flat world at y = 0 keeps gravity honest without needing chunk data: standing on it exercises
        // the same travel code as the real world.
        var player = new PlayerEntity(new FlatWorld(), new Vector3(0.5f, 0f, 0.5f));
        var sync = new PlayerSync();
        var start = player.Position;

        var sent = 0;
        var corrections = 0;
        var reason = string.Empty;
        var survived = false;

        var clock = Stopwatch.StartNew();
        var interval = TimeSpan.FromMilliseconds(50);

        // The server teleports a joining player to spawn and waits for the acknowledgement. Moving before
        // answering that teleport is a protocol violation, so the walk waits for it first.
        var joinTeleportSeen = false;
        var waited = Stopwatch.StartNew();

        while (!joinTeleportSeen && waited.Elapsed.TotalSeconds < 5 && !cancellationToken.IsCancellationRequested)
        {
            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeTimeout.CancelAfter(TimeSpan.FromMilliseconds(500));

            try
            {
                var body = await connection.ReceiveAsync(handshakeTimeout.Token).ConfigureAwait(false);
                var reader = new PayloadReader(body);
                reader.TryReadVarInt(out var packetId);

                switch (packetId)
                {
                    case PacketIds.PlayClientbound.KeepAlive:
                        reader.TryReadLong(out var keepAliveId);
                        if (!TrySend(connection, PlayPackets.BuildKeepAliveReply(keepAliveId), ref reason))
                        {
                            return new Result(sent, corrections, 0, reason, false);
                        }

                        break;

                    case PacketIds.PlayClientbound.PlayerPosition:
                        if (PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var joinCorrection) == DecodeStatus.Success &&
                            joinCorrection is not null)
                        {
                            var (id, snapped, distance) = ApplyTeleport(connection, sync, player, joinCorrection);
                            corrections++;
                            joinTeleportSeen = true;
                            log($"join teleport {id}: deviation {distance:F2} snapped={snapped} (accepted)");
                        }

                        break;

                    default:
                        log($"join phase packet id {packetId} ({body.Length} bytes)");
                        break;

                    case PacketIds.PlayClientbound.Disconnect:
                        reason = DescribeDisconnect(body, reader);
                        log($"disconnected during join: {reason}");
                        return new Result(sent, corrections, 0, reason, false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                log("waiting for the join teleport...");
            }
        }

        log(joinTeleportSeen ? "join teleport accepted, walking" : "no join teleport arrived, walking anyway");

        while (clock.Elapsed.TotalSeconds < seconds && !cancellationToken.IsCancellationRequested)
        {
            // Receive whatever is pending, with a short timeout so the walk keeps its 20 Hz cadence.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(20));

            try
            {
                var body = await connection.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                var reader = new PayloadReader(body);
                reader.TryReadVarInt(out var packetId);

                switch (packetId)
                {
                    case PacketIds.PlayClientbound.KeepAlive:
                        reader.TryReadLong(out var keepAliveId);
                        connection.Send(PlayPackets.BuildKeepAliveReply(keepAliveId));
                        break;

                    case PacketIds.PlayClientbound.Ping:
                        reader.TryReadInt(out var pingId);
                        connection.Send(PlayPackets.BuildPongReply(pingId));
                        break;

                    case PacketIds.PlayClientbound.PlayerPosition:
                        if (PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var correction) == DecodeStatus.Success &&
                            correction is not null)
                        {
                            var (_, snapped, distance) = ApplyTeleport(connection, sync, player, correction);
                            corrections++;
                            log($"correction {corrections}: deviation {distance:F2} snapped={snapped}");
                        }

                        break;

                    case PacketIds.PlayClientbound.Disconnect:
                        reason = DescribeDisconnect(body, reader);
                        log($"disconnected: {reason}");
                        return new Result(sent, corrections, Distance(start, player.Position), reason, false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Nothing arrived in this window; that is normal for a quiet server.
            }

            // One tick at a time, sent only when something actually changed. MC_WALK_MODE=status sends the
            // status packet instead, which isolates "is the id right" from "are the coordinates legal".
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            var kind = Environment.GetEnvironmentVariable("MC_WALK_MODE") == "status"
                ? MovePacketKind.StatusOnly
                : sync.Decide(player, 0f, 0f);
            if (sync.Build(kind, player, 0f, 0f) is { } packet)
            {
                if (!TrySend(connection, packet, ref reason))
                {
                    log(reason);
                    break;
                }

                sent++;
            }

            var remaining = interval - TimeSpan.FromMilliseconds(clock.Elapsed.TotalMilliseconds % interval.TotalMilliseconds);
            if (remaining > TimeSpan.Zero && remaining < interval)
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }

            survived = true;
        }

        return new Result(sent, corrections, Distance(start, player.Position), reason, survived);
    }

    /// <summary>Sends a packet, reporting a closed peer instead of throwing.</summary>
    private static bool TrySend(MinecraftConnection connection, ReadOnlySpan<byte> packet, ref string failure)
    {
        try
        {
            connection.Send(packet);
            return true;
        }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)
        {
            failure = $"connection closed while sending: {exception.Message}";
            return false;
        }
    }

    /// <summary>Applies a server teleport and acknowledges it.</summary>
    private static (int? Id, bool Snapped, double Distance) ApplyTeleport(
        MinecraftConnection connection,
        PlayerSync sync,
        PlayerEntity player,
        PlayerPositionInfo correction)
    {
        var outcome = sync.ApplyServerPosition(player, correction, isTeleport: true);
        if (outcome.TeleportId is { } teleportId)
        {
            connection.Send(PlayMovementPackets.BuildAcceptTeleportation(
                teleportId,
                player.Position.X,
                player.Position.Y,
                player.Position.Z,
                0f,
                0f));
        }

        return (outcome.TeleportId, outcome.Snapped, outcome.Distance);
    }

    /// <summary>
    /// Summarises a disconnect. The reason is a chat component rather than a plain string, so the raw prefix
    /// is reported instead of a misparsed text.
    /// </summary>
    private static string DescribeDisconnect(ReadOnlySpan<byte> body, PayloadReader reader)
    {
        var tail = body[reader.Position..];
        var preview = tail.Length == 0
            ? "(empty)"
            : Convert.ToHexString(tail[..Math.Min(24, tail.Length)]);

        return $"reason component {tail.Length} bytes starting {preview}";
    }

    private static double Distance(Vector3 from, Vector3 to) => (to - from).Length();

    /// <summary>Ground at y = 0, nothing else; enough to stand and walk.</summary>
    private sealed class FlatWorld : IEntityWorld
    {
        public bool IsSolid(int x, int y, int z) => y < 0;

        public float Friction(int x, int y, int z) => 0.6f;
    }
}
