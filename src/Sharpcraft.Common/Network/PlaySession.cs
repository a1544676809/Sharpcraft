// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

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
    bool Survived,
    int ChunksReceived = 0,
    int ChunksFailed = 0)
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
        Action<string>? log = null,
        Func<JoinGameInfo, (int SectionCount, int MinSectionY, int AirState)>? dimensionLayout = null,
        Func<ParsedChunk, bool>? onChunk = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var counts = new Dictionary<int, int>();
        JoinGameInfo? joinGame = null;
        var keepAlives = 0;
        var pings = 0;
        var disconnected = string.Empty;
        var survived = false;
        var chunksReceived = 0;
        var chunksFailed = 0;
        var layout = (SectionCount: 24, MinSectionY: -4, AirState: 0);
        var haveLayout = false;

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
                    case PacketIds.PlayClientbound.LevelChunkWithLight:
                    {
                        if (!haveLayout && dimensionLayout is not null)
                        {
                            layout = dimensionLayout(joinGame ?? new JoinGameInfo(0, false, [], 0, 0, 0, false, false, false));
                            haveLayout = true;
                        }

                        var status = ChunkPacketParser.TryRead(
                            body, layout.SectionCount, layout.MinSectionY, layout.AirState, out var parsed);
                        if (Environment.GetEnvironmentVariable("MC_DUMP_CHUNK_DIR") is { Length: > 0 } dumpDir &&
                            status != DecodeStatus.Success)
                        {
                            Directory.CreateDirectory(dumpDir);
                            var path = Path.Combine(dumpDir, $"failed-{chunksFailed}-{body.Length}.bin");
                            File.WriteAllBytes(path, body.ToArray());
                            log?.Invoke($"failed chunk dumped to {path} ({body.Length} bytes, {status})");
                        }

                        if (chunksReceived == 0 && status != DecodeStatus.Success)
                        {
                            var preview = body.Length > 48 ? MinecraftLoginCrypto.ToHex(body[..48]) : MinecraftLoginCrypto.ToHex(body);
                            log?.Invoke($"chunk packet diagnostic: len={body.Length} head={preview}");
                        }

                        if (status == DecodeStatus.Success && parsed is not null)
                        {
                            chunksReceived++;
                            onChunk?.Invoke(parsed);
                        }
                        else
                        {
                            chunksFailed++;
                            log?.Invoke($"chunk packet failed: {status}");
                        }

                        break;
                    }

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
                            var preview = body;
                            log?.Invoke(
                                $"join game: header could not be parsed; len={body.Length} head={MinecraftLoginCrypto.ToHex(preview)}");
                        }

                        break;

                    case PacketIds.PlayClientbound.Disconnect:
                    {
                        reader.TryReadString(out var reason, 262144);
                        disconnected = reason.Length == 0 || reason.Any(char.IsControl)
                            ? "raw: " + MinecraftLoginCrypto.ToHex(body)
                            : reason;
                        log?.Invoke($"disconnected by the server: {disconnected}");
                        return new PlaySessionStats(joinGame, keepAlives, pings, counts, disconnected, false, chunksReceived, chunksFailed);
                    }

                    case PacketIds.PlayClientbound.StartConfiguration:
                        log?.Invoke("server asked to reconfigure; stopping");
                        return new PlaySessionStats(joinGame, keepAlives, pings, counts, "start_configuration", false, chunksReceived, chunksFailed);

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

        return new PlaySessionStats(joinGame, keepAlives, pings, counts, disconnected, survived, chunksReceived, chunksFailed);
    }
}
