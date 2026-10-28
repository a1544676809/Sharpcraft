// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Sharpcraft.Client.Assets;

namespace Sharpcraft.Launcher.Assets;

/// <summary>Result of building a pack.</summary>
/// <param name="PackPath">Path of the published pack.</param>
/// <param name="VersionId">Version id used in the name.</param>
/// <param name="JarSha1">Source jar hash.</param>
/// <param name="PackSha256">Hash of the pack file itself.</param>
/// <param name="EntryCount">Payload entries written.</param>
/// <param name="PackSize">Pack size in bytes.</param>
/// <param name="Reused">True when an existing pack for the same jar was reused.</param>
/// <param name="ExcludedCounts">Entries skipped, grouped by top-level directory.</param>
/// <param name="Elapsed">Wall-clock build time.</param>
public sealed record AssetPackResult(
    string PackPath,
    string VersionId,
    string JarSha1,
    string PackSha256,
    int EntryCount,
    long PackSize,
    bool Reused,
    IReadOnlyDictionary<string, int> ExcludedCounts,
    TimeSpan Elapsed)
{
    /// <summary>Human-readable one-line summary.</summary>
    public string Describe() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}pack {1}: {2} entries, {3:F1} MB, sha256 {4}{5}",
        Reused ? "reused existing " : string.Empty,
        Path.GetFileName(PackPath),
        EntryCount,
        PackSize / 1024.0 / 1024.0,
        AssetPackBuilder.Shorten(PackSha256),
        Elapsed > TimeSpan.Zero ? $" ({Elapsed.TotalMilliseconds:F0} ms)" : string.Empty);
}

/// <summary>
/// Builds a single-file asset pack from a client jar (ADR-0005). Output is one zip containing a
/// manifest plus the whitelisted entries at their original paths, published atomically and named
/// after its own hash.
/// </summary>
public static class AssetPackBuilder
{
    /// <summary>Fixed timestamp so the same jar always produces byte-identical packs.</summary>
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds (or reuses) a pack for <paramref name="jarPath"/>.
    /// </summary>
    /// <param name="jarPath">Client jar to read.</param>
    /// <param name="outputDirectory">Directory that receives the pack; must be outside the repository.</param>
    /// <param name="versionId">Version id used in the pack file name.</param>
    /// <param name="expectedJarSha1">When supplied, the jar is verified against this hash first.</param>
    /// <exception cref="JarVerificationException">The jar does not match the expected hash.</exception>
    /// <exception cref="InvalidOperationException">The output directory is inside the repository.</exception>
    public static AssetPackResult Build(
        string jarPath,
        string outputDirectory,
        string versionId,
        string? expectedJarSha1 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);

        var started = System.Diagnostics.Stopwatch.StartNew();

        // The guard runs before anything is created: a refused output must leave no trace.
        if (HostPaths.IsInsideRepository(outputDirectory))
        {
            throw new InvalidOperationException(
                $"refusing to write assets inside the repository: {outputDirectory}. " +
                "Point the output at a user data directory instead (AGENTS.md principle 3).");
        }

        Directory.CreateDirectory(outputDirectory);

        var jarSha1 = expectedJarSha1 is null
            ? JarVerifier.ComputeSha1(jarPath)
            : JarVerifier.Verify(jarPath, expectedJarSha1);

        if (TryReuse(outputDirectory, versionId, jarSha1, started, out var reused))
        {
            return reused;
        }

        var excluded = new Dictionary<string, int>(StringComparer.Ordinal);
        var entries = new List<AssetPackEntry>();
        var temporary = Path.Combine(outputDirectory, $".{versionId}-{Guid.NewGuid():N}.pack.tmp");

        try
        {
            using (var source = ZipFile.OpenRead(jarPath))
            {
                using var targetStream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var target = new ZipArchive(targetStream, ZipArchiveMode.Create);

                // The manifest is written last, so reserve it by building the payload first into
                // memory-free streaming order: entries, then manifest.
                foreach (var entry in source.Entries)
                {
                    if (!AssetSelection.IsIncluded(entry.FullName))
                    {
                        var category = AssetSelection.ExcludedCategory(entry.FullName);
                        if (category is not null)
                        {
                            excluded[category] = excluded.GetValueOrDefault(category) + 1;
                        }

                        continue;
                    }

                    var payload = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    payload.LastWriteTime = DeterministicTimestamp;

                    using var input = entry.Open();
                    using var output = payload.Open();
                    var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var length = CopyHashing(input, output, hash);

                    entries.Add(new AssetPackEntry(
                        entry.FullName,
                        length,
                        Convert.ToHexStringLower(hash.GetHashAndReset())));
                }

                var manifest = new AssetPackManifest(
                    AssetPackManifest.CurrentFormat,
                    versionId,
                    jarSha1,
                    DateTimeOffset.UnixEpoch,
                    entries);

                var manifestEntry = target.CreateEntry("manifest.json", CompressionLevel.Optimal);
                manifestEntry.LastWriteTime = DeterministicTimestamp;
                using (var manifestStream = manifestEntry.Open())
                {
                    manifestStream.Write(manifest.ToJson());
                }
            }

            var packSha256 = JarVerifier.ComputeSha256OfFile(temporary);
            var finalName = $"minecraft-{versionId}-{Shorten(packSha256)}.pack.zip";
            var finalPath = Path.Combine(outputDirectory, finalName);

            File.Move(temporary, finalPath, overwrite: true);
            started.Stop();

            return new AssetPackResult(
                finalPath,
                versionId,
                jarSha1,
                packSha256,
                entries.Count,
                new FileInfo(finalPath).Length,
                Reused: false,
                excluded,
                started.Elapsed);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>Reads the manifest out of an existing pack.</summary>
    public static AssetPackManifest ReadManifest(string packPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packPath);

        using var archive = ZipFile.OpenRead(packPath);
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException($"pack has no manifest.json: {packPath}");

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return AssetPackManifest.ReadJson(buffer.ToArray());
    }

    /// <summary>Shortens a hex hash for file names and messages.</summary>
    internal static string Shorten(string hash) =>
        hash.Length <= 12 ? hash : hash[..12];

    private static bool TryReuse(
        string outputDirectory,
        string versionId,
        string jarSha1,
        System.Diagnostics.Stopwatch started,
        out AssetPackResult result)
    {
        result = null!;

        foreach (var candidate in Directory.EnumerateFiles(outputDirectory, $"minecraft-{versionId}-*.pack.zip"))
        {
            AssetPackManifest manifest;
            try
            {
                manifest = ReadManifest(candidate);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                // A damaged pack must not block a rebuild.
                continue;
            }

            if (!string.Equals(manifest.JarSha1, jarSha1, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash = JarVerifier.ComputeSha256OfFile(candidate);
            started.Stop();
            result = new AssetPackResult(
                candidate,
                versionId,
                jarSha1,
                hash,
                manifest.Entries.Count,
                new FileInfo(candidate).Length,
                Reused: true,
                new Dictionary<string, int>(),
                started.Elapsed);
            return true;
        }

        return false;
    }

    private static long CopyHashing(Stream input, Stream output, IncrementalHash hash)
    {
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024);
        long total = 0;

        try
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                output.Write(buffer, 0, read);
                total += read;
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        return total;
    }
}
