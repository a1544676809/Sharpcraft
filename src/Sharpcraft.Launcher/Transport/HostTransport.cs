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

    /// <summary>Environment variable naming an explicit proxy for this application.</summary>
    public const string ProxyVariable = "SHARPCRAFT_PROXY";

    /// <summary>
    /// Resolves the proxy to use, in order: <c>SHARPCRAFT_PROXY</c>, <c>HTTPS_PROXY</c>,
    /// <c>HTTP_PROXY</c>, <c>ALL_PROXY</c>. Returns null when none is configured, which means the
    /// racing direct connection is used.
    /// </summary>
    /// <param name="readVariable">Reads an environment variable; injected so tests can fake it.</param>
    public static Uri? ResolveProxy(Func<string, string?> readVariable)
    {
        ArgumentNullException.ThrowIfNull(readVariable);

        foreach (var name in new[] { ProxyVariable, "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY" })
        {
            var value = readVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // Bare "host:port" is accepted, as proxy environment variables are often written that way.
            if (!value.Contains("://", StringComparison.Ordinal))
            {
                value = "http://" + value;
            }

            if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    /// <summary>Creates an HTTP client with address racing enabled.</summary>
    /// <param name="timeout">Overall request timeout; defaults to 30 seconds.</param>
    /// <remarks>
    /// When a proxy is configured the racing connect is dropped on purpose: <see cref="ConnectCallback"/>
    /// connects directly and therefore bypasses proxies entirely, so honouring a proxy and racing
    /// addresses are mutually exclusive. The proxy path keeps every other setting.
    /// </remarks>
    public static HttpClient Create(TimeSpan? timeout = null)
    {
        var proxy = ResolveProxy(Environment.GetEnvironmentVariable);

        if (Environment.GetEnvironmentVariable("MC_TRANSPORT_DEBUG") is "1")
        {
            Console.Error.WriteLine(
                proxy is null
                    ? "[debug] transport: direct, address racing"
                    : $"[debug] transport: proxy {proxy.Host}:{proxy.Port} (address racing disabled)");
        }

        return Build(proxy, timeout);
    }

    private static HttpClient Build(Uri? proxy, TimeSpan? timeout) =>
        new(new SocketsHttpHandler
        {
            ConnectCallback = proxy is null ? ConnectAsync : null,
            Proxy = proxy is null ? null : new WebProxy(proxy),
            UseProxy = proxy is not null,
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
