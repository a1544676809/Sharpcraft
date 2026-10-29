// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Launcher.Transport;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// Proxy resolution for the host transport: an explicit variable wins over the conventional ones, a
/// bare host:port is accepted, and no configuration means the direct racing connection.
/// </summary>
public sealed class HostTransportProxyTests
{
    [Fact]
    public void NoVariablesMeansDirect()
    {
        Assert.Null(HostTransport.ResolveProxy(_ => null));
    }

    [Fact]
    public void ExplicitVariableWinsOverTheConventionalOnes()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [HostTransport.ProxyVariable] = "http://127.0.0.1:10809",
            ["HTTPS_PROXY"] = "http://127.0.0.1:9999"
        };

        var proxy = HostTransport.ResolveProxy(name => values.GetValueOrDefault(name));

        Assert.NotNull(proxy);
        Assert.Equal(10809, proxy!.Port);
    }

    [Fact]
    public void HttpsProxyIsUsedWhenNoExplicitVariableExists()
    {
        var proxy = HostTransport.ResolveProxy(name => name == "HTTPS_PROXY" ? "http://proxy.example:3128" : null);

        Assert.NotNull(proxy);
        Assert.Equal("proxy.example", proxy!.Host);
        Assert.Equal(3128, proxy.Port);
    }

    [Fact]
    public void BareHostAndPortIsAccepted()
    {
        var proxy = HostTransport.ResolveProxy(name => name == "HTTP_PROXY" ? "127.0.0.1:7890" : null);

        Assert.NotNull(proxy);
        Assert.Equal(Uri.UriSchemeHttp, proxy!.Scheme);
        Assert.Equal(7890, proxy.Port);
    }

    [Fact]
    public void UnparsableValueFallsThroughToTheNextVariable()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HTTPS_PROXY"] = "::not a uri::",
            ["ALL_PROXY"] = "socks5://127.0.0.1:10808"
        };

        var proxy = HostTransport.ResolveProxy(name => values.GetValueOrDefault(name));

        Assert.NotNull(proxy);
        Assert.Equal(10808, proxy!.Port);
    }
}
