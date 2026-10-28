// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Two-way numeric mapping with monotonically increasing ids, equivalent to
/// Minecraft's <c>IdMapper</c> (used for block states and other dense id tables).
/// </summary>
/// <remarks>
/// Ids are assigned in insertion order and are never reused; the by-id side is sparse
/// until filled. Values are compared by reference, matching the vanilla identity map.
/// </remarks>
public sealed class IdMapper<T>
    where T : class
{
    private readonly Dictionary<T, int> _toId = new(ReferenceEqualityComparer.Instance);
    private readonly List<T?> _byId = [];
    private int _nextId;

    /// <summary>Number of ids handed out so far (including gaps).</summary>
    public int Count => _nextId;

    /// <summary>Assigns the next id to <paramref name="value"/> and returns it.</summary>
    public int Add(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var id = _nextId;
        AddMapping(value, id);
        return id;
    }

    /// <summary>Maps <paramref name="value"/> to an explicit id, growing the table as needed.</summary>
    public void AddMapping(T value, int id)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(id);

        if (_toId.ContainsKey(value))
        {
            throw new InvalidOperationException($"value already mapped with id {_toId[value]}");
        }

        if (id < _byId.Count && _byId[id] is { } existing)
        {
            throw new InvalidOperationException($"id {id} already mapped to {existing}");
        }

        while (_byId.Count <= id)
        {
            _byId.Add(null);
        }

        _byId[id] = value;
        _toId[value] = id;
        if (_nextId <= id)
        {
            _nextId = id + 1;
        }
    }

    /// <summary>Id of <paramref name="value"/>, or -1 when unmapped (vanilla default).</summary>
    public int GetId(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _toId.TryGetValue(value, out var id) ? id : -1;
    }

    /// <summary>Attempts to read the id of a value.</summary>
    public bool TryGetId(T value, out int id)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _toId.TryGetValue(value, out id);
    }

    /// <summary>Value at <paramref name="id"/>, or null when the id is unmapped or out of range.</summary>
    public T? ById(int id) => id >= 0 && id < _byId.Count ? _byId[id] : null;
}
