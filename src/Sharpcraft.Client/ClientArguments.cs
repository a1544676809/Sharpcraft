// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Lifecycle;

namespace Sharpcraft.Client;

/// <summary>
/// The client's command-line contract (docs/modules/launcher.md §5.1). Parsing is separate from
/// <c>Main</c> so it can be tested without starting a game.
/// </summary>
/// <param name="VersionId">Version id.</param>
/// <param name="GameDirectory">Instance directory.</param>
/// <param name="AssetsDirectory">Official assets root.</param>
/// <param name="AssetIndexId">Asset index id.</param>
/// <param name="PackPath">Base asset pack.</param>
/// <param name="SessionFilePath">Session file holding the token.</param>
/// <param name="Username">Profile name, when passed directly.</param>
/// <param name="Uuid">Profile UUID, when passed directly.</param>
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
    IReadOnlyList<string> Errors)
{
    /// <summary>Usage text, printed when parsing fails or help is requested.</summary>
    public const string Usage = """
        Sharpcraft.Client — started by a launcher, never by hand.

          --version <id>          version id (required)
          --gameDir <dir>         instance directory (required)
          --assetsDir <dir>       official assets root
          --assetIndex <id>       official asset index id
          --pack <path>           base asset pack
          --sessionFile <path>    session file holding the token
          --username <name>       profile name (alternative to the session file)
          --uuid <uuid>           profile UUID; derived from the name when absent
          --offlineDeveloperMode  start without online services (launcher decides, as in vanilla)
          --accessToken <token>   accepted for launcher compatibility; never required

        Tokens travel in the session file, not on the command line.
        """;

    private static readonly HashSet<string> KnownSwitches = new(StringComparer.Ordinal)
    {
        "--version",
        "--gameDir",
        "--assetsDir",
        "--assetIndex",
        "--pack",
        "--sessionFile",
        "--username",
        "--uuid",
        "--accessToken"
    };

    /// <summary>True when the handoff carries everything needed to start.</summary>
    public bool IsUsable => Errors.Count == 0;

    /// <summary>Parses the arguments; collects every problem instead of failing on the first.</summary>
    public static ClientArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? version = null, gameDir = null, assetsDir = null, assetIndex = null;
        string? pack = null, session = null, username = null, uuid = null, accessToken = null;
        var offline = false;
        var errors = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                errors.Add($"unexpected argument '{argument}'");
                continue;
            }

            // Boolean switches take no value.
            if (argument == "--offlineDeveloperMode")
            {
                offline = true;
                continue;
            }

            // Unknown switches are skipped together with an optional value, so extras from other
            // launchers (--width 1280, --demo) never break startup.
            if (!KnownSwitches.Contains(argument))
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
                default:
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("--version is required");
        }

        if (string.IsNullOrWhiteSpace(gameDir))
        {
            errors.Add("--gameDir is required");
        }

        if (string.IsNullOrWhiteSpace(session) && string.IsNullOrWhiteSpace(username))
        {
            errors.Add("either --sessionFile or --username is required");
        }

        // Vanilla derives a name-based UUID when none is supplied; a third-party launcher that
        // omits --uuid must keep working, so the same fallback applies here.
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

        return new ClientArguments(
            version, gameDir, assetsDir, assetIndex, pack, session, username, uuid, offline, accessToken, errors);
    }
}
