// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using DnsClient;
using DnsClient.Protocol;

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// A server endpoint plus the address string that must go into the handshake.
/// </summary>
/// <param name="ConnectHost">Host to open the TCP connection to (SRV target when used).</param>
/// <param name="ConnectPort">Port to connect to (SRV port when used).</param>
/// <param name="HandshakeHost">Address to advertise in the handshake (the user-supplied one).</param>
/// <param name="HandshakePort">Port to advertise in the handshake.</param>
/// <param name="UsedSrv">Whether an SRV record was used.</param>
public sealed record ResolvedServerAddress(
    string ConnectHost,
    int ConnectPort,
    string HandshakeHost,
    int HandshakePort,
    bool UsedSrv);

/// <summary>
/// Resolves <c>host[:port]</c> the way the vanilla client does: an explicit port disables
/// SRV lookup, otherwise <c>_minecraft._tcp.&lt;host&gt;</c> is consulted
/// (docs/specs/network/protocol-phases.md 6).
/// </summary>
/// <remarks>
/// The handshake keeps advertising the address the user typed, because proxies and
/// forwarding setups match on it; only the TCP destination comes from the SRV record.
/// </remarks>
public static class ServerAddressResolver
{
    /// <summary>Default Minecraft port.</summary>
    public const int DefaultPort = 25565;

    private const string SrvPrefix = "_minecraft._tcp.";

    /// <summary>Resolves an address, falling back to the literal host/port when no SRV record exists.</summary>
    public static ResolvedServerAddress Resolve(string address, int? explicitPort = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);

        var (host, port) = Split(address, explicitPort);

        if (explicitPort is not null || port != DefaultPort || address.Contains(':', StringComparison.Ordinal))
        {
            // An explicit port means "do not consult SRV", matching the vanilla client.
            return new ResolvedServerAddress(host, port, host, port, UsedSrv: false);
        }

        return TryResolveSrv(host, out var target)
            ? new ResolvedServerAddress(target.Host, target.Port, host, port, UsedSrv: true)
            : new ResolvedServerAddress(host, port, host, port, UsedSrv: false);
    }

    /// <summary>Resolves an address and returns the endpoint used for the TCP connection.</summary>
    public static (string Host, int Port) Split(string address, int? explicitPort = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);

        var separator = address.LastIndexOf(':');
        if (separator < 0)
        {
            return (address, explicitPort ?? DefaultPort);
        }

        var host = address[..separator];
        var portText = address[(separator + 1)..];
        if (string.IsNullOrEmpty(host) || !int.TryParse(portText, out var parsed) || parsed <= 0 || parsed > 65535)
        {
            return (address, explicitPort ?? DefaultPort);
        }

        return (host, explicitPort ?? parsed);
    }

    private static bool TryResolveSrv(string host, out (string Host, int Port) target)
    {
        target = default;

        try
        {
            var client = new LookupClient(new LookupClientOptions { Timeout = TimeSpan.FromSeconds(5), Retries = 1 });
            var result = client.Query($"{SrvPrefix}{host}", QueryType.SRV);
            var record = result.Answers
                .OfType<SrvRecord>()
                .OrderBy(entry => entry.Priority)
                .ThenByDescending(entry => entry.Weight)
                .FirstOrDefault();
            if (record is null)
            {
                return false;
            }

            var resolvedHost = record.Target.Value.TrimEnd('.');
            if (string.IsNullOrEmpty(resolvedHost))
            {
                return false;
            }

            target = (resolvedHost, record.Port);
            return true;
        }
        catch (DnsResponseException)
        {
            // NXDOMAIN / SERVFAIL simply means "no SRV", not a fatal error.
            return false;
        }
    }
}
