// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Network;

/// <summary>Statistics gathered while holding a play connection open.</summary>
/// <param name="JoinGame">Parsed JoinGame header, or null when it never arrived.</param>
/// <param name="KeepAlivesAnswered">Number of keep-alive pings answered.</param>
/// <param name="PingsAnswered">Number of play-level pings answered.</param>
/// <param name="PacketCounts">Received packet counts keyed by id.</param>
/// <param name="DisconnectedReason">Reason when the server dropped us.</param>
/// <param name="Survived">True when the connection was still alive when the loop ended.</param>
public sealed record PlaySessionStats(
    JoinGameInfo? JoinGame,
    int KeepAlivesAnswered,
    int PingsAnswered,
    IReadOnlyDictionary<int, int> PacketCounts,
    string DisconnectedReason,
    bool Survived)
{
    /// <summary>Total packets received.</summary>
    public int TotalPackets => PacketCounts.Values.Sum();
}

/// <summary>
/// Minimal play-phase loop: answers keep-alive and ping so the server does not drop the
/// connection, records what arrives, and reports a disconnect instead of throwing
/// (docs/specs/network/protocol-phases.md 1).
/// </summary>
public static class PlaySession
{
    /// <summary>Runs the loop for a bounded time.</summary>
    /// <param name="connection">Connection already in the play phase.</param>
    /// <param name="duration">How long to stay connected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="log">Optional per-packet trace.</param>
    public static async Task<PlaySessionStats> RunAsync(
        MinecraftConnection connection,
        TimeSpan duration,
        CancellationToken cancellationToken,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var counts = new Dictionary<int, int>();
        JoinGameInfo? joinGame = null;
        var keepAlives = 0;
        var pings = 0;
        var disconnected = string.Empty;
        var survived = false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);

        try
        {
            while (!timeout.IsCancellationRequested)
            {
                var body = await connection.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                var reader = new PayloadReader(body);
                if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
                {
                    continue;
                }

                counts[packetId] = counts.TryGetValue(packetId, out var seen) ? seen + 1 : 1;

                switch (packetId)
                {
                    case PacketIds.PlayClientbound.KeepAlive:
                        if (reader.TryReadLong(out var keepAliveId) == DecodeStatus.Success)
                        {
                            connection.Send(PlayPackets.BuildKeepAliveReply(keepAliveId));
                            keepAlives++;
                        }

                        break;

                    case PacketIds.PlayClientbound.Ping:
                        if (reader.TryReadInt(out var pingId) == DecodeStatus.Success)
                        {
                            connection.Send(PlayPackets.BuildPongReply(pingId));
                            pings++;
                        }

                        break;

                    case PacketIds.PlayClientbound.Login:
                        if (PlayPackets.TryReadJoinGame(body, out var info) == DecodeStatus.Success)
                        {
                            joinGame = info;
                            log?.Invoke(
                                $"join game: id={info.PlayerId} hardcore={info.Hardcore} " +
                                $"levels=[{string.Join(", ", info.Levels)}] max={info.MaxPlayers} " +
                                $"view={info.ChunkRadius} sim={info.SimulationDistance}");
                        }
                        else
                        {
                            log?.Invoke("join game: header could not be parsed");
                        }

                        break;

                    case PacketIds.PlayClientbound.Disconnect:
                    {
                        reader.TryReadString(out var reason, 262144);
                        disconnected = reason.Length == 0 || reason.Any(char.IsControl)
                            ? "raw: " + MinecraftLoginCrypto.ToHex(body)
                            : reason;
                        log?.Invoke($"disconnected by the server: {disconnected}");
                        return new PlaySessionStats(joinGame, keepAlives, pings, counts, disconnected, false);
                    }

                    case PacketIds.PlayClientbound.StartConfiguration:
                        log?.Invoke("server asked to reconfigure; stopping");
                        return new PlaySessionStats(joinGame, keepAlives, pings, counts, "start_configuration", false);

                    default:
                        log?.Invoke($"play <- id {packetId} ({body.Length} bytes)");
                        break;
                }
            }

            survived = true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The scheduled duration elapsed: that is success, not a failure.
            survived = true;
        }

        return new PlaySessionStats(joinGame, keepAlives, pings, counts, disconnected, survived);
    }
}
