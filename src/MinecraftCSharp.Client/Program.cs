// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Globalization;
using MinecraftCSharp.Common.Lifecycle;

namespace MinecraftCSharp.Client;

/// <summary>
/// Client gameplay entry point: rendering presentation, UI, input, audio playback, multiplayer
/// session. Side: Client. This milestone validates the launcher handoff; gameplay arrives with M4
/// (docs/milestones/roadmap.md).
/// </summary>
internal static class Program
{
    /// <summary>Exit code for arguments that cannot be parsed.</summary>
    private const int ExitBadArguments = 2;

    /// <summary>Exit code for a well-formed handoff that cannot be used (missing session or pack).</summary>
    private const int ExitUnusableHandoff = 3;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(ClientArguments.Usage);
            return args.Length == 0 ? ExitBadArguments : 0;
        }

        var parsed = ClientArguments.Parse(args);
        if (!parsed.IsUsable)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine($"[client] {error}");
            }

            Console.Error.WriteLine(ClientArguments.Usage);
            return ExitBadArguments;
        }

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[client] MinecraftCSharp.Client starting (version {0})",
            parsed.VersionId));

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] game dir:    {0}", parsed.GameDirectory));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] assets dir:  {0}", parsed.AssetsDirectory ?? "(none)"));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] asset index: {0}", parsed.AssetIndexId ?? "(none)"));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] asset pack:  {0}", parsed.PackPath ?? "(none)"));

        var username = parsed.Username;
        var uuid = parsed.Uuid;

        if (!string.IsNullOrWhiteSpace(parsed.SessionFilePath))
        {
            try
            {
                var session = SessionFile.ReadFrom(parsed.SessionFilePath);
                username = session.Username;
                uuid = session.Uuid;
                Console.WriteLine($"[client] session:     {session}");
            }
            catch (InvalidDataException exception)
            {
                Console.Error.WriteLine($"[client] {exception.Message}");
                return ExitUnusableHandoff;
            }
        }

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[client] profile:     {0} ({1})",
            username ?? "(unknown)",
            uuid ?? "(unknown)"));

        if (string.IsNullOrWhiteSpace(parsed.PackPath) || !File.Exists(parsed.PackPath))
        {
            Console.Error.WriteLine("[client] asset pack missing; the launcher must build it before starting the client");
            return ExitUnusableHandoff;
        }

        Console.WriteLine("[client] handoff verified; gameplay arrives with milestone M4");
        return 0;
    }
}
