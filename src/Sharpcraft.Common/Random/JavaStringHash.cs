// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// Reimplementation of <c>java.lang.String.hashCode()</c>, needed because the legacy
/// positional random factory keys per-name streams with it (docs/analysis/common/random.md).
/// </summary>
public static class JavaStringHash
{
    /// <summary>Returns the Java string hash: <c>h = 31 * h + c</c> over UTF-16 code units.</summary>
    public static int Of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var hash = 0;
        foreach (var character in value)
        {
            hash = unchecked((hash * 31) + character);
        }

        return hash;
    }
}
