// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Auth;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Launcher.Accounts;
using Sharpcraft.Launcher.Assets;
using Sharpcraft.Launcher.Launch;
using Sharpcraft.Launcher.Transport;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher;

/// <summary>
/// Launcher entry point. This build carries the authentication, probe and play slices
/// (docs/modules/launcher.md); version/asset handling comes later.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {

        

        // Real-time loops need finer sleep granularity than the default; on Windows that is about
        // 15.6 ms, which makes a 50 ms tick wait overshoot and the simulation run slow
        // (docs/specs/network/play-movement.md 7.5).
        using var timerResolution = Sharpcraft.Framework.Platform.TimerResolution.Elevate();

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
            LauncherCommand.Walk => await WalkAsync(commandLine.Target!).ConfigureAwait(false),
            LauncherCommand.Frame => await FrameAsync(commandLine.Target!).ConfigureAwait(false),
            LauncherCommand.Window => await WindowAsync(commandLine.Target).ConfigureAwait(false),
            LauncherCommand.Assets => Assets(commandLine.Target!),
            LauncherCommand.Launch => await LaunchAsync(commandLine.Target).ConfigureAwait(false),
            LauncherCommand.Accounts => AccountsCommand.Run(commandLine.Target),
            _ => 2
        };
    }

    private static int WriteUsage()
    {
        Console.WriteLine(LauncherCommandLine.Usage);
        return 0;
    }

    private static int DevMoved(string command)
    {
        Console.Error.WriteLine($"\x27{command}\x27 is a development/diagnostic command; run the standalone client instead:");
        Console.Error.WriteLine("  Sharpcraft.Client.exe --window|--frame|--walk --quickPlayMultiplayer <host:port> ... (docs/modules/client.md)");
        return 2;
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
            AccountsCommand.RecordPremiumSignIn(signedIn.Profile.Name, signedIn.Profile.Id);
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
            PrintExceptionChain(exception);
            return ReportAuthFailure(exception);
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"network failure: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Prints the exception chain when MC_TRANSPORT_DEBUG=1. Transport failures are classified by
    /// endpoint but the underlying socket or TLS reason is what makes them fixable.
    /// </summary>
    private static void PrintExceptionChain(Exception exception)
    {
        if (Environment.GetEnvironmentVariable("MC_TRANSPORT_DEBUG") is not "1")
        {
            return;
        }

        var depth = 0;
        for (var current = exception; current is not null && depth < 6; current = current.InnerException, depth++)
        {
            Console.Error.WriteLine($"  [debug] {current.GetType().Name}: {current.Message}");
        }
    }

    /// <summary>
    /// Signs in, joins play, and walks forward for a bounded time so the movement packets meet a real server.
    /// </summary>
    private static async Task<int> WalkAsync(string? target)
    {
        await Task.CompletedTask;
        return DevMoved("walk");
    }

    /// <summary>Signs in, then walks the login and configuration phases of a real server.</summary>
    private static async Task<int> PlayAsync(string? target)
    {
        await Task.CompletedTask;
        return DevMoved("play");
    }

    private static async Task<int> FrameAsync(string? target)
    {
        await Task.CompletedTask;
        return DevMoved("frame");
    }

    /// <summary>Runs the launch flow: verify, pack, sign in, start the client (docs/modules/launcher.md §5.1).</summary>
    private static async Task<int> LaunchAsync(string? target)
    {
        using var cancellation = CancelOnCtrlC();
        using var http = CreateHttpClient();

        var parts = (target ?? string.Empty).Split('|');
        if (parts.Length < 2)
        {
            Console.Error.WriteLine("launch needs <jar> <version-json>");
            return 2;
        }

        var offlineName = parts.Length >= 3 ? parts[2] : null;

        return await LaunchCommand
            .RunAsync(parts[0], parts[1], http, offlineName, cancellation.Token)
            .ConfigureAwait(false);
    }

    /// <summary>Builds or reuses the asset pack for a client jar (docs/modules/assets.md).</summary>
    private static int Assets(string target)
    {
        var parts = target.Split('|', 2);
        var jarPath = parts[0];
        VersionManifest? manifest = null;

        if (parts.Length == 2)
        {
            manifest = VersionManifest.ReadFile(parts[1]);
        }

        var versionId = manifest?.Id ?? versionIdFromJar(jarPath);
        var output = Environment.GetEnvironmentVariable("MC_ASSETS_OUT") is { Length: > 0 } configured
            ? configured
            : HostPaths.DefaultPacksDirectory();

        try
        {
            if (manifest is not null)
            {
                Console.WriteLine($"manifest: version {manifest.Id}, asset index {manifest.AssetIndexId}, " +
                                  $"client sha1 {manifest.ClientJarSha1}");
            }

            var result = AssetPackBuilder.Build(jarPath, output, versionId, manifest?.ClientJarSha1);

            Console.WriteLine(result.Describe());
            Console.WriteLine($"  jar sha1:  {result.JarSha1}");
            Console.WriteLine($"  pack path: {result.PackPath}");

            foreach (var (category, count) in result.ExcludedCounts.OrderByDescending(pair => pair.Value))
            {
                Console.WriteLine($"  skipped {category}: {count}");
            }

            VerifyResolution(result.PackPath, jarPath, manifest?.AssetIndexId);

            return 0;
        }
        catch (JarVerificationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 4;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"assets failed: {exception.Message}");
            return 1;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"assets failed: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Proves the resolution stack works against the real install: the base pack answers textures,
    /// the official index answers languages, and the built-in fallback answers anything missing.
    /// </summary>
    private static void VerifyResolution(string packPath, string jarPath, string? assetIndexId)
    {
        var assetsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(jarPath)!, "..", "..", "assets"));
        var probes = new List<(string Label, ResourceKind Kind, Identifier Id)>
        {
            ("texture block/stone", ResourceKind.Texture, new Identifier("minecraft", "block/stone")),
            ("texture block/debug", ResourceKind.Texture, new Identifier("minecraft", "block/debug")),
            ("model block/stone", ResourceKind.Model, new Identifier("minecraft", "block/stone")),
            ("lang en_us", ResourceKind.Language, new Identifier("minecraft", "en_us")),
            ("lang zh_cn", ResourceKind.Language, new Identifier("minecraft", "zh_cn"))
        };

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new BuiltInFallbackSource())
            .Add(new ZipAssetSource(packPath, "base-pack"));

        if (assetIndexId is not null)
        {
            var indexPath = Path.Combine(assetsDirectory, "indexes", assetIndexId + ".json");
            if (File.Exists(indexPath))
            {
                stack.Add(IndexedAssetSource.Open(indexPath, assetIndexId));
            }
            else
            {
                Console.WriteLine($"[assets] official index not found, skipping mount: {indexPath}");
            }
        }

        Console.WriteLine("resolution:");

        foreach (var (label, kind, id) in probes)
        {
            var present = stack.TryOpen(kind, id, out var stream, out var sourceId);
            if (present)
            {
                using (stream)
                {
                    Console.WriteLine($"  {label,-22} {stream.Length,9} bytes  <- {sourceId}");
                }
            }
            else
            {
                Console.WriteLine($"  {label,-22} {'-',9}        <- missing");
            }
        }

        var meta = stack.ReadMetadata(ResourceKind.Texture, new Identifier("minecraft", "block/water_still"));
        Console.WriteLine($"  {"meta water_still",-22} {meta?.Length ?? 0,9} bytes");

        // Prove the decode path on real textures, not just synthetic fixtures.
        foreach (var probe in new[] { "block/stone", "block/water_still", "block/grass_block_top" })
        {
            var bytes = stack.Read(ResourceKind.Texture, new Identifier("minecraft", probe));
            if (bytes is null)
            {
                continue;
            }

            try
            {
                var image = PngDecoder.Decode(bytes);
                var opaque = 0;
                var transparent = 0;
                for (var i = 3; i < image.Pixels.Length; i += RgbaImage.BytesPerPixel)
                {
                    if (image.Pixels[i] == 0)
                    {
                        transparent++;
                    }
                    else
                    {
                        opaque++;
                    }
                }

                Console.WriteLine(
                    $"  decode {probe,-22} {image.Width}x{image.Height}  " +
                    $"opaque {opaque} / transparent {transparent}");
            }
            catch (InvalidDataException exception)
            {
                Console.WriteLine($"  decode {probe,-22} failed: {exception.Message}");
            }
        }

        var textures = stack.ListAll(ResourcePath.Prefix(ResourceKind.Texture)).Count;
        var languages = stack.ListAll(ResourcePath.Prefix(ResourceKind.Language)).Count;
        Console.WriteLine($"  listed textures        {textures,9}");
        Console.WriteLine($"  listed languages       {languages,9}");
    }

    private static string versionIdFromJar(string jarPath) =>
        Path.GetFileNameWithoutExtension(jarPath) is { Length: > 0 } name ? name : "unknown";

    /// <summary>Window width the host asks for, so a measurement can use a realistic size.</summary>
    private static int WindowWidth() =>
        int.TryParse(Environment.GetEnvironmentVariable("MC_WINDOW_WIDTH"), out var width) && width > 0 ? width : 960;

    /// <summary>Window height the host asks for.</summary>
    private static int WindowHeight() =>
        int.TryParse(Environment.GetEnvironmentVariable("MC_WINDOW_HEIGHT"), out var height) && height > 0 ? height : 540;

    /// <summary>True when the caller supplied a local scene through the environment, so no server is needed.</summary>
    private static bool IsLocalScene() =>
        Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 } ||
        Environment.GetEnvironmentVariable("MC_FRAME_WATER") == "1" ||
        Environment.GetEnvironmentVariable("MC_FRAME_REPLAY") is { Length: > 0 };

    /// <summary>
    /// Builds the identity the game library runs as for a dev/diagnostic command. A local scene needs no sign-in;
    /// a real server uses the launcher's normal authentication (this is host plumbing, not the client's job).
    /// </summary>

    private static async Task<int> WindowAsync(string? target)
    {
        await Task.CompletedTask;
        return DevMoved("window");
    }

    private static async Task<int> ProbeAsync(string address)
    {
        using var cancellation = CancelOnCtrlC();
        using var http = CreateHttpClient();

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

    private static Func<int, (int SectionCount, int MinSectionY)> ResolveDimensionLayout(DynamicRegistries registries)
    {
        var registryKey = Identifier.Parse("minecraft:dimension_type");

        return dimensionTypeId =>
        {
            var entry = registries.ById(registryKey, dimensionTypeId);
            var info = DimensionTypeInfo.TryRead(entry?.Data);
            if (info is null)
            {
                Console.WriteLine($"  dimension type {dimensionTypeId} unknown; assuming 384 blocks at -64");
                return (24, -4);
            }

            Console.WriteLine(
                $"  dimension type {dimensionTypeId} -> minY={info.MinY} height={info.Height} " +
                $"sections={info.SectionCount}");
            return (info.SectionCount, info.MinSectionY);
        };
    }

    private static int PlayHoldSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("MC_PLAY_SECONDS");
        return int.TryParse(raw, out var seconds) && seconds > 0 && seconds <= 600 ? seconds : 10;
    }

    private static HttpClient CreateHttpClient() => HostTransport.Create();

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
        DeviceCodePrompter.Show(challenge);
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
