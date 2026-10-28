// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Launcher;

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
    Play
}

/// <summary>Parsed command line.</summary>
/// <param name="Command">Command to run.</param>
/// <param name="Target">Target address for commands that need one.</param>
/// <param name="Error">Parse error, when the line is invalid.</param>
public sealed record LauncherCommandLine(LauncherCommand Command, string? Target, string? Error)
{
    /// <summary>Usage text.</summary>
    public const string Usage = """
        MinecraftCSharp launcher (vertical slice)

          login              sign in through the Microsoft device code flow
          logout             delete the cached tokens
          probe <address>    query a server's status (SRV aware)
          play <address>     sign in, then walk login and configuration phases
          help               show this text

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

            default:
                return new LauncherCommandLine(LauncherCommand.Help, null, $"unknown command '{head}'");
        }
    }

    private static LauncherCommandLine WithAddress(LauncherCommand command, IReadOnlyList<string> args, string name) =>
        args.Count switch
        {
            1 => new LauncherCommandLine(command, null, $"{name} needs a server address"),
            2 => new LauncherCommandLine(command, args[1], null),
            _ => new LauncherCommandLine(command, null, $"{name} takes exactly one address")
        };
}
