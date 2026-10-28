// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Server;

/// <summary>
/// Marks the assembly boundary. Authoritative logic: world simulation, tick advancement, commands, saves. Side: Server.
/// This type exists so the assembly is never empty; delete it once real types land.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Authoritative logic: world simulation, tick advancement, commands, saves. Side: Server.";
}
