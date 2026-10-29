// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Player list ordering and placement must match the reference overlay even though the drawing code is ours
/// (docs/modules/ui.md 8.3).
/// </summary>
public sealed class PlayerListPanelTests
{
    [Fact]
    public void HigherListOrderComesFirst()
    {
        var ordered = Ordered(
            Entry("low", listOrder: 1),
            Entry("high", listOrder: 9),
            Entry("middle", listOrder: 5));

        Assert.Equal(["high", "middle", "low"], ordered.Select(entry => entry.Name));
    }

    [Fact]
    public void SpectatorsSortAfterPlayersOfTheSameOrder()
    {
        var ordered = Ordered(
            Entry("spectator", listOrder: 0, gameMode: PlayerListPanel.SpectatorGameMode),
            Entry("player", listOrder: 0, gameMode: 1));

        Assert.Equal(["player", "spectator"], ordered.Select(entry => entry.Name));
    }

    [Fact]
    public void NamesBreakTiesIgnoringCase()
    {
        var ordered = Ordered(
            Entry("charlie", listOrder: 0),
            Entry("Alice", listOrder: 0),
            Entry("bob", listOrder: 0));

        Assert.Equal(["Alice", "bob", "charlie"], ordered.Select(entry => entry.Name));
    }

    [Fact]
    public void UnlistedPlayersAreSkipped()
    {
        var ordered = Ordered(
            Entry("hidden", listOrder: 0, listed: false),
            Entry("shown", listOrder: 0));

        Assert.Equal(["shown"], ordered.Select(entry => entry.Name));
    }

    [Fact]
    public void AtMostEightyPlayersAreKept()
    {
        var entries = new PlayerListEntry[100];

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = Entry($"player{index:000}", listOrder: index);
        }

        var ordered = Ordered(entries);

        Assert.Equal(PlayerListPanel.MaxPlayers, ordered.Length);
        Assert.Equal("player099", ordered[0].Name);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 5)]
    [InlineData(149, 5)]
    [InlineData(150, 4)]
    [InlineData(299, 4)]
    [InlineData(300, 3)]
    [InlineData(599, 3)]
    [InlineData(600, 2)]
    [InlineData(999, 2)]
    [InlineData(1000, 1)]
    [InlineData(5000, 1)]
    public void SignalBarsFollowTheReferenceThresholds(int latency, int expected) =>
        Assert.Equal(expected, PlayerListPanel.PingBarsFor(latency));

    [Fact]
    public void TwentyOnePlayersWrapIntoASecondColumn()
    {
        var entries = new PlayerListEntry[21];

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = Entry($"p{index:00}", listOrder: 100 - index);
        }

        var ordered = new PlayerListEntry[PlayerListPanel.MaxPlayers];
        var count = PlayerListPanel.Order(entries, ordered);
        var rows = new PlayerListRow[PlayerListPanel.MaxPlayers];
        var written = PlayerListPanel.BuildRows(ordered.AsSpan(0, count), 320f, 10f, 100f, rows);

        Assert.Equal(21, written);

        // Two columns of one hundred pixels with a five pixel gap are two hundred and five wide, so the block
        // starts at (320 - 205) / 2 = 57.5 and the second column starts one slot plus one gap later.
        Assert.Equal(57.5f, rows[0].X, 3);
        Assert.Equal(162.5f, rows[20].X, 3);
        Assert.Equal(10f, rows[0].Y, 3);
        Assert.Equal(10f + (19 * PlayerListPanel.RowHeight), rows[19].Y, 3);
        Assert.Equal(10f, rows[20].Y, 3);
    }

    private static PlayerListEntry[] Ordered(params PlayerListEntry[] entries)
    {
        var ordered = new PlayerListEntry[PlayerListPanel.MaxPlayers];
        var count = PlayerListPanel.Order(entries, ordered);
        return ordered[..count];
    }

    private static PlayerListEntry Entry(
        string name,
        int listOrder,
        int gameMode = 1,
        bool listed = true) =>
        new(Guid.NewGuid(), name, gameMode, 42, listed, listOrder);
}
