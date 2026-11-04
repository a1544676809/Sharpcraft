// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Per-entity synced attribute values (base + modifiers already combined by the packet decoder). This is a read-only
/// presentation cache; it never drives authoritative simulation.
/// </summary>
public sealed class TrackedAttributeMap
{
    private readonly Dictionary<int, float> _values = [];

    /// <summary>Attribute value, or <paramref name="fallback"/> when the server has not sent it.</summary>
    public float Get(int attributeId, float fallback = 0f) =>
        _values.TryGetValue(attributeId, out var value) ? value : fallback;

    /// <summary>Replaces the whole map from one update snapshot.</summary>
    public void Replace(ReadOnlySpan<Common.Network.AttributeValue> values)
    {
        _values.Clear();
        foreach (var value in values)
        {
            _values[value.AttributeId] = value.Value;
        }
    }
}
