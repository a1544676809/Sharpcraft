// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Network;

namespace MinecraftCSharp.Launcher;

/// <summary>
/// Walks the configuration phase far enough to prove the login chain works against a real
/// server, then leaves. It answers only the packets a server blocks on (known packs, keep
/// alive, ping, code of conduct) and declines resource packs
/// (docs/specs/network/protocol-phases.md 1).
/// </summary>
internal static class ConfigurationProbe
{
    private static readonly Dictionary<int, string> ClientboundNames = new()
    {
        [PacketIds.ConfigurationClientbound.CookieRequest] = "cookie_request",
        [PacketIds.ConfigurationClientbound.CustomPayload] = "custom_payload",
        [PacketIds.ConfigurationClientbound.Disconnect] = "disconnect",
        [PacketIds.ConfigurationClientbound.FinishConfiguration] = "finish_configuration",
        [PacketIds.ConfigurationClientbound.KeepAlive] = "keep_alive",
        [PacketIds.ConfigurationClientbound.Ping] = "ping",
        [PacketIds.ConfigurationClientbound.ResetChat] = "reset_chat",
        [PacketIds.ConfigurationClientbound.RegistryData] = "registry_data",
        [PacketIds.ConfigurationClientbound.ResourcePackPop] = "resource_pack_pop",
        [PacketIds.ConfigurationClientbound.ResourcePackPush] = "resource_pack_push",
        [PacketIds.ConfigurationClientbound.PostEffects] = "post_effects",
        [PacketIds.ConfigurationClientbound.StoreCookie] = "store_cookie",
        [PacketIds.ConfigurationClientbound.Transfer] = "transfer",
        [PacketIds.ConfigurationClientbound.UpdateEnabledFeatures] = "update_enabled_features",
        [PacketIds.ConfigurationClientbound.UpdateTags] = "update_tags",
        [PacketIds.ConfigurationClientbound.SelectKnownPacks] = "select_known_packs",
        [PacketIds.ConfigurationClientbound.CustomReportDetails] = "custom_report_details",
        [PacketIds.ConfigurationClientbound.ServerLinks] = "server_links",
        [PacketIds.ConfigurationClientbound.ClearDialog] = "clear_dialog",
        [PacketIds.ConfigurationClientbound.ShowDialog] = "show_dialog",
        [PacketIds.ConfigurationClientbound.CodeOfConduct] = "code_of_conduct"
    };

    /// <summary>Result of the configuration walk.</summary>
    internal sealed record Result(bool ReachedPlay, string Reason, IReadOnlyList<string> Sequence);

