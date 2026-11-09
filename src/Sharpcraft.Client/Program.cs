// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Game;

namespace Sharpcraft.Client;

/// <summary>
/// Standalone client entry point: it opens the window, connects to a server and runs the game loop itself,
/// without any launcher being present (docs/modules/client.md, ADR-0026).
/// </summary>
internal static class Program
{
    /// <summary>Exit code for arguments that cannot be parsed.</summary>
    private const int ExitBadArguments = 2;

    /// <summary>Exit code for a well-formed handoff that cannot be used (missing pack, unreadable session).</summary>
    private const int ExitUnusableHandoff = 3;

    /// <summary>Exit code for a runtime failure after the arguments were accepted.</summary>
    private const int ExitRuntimeFailure = 1;

    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            Console.WriteLine(ClientArguments.Usage);
            return 0;
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

        // The client reads no environment (principle 13): every debug switch comes from the host explicitly.
        GameOptions.Configure(parsed.BuildDebugValues());

    // Apply host-provided diagnostics (explicit options only; the game reads no environment — principle 13).
    Sharpcraft.Client.Assets.PackWatcher.IsEnabled =
        GameOptions.Flag(Sharpcraft.Client.Assets.PackWatcher.SwitchName);
    Sharpcraft.Common.Network.PlaySession.ChunkDumpDirectory = GameOptions.Get("MC_DUMP_CHUNK_DIR");
    if (float.TryParse(
            GameOptions.Get("MC_TEXT_BACKGROUND_OPACITY"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var debugOpacity))
    {
        Sharpcraft.Client.Ui.ChatLog.BackgroundOpacity = Math.Clamp(debugOpacity, 0f, 1f);
    }

        using var timerResolution = Sharpcraft.Framework.Platform.TimerResolution.Elevate();

        try
        {
            return RunAsync(parsed).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[client] fatal: {exception}");
            return ExitRuntimeFailure;
        }
    }

    private static async Task<int> RunAsync(ClientArguments parsed)
    {
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[client] Sharpcraft.Client starting (version {0}, mode {1})",
            parsed.VersionId,
            parsed.Mode));

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] game dir:    {0}", parsed.GameDirectory));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] assets dir:  {0}", parsed.AssetsDirectory ?? "(none)"));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] asset index: {0}", parsed.AssetIndexId ?? "(none)"));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[client] asset pack:  {0}", parsed.PackPath ?? "(none)"));

        GameIdentity identity;
        try
        {
            identity = GameIdentity.FromArguments(parsed);
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"[client] {exception.Message}");
            return ExitUnusableHandoff;
        }

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[client] profile:     {0} ({1}){2}",
            identity.Name,
            identity.Uuid,
            identity.Offline ? " [offline]" : string.Empty));

        // The asset pack is produced by the launcher (or any launcher) before the client starts; without it the
        // world renders colour-only, so a missing pack is treated as an unusable handoff for the product path.
        if (string.IsNullOrWhiteSpace(parsed.PackPath) || !File.Exists(parsed.PackPath))
        {
            Console.Error.WriteLine("[client] asset pack missing; a launcher must build it before starting the client");
            return ExitUnusableHandoff;
        }

        using var cancellation = CancelOnCtrlC();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        return parsed.Mode switch
        {
            ClientRunMode.Frame => await RunFrameAsync(parsed, identity, http, cancellation.Token).ConfigureAwait(false),
            ClientRunMode.Window => await RunWindowAsync(parsed, identity, http, cancellation.Token).ConfigureAwait(false),
            ClientRunMode.Walk => await RunWalkAsync(parsed, identity, http, cancellation.Token).ConfigureAwait(false),
            _ => await RunGameAsync(parsed, identity, http, cancellation.Token).ConfigureAwait(false)
        };
    }

    /// <summary>The product path: open the window, connect if an address was given, and run the game loop.</summary>
    private static async Task<int> RunGameAsync(
        ClientArguments parsed,
        GameIdentity identity,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var collectSeconds = Number("MC_FRAME_SECONDS", 8);
        var frames = Number("MC_WINDOW_FRAMES", 0);
        var visibleFor = GameOptions.Get("MC_WINDOW_SECONDS") is { Length: > 0 } seconds && int.TryParse(seconds, out var windowSeconds)
            ? TimeSpan.FromSeconds(windowSeconds)
            : (TimeSpan?)null;

        var path = await WindowCommand.RunAsync(
            parsed.QuickPlayMultiplayer,
            http,
            parsed.Width,
            parsed.Height,
            collectSeconds,
            frames,
            visibleFor,
            identity,
            parsed.GameDirectory,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"frame written to {path}");
        return 0;
    }

    private static async Task<int> RunFrameAsync(
        ClientArguments parsed,
        GameIdentity identity,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var collectSeconds = Number("MC_FRAME_SECONDS", 8);
        var path = await FrameCommand.RunAsync(
            parsed.QuickPlayMultiplayer ?? string.Empty,
            http,
            parsed.Width,
            parsed.Height,
            collectSeconds,
            identity,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"frame written to {path}");
        return 0;
    }

    private static async Task<int> RunWindowAsync(
        ClientArguments parsed,
        GameIdentity identity,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var collectSeconds = Number("MC_FRAME_SECONDS", 8);
        var frames = Number("MC_WINDOW_FRAMES", 0);
        var visibleFor = GameOptions.Get("MC_WINDOW_SECONDS") is { Length: > 0 } seconds && int.TryParse(seconds, out var windowSeconds)
            ? TimeSpan.FromSeconds(windowSeconds)
            : TimeSpan.FromSeconds(8);

        var path = await WindowCommand.RunAsync(
            parsed.QuickPlayMultiplayer,
            http,
            parsed.Width,
            parsed.Height,
            collectSeconds,
            frames,
            visibleFor,
            identity,
            parsed.GameDirectory,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"frame written to {path}");
        return 0;
    }

    private static async Task<int> RunWalkAsync(
        ClientArguments parsed,
        GameIdentity identity,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parsed.QuickPlayMultiplayer))
        {
            Console.Error.WriteLine("[client] --walk needs --quickPlayMultiplayer <address>");
            return ExitBadArguments;
        }

        var (outcome, connection) = await GameConnector
            .LoginAsync(parsed.QuickPlayMultiplayer, http, identity, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Success || connection is null)
        {
            Console.Error.WriteLine($"[client] server refused the login: {outcome.Reason}");
            return ExitRuntimeFailure;
        }

        using (connection)
        {
            var probe = await ConfigurationProbe
                .RunAsync(connection, TimeSpan.FromSeconds(20), cancellationToken)
                .ConfigureAwait(false);

            if (!probe.ReachedPlay)
            {
                Console.Error.WriteLine($"[client] did not reach play: {probe.Reason}");
                return ExitRuntimeFailure;
            }

            var seconds = Number("MC_WALK_SECONDS", 5);
            var result = await WalkCommand
                .RunAsync(connection, seconds, line => Console.WriteLine($"  {line}"), cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine(
                $"walk: sent={result.TicksSent} corrections={result.Corrections} " +
                $"distance={result.Distance:F2} survived={result.Survived}");
            return result.Survived ? 0 : ExitRuntimeFailure;
        }
    }

    private static int Number(string name, int fallback) =>
        GameOptions.Get(name) is { Length: > 0 } raw && int.TryParse(raw, out var parsed) ? parsed : fallback;

    private static CancellationTokenSource CancelOnCtrlC()
    {
        var source = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            source.Cancel();
        };
        return source;
    }
}
