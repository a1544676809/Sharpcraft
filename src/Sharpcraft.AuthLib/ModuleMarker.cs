// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.AuthLib;

/// <summary>
/// Marks the neutral shared profile/auth library boundary. It contains profile-property data, signature
/// verification, public-key discovery and skin-texture payload parsing; it never references a game side or a host
/// executable.
/// </summary>
internal static class ModuleMarker
{
    internal const string AssemblyRole = "Neutral shared profile/auth library (vanilla authlib equivalent).";
}
