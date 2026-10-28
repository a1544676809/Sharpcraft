// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Framework;

/// <summary>
/// Marks the assembly boundary. Engine layer: WGPU abstraction, windowing, input, resource loading, audio backend, performance infrastructure. Side: peripheral host layer.
/// This type exists so the assembly is never empty; delete it once real types land.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Engine layer: WGPU abstraction, windowing, input, resource loading, audio backend, performance infrastructure. Side: peripheral host layer.";
}
