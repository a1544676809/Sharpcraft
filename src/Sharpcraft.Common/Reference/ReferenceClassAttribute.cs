// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;

namespace Sharpcraft.Common.Reference;

/// <summary>How a Sharpcraft type relates to the reference (vanilla 26.3) type it maps to.</summary>
public enum ReferenceKind
{
    /// <summary>One-to-one behavioural mirror of a single reference class.</summary>
    Mirror,

    /// <summary>Several reference classes are merged into this one Sharpcraft type.</summary>
    Merged,

    /// <summary>An adapter/insulation layer around one or more reference classes.</summary>
    Adapter,

    /// <summary>Sharpcraft-only infrastructure with no reference counterpart (state why in Note).</summary>
    New,

    /// <summary>Placeholder that does not yet implement the reference behaviour.</summary>
    Stub
}

/// <summary>Implementation completeness of the mapped reference behaviour.</summary>
public enum ReferenceStatus
{
    /// <summary>Behaviour is implemented and checked against the reference.</summary>
    Full,

    /// <summary>Partially implemented; the gap must be described in Note.</summary>
    Partial,

    /// <summary>Recognised but not started.</summary>
    NotStarted
}

/// <summary>
/// Declares which reference (vanilla Minecraft 26.3) class a Sharpcraft type corresponds to. This is the single source of
/// truth for the "implementation ↔ reference class" mapping (AGENTS.md 3.5) and is consumed by tools/Sharpcraft.ClassMap.
/// Only names and <c>ref://mc-26.3-analysis/...</c> pointers are stored — never reference source code (principle 3).
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false,
    Inherited = false)]
public sealed class ReferenceClassAttribute : Attribute
{
    /// <param name="referenceClass">Fully-qualified reference class name, e.g. <c>net.minecraft.client.multiplayer.ClientPacketListener</c>.</param>
    /// <param name="referencePath">Analysis-workspace pointer, e.g. <c>ref://mc-26.3-analysis/src/.../ClientPacketListener.java</c>.</param>
    public ReferenceClassAttribute(string referenceClass, string referencePath)
    {
        ReferenceClass = referenceClass;
        ReferencePath = referencePath;
    }

    /// <summary>Fully-qualified reference class name.</summary>
    public string ReferenceClass { get; }

    /// <summary>Analysis-workspace pointer to the reference file (<c>ref://mc-26.3-analysis/...</c>).</summary>
    public string ReferencePath { get; }

    /// <summary>Relationship kind; defaults to <see cref="ReferenceKind.Mirror"/>.</summary>
    public ReferenceKind Kind { get; init; } = ReferenceKind.Mirror;

    /// <summary>Completeness; defaults to <see cref="ReferenceStatus.Full"/>.</summary>
    public ReferenceStatus Status { get; init; } = ReferenceStatus.Full;

    /// <summary>Optional note describing merges, gaps or intentional differences.</summary>
    public string? Note { get; init; }
}
