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
        CancellationToken cancellationToken,
        IEntityWorld? world = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(log);

        // The server re-runs the move on its own terrain and rejects a client that ends up more than a
        // quarter block away, so the walk must happen in the same world the server has. A flat world is only
        // a fallback when no chunks were collected.
        var player = new PlayerEntity(world ?? new FlatWorld(), new Vector3(0.5f, 0f, 0.5f));
        var sync = new PlayerSync();
        var start = player.Position;

        var sent = 0;
        var corrections = 0;
        var reason = string.Empty;
        var survived = false;

        var clock = Stopwatch.StartNew();
        var interval = TimeSpan.FromMilliseconds(int.TryParse(Environment.GetEnvironmentVariable("MC_WALK_INTERVAL_MS"), out var intervalMs) ? intervalMs : 50);
        var lastSend = TimeSpan.Zero;
        var lastTrace = TimeSpan.Zero;
        var maxSends = int.TryParse(Environment.GetEnvironmentVariable("MC_WALK_MAX_SENDS"), out var max) ? max : int.MaxValue;

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

                            // The server ignores position updates until the client reports that the world is
                            // loaded. The packet has an empty body, so the id is the whole payload.
                            // Probe mode: send one id with a deliberately oversized body, then stop. The server names the
            // packet it tried to decode in its log, which is the only authoritative id table we have.
            if (int.TryParse(Environment.GetEnvironmentVariable("MC_PROBE_ID"), out var probeId) && probeId > 0)
            {
                var probe = new byte[64];
                var probeWriter = new PayloadWriter(probe);
                probeWriter.WriteVarInt(probeId);
                for (var index = 0; index < 40; index++)
                {
                    probeWriter.WriteByte(0xFF);
                }

                TrySend(connection, probe.AsSpan(0, probeWriter.Position), ref reason);
                log($"probed id {probeId} with 40 body bytes");
                await Task.Delay(600, cancellationToken).ConfigureAwait(false);
                return new Result(0, corrections, 0, reason, true);
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("MC_LOAD_ID"), out var loadId) && loadId > 0)
                            {
                                var loadPacket = new byte[2];
                                var loadWriter = new PayloadWriter(loadPacket);
                                loadWriter.WriteVarInt(loadId);
                                TrySend(connection, loadPacket.AsSpan(0, loadWriter.Position), ref reason);
                                log($"sent player loaded (id {loadId})");
                            }
                            log(
                                $"join teleport {id}: pos=({joinCorrection.X:F3},{joinCorrection.Y:F3},{joinCorrection.Z:F3}) " +
                                $"delta=({joinCorrection.DeltaX:F3},{joinCorrection.DeltaY:F3},{joinCorrection.DeltaZ:F3}) " +
                                $"yaw={joinCorrection.Yaw:F2} pitch={joinCorrection.Pitch:F2} relatives={joinCorrection.RelativeFlags} " +
                                $"deviation={distance:F2} snapped={snapped}");
                            log($"  accepted at ({player.Position.X:F3},{player.Position.Y:F3},{player.Position.Z:F3})");
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

        // Fixed 20 Hz simulation with one position packet per tick. The server re-runs every move on its own
        // terrain and rejects a client whose reported position drifts more than a quarter block from the
        // result, so the client tick rate and the send rate have to agree with the server's tick.
        var tickInterval = TimeSpan.FromMilliseconds(50);
        var accumulator = TimeSpan.Zero;
        // Anchored to the moment walking starts: counting from zero would treat the whole join sequence
        // as a backlog and burst dozens of ticks, which is exactly what the server rejects.
        var lastFrame = clock.Elapsed;

        while (clock.Elapsed.TotalSeconds < seconds && !cancellationToken.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(5));

            try
            {
                var body = await connection.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                var reader = new PayloadReader(body);
                reader.TryReadVarInt(out var packetId);

                switch (packetId)
                {
                    case PacketIds.PlayClientbound.KeepAlive:
                        reader.TryReadLong(out var keepAliveId);
                        if (!TrySend(connection, PlayPackets.BuildKeepAliveReply(keepAliveId), ref reason))
                        {
                            return new Result(sent, corrections, Distance(start, player.Position), reason, false);
                        }

                        break;

                    case PacketIds.PlayClientbound.Ping:
                        reader.TryReadInt(out var pingId);
                        TrySend(connection, PlayPackets.BuildPongReply(pingId), ref reason);
                        break;

                    case PacketIds.PlayClientbound.PlayerPosition:
                        if (PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var correction) == DecodeStatus.Success &&
                            correction is not null)
                        {
                            var (_, snapped, distance) = ApplyTeleport(connection, sync, player, correction);
                            corrections++;
                            accumulator = TimeSpan.Zero;
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
            catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)
            {
                reason = $"server closed the connection: {exception.Message}";
                log(reason);
                return new Result(sent, corrections, Distance(start, player.Position), reason, false);
            }

            var now = clock.Elapsed;
            accumulator += now - lastFrame;
            lastFrame = now;

            while (accumulator >= tickInterval)
            {
                accumulator -= tickInterval;
                player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);

                var kind = sync.Decide(player, 0f, 0f);
                if (sync.Build(kind, player, 0f, 0f) is { } packet)
                {
                    if (!TrySend(connection, packet, ref reason))
                    {
                        log(reason);
                        return new Result(sent, corrections, Distance(start, player.Position), reason, false);
                    }

                    sent++;
                }
            }

            if (now - lastTrace >= TimeSpan.FromSeconds(1))
            {
                lastTrace = now;
                log($"at ({player.Position.X:F2},{player.Position.Y:F2},{player.Position.Z:F2}) " +
                    $"ground={player.OnGround} wall={player.HorizontalCollision} speed={player.DeltaMovement.Length():F3}");
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
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
                correction.Yaw,
                correction.Pitch));
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
