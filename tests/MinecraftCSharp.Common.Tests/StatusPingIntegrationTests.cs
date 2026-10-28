// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Network;
using Xunit;

namespace MinecraftCSharp.Common.Tests;

/// <summary>
/// Manual integration test: performs a real server list ping so the codecs are validated
/// against an actual server instead of against themselves.
/// </summary>
/// <remarks>
/// Set <c>MC_TEST_SERVER</c> (for example <c>mcf.ddger.top</c> or <c>host:25566</c>) to run it.
/// Without the variable the test is a no-op, because CI has no guaranteed reachable server.
/// </remarks>
public sealed class StatusPingIntegrationTests
{
    private const string ServerVariable = "MC_TEST_SERVER";

    [Fact]
    public void RealServerAnswersStatusPing()
    {
        var target = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var result = StatusPingClient.Query(target, TimeSpan.FromSeconds(15));

        Assert.True(result.ProtocolVersion > 0, "server must report a protocol version");
        Assert.True(result.MaxPlayers >= result.OnlinePlayers, "online players cannot exceed the cap");
        Assert.True(result.LatencyMilliseconds >= 0, "latency must be non-negative");

        Console.WriteLine(
            $"[status] address={target} protocol={result.ProtocolVersion} version={result.VersionName} " +
            $"players={result.OnlinePlayers}/{result.MaxPlayers} latency={result.LatencyMilliseconds}ms motd={result.Description}");
    }

}
