// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Ordered content registry. Enumeration follows <b>registration order</b>, which is the
/// only ordering contract (see docs/analysis/common/registry-order.md).
/// </summary>
/// <remarks>
/// Rules taken from the analysis:
/// <list type="bullet">
///   <item>Iteration order = registration order; no dictionary enumeration is exposed.</item>
///   <item>Duplicate identifiers are rejected.</item>
///   <item>Once frozen the registry rejects further writes.</item>
///   <item>Numeric ids come from the same insertion order, so they stay consistent with iteration.</item>
/// </list>
/// </remarks>
public sealed class Registry<T>
    where T : class
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<Identifier, int> _index = [];
    private readonly IdMapper<T> _ids = new();
    private bool _frozen;

    /// <summary>A registered element plus its identifier.</summary>
    public readonly record struct Entry(Identifier Id, T Value);

    /// <summary>Number of registered elements.</summary>
    public int Count => _entries.Count;

    /// <summary>True once <see cref="Freeze"/> has been called.</summary>
    public bool IsFrozen => _frozen;

    /// <summary>Elements in registration order. This is the only ordering contract.</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>Identifiers in registration order.</summary>
    public IEnumerable<Identifier> Ids => _entries.Select(entry => entry.Id);

    /// <summary>Registers an element, assigning the next numeric id.</summary>
    /// <exception cref="InvalidOperationException">The registry is frozen or the id is taken.</exception>
    public Entry Register(Identifier id, T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_frozen)
        {
            throw new InvalidOperationException($"registry is frozen; cannot register '{id}'");
        }

        if (_index.ContainsKey(id))
        {
            throw new InvalidOperationException($"duplicate key '{id}' in registry");
        }

        _ids.Add(value);
        _index[id] = _entries.Count;
        var entry = new Entry(id, value);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Looks up an element by identifier.</summary>
    public bool TryGet(Identifier id, out T? value)
    {
        if (_index.TryGetValue(id, out var position))
        {
            value = _entries[position].Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Looks up an element, throwing when absent.</summary>
    /// <exception cref="KeyNotFoundException">No element with that identifier.</exception>
    public T Get(Identifier id) =>
        TryGet(id, out var value) ? value! : throw new KeyNotFoundException($"no entry '{id}' in registry");

    /// <summary>Numeric id of a registered element, or -1 when absent.</summary>
    public int GetId(T value) => _ids.GetId(value);

    /// <summary>Element for a numeric id, or null when unmapped.</summary>
    public T? ById(int id) => _ids.ById(id);

    /// <summary>Entries whose identifier path starts with <paramref name="pathPrefix"/>.</summary>
    public IEnumerable<Entry> WithPathPrefix(string pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(pathPrefix);

        return _entries.Where(entry => entry.Id.Path.StartsWith(pathPrefix, StringComparison.Ordinal));
    }

    /// <summary>Freezes the registry; later registrations are rejected.</summary>
    public void Freeze() => _frozen = true;
}
