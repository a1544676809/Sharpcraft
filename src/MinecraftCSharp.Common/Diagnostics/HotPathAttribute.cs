// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Diagnostics;

/// <summary>
/// Marks a member that runs inside a steady-state hot path (per frame or per tick).
/// Members carrying this attribute must have a matching allocation test proving zero
/// steady-state allocation (AGENTS.md 3.6); an unverified <c>[HotPath]</c> member is
/// treated as a rule violation during review.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class HotPathAttribute : Attribute
{
    /// <summary>Creates the marker without a description.</summary>
    public HotPathAttribute()
    {
    }

    /// <summary>Creates the marker with a short description of why the member is hot.</summary>
    public HotPathAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>Short description of why the member is considered hot.</summary>
    public string? Reason { get; }
}
