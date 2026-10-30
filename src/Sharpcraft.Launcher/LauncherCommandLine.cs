// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Launcher;

/// <summary>Supported launcher commands (docs/modules/launcher.md 3).</summary>
public enum LauncherCommand
{
    /// <summary>Show usage.</summary>
    Help,

    /// <summary>Run the device code login flow.</summary>
    Login,

    /// <summary>Delete the cached tokens.</summary>
    Logout,

    /// <summary>Query a server's status.</summary>
    Probe,

    /// <summary>Sign in and walk a server's login/configuration phases.</summary>
    Play,

    /// <summary>Walk forward on a live server to verify the movement packets.</summary>
    Walk,

    /// <summary>Collect chunks and render one offscreen frame.</summary>
    Frame,

    /// <summary>Collect chunks and present them in a window.</summary>
    Window,

    /// <summary>Build (or reuse) the asset pack for a client jar.</summary>
    Assets,

    /// <summary>Verify, pack, sign in, then start the client.</summary>
    Launch,

    /// <summary>List, add or remove accounts (docs/modules/accounts.md).</summary>
    Accounts
}

/// <summary>Parsed command line.</summary>
/// <param name="Command">Command to run.</param>
/// <param name="Target">Target address for commands that need one.</param>
/// <param name="Error">Parse error, when the line is invalid.</param>
public sealed record LauncherCommandLine(LauncherCommand Command, string? Target, string? Error)
{
    /// <summary>Usage text.</summary>
    public const string Usage = """
        Sharpcraft launcher (vertical slice)

          login              sign in through the Microsoft device code flow
          logout             delete the cached tokens
          probe <address>    query a server's status (SRV aware)
          play <address>     sign in, then walk login and configuration phases
          frame <address>    collect chunks, then render one offscreen frame to a PNG
          window <address>   collect chunks, then present them in a window (also writes the PNG)
          assets <jar> [<version-json>]
                             verify a client jar, then build or reuse its asset pack
          launch <jar> <version-json> [--offline <name>]
                             verify, pack, sign in, then start the client
          accounts [add-offline <name> | remove <name>]
                             list accounts and whether offline login is available
          help               show this text

        The asset pack is written outside the repository, under the user's local app data,
        unless MC_ASSETS_OUT names another directory.

        Tokens are cached only outside the repository, under the user's local app data.
        """;

    /// <summary>Parses arguments; never throws.</summary>
    public static LauncherCommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return new LauncherCommandLine(LauncherCommand.Help, null, null);
        }

        var head = args[0].Trim();
        switch (head)
        {
            case "help" or "--help" or "-h" or "/?":
                return new LauncherCommandLine(LauncherCommand.Help, null, null);

            case "login":
                return args.Count == 1
                    ? new LauncherCommandLine(LauncherCommand.Login, null, null)
                    : new LauncherCommandLine(LauncherCommand.Login, null, "login takes no arguments");

            case "logout":
                return args.Count == 1
                    ? new LauncherCommandLine(LauncherCommand.Logout, null, null)
                    : new LauncherCommandLine(LauncherCommand.Logout, null, "logout takes no arguments");

            case "probe":
                return WithAddress(LauncherCommand.Probe, args, "probe");

            case "play":
                return WithAddress(LauncherCommand.Play, args, "play");

            case "walk":
                return WithAddress(LauncherCommand.Walk, args, "walk");

            case "frame":
                return WithAddress(LauncherCommand.Frame, args, "frame");

            case "window":
                return WithAddress(LauncherCommand.Window, args, "window");

            case "assets":
                return WithJar(LauncherCommand.Assets, args);

            case "launch":
                return WithLaunch(args);

            case "accounts":
                return new LauncherCommandLine(
                    LauncherCommand.Accounts,
                    string.Join(' ', args.Skip(1)),
                    null);

            default:
                return new LauncherCommandLine(LauncherCommand.Help, null, $"unknown command '{head}'");
        }
    }

    private static LauncherCommandLine WithLaunch(IReadOnlyList<string> args)
    {
        // launch <jar> <version-json> [--offline <name>]
        if (args.Count < 3)
        {
            return new LauncherCommandLine(LauncherCommand.Launch, null, "launch needs <jar> <version-json>");
        }

        var target = args[1] + "|" + args[2];

        if (args.Count == 3)
        {
            return new LauncherCommandLine(LauncherCommand.Launch, target, null);
        }

        if (args.Count == 5 && args[3] == "--offline")
        {
            return new LauncherCommandLine(LauncherCommand.Launch, target + "|" + args[4], null);
        }

        return new LauncherCommandLine(
            LauncherCommand.Launch,
            null,
            "launch takes <jar> <version-json> and an optional --offline <name>");
    }

    private static LauncherCommandLine WithJar(LauncherCommand command, IReadOnlyList<string> args) =>
        args.Count switch
        {
            1 => new LauncherCommandLine(command, null, "assets needs a client jar path"),
            2 => new LauncherCommandLine(command, args[1], null),
            3 => new LauncherCommandLine(command, args[1] + "|" + args[2], null),
            _ => new LauncherCommandLine(command, null, "assets takes <jar> and an optional <version-json>")
        };

    /// <summary>
    /// True when the caller supplied a local scene through the environment, so no server address is needed.
    /// </summary>
    private static bool HasLocalScene() =>
        Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 } ||
        Environment.GetEnvironmentVariable("MC_FRAME_WATER") == "1" ||
        Environment.GetEnvironmentVariable("MC_FRAME_REPLAY") is { Length: > 0 };

    private static LauncherCommandLine WithAddress(LauncherCommand command, IReadOnlyList<string> args, string name) =>
        args.Count switch
        {
            1 when command is LauncherCommand.Window or LauncherCommand.Frame && HasLocalScene()
            => new LauncherCommandLine(command, null, null),
        1 => new LauncherCommandLine(command, null, $"{name} needs a server address"),
            2 => new LauncherCommandLine(command, args[1], null),
            _ => new LauncherCommandLine(command, null, $"{name} takes exactly one address")
        };
}
