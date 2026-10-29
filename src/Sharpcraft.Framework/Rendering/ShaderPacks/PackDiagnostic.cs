// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.ShaderPacks;

/// <summary>Why a pack was rejected.</summary>
public enum PackDiagnosticKind
{
    /// <summary>The pack name is empty.</summary>
    MissingName,

    /// <summary>Two targets or two phases share a name.</summary>
    DuplicateName,

    /// <summary>A phase names a target that was never declared.</summary>
    UnknownTarget,

    /// <summary>A phase depends on a phase that does not exist.</summary>
    UnknownDependency,

    /// <summary>The dependency graph has a cycle.</summary>
    CyclicDependency,

    /// <summary>No phase is marked as writing the framebuffer.</summary>
    MissingFinalPhase,

    /// <summary>More than one phase claims the framebuffer.</summary>
    MultipleFinalPhases,

    /// <summary>A phase writes the same target twice.</summary>
    DuplicateWrite,

    /// <summary>A uniform is not in <see cref="KnownUniforms"/>.</summary>
    UnknownUniform,

    /// <summary>A target size is not usable.</summary>
    InvalidSize
}

/// <summary>One reason a pack was rejected, carrying enough context to fix the pack.</summary>
/// <param name="Kind">Category.</param>
/// <param name="Where">Phase or target the problem belongs to, when applicable.</param>
/// <param name="Detail">Human-readable detail; never contains asset contents or absolute paths.</param>
public sealed record PackDiagnostic(PackDiagnosticKind Kind, string? Where, string Detail)
{
    /// <inheritdoc />
    public override string ToString() => Where is null ? $"{Kind}: {Detail}" : $"{Kind} at {Where}: {Detail}";
}

/// <summary>Result of validating a pack: either accepted or a list of reasons.</summary>
/// <param name="Diagnostics">Empty when the pack is valid.</param>
/// <param name="ExecutionOrder">Phase names in the order they must run; empty when invalid.</param>
public sealed record PackValidation(IReadOnlyList<PackDiagnostic> Diagnostics, IReadOnlyList<string> ExecutionOrder)
{
    /// <summary>True when the pack may be executed.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}