    /// <summary>Acknowledges the login and runs the configuration phase.</summary>
    internal static async Task<Result> RunAsync(
        MinecraftConnection connection,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // Login acknowledged (serverbound id 3) moves both sides to configuration.
        connection.Send([PacketIds.LoginServerbound.LoginAcknowledged]);

        // A client that never announces itself is treated as broken by most servers.
        SendClientInformation(connection, log);

        var sequence = new List<string>();
        var deadline = DateTime.UtcNow + timeout;

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var body = await connection.ReceiveAsync(timeoutSource.Token).ConfigureAwait(false);
            var reader = new PayloadReader(body);
            if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
            {
                continue;
            }

            var name = ClientboundNames.TryGetValue(packetId, out var known) ? known : $"unknown_{packetId}";
            sequence.Add(name);
            log?.Invoke($"configuration <- {name} ({packetId})");

            switch (packetId)
            {
                case PacketIds.ConfigurationClientbound.SelectKnownPacks:
                    // Echo the server's list back; an empty list is the safe universal answer.
                    SendWithPayload(connection, PacketIds.ConfigurationServerbound.SelectKnownPacks, reader.Position < body.Length ? body[reader.Position..] : []);
                    break;

                case PacketIds.ConfigurationClientbound.KeepAlive:
                {
                    if (reader.TryReadLong(out var keepAlive) == DecodeStatus.Success)
                    {
                        var reply = new byte[16];
                        var writer = new PayloadWriter(reply);
                        writer.WriteVarInt(PacketIds.ConfigurationServerbound.KeepAlive);
                        writer.WriteLong(keepAlive);
                        connection.Send(reply.AsSpan(0, writer.Position));
                    }

                    break;
                }

                case PacketIds.ConfigurationClientbound.Ping:
                {
                    if (reader.TryReadVarInt(out var ping) == DecodeStatus.Success)
                    {
                        var reply = new byte[16];
                        var writer = new PayloadWriter(reply);
                        writer.WriteVarInt(PacketIds.ConfigurationServerbound.Pong);
                        writer.WriteInt(ping);
                        connection.Send(reply.AsSpan(0, writer.Position));
                    }

                    break;
                }

                case PacketIds.ConfigurationClientbound.CodeOfConduct:
                {
                    if (reader.TryReadVarInt(out var conductId) == DecodeStatus.Success)
                    {
                        var reply = new byte[16];
                        var writer = new PayloadWriter(reply);
                        writer.WriteVarInt(PacketIds.ConfigurationServerbound.AcceptCodeOfConduct);
                        writer.WriteVarInt(conductId);
                        connection.Send(reply.AsSpan(0, writer.Position));
                    }

                    break;
                }

                case PacketIds.ConfigurationClientbound.ResourcePackPush:
                {
                    // Decline every pack: this probe has no asset pipeline to load them with.
                    if (reader.TryReadUuid(out var packId) == DecodeStatus.Success)
                    {
                        var reply = new byte[32];
                        var writer = new PayloadWriter(reply);
                        writer.WriteVarInt(PacketIds.ConfigurationServerbound.ResourcePack);
                        writer.WriteUuid(packId);
                        writer.WriteVarInt(1); // DECLINED
                        connection.Send(reply.AsSpan(0, writer.Position));
                    }

                    break;
                }

                case PacketIds.ConfigurationClientbound.Disconnect:
                {
                    // The reason is a component; dump the payload when it cannot be read as text.
                    reader.TryReadString(out var reason, 262144);
                    if (reason.Length == 0 || reason.Any(character => char.IsControl(character)))
                    {
                        reason = "raw: " + MinecraftLoginCrypto.ToHex(body);
                    }

                    return new Result(false, reason, sequence);
                }

                case PacketIds.ConfigurationClientbound.FinishConfiguration:
                {
                    connection.Send([PacketIds.ConfigurationServerbound.FinishConfiguration]);

                    // The very next packet is the first play packet: that is the proof we are in.
                    var firstPlay = await connection.ReceiveAsync(timeoutSource.Token).ConfigureAwait(false);
                    var playReader = new PayloadReader(firstPlay);
                    playReader.TryReadVarInt(out var playId);
                    sequence.Add($"play <- id {playId}");
                    log?.Invoke($"play <- first packet id {playId}");
                    return new Result(true, string.Empty, sequence);
                }
            }
        }

        return new Result(false, "configuration timeout", sequence);
    }

    private static void SendWithPayload(MinecraftConnection connection, int packetId, ReadOnlySpan<byte> payload)
    {
        var prefixLength = VarInt.GetByteSize(packetId);
        var body = new byte[prefixLength + payload.Length];
        var written = VarInt.Write(body, packetId);
        payload.CopyTo(body.AsSpan(written));
        connection.Send(body);
    }

    private static void SendClientInformation(MinecraftConnection connection, Action<string>? log)
    {
        // Mirrors ClientInformation.createDefault() field order and values: en_us, view
        // distance 2, full chat, colours on, no model customisation, right hand, no text
        // filtering, listing disabled, all particles.
        Span<byte> buffer = stackalloc byte[64];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.ConfigurationServerbound.ClientInformation);
        writer.WriteString("en_us", 16);
        writer.WriteByte(2);
        writer.WriteVarInt(0);            // ChatVisiblity.FULL
        writer.WriteBool(true);           // chat colours
        writer.WriteByte(0);              // model customisation
        writer.WriteVarInt(1);            // HumanoidArm.RIGHT
        writer.WriteBool(false);          // text filtering
        writer.WriteBool(false);          // allows listing
        writer.WriteVarInt(0);            // ParticleStatus.ALL

        connection.Send(buffer[..writer.Position]);
        log?.Invoke("client_information -> sent");
    }

}
