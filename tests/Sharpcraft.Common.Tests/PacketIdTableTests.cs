// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Cross-checks the packet ids we hard-code against the table exported from the real jar
/// (docs/specs/network/protocol-phases.md 3). This is what stops a hand-written constant
/// from silently drifting away from vanilla.
/// </summary>
public sealed class PacketIdTableTests
{
    private const string VectorFile = "TestData/vanilla-packet-ids.txt";

    [Fact]
    public void HandshakeStatusAndLoginIdsMatchVanilla()
    {
        var table = LoadTable();

        AssertIds(table, "handshaking", "serverbound", new()
        {
            ["minecraft:intention"] = PacketIds.HandshakeServerbound.ClientIntention
        });

        AssertIds(table, "status", "serverbound", new()
        {
            ["minecraft:status_request"] = PacketIds.StatusServerbound.StatusRequest,
            ["minecraft:ping_request"] = PacketIds.StatusServerbound.PingRequest
        });

        AssertIds(table, "status", "clientbound", new()
        {
            ["minecraft:status_response"] = PacketIds.StatusClientbound.StatusResponse,
            ["minecraft:pong_response"] = PacketIds.StatusClientbound.PongResponse
        });

        AssertIds(table, "login", "serverbound", new()
        {
            ["minecraft:hello"] = PacketIds.LoginServerbound.LoginStart,
            ["minecraft:key"] = PacketIds.LoginServerbound.Key,
            ["minecraft:custom_query_answer"] = PacketIds.LoginServerbound.CustomQueryAnswer,
            ["minecraft:login_acknowledged"] = PacketIds.LoginServerbound.LoginAcknowledged,
            ["minecraft:cookie_response"] = PacketIds.LoginServerbound.CookieResponse
        });

        AssertIds(table, "login", "clientbound", new()
        {
            ["minecraft:login_disconnect"] = PacketIds.LoginClientbound.LoginDisconnect,
            ["minecraft:hello"] = PacketIds.LoginClientbound.Hello,
            ["minecraft:login_finished"] = PacketIds.LoginClientbound.LoginFinished,
            ["minecraft:login_compression"] = PacketIds.LoginClientbound.LoginCompression,
            ["minecraft:custom_query"] = PacketIds.LoginClientbound.CustomQuery,
            ["minecraft:cookie_request"] = PacketIds.LoginClientbound.CookieRequest
        });
    }

    [Fact]
    public void TableCoversEveryPhaseWithContiguousIds()
    {
        var table = LoadTable();

        var expected = new Dictionary<(string Phase, string Flow), int>
        {
            [("handshaking", "serverbound")] = 1,
            [("status", "serverbound")] = 2,
            [("status", "clientbound")] = 2,
            [("login", "serverbound")] = 5,
            [("login", "clientbound")] = 6,
            [("configuration", "serverbound")] = 10,
            [("configuration", "clientbound")] = 21,
            [("play", "serverbound")] = 69,
            [("play", "clientbound")] = 144
        };

        foreach (var (key, count) in expected)
        {
            var ids = table.Where(entry => entry.Phase == key.Phase && entry.Flow == key.Flow)
                .Select(entry => entry.Id)
                .OrderBy(id => id)
                .ToList();

            Assert.Equal(count, ids.Count);
            Assert.Equal(Enumerable.Range(0, count), ids);
        }
    }

    [Fact]
    public void TableHasNoDuplicateIdentifiersWithinADirection()
    {
        var table = LoadTable();

        foreach (var group in table.GroupBy(entry => (entry.Phase, entry.Flow)))
        {
            var names = group.Select(entry => entry.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }
    }

    private static void AssertIds(
        List<(string Phase, string Flow, int Id, string Name)> table,
        string phase,
        string flow,
        Dictionary<string, int> expected)
    {
        var actual = table
            .Where(entry => entry.Phase == phase && entry.Flow == flow)
            .ToDictionary(entry => entry.Name, entry => entry.Id, StringComparer.Ordinal);

        // Every id we hard-code must exist in vanilla's table with the same value.
        foreach (var (name, id) in expected)
        {
            Assert.True(actual.ContainsKey(name), $"{phase}/{flow} has no packet named {name}");
            Assert.Equal(id, actual[name]);
        }

        // And we must not be missing any packet of a phase we already implement.
        Assert.Equal(actual.Count, expected.Count);
    }

    private static List<(string Phase, string Flow, int Id, string Name)> LoadTable()
    {
        var path = Path.Combine(AppContext.BaseDirectory, VectorFile);
        Assert.True(File.Exists(path), $"packet table missing from test output: {path}");

        return File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (parts[0], parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture), parts[3]))
            .ToList();
    }
}
