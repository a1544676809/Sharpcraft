// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Address parsing rules for server targets (docs/specs/network/protocol-phases.md 4.1).
/// </summary>
public sealed class ServerAddressResolverTests
{
    [Theory]
    [InlineData("example.com", "example.com", 25565)]
    [InlineData("example.com:25566", "example.com", 25566)]
    [InlineData("192.168.1.10", "192.168.1.10", 25565)]
    [InlineData("192.168.1.10:25570", "192.168.1.10", 25570)]
    public void SplitKeepsExplicitPortsAndDefaultsOthers(string address, string expectedHost, int expectedPort)
    {
        var (host, port) = ServerAddressResolver.Split(address);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Fact]
    public void ExplicitPortOverridesEverything()
    {
        var (host, port) = ServerAddressResolver.Split("example.com:1234", explicitPort: 25566);

        Assert.Equal("example.com", host);
        Assert.Equal(25566, port);
    }

    [Fact]
    public void ExplicitPortSuppressesSrvLookup()
    {
        var resolved = ServerAddressResolver.Resolve("example.com:25566");

        Assert.False(resolved.UsedSrv);
        Assert.Equal("example.com", resolved.ConnectHost);
        Assert.Equal(25566, resolved.ConnectPort);
    }

    [Fact]
    public void HandshakeAddressStaysAsTypedEvenWhenSrvIsUsed()
    {
        // Uses a real SRV-only style host; if DNS is unavailable the resolver falls back and
        // the assertions still hold because the handshake address never changes.
        var resolved = ServerAddressResolver.Resolve("mcf.ddger.top");

        Assert.Equal("mcf.ddger.top", resolved.HandshakeHost);
        Assert.Equal(25565, resolved.HandshakePort);
        Assert.True(resolved.ConnectPort > 0);
    }

    [Fact]
    public void InvalidAddressesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => ServerAddressResolver.Resolve(string.Empty));
    }
}
