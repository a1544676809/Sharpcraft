// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>
/// One block-state property with its legal values in declaration order.
/// </summary>
/// <remarks>
/// Value order is significant and is <b>not</b> alphabetical: booleans are
/// <c>[true, false]</c>, integers ascend, enums follow declaration order
/// (see docs/analysis/common/registry-order.md).
/// </remarks>
public sealed record BlockStateProperty(string Name, IReadOnlyList<string> Values)
{
    /// <summary>Number of legal values.</summary>
    public int Radix => Values.Count;

    /// <summary>Index of a value, or -1 when unknown.</summary>
    public int IndexOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        for (var i = 0; i < Values.Count; i++)
        {
            if (string.Equals(Values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
