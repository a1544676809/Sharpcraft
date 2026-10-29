// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.World;

/// <summary>One row of the player list.</summary>
/// <param name="Id">Profile id.</param>
/// <param name="Name">Name, empty until an update carries one.</param>
/// <param name="GameMode">Game mode, negative when not yet known.</param>
/// <param name="Latency">Latency in milliseconds.</param>
/// <param name="Listed">Whether the server wants this player shown in the list.</param>
/// <param name="ListOrder">Server supplied sort order.</param>
public readonly record struct PlayerListEntry(
    Guid Id,
    string Name,
    int GameMode,
    int Latency,
    bool Listed,
    int ListOrder);

/// <summary>
/// The player list the server describes (docs/modules/ui.md 13).
/// </summary>
/// <remarks>
/// Updates are partial by design: a packet may carry only a latency change, and that must not erase the name. So
/// each field is merged only when the packet actually carried it, and the entry array is compacted in place on
/// removal so drawing can read a span without copying or sorting into a fresh array every frame.
/// </remarks>
public sealed class PlayerListRoster
{
    private readonly Dictionary<Guid, int> _indexById = [];
    private PlayerListEntry[] _entries = new PlayerListEntry[64];
    private int _count;

    /// <summary>How many players the roster knows about, listed or not.</summary>
    public int Count => _count;

    /// <summary>The rows, ready to draw. Not sorted.</summary>
    public ReadOnlySpan<PlayerListEntry> Entries => _entries.AsSpan(0, _count);

    /// <summary>Applies one update packet.</summary>
    /// <param name="updates">Entries from the packet.</param>
    public void Apply(ReadOnlySpan<PlayerInfoUpdate> updates)
    {
        for (var index = 0; index < updates.Length; index++)
        {
            var update = updates[index];
            var slot = FindOrAdd(update.Id);
            ref var entry = ref _entries[slot];

            if (update.Name is { } name)
            {
                entry = entry with { Name = name };
            }

            if (update.GameMode is { } gameMode)
            {
                entry = entry with { GameMode = gameMode };
            }

            if (update.Listed is { } listed)
            {
                entry = entry with { Listed = listed };
            }

            if (update.Latency is { } latency)
            {
                entry = entry with { Latency = latency };
            }

            if (update.ListOrder is { } order)
            {
                entry = entry with { ListOrder = order };
            }
        }
    }

    /// <summary>Removes players the server dropped.</summary>
    /// <param name="ids">Profile ids to remove.</param>
    public void Remove(ReadOnlySpan<Guid> ids)
    {
        for (var index = 0; index < ids.Length; index++)
        {
            if (!_indexById.TryGetValue(ids[index], out var slot))
            {
                continue;
            }

            // Move the last row into the hole so the live rows stay contiguous and drawing reads one span.
            var last = _count - 1;

            if (slot != last)
            {
                _entries[slot] = _entries[last];
                _indexById[_entries[slot].Id] = slot;
            }

            _count = last;
            _indexById.Remove(ids[index]);
        }
    }

    /// <summary>Forgets everyone, used when leaving a world.</summary>
    public void Clear()
    {
        _indexById.Clear();
        _count = 0;
    }

    private int FindOrAdd(Guid id)
    {
        if (_indexById.TryGetValue(id, out var slot))
        {
            return slot;
        }

        if (_count == _entries.Length)
        {
            Array.Resize(ref _entries, _entries.Length * 2);
        }

        slot = _count++;
        _entries[slot] = new PlayerListEntry(id, string.Empty, -1, 0, true, 0);
        _indexById[id] = slot;
        return slot;
    }
}
