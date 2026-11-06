// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Text.Json;
using Sharpcraft.Common.Lifecycle;

namespace Sharpcraft.Client;

/// <summary>How the standalone client should run.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.ClientRunMode", "ref://mc-26.3-analysis/src/net/minecraft/client/main/Main.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft extension for diagnostic modes; no vanilla counterpart")]
public enum ClientRunMode
{
    /// <summary>Open the window, connect (when an address was given) and run the game loop.</summary>
    Game,

    /// <summary>Render one offscreen frame to a PNG (diagnostic).</summary>
    Frame,

    /// <summary>Present the world in a window without the full game session (diagnostic).</summary>
    Window,

    /// <summary>Walk a live server without rendering, to verify movement packets (diagnostic).</summary>
    Walk
}

/// <summary>
/// The client's command-line contract, aligned with vanilla <c>net.minecraft.client.main.Main</c>
/// (docs/decisions/0026-launcher-client-decoupling.md §4). Parsing is separate from <c>Main</c> so it can be
/// tested without starting a game.
/// </summary>
/// <param name="VersionId">Version id (required).</param>
/// <param name="GameDirectory">Instance directory.</param>
/// <param name="AssetsDirectory">Official assets root.</param>
/// <param name="AssetIndexId">Asset index id.</param>
/// <param name="PackPath">Base asset pack (launcher extension).</param>
/// <param name="SessionFilePath">Session file holding the token (launcher extension).</param>
/// <param name="Username">Profile name.</param>
/// <param name="Uuid">Profile UUID (derived from the name when absent or invalid).</param>
/// <param name="OfflineDeveloperMode">Start without online services or verification, as in vanilla.</param>
/// <param name="AccessToken">Accepted for vanilla compatibility; never validated.</param>
/// <param name="QuickPlayMultiplayer">Address to join directly (vanilla 26.3 removed --server/--port).</param>
/// <param name="VersionType">Version type label.</param>
/// <param name="Width">Initial window width.</param>
/// <param name="Height">Initial window height.</param>
/// <param name="ResourcePackDir">Directory of extra resource packs.</param>
/// <param name="DebugOptionsFile">Host-exported debug options JSON (launcher extension).</param>
/// <param name="Mode">Diagnostic run mode.</param>
/// <param name="Errors">Everything that makes the handoff unusable.</param>
public sealed record ClientArguments(
    string? VersionId,
    string? GameDirectory,
    string? AssetsDirectory,
    string? AssetIndexId,
    string? PackPath,
    string? SessionFilePath,
    string? Username,
    string? Uuid,
    bool OfflineDeveloperMode,
    string? AccessToken,
    string? QuickPlayMultiplayer,
    string? VersionType,
    int Width,
    int Height,
    string? ResourcePackDir,
    string? DebugOptionsFile,
    ClientRunMode Mode,
    IReadOnlyList<string> Errors)
{
    /// <summary>Usage text, printed when parsing fails or help is requested.</summary>
    public const string Usage = """
        Sharpcraft.Client — a standalone Minecraft client (can be started by any launcher or by hand).

          --version <id>              version id (required)
          --accessToken <token>       accepted for compatibility; not validated
          --username <name>           profile name (default Player<ms%1000>)
          --uuid <uuid>               profile UUID; derived from the name when absent or invalid
          --offlineDeveloperMode      start without online services or verification
          --quickPlayMultiplayer <a>  join a server directly (host:port)
          --gameDir <dir>             instance directory
          --assetsDir <dir>           official assets root
          --assetIndex <id>           official asset index id
          --width <n> / --height <n>  window size
          --versionType <type>        version type label
          --resourcePackDir <dir>     extra resource pack directory
          --pack <path>               base asset pack (Sharpcraft extension)
          --sessionFile <path>        session file holding the token (Sharpcraft extension)
          --debugOptionsFile <path>   host debug options JSON (Sharpcraft extension)
          --frame | --window | --walk diagnostic modes (Sharpcraft extension)

        Unknown arguments are ignored, like vanilla's jopt-simple non-options.
        """;

    private static readonly HashSet<string> ValueSwitches = new(StringComparer.Ordinal)
    {
        "--version",
        "--gameDir",
        "--assetsDir",
        "--assetIndex",
        "--pack",
        "--sessionFile",
        "--username",
        "--uuid",
        "--accessToken",
        "--quickPlayMultiplayer",
        "--versionType",
        "--width",
        "--height",
        "--resourcePackDir",
        "--debugOptionsFile"
    };

    /// <summary>True when the handoff carries everything needed to start.</summary>
    public bool IsUsable => Errors.Count == 0;

    /// <summary>Default profile name vanilla uses when none is supplied.</summary>
    public static string DefaultUsername() =>
        "Player" + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses the arguments; collects every problem instead of failing on the first.</summary>
    public static ClientArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? version = null, gameDir = null, assetsDir = null, assetIndex = null;
        string? pack = null, session = null, username = null, uuid = null, accessToken = null;
        string? quickPlay = null, versionType = null, resourcePackDir = null, debugOptionsFile = null;
        var width = 854;
        var height = 480;
        var offline = false;
        var mode = ClientRunMode.Game;
        var errors = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                // Non-option token: ignored like vanilla's jopt-simple, never an error.
                continue;
            }

            // Boolean switches take no value.
            switch (argument)
            {
                case "--offlineDeveloperMode":
                    offline = true;
                    continue;
                case "--frame":
                    mode = ClientRunMode.Frame;
                    continue;
                case "--window":
                    mode = ClientRunMode.Window;
                    continue;
                case "--walk":
                    mode = ClientRunMode.Walk;
                    continue;
            }

            // Unknown switches are skipped together with an optional value, so extras from other launchers
            // (--demo, --server legacy, --port) never break startup.
            if (!ValueSwitches.Contains(argument))
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    i++;
                }

                continue;
            }

            if (i + 1 >= args.Count)
            {
                errors.Add($"{argument} needs a value");
                break;
            }

            var value = args[++i];
            switch (argument)
            {
                case "--version":
                    version = value;
                    break;
                case "--gameDir":
                    gameDir = value;
                    break;
                case "--assetsDir":
                    assetsDir = value;
                    break;
                case "--assetIndex":
                    assetIndex = value;
                    break;
                case "--pack":
                    pack = value;
                    break;
                case "--sessionFile":
                    session = value;
                    break;
                case "--username":
                    username = value;
                    break;
                case "--uuid":
                    uuid = value;
                    break;
                case "--accessToken":
                    accessToken = value;
                    break;
                case "--quickPlayMultiplayer":
                    quickPlay = value;
                    break;
                case "--versionType":
                    versionType = value;
                    break;
                case "--width":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWidth) && parsedWidth > 0)
                    {
                        width = parsedWidth;
                    }

                    break;
                case "--height":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeight) && parsedHeight > 0)
                    {
                        height = parsedHeight;
                    }

                    break;
                case "--resourcePackDir":
                    resourcePackDir = value;
                    break;
                case "--debugOptionsFile":
                    debugOptionsFile = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("--version is required");
        }

        // Vanilla defaults the game directory; it is not a hard requirement for a standalone start.
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            gameDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sharpcraft",
                "instances",
                string.IsNullOrWhiteSpace(version) ? "default" : version);
        }

        // Vanilla's --username default; --uuid absent or invalid falls back to the offline name UUID.
        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(session))
        {
            username = DefaultUsername();
        }

        if (string.IsNullOrWhiteSpace(uuid) && !string.IsNullOrWhiteSpace(username))
        {
            if (OfflineIdentity.IsValidName(username))
            {
                uuid = OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(username));
            }
            else
            {
                errors.Add($"--username '{username}' is not a valid profile name");
            }
        }
        else if (!string.IsNullOrWhiteSpace(uuid) && !Guid.TryParse(uuid, out _))
        {
            // Invalid UUID: vanilla derives the offline UUID instead of failing.
            if (!string.IsNullOrWhiteSpace(username) && OfflineIdentity.IsValidName(username))
            {
                uuid = OfflineIdentity.FormatUuid(OfflineIdentity.DeriveUuid(username));
            }
            else
            {
                uuid = null;
            }
        }

        return new ClientArguments(
            version, gameDir, assetsDir, assetIndex, pack, session, username, uuid, offline, accessToken,
            quickPlay, versionType, width, height, resourcePackDir, debugOptionsFile, mode, errors);
    }

    /// <summary>
    /// The explicit debug values the host asked for: the contents of <c>--debugOptionsFile</c> plus the pack and
    /// resource-pack paths the launcher already knows. Passed to the game library as <c>GameOptions</c>.
    /// </summary>
    internal IReadOnlyDictionary<string, string> BuildDebugValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(DebugOptionsFile) && File.Exists(DebugOptionsFile))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(DebugOptionsFile));
                if (parsed is not null)
                {
                    foreach (var pair in parsed)
                    {
                        if (!string.IsNullOrEmpty(pair.Key))
                        {
                            values[pair.Key] = pair.Value;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // A malformed debug file is the host's problem; the game still starts with production defaults.
            }
        }

        if (!string.IsNullOrWhiteSpace(PackPath))
        {
            values["MC_ASSETS_PACK"] = PackPath;
        }

        if (!string.IsNullOrWhiteSpace(ResourcePackDir))
        {
            values["MC_RESOURCE_PACKS"] = ResourcePackDir;
        }

        return values;
    }
}
