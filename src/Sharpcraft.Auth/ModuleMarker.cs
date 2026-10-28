// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Auth;

/// <summary>
/// Marks the assembly boundary. Microsoft / Xbox Live / XSTS / Minecraft services authentication chain. Side: peripheral host layer.
/// This type exists so the assembly is never empty; delete it once real types land.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Microsoft / Xbox Live / XSTS / Minecraft services authentication chain. Side: peripheral host layer.";
}
