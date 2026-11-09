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

    /// <summary>
    /// Default instance directory, mirroring the reference default of the user's roaming application data
    /// (<c>Minecraft.gameDirectory</c>). Screenshots live under <c>&lt;gameDir&gt;/screenshots</c>, so this must not point
    /// inside the repository or the local app data folder.
    /// </summary>
    internal static string DefaultGameDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".sharpcraft");
    /// <summary>The one directory interface documents are read from (ADR-0010).</summary>
    internal static string DefaultUiDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sharpcraft");
}
