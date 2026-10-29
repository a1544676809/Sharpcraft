// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.ShaderPacks;

/// <summary>A render target a pack asks the engine to allocate.</summary>
/// <param name="Name">Identifier the phases use to refer to it.</param>
/// <param name="Size">How to size it.</param>
/// <param name="Format">Colour format, or null for depth.</param>
public sealed record RenderTargetDescription(string Name, TargetSize Size, string? Format);

/// <summary>One phase (pass) of a pack.</summary>
/// <param name="Name">Phase name, used in diagnostics.</param>
/// <param name="Program">Shader program identifier; the pack supplies the source.</param>
/// <param name="Writes">Targets this phase writes.</param>
/// <param name="Reads">Targets this phase samples.</param>
/// <param name="After">Phases that must run before this one.</param>
/// <param name="Final">True for the phase that writes the framebuffer.</param>
public sealed record PhaseDescription(
    string Name,
    string Program,
    IReadOnlyList<string> Writes,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> After,
    bool Final);

/// <summary>Uniform names the engine can supply; a pack may only use these.</summary>
public static class KnownUniforms
{
    /// <summary>Every uniform the engine provides to phases.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "viewProjection",
        "cameraPosition",
        "frameTime",
        "targetWidth",
        "targetHeight"
    ];
}

/// <summary>
/// A whole pack as data: targets, phases and the uniforms it consumes. Loading a concrete pack file
/// format is out of scope here; this is the contract the engine executes
/// (docs/specs/render/shader-packs.md 1).
/// </summary>
/// <param name="Name">Pack name for diagnostics.</param>
/// <param name="Targets">Declared render targets, framebuffer excluded.</param>
/// <param name="Phases">Declared phases in written order.</param>
/// <param name="Uniforms">Uniform names the pack consumes.</param>
public sealed record ShaderPackDescription(
    string Name,
    IReadOnlyList<RenderTargetDescription> Targets,
    IReadOnlyList<PhaseDescription> Phases,
    IReadOnlyList<string> Uniforms);
