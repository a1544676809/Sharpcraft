// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>One entry of the player list as it will be drawn.</summary>
/// <param name="Name">Player name.</param>
/// <param name="Latency">Latency in milliseconds, negative when unknown.</param>
/// <param name="PingBars">Filled signal bars, 0 when unknown.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Row width.</param>
public readonly record struct PlayerListRow(
    string Name,
    int Latency,
    int PingBars,
    float X,
    float Y,
    float Width);

/// <summary>
/// Ordering and placement for the player list, following the reference overlay
/// (ref://mc-26.3-analysis/src/net/minecraft/client/gui/components/PlayerTabOverlay.java).
/// </summary>
/// <remarks>
/// The interface is ours but the behaviour is not: sorting, the eighty player cap, twenty rows per column and the
/// latency thresholds all come from the reference, because a player list that reports latency differently from the
/// original is a behaviour difference even though it is drawn by different code.
/// </remarks>
public static class PlayerListPanel
{
    /// <summary>Rows in one column.</summary>
    public const int MaxRowsPerColumn = 20;

    /// <summary>Players shown at most.</summary>
    public const int MaxPlayers = 80;

    /// <summary>Vertical pitch of a row.</summary>
    public const float RowHeight = 9f;

    /// <summary>Gap between columns.</summary>
    public const float ColumnGap = 5f;

    /// <summary>Row background, the reference's default with a translucent white wash.</summary>
    public const uint RowBackground = 0x20FFFFFF;

    /// <summary>Game mode value that sorts last and is hidden from the list in the reference.</summary>
    public const int SpectatorGameMode = 3;

    /// <summary>Signal bars for a latency, following the reference thresholds.</summary>
    /// <param name="latency">Latency in milliseconds; negative means unknown.</param>
    public static int PingBarsFor(int latency) => latency switch
    {
        < 0 => 0,
        < 150 => 5,
        < 300 => 4,
        < 600 => 3,
        < 1000 => 2,
        _ => 1
    };

    /// <summary>
    /// Copies the listed players into <paramref name="ordered"/> in the reference's order.
    /// </summary>
    /// <param name="roster">Every player the server described.</param>
    /// <param name="ordered">Receives the rows; must hold at least <see cref="MaxPlayers"/>.</param>
    /// <returns>How many rows were written.</returns>
    /// <remarks>
    /// Insertion sort on purpose: at most eighty entries, and it is stable, which the reference's sort also is, so
    /// players with identical keys keep the order the server sent them in.
    /// </remarks>
    public static int Order(ReadOnlySpan<World.PlayerListEntry> roster, Span<World.PlayerListEntry> ordered)
    {
        var limit = Math.Min(ordered.Length, MaxPlayers);
        var count = 0;

        for (var index = 0; index < roster.Length; index++)
        {
            if (!roster[index].Listed)
            {
                continue;
            }

            int slot;

            if (count < limit)
            {
                slot = count;
                count++;
            }
            else
            {
                // Full: keep the best players rather than the first ones the server happened to mention. An entry
                // that does not beat the worst kept one is dropped, which is what sorting everything and taking the
                // first eighty amounts to.
                if (Compare(roster[index], ordered[limit - 1]) >= 0)
                {
                    continue;
                }

                slot = limit - 1;
            }

            while (slot > 0 && Compare(roster[index], ordered[slot - 1]) < 0)
            {
                ordered[slot] = ordered[slot - 1];
                slot--;
            }

            ordered[slot] = roster[index];
        }

        return count;
    }

    /// <summary>Places ordered players into columns.</summary>
    /// <param name="ordered">Players in draw order.</param>
    /// <param name="screenWidth">Virtual width, used to centre the columns.</param>
    /// <param name="top">Top edge of the first row.</param>
    /// <param name="slotWidth">Width of one row.</param>
    /// <param name="rows">Receives one row per player.</param>
    /// <returns>How many rows were written.</returns>
    public static int BuildRows(
        ReadOnlySpan<World.PlayerListEntry> ordered,
        float screenWidth,
        float top,
        float slotWidth,
        Span<PlayerListRow> rows)
    {
        var columns = Math.Max(1, (ordered.Length + MaxRowsPerColumn - 1) / MaxRowsPerColumn);
        var totalWidth = (slotWidth * columns) + (ColumnGap * (columns - 1));
        var left = (screenWidth / 2f) - (totalWidth / 2f);

        for (var index = 0; index < ordered.Length && index < rows.Length; index++)
        {
            var column = index / MaxRowsPerColumn;
            var row = index % MaxRowsPerColumn;

            rows[index] = new PlayerListRow(
                ordered[index].Name,
                ordered[index].Latency,
                PingBarsFor(ordered[index].Latency),
                left + (column * (slotWidth + ColumnGap)),
                top + (row * RowHeight),
                slotWidth);
        }

        return Math.Min(ordered.Length, rows.Length);
    }

    /// <summary>The reference's ordering, reduced to the fields the roster carries.</summary>
    private static int Compare(World.PlayerListEntry left, World.PlayerListEntry right)
    {
        // Higher list order first.
        var byOrder = right.ListOrder.CompareTo(left.ListOrder);

        if (byOrder != 0)
        {
            return byOrder;
        }

        // Spectators last.
        var leftSpectator = left.GameMode == SpectatorGameMode ? 1 : 0;
        var rightSpectator = right.GameMode == SpectatorGameMode ? 1 : 0;

        if (leftSpectator != rightSpectator)
        {
            return leftSpectator - rightSpectator;
        }

        // Then by name, ignoring case. Team ordering is absent until teams are modelled.
        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
