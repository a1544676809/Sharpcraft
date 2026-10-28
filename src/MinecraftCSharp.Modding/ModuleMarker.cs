// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Modding;

/// <summary>
/// Marks the assembly boundary. Mod API contracts, loader and hot-reload host. Side: peripheral host layer.
/// This type exists so the assembly is never empty; delete it once real types land.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Mod API contracts, loader and hot-reload host. Side: peripheral host layer.";
}
