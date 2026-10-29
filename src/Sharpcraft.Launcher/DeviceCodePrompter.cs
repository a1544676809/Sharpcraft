// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using Sharpcraft.Auth;

namespace Sharpcraft.Launcher;

/// <summary>
/// Presents a device-code challenge: prints the code and, unless disabled, opens the verification page
/// in the user's browser with the code pre-filled. Printing alone is not enough — a code nobody notices
/// expires in about fifteen minutes and the sign-in has to start over.
/// </summary>
/// <remarks>
/// The browser is opened through the platform opener (Windows <c>msedge</c> when present, otherwise the
/// shell default; macOS <c>open</c>; Linux <c>xdg-open</c>). Failing to open it is not an error: the
/// printed URL and code remain the fallback, so a headless or locked-down machine still works.
/// </remarks>
internal static class DeviceCodePrompter
{
    /// <summary>Environment variable that suppresses opening a browser.</summary>
    public const string DisableVariable = "SHARPCRAFT_NO_BROWSER";

    /// <summary>Builds the verification URL with the code pre-filled when the page accepts it.</summary>
    public static string VerificationUrl(DeviceCodeChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var baseUrl = challenge.VerificationUri;

        // Microsoft's device sign-in page accepts the code as a query parameter; when it does not, the
        // page still loads and the user types the code shown here.
        return baseUrl.Contains('?', StringComparison.Ordinal)
            ? baseUrl + "&otc=" + Uri.EscapeDataString(challenge.UserCode)
            : baseUrl + "?otc=" + Uri.EscapeDataString(challenge.UserCode);
    }

    /// <summary>Browser executables probed on Windows, in order, before the shell default.</summary>
    private static readonly string[] WindowsBrowserCandidates =
    [
        "msedge.exe",
        "chrome.exe",
        "firefox.exe"
    ];

    /// <summary>Resolves a browser executable that actually exists on Windows, or null.</summary>
    /// <param name="candidates">Executables to probe.</param>
    /// <param name="exists">Existence predicate, injected so tests need no real installation.</param>
    public static string? FindWindowsBrowser(
        IEnumerable<string> candidates,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(exists);

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var candidate in candidates)
        {
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                var path = System.IO.Path.Combine(root, "Microsoft", "Edge", "Application", candidate);
                if (exists(path))
                {
                    return path;
                }

                path = System.IO.Path.Combine(root, "Google", "Chrome", "Application", candidate);
                if (exists(path))
                {
                    return path;
                }

                path = System.IO.Path.Combine(root, "Mozilla Firefox", candidate);
                if (exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    /// <summary>Chooses the opener command for a platform.</summary>
    /// <param name="url">URL to open.</param>
    /// <param name="platform">Platform name, as reported by the runtime.</param>
    /// <param name="windowsBrowser">Browser executable to prefer on Windows, or null for the shell default.</param>
    public static (string FileName, string[] Arguments) OpenerFor(string url, OSPlatformKind platform, string? windowsBrowser) =>
        platform switch
        {
            OSPlatformKind.Windows when windowsBrowser is { Length: > 0 } => (windowsBrowser, [url]),
            OSPlatformKind.Windows => ("cmd.exe", ["/c", "start", string.Empty, url]),
            OSPlatformKind.MacOS => ("open", [url]),
            _ => ("xdg-open", [url])
        };

    /// <summary>Prints the challenge and tries to open the verification page.</summary>
    public static void Show(DeviceCodeChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var url = VerificationUrl(challenge);

        Console.WriteLine($"Open {url} and enter the code: {challenge.UserCode}");
        Console.WriteLine($"  code: {challenge.UserCode}   (valid for about {challenge.ExpiresIn.TotalMinutes:0} minutes)");

        if (Environment.GetEnvironmentVariable(DisableVariable) is { Length: > 0 })
        {
            return;
        }

        var platform = CurrentPlatform();
        var configured = Environment.GetEnvironmentVariable("SHARPCRAFT_BROWSER");

        // An explicit setting wins; otherwise probe the usual install locations, then the shell.
        var candidates = new List<string>();
        if (configured is { Length: > 0 })
        {
            candidates.Add(configured);
        }
        else if (platform == OSPlatformKind.Windows)
        {
            if (FindWindowsBrowser(WindowsBrowserCandidates, File.Exists) is { } installed)
            {
                candidates.Add(installed);
            }
        }

        if (platform == OSPlatformKind.Windows)
        {
            candidates.Add("cmd.exe");
        }

        if (candidates.Count == 0)
        {
            candidates.Add(OpenerFor(url, platform, null).FileName);
        }

        foreach (var candidate in candidates)
        {
            var browser = candidate == "cmd.exe" ? null : candidate;
            var (fileName, arguments) = OpenerFor(url, platform, browser);

            try
            {
                var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo);
                if (process is not null)
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Try the next candidate; the printed URL stays the fallback.
                _ = exception;
            }
        }

        Console.WriteLine($"  (could not open a browser; please open {url} manually)");
    }

    /// <summary>Platform the process is running on, in the shape the opener selection needs.</summary>
    public static OSPlatformKind CurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return OSPlatformKind.Windows;
        }

        return OperatingSystem.IsMacOS() ? OSPlatformKind.MacOS : OSPlatformKind.Linux;
    }

    /// <summary>Platforms with a distinct opener.</summary>
    internal enum OSPlatformKind
    {
        Windows,
        MacOS,
        Linux
    }
}
