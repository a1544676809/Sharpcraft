// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftCSharp.Auth;
using MinecraftCSharp.Common.Lifecycle;

namespace MinecraftCSharp.Launcher.Launch;

/// <summary>Everything the launcher hands to the client process.</summary>
/// <param name="VersionId">Version id.</param>
/// <param name="ClientPath">Executable or assembly to start.</param>
/// <param name="Arguments">Arguments, token-free.</param>
/// <param name="GameDirectory">Instance directory.</param>
/// <param name="PackPath">Base asset pack.</param>
/// <param name="AssetIndexId">Official asset index id.</param>
/// <param name="SessionFilePath">Session file holding the token.</param>
public sealed record LaunchPlan(
    string VersionId,
    string ClientPath,
    IReadOnlyList<string> Arguments,
    string GameDirectory,
    string PackPath,
    string? AssetIndexId,
    string SessionFilePath)
{
    /// <summary>Renders the plan without leaking secrets.</summary>
    public string Describe()
    {
        var lines = new List<string>
        {
            $"launch plan for {VersionId}",
            $"  client:      {ClientPath}",
            $"  game dir:    {GameDirectory}",
            $"  pack:        {PackPath}",
            $"  asset index: {AssetIndexId ?? "(none)"}",
            string.IsNullOrEmpty(SessionFilePath)
                ? "  session:     (none — offline launch, no token)"
                : $"  session:     {SessionFilePath} (token in file, not on the command line)",
            "  arguments:"
        };

        for (var i = 0; i < Arguments.Count; i++)
        {
            var argument = Arguments[i];

            // Only the token is a secret; paths and identifiers must stay readable for diagnosis.
            if (i > 0 && Arguments[i - 1] == SecretSwitch)
            {
                lines.Add("    " + Mask(argument));
            }
            else
            {
                lines.Add("    " + argument);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The one switch whose value must never be printed.</summary>
    public const string SecretSwitch = "--accessToken";

    /// <summary>Masks a secret value so plans can be printed safely.</summary>
    public static string Mask(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return secret.Length <= 8 ? "…(redacted)" : secret[..8] + "…(redacted)";
    }
}

/// <summary>Inputs the launch command reads before it can assemble a plan.</summary>
/// <param name="VersionId">Version id from the manifest.</param>
/// <param name="JarPath">Verified client jar.</param>
/// <param name="PackPath">Base asset pack.</param>
/// <param name="AssetsDirectory">Official assets root.</param>
/// <param name="AssetIndexId">Asset index id from the manifest.</param>
/// <param name="GameDirectory">Instance directory.</param>
/// <param name="Profile">Signed-in profile.</param>
/// <param name="AccessToken">Minecraft access token.</param>
public sealed record LaunchContext(
    string VersionId,
    string JarPath,
    string PackPath,
    string AssetsDirectory,
    string? AssetIndexId,
    string GameDirectory,
    MinecraftProfile Profile,
    string AccessToken);

/// <summary>Assembles the client command line. Pure so it can be tested without spawning.</summary>
public static class LaunchPlanBuilder
{
    /// <summary>Environment variable that overrides the client executable path.</summary>
    public const string ClientPathVariable = "MC_CLIENT_PATH";

    /// <summary>Default client file name next to the launcher.</summary>
    public const string DefaultClientName = "MinecraftCSharp.Client.dll";

    /// <summary>Builds the plan for a verified launch context.</summary>
    public static LaunchPlan Build(LaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sessionPath = Path.Combine(context.GameDirectory, SessionFile.FileName);

        var arguments = new List<string>
        {
            "--version", context.VersionId,
            "--gameDir", context.GameDirectory,
            "--assetsDir", context.AssetsDirectory,
            "--pack", context.PackPath,
            "--sessionFile", sessionPath,
            "--username", context.Profile.Name,
            "--uuid", context.Profile.Id
        };

        if (!string.IsNullOrWhiteSpace(context.AssetIndexId))
        {
            arguments.Add("--assetIndex");
            arguments.Add(context.AssetIndexId);
        }

        return new LaunchPlan(
            context.VersionId,
            ResolveClientPath(),
            arguments,
            context.GameDirectory,
            context.PackPath,
            context.AssetIndexId,
            sessionPath);
    }

    /// <summary>
    /// Finds the client next to the launcher. In a source checkout the launcher runs from
    /// <c>bin/&lt;config&gt;/&lt;tfm&gt;</c>, so the sibling build output is probed as well.
    /// </summary>
    public static string ResolveClientPath()
    {
        var configured = Environment.GetEnvironmentVariable(ClientPathVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var besideLauncher = Path.Combine(AppContext.BaseDirectory, DefaultClientName);
        if (File.Exists(besideLauncher))
        {
            return besideLauncher;
        }

        // Source checkout layout: src/MinecraftCSharp.Launcher/bin/<cfg>/<tfm>/ -> sibling project.
        var launcherOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var tfm = launcherOutput.Name;
        var configuration = launcherOutput.Parent?.Name;
        if (configuration is not null)
        {
            var probe = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "MinecraftCSharp.Client", "bin", configuration, tfm, DefaultClientName));

            if (File.Exists(probe))
            {
                return probe;
            }
        }

        return besideLauncher;
    }

    /// <summary>True when the resolved client is a dll that needs the dotnet host.</summary>
    public static bool NeedsDotnetHost(string clientPath) =>
        clientPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
}
