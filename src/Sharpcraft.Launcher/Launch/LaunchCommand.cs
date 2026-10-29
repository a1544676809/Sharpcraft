// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Globalization;
using Sharpcraft.Auth;
using Sharpcraft.Common.Lifecycle;
using Sharpcraft.Launcher.Accounts;
using Sharpcraft.Launcher.Assets;
using Sharpcraft.Client.Assets;

namespace Sharpcraft.Launcher.Launch;

/// <summary>
/// The <c>launch</c> command: verify the jar, make sure the asset pack exists, mount the official
/// index, sign in, then hand a token-free command line to the client
/// (docs/modules/launcher.md §5.1).
/// </summary>
public static class LaunchCommand
{
    /// <summary>Environment variable that prints the plan instead of starting a process.</summary>
    public const string DryRunVariable = "MC_LAUNCH_DRYRUN";

    /// <summary>Environment variable that opts back into putting the token on the command line.</summary>
    public const string InlineTokenVariable = "MC_LAUNCH_INLINE_TOKEN";

    /// <summary>Runs the launch flow for a jar and its version manifest.</summary>
    /// <param name="jarPath">Client jar to verify.</param>
    /// <param name="manifestPath">Version JSON the jar must match.</param>
    /// <param name="http">HTTP client for authentication.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Process exit code, or the launcher's own failure code.</returns>
    public static async Task<int> RunAsync(
        string jarPath,
        string manifestPath,
        HttpClient http,
        string? offlineName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(http);

        VersionManifest manifest;
        try
        {
            manifest = VersionManifest.ReadFile(manifestPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        string jarSha1;
        try
        {
            jarSha1 = manifest.ClientJarSha1 is { Length: > 0 } expected
                ? JarVerifier.Verify(jarPath, expected, manifest.ClientJarSize)
                : JarVerifier.ComputeSha1(jarPath);

            if (manifest.ClientJarSha1 is null)
            {
                Console.WriteLine($"[launch] warning: manifest declares no client hash, using {AssetPackBuilder.Shorten(jarSha1)}");
            }
        }
        catch (JarVerificationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 4;
        }

        var packsDirectory = Environment.GetEnvironmentVariable("MC_ASSETS_OUT") is { Length: > 0 } configured
            ? configured
            : HostPaths.DefaultPacksDirectory();

        AssetPackResult pack;
        try
        {
            pack = AssetPackBuilder.Build(jarPath, packsDirectory, manifest.Id, jarSha1);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or InvalidDataException)
        {
            Console.Error.WriteLine($"[launch] asset pack failed: {exception.Message}");
            return 1;
        }

        Console.WriteLine($"[launch] {pack.Describe()}");

        var assetsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(jarPath)!, "..", "..", "assets"));
        var indexId = manifest.AssetIndexId;
        if (indexId is not null)
        {
            var indexPath = Path.Combine(assetsDirectory, "indexes", indexId + ".json");
            if (File.Exists(indexPath))
            {
                try
                {
                    var index = OfficialObjectsSource.Open(indexPath, indexId);
                    Console.WriteLine($"[launch] official index {indexId}: {index.ObjectCount} objects (read-only mount)");
                    index.Dispose();
                }
                catch (InvalidOperationException exception)
                {
                    Console.Error.WriteLine($"[launch] {exception.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine($"[launch] warning: asset index {indexId} not found at {indexPath}; " +
                                  "sounds and non-English languages will be unavailable");
            }
        }

        if (offlineName is not null)
        {
            return RunOffline(manifest, pack, assetsDirectory, offlineName);
        }

        AuthenticationFlow.SignedIn signedIn;
        try
        {
            signedIn = await AuthenticationFlow.EnsureSignedInAsync(
                http,
                TokenStore.CreateDefault(),
                forceInteractive: false,
                onChallenge: (challenge, _) =>
                {
                    DeviceCodePrompter.Show(challenge);
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[launch] cancelled");
            return 1;
        }
        catch (AuthException exception)
        {
            Console.Error.WriteLine($"[launch] authentication failed: {exception.Message}");
            return 1;
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"[launch] authentication failed: {exception.Message}");
            return 1;
        }

        var gameDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sharpcraft",
            "instances",
            manifest.Id);

        var context = new LaunchContext(
            manifest.Id,
            jarPath,
            pack.PackPath,
            assetsDirectory,
            indexId,
            gameDirectory,
            signedIn.Profile,
            signedIn.Session.AccessToken);

        var plan = LaunchPlanBuilder.Build(context);

        // The token goes to a file, never to the command line (principle 8 hardening).
        var session = new SessionFile(
            manifest.Id,
            signedIn.Profile.Name,
            signedIn.Profile.Id,
            signedIn.Session.AccessToken,
            DateTimeOffset.UtcNow);

        session.WriteTo(plan.SessionFilePath);

        var inlineToken = Environment.GetEnvironmentVariable(InlineTokenVariable) is "1";
        if (inlineToken)
        {
            Console.Error.WriteLine(
                $"[launch] warning: {InlineTokenVariable}=1 puts the access token on the command line, " +
                "which other users on this machine can read");
            plan = plan with
            {
                Arguments = [.. plan.Arguments, "--accessToken", signedIn.Session.AccessToken]
            };
        }

        Console.WriteLine(plan.Describe());

        if (Environment.GetEnvironmentVariable(DryRunVariable) is "1")
        {
            Console.WriteLine("[launch] dry run: client not started");
            return 0;
        }

        return StartClient(plan);
    }

    /// <summary>
    /// Offline launch: the gate from AGENTS.md principle 7.1 runs here, and the client is told to
    /// skip online services. No token is produced or passed (docs/modules/accounts.md §11).
    /// </summary>
    private static int RunOffline(
        VersionManifest manifest,
        AssetPackResult pack,
        string assetsDirectory,
        string name)
    {
        (string Name, string Uuid) account;
        try
        {
            account = AccountsCommand.ResolveOfflineAccount(name);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"[launch] {exception.Message}");
            return 4;
        }

        var gameDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sharpcraft",
            "instances",
            manifest.Id);

