// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.Shaders;

/// <summary>
/// The list of pipelines, so "which shaders exist" is answered in one place instead of by memory.
/// </summary>
/// <remarks>
/// Each entry names its file, the entry points the renderer asks for and the bindings its layout declares. Validation
/// runs at startup and fails loudly, because a shader that references a binding its layout lacks used to surface as an
/// opaque device error (docs/specs/render/shader-organisation.md 5-6).
/// </remarks>
public static class ShaderPipelines
{
    /// <summary>Every pipeline with its expected entries and bindings.</summary>
    public static IReadOnlyList<ShaderDescriptor> All { get; } =
    [
        new ShaderDescriptor("terrain", "terrain.wgsl", ["vs_main", "fs_main"], [0, 1, 2, 3]),
        new ShaderDescriptor("sky", "sky.wgsl", ["vs_sky", "fs_sky"], []),
        new ShaderDescriptor("ui_panel", "ui_panel.wgsl", ["vs_main", "fs_main"], [2]),
        new ShaderDescriptor("ui_sprite", "ui_sprite.wgsl", ["vs_main", "fs_main"], [0, 1, 2]),
        new ShaderDescriptor("ui_text", "ui_text.wgsl", ["vs_main", "fs_main"], [0, 1, 2]),
    ];

    private static bool _validated;

    /// <summary>Loads and validates every pipeline once; throws with every violation found.</summary>
    public static void EnsureValid()
    {
        if (_validated)
        {
            return;
        }

        var violations = new List<string>();

        foreach (var descriptor in All)
        {
            violations.AddRange(descriptor.Validate(ShaderLibrary.Load(descriptor.Name)));
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException("shader/layout disagreement:\n" + string.Join("\n", violations));
        }

        _validated = true;
    }
}
