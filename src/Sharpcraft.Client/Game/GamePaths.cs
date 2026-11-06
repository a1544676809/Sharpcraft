// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Game;

/// <summary>
/// Location rules the standalone client needs on its own. The launcher has its own copy of the repository
/// guard; the client must not depend on the launcher, so the few directory defaults it needs live here.
/// </summary>
internal static class GamePaths
{
    /// <summary>Default directory the launcher publishes asset packs into, under the user's local app data.</summary>
    internal static string DefaultPacksDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sharpcraft",
            "packs");

    /// <summary>The one directory interface documents are read from (ADR-0010).</summary>
    internal static string DefaultUiDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sharpcraft");
}
