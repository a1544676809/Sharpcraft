// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Net.Sockets;

namespace Sharpcraft.Launcher.Transport;

/// <summary>
/// Host HTTP transport. The default .NET connect path walks the resolved addresses in DNS order and
/// waits for each to time out, which strands startup on machines that publish a broken IPv6 route
/// (measured: 21 s per request to <c>api.minecraftservices.com</c> before giving up, even though
/// IPv4 answered in milliseconds). This factory races the resolved addresses instead, so the
/// reachable family wins immediately and an unreachable one costs nothing.
/// </summary>
public static class HostTransport
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>User agent sent with every request; the endpoints reject anonymous clients.</summary>
    public const string UserAgent = "Sharpcraft/0.1 (+https://github.com/; launcher)";

    /// <summary>Creates an HTTP client with address racing enabled.</summary>
    /// <param name="timeout">Overall request timeout; defaults to 30 seconds.</param>
    public static HttpClient Create(TimeSpan? timeout = null) =>
        new(new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = timeout ?? DefaultTimeout,

            // Requests without a User-Agent are reset by the edge in front of the Xbox endpoints
            // ("response ended prematurely"); identifying the client makes the connection survive.
            DefaultRequestHeaders = { { "User-Agent", UserAgent } },

            // The CDN in front of the Minecraft services closes the connection when the first
            // request is negotiated as HTTP/2 ("response ended prematurely"), so HTTP/1.1 is asked
            // for explicitly and negotiation falls back from there.
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

    /// <summary>
    /// Orders addresses so IPv4 is attempted first while IPv6 stays usable on IPv6-only networks.
    /// Exposed for tests.
    /// </summary>
    public static IReadOnlyList<IPAddress> PreferIpv4(IEnumerable<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        var ordered = new List<IPAddress>();
        var v4 = new List<IPAddress>();
        var v6 = new List<IPAddress>();

        foreach (var address in addresses)
        {
            switch (address.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    v4.Add(address);
                    break;
                case AddressFamily.InterNetworkV6:
                    v6.Add(address);
                    break;
                default:
                    break;
            }
        }

        ordered.AddRange(v4);
        ordered.AddRange(v6);
        return ordered;
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = PreferIpv4(await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false));

        if (addresses.Count == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        using var attempts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = new List<Task<Socket>>(addresses.Count);

        try
        {
            foreach (var address in addresses)
            {
                tasks.Add(ConnectOneAsync(address, endpoint.Port, attempts.Token));
            }

            while (tasks.Count > 0)
            {
                var finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);

                try
                {
                    var socket = await finished.ConfigureAwait(false);

                    // Another address won the race; the losers are cancelled below.
                    await attempts.CancelAsync().ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                {
                    // Try the next address.
                }
            }

            throw new SocketException((int)SocketError.ConnectionRefused);
        }
        finally
        {
            await attempts.CancelAsync().ConfigureAwait(false);

            foreach (var task in tasks)
            {
                _ = task.ContinueWith(
                    static finished =>
                    {
                        if (finished.Status == TaskStatus.RanToCompletion)
                        {
                            finished.Result.Dispose();
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private static async Task<Socket> ConnectOneAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
