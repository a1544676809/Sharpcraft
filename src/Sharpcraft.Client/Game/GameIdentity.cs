// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Lifecycle;

namespace Sharpcraft.Client.Game;

/// <summary>
/// The identity a client session runs as, built from the command line (or a launcher session file).
/// </summary>
/// <param name="Name">Profile name.</param>
/// <param name="Uuid">Profile UUID.</param>
/// <param name="AccessToken">Minecraft access token, empty for offline sessions.</param>
/// <param name="Offline">True when the session runs without online services (vanilla --offlineDeveloperMode).</param>
internal sealed record GameIdentity(string Name, string Uuid, string AccessToken, bool Offline)
{
    /// <summary>Builds the identity from parsed arguments, preferring the launcher's session file.</summary>
    internal static GameIdentity FromArguments(ClientArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!string.IsNullOrWhiteSpace(arguments.SessionFilePath) && File.Exists(arguments.SessionFilePath))
        {
            var session = SessionFile.ReadFrom(arguments.SessionFilePath);
            return new GameIdentity(session.Username, session.Uuid, session.AccessToken, arguments.OfflineDeveloperMode);
        }

        return new GameIdentity(
            arguments.Username ?? ClientArguments.DefaultUsername(),
            arguments.Uuid ?? string.Empty,
            arguments.AccessToken ?? string.Empty,
            arguments.OfflineDeveloperMode);
    }
}
