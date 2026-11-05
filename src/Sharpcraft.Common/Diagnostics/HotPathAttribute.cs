// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Diagnostics;

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
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.HotPathAttribute", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft internal utility; no reference counterpart")]
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