        var arguments = new List<string>
        {
            "--version", manifest.Id,
            "--gameDir", gameDirectory,
            "--assetsDir", assetsDirectory,
            "--pack", pack.PackPath,
            "--offlineDeveloperMode",
            "--username", account.Name,
            "--uuid", account.Uuid
        };

        if (!string.IsNullOrWhiteSpace(manifest.AssetIndexId))
        {
            arguments.Add("--assetIndex");
            arguments.Add(manifest.AssetIndexId);
        }

        var plan = new LaunchPlan(
            manifest.Id,
            LaunchPlanBuilder.ResolveClientPath(),
            arguments,
            gameDirectory,
            pack.PackPath,
            manifest.AssetIndexId,
            SessionFilePath: string.Empty);

        Console.WriteLine(plan.Describe());
        Console.WriteLine("[launch] offline session: no token is produced; the launcher approved this account");

        if (Environment.GetEnvironmentVariable(DryRunVariable) is "1")
        {
            Console.WriteLine("[launch] dry run: client not started");
            return 0;
        }

        return StartClient(plan);
    }

    private static int StartClient(LaunchPlan plan)
    {
        if (!File.Exists(plan.ClientPath))
        {
            Console.Error.WriteLine(
                $"[launch] client not found: {plan.ClientPath}. Build src/Sharpcraft.Client or set " +
                $"{LaunchPlanBuilder.ClientPathVariable}.");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = plan.GameDirectory
        };

        if (LaunchPlanBuilder.NeedsDotnetHost(plan.ClientPath))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(plan.ClientPath);
        }
        else
        {
            startInfo.FileName = plan.ClientPath;
        }

        foreach (var argument in plan.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Directory.CreateDirectory(plan.GameDirectory);

        Console.WriteLine($"[launch] starting {Path.GetFileName(plan.ClientPath)}");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("[launch] failed to start the client process");
            return 1;
        }

        process.WaitForExit();

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[launch] client exited with code {0}",
            process.ExitCode));

        return process.ExitCode;
    }
}
