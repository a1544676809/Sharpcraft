// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common;

/// <summary>
/// Marks the assembly boundary. Shared values and containers: identifiers, registries, math primitives, NBT, protocol codecs, world data structures. Side: Common.
/// This type exists so the assembly is never empty; delete it once real types land.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Shared values and containers: identifiers, registries, math primitives, NBT, protocol codecs, world data structures. Side: Common.";
}
