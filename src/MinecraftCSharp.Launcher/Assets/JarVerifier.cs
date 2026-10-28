// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Globalization;
using System.Security.Cryptography;

namespace MinecraftCSharp.Launcher.Assets;

/// <summary>
/// Verifies that a client jar is the one the version manifest expects
/// (docs/modules/assets.md §6). A mismatch is always fatal: the pipeline must never extract from a
/// jar of unknown provenance (AGENTS.md principle 3).
/// </summary>
public static class JarVerifier
{
    /// <summary>Computes the SHA-1 of a file without loading it into memory.</summary>
    public static string ComputeSha1(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return ComputeSha1(stream);
    }

    /// <summary>Computes the SHA-1 of a stream from its current position.</summary>
    public static string ComputeSha1(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var hash = SHA1.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Verifies a jar against the expected SHA-1. Returns the computed hash on success and throws
    /// <see cref="JarVerificationException"/> with both hashes on mismatch.
    /// </summary>
    /// <param name="path">Path to the client jar.</param>
    /// <param name="expectedSha1">SHA-1 from the version manifest.</param>
    /// <param name="expectedSize">Expected size in bytes, or 0 to skip the size check.</param>
    public static string Verify(string path, string expectedSha1, long expectedSize = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha1);

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new JarVerificationException(
                $"client jar not found: {path}",
                expectedSha1,
                actual: null,
                hint: "Pass the client jar that matches the version manifest; the launcher never downloads or " +
                      "substitutes one on your behalf.");
        }

        if (expectedSize > 0 && info.Length != expectedSize)
        {
            throw new JarVerificationException(
                $"client jar has {info.Length} bytes but the manifest expects {expectedSize}",
                expectedSha1,
                actual: null,
                hint: "The file is incomplete or belongs to a different version.");
        }

        var actual = ComputeSha1(path);
        if (!string.Equals(actual, expectedSha1, StringComparison.OrdinalIgnoreCase))
        {
            throw new JarVerificationException(
                "client jar does not match the version manifest",
                expectedSha1,
                actual,
                hint: "Use the jar of the target baseline version. There is deliberately no override: extracting " +
                      "assets from an unknown jar would violate the project's asset policy.");
        }

        return actual;
    }

    /// <summary>Streaming SHA-256 of a file, used for pack payload verification.</summary>
    public static string ComputeSha256OfFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Formats a hash for messages, keeping the first eight characters readable.</summary>
    internal static string Shorten(string sha1) =>
        sha1.Length <= 12 ? sha1 : string.Concat(sha1.AsSpan(0, 12), "…");
}

/// <summary>Raised when a jar cannot be trusted as the expected baseline artifact.</summary>
public sealed class JarVerificationException : Exception
{
    /// <summary>Creates the exception with both hashes and an actionable hint.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="expectedSha1">Hash the manifest asked for.</param>
    /// <param name="actual">Hash that was found, or null when the file was unusable.</param>
    /// <param name="hint">What the user should do next.</param>
    public JarVerificationException(string message, string expectedSha1, string? actual, string hint)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0}\n  expected sha1: {1}\n  actual sha1:   {2}\n  hint: {3}",
            message,
            expectedSha1,
            actual ?? "(not computed)",
            hint))
    {
        ExpectedSha1 = expectedSha1;
        ActualSha1 = actual;
        Hint = hint;
    }

    /// <summary>Hash the manifest expects.</summary>
    public string ExpectedSha1 { get; }

    /// <summary>Hash that was found, or null.</summary>
    public string? ActualSha1 { get; }

    /// <summary>Suggested next step for the user.</summary>
    public string Hint { get; }
}
