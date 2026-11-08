// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Diagnostics;

/// <summary>
/// Marks code that deliberately simplifies or approximates vanilla behavior. The marker is checked by the repository
/// build gate against a machine-readable entry in the module documentation; the documentation entry remains after the
/// marker is removed when the simplification is resolved.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor |
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Assembly,
    AllowMultiple = true,
    Inherited = false)]
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.SimplificationAttribute", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft internal utility; no reference counterpart")]
public sealed class SimplificationAttribute : Attribute
{
    /// <summary>Creates a simplification marker with its owning task.</summary>
    /// <param name="id">Stable machine-readable identifier used by the documentation gate.</param>
    /// <param name="task">Traceability task that owns the simplification audit.</param>
    public SimplificationAttribute(string id, string task)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(task);
        Id = id;
        Task = task;
    }

    /// <summary>Stable machine-readable identifier used by the documentation gate.</summary>
    public string Id { get; }

    /// <summary>Traceability task that owns the simplification audit.</summary>
    public string Task { get; }

    /// <summary>Optional human-readable description of the simplification.</summary>
    public string? Note { get; init; }
}
