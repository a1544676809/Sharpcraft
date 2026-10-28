// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Auth;
using MinecraftCSharp.Common.Network;

namespace MinecraftCSharp.Launcher;

/// <summary>
/// Standalone launcher entry point. This build only carries the authentication and probe
/// vertical slices (docs/modules/launcher.md); version/asset handling comes later.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var commandLine = LauncherCommandLine.Parse(args);
        if (commandLine.Error is { } error)
        {
            Console.Error.WriteLine($"error: {error}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(LauncherCommandLine.Usage);
            return 2;
        }

        return commandLine.Command switch
        {
            LauncherCommand.Help => WriteUsage(),
            LauncherCommand.Probe => await ProbeAsync(commandLine.Target!).ConfigureAwait(false),
            LauncherCommand.Login => await LoginAsync().ConfigureAwait(false),
            _ => 2
        };
    }

    private static int WriteUsage()
    {
        Console.WriteLine(LauncherCommandLine.Usage);
        return 0;
    }

    /// <summary>Runs the device code flow and reports the profile, never the tokens.</summary>
    private static async Task<int> LoginAsync()
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        try
        {
            var flow = new DeviceCodeFlow(http);
            var challenge = await flow.RequestCodeAsync(cancellation.Token).ConfigureAwait(false);

            Console.WriteLine($"Open {challenge.VerificationUri} and enter the code: {challenge.UserCode}");
            if (!string.IsNullOrEmpty(challenge.Message))
            {
                Console.WriteLine(challenge.Message);
            }

            Console.WriteLine("Waiting for authorisation (Ctrl+C to cancel)...");
            var microsoft = await flow.PollAsync(challenge, cancellation.Token).ConfigureAwait(false);

            var xbox = new XboxLiveAuthenticator(http);
            var userToken = await xbox.AuthenticateAsync(microsoft.AccessToken, cancellation.Token).ConfigureAwait(false);
            var xsts = await xbox.AuthorizeAsync(userToken, cancellation.Token).ConfigureAwait(false);

            var services = new MinecraftServicesClient(http);
            var session = await services.LoginWithXboxAsync(xsts, cancellation.Token).ConfigureAwait(false);
            var profile = await services.GetProfileAsync(session, cancellation.Token).ConfigureAwait(false);

            Console.WriteLine($"Signed in as {profile.Name} ({profile.Id})");
            Console.WriteLine($"Session valid until {session.ExpiresAt:u} (not persisted)");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return 1;
        }
        catch (AuthException exception)
        {
            Console.Error.WriteLine($"login failed [{exception.Error}] {exception.Message}");
            if (exception.ServiceCode is { } code)
            {
                Console.Error.WriteLine($"service code: {code}");
            }

            return 1;
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"network failure: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> ProbeAsync(string address)
    {
        try
        {
            var result = await Task.Run(() => StatusPingClient.Query(address, TimeSpan.FromSeconds(15)))
                .ConfigureAwait(false);

            Console.WriteLine($"{address}: protocol {result.ProtocolVersion} ({result.VersionName})");
            Console.WriteLine($"players {result.OnlinePlayers}/{result.MaxPlayers}, latency {result.LatencyMilliseconds} ms");
            if (!string.IsNullOrWhiteSpace(result.Description))
            {
                Console.WriteLine($"motd: {result.Description}");
            }

            return 0;
        }
        catch (Exception exception)
            when (exception is TimeoutException or InvalidDataException or System.Net.Sockets.SocketException)
        {
            Console.Error.WriteLine($"probe failed: {exception.Message}");
            return 1;
        }
    }
}
