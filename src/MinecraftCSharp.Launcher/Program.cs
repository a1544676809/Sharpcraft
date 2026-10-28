// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Auth;
using MinecraftCSharp.Common.Network;

namespace MinecraftCSharp.Launcher;

/// <summary>
/// Launcher entry point. This build carries the authentication, probe and play slices
/// (docs/modules/launcher.md); version/asset handling comes later.
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
            LauncherCommand.Logout => Logout(),
            LauncherCommand.Probe => await ProbeAsync(commandLine.Target!).ConfigureAwait(false),
            LauncherCommand.Login => await LoginAsync().ConfigureAwait(false),
            LauncherCommand.Play => await PlayAsync(commandLine.Target!).ConfigureAwait(false),
            _ => 2
        };
    }

    private static int WriteUsage()
    {
        Console.WriteLine(LauncherCommandLine.Usage);
        return 0;
    }

    private static int Logout()
    {
        var store = TokenStore.CreateDefault();
        var removed = store.Delete();
        Console.WriteLine(removed ? $"removed cached tokens at {store.FilePath}" : "no cached tokens");
        return 0;
    }

    /// <summary>Signs in and reports the profile, never the tokens.</summary>
    private static async Task<int> LoginAsync()
    {
        using var cancellation = CancelOnCtrlC();
        using var http = CreateHttpClient();

        try
        {
            var signedIn = await AuthenticationFlow.EnsureSignedInAsync(
                http,
                TokenStore.CreateDefault(),
                forceInteractive: false,
                onChallenge: PrintChallenge,
                cancellation.Token).ConfigureAwait(false);

            Console.WriteLine($"Signed in as {signedIn.Profile.Name} ({signedIn.Profile.Id})");
            Console.WriteLine(signedIn.FromCache ? "token source: local cache" : "token source: device code flow");
            Console.WriteLine($"Minecraft session valid until {signedIn.Session.ExpiresAt:u}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return 1;
        }
        catch (AuthException exception)
        {
            return ReportAuthFailure(exception);
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"network failure: {exception.Message}");
            return 1;
        }
    }

    /// <summary>Signs in, then walks the login and configuration phases of a real server.</summary>
    private static async Task<int> PlayAsync(string address)
    {
        using var cancellation = CancelOnCtrlC();
        using var http = CreateHttpClient();

        try
        {
            var signedIn = await AuthenticationFlow.EnsureSignedInAsync(
                http,
                TokenStore.CreateDefault(),
                forceInteractive: false,
                onChallenge: PrintChallenge,
                cancellation.Token).ConfigureAwait(false);

            Console.WriteLine($"Signed in as {signedIn.Profile.Name} ({signedIn.Profile.Id})");

            var join = new SessionServerClient(http);
            var (outcome, connection) = await MinecraftLoginClient.LoginAsync(
                address,
                (signedIn.Profile.Name, signedIn.Profile.Id),
                signedIn.Session.AccessToken,
                (token, hash, token2) => join.JoinAsync(signedIn.Session, signedIn.Profile.Id, hash, token2),
                cancellationToken: cancellation.Token).ConfigureAwait(false);

            Console.WriteLine(
                $"login: success={outcome.Success} encrypted={outcome.Encrypted} " +
                $"compression={outcome.CompressionThreshold} hash={ShortHash(outcome.ServerHash)}");

            if (!outcome.Success)
            {
                Console.Error.WriteLine($"server refused the login: {outcome.Reason}");
                return 1;
            }

            using (connection)
            {
                var probe = await ConfigurationProbe.RunAsync(
                    connection!,
                    TimeSpan.FromSeconds(20),
                    cancellation.Token,
                    log: line => Console.WriteLine($"  {line}")).ConfigureAwait(false);

                Console.WriteLine($"configuration packets: {probe.Sequence.Count}");
                foreach (var name in probe.Sequence)
                {
                    Console.WriteLine($"  - {name}");
                }

                if (probe.ReachedPlay)
                {
                    Console.WriteLine("reached the play phase; disconnecting as planned");
                    return 0;
                }

                Console.Error.WriteLine($"did not reach play: {probe.Reason}");
                return 1;
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("cancelled");
            return 1;
        }
        catch (AuthException exception)
        {
            return ReportAuthFailure(exception);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or TimeoutException)
        {
            Console.Error.WriteLine($"connection failed: {exception.Message}");
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

    private static HttpClient CreateHttpClient() => new() { Timeout = TimeSpan.FromSeconds(30) };

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

    private static Task PrintChallenge(DeviceCodeChallenge challenge, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Open {challenge.VerificationUri} and enter the code: {challenge.UserCode}");
        if (!string.IsNullOrEmpty(challenge.Message))
        {
            Console.WriteLine(challenge.Message);
        }

        Console.WriteLine("Waiting for authorisation (Ctrl+C to cancel)...");
        return Task.CompletedTask;
    }

    private static int ReportAuthFailure(AuthException exception)
    {
        Console.Error.WriteLine($"login failed [{exception.Error}] {exception.Message}");
        if (exception.ServiceCode is { } code)
        {
            Console.Error.WriteLine($"service code: {code}");
        }

        return 1;
    }

    private static string ShortHash(string hash) =>
        string.IsNullOrEmpty(hash) ? "-" : hash.Length <= 12 ? hash : hash[..12] + "...";
}
