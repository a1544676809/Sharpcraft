// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Sharpcraft.Launcher.Assets;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// Version manifest parsing and jar verification (docs/modules/assets.md §7). Fixtures are built in
/// the test so no Minecraft artifact is ever committed.
/// </summary>
public sealed class AssetVerificationTests
{
    private const string ManifestJson = """
        {
          "id": "26.3",
          "assetIndex": { "id": "32", "sha1": "d6540f46a8fec49e45fb7312cdcf401a8e4e15ff", "totalSize": 480496538 },
          "downloads": { "client": { "sha1": "e877b6a07acd633fb3bb475002175cec036e7b87", "size": 27000000 } }
        }
        """;

    [Fact]
    public void ManifestExposesVersionIndexAndJarHash()
    {
        var manifest = VersionManifest.Parse(ManifestJson);

        Assert.Equal("26.3", manifest.Id);
        Assert.Equal("32", manifest.AssetIndexId);
        Assert.Equal("d6540f46a8fec49e45fb7312cdcf401a8e4e15ff", manifest.AssetIndexSha1);
        Assert.Equal(480496538, manifest.AssetIndexTotalSize);
        Assert.Equal("e877b6a07acd633fb3bb475002175cec036e7b87", manifest.ClientJarSha1);
        Assert.Equal(27_000_000, manifest.ClientJarSize);
    }

    [Fact]
    public void ManifestToleratesMissingSections()
    {
        var manifest = VersionManifest.Parse("""{ "id": "26.3" }""");

        Assert.Equal("26.3", manifest.Id);
        Assert.Null(manifest.AssetIndexId);
        Assert.Null(manifest.ClientJarSha1);
        Assert.Equal(0, manifest.ClientJarSize);
    }

    [Fact]
    public void ManifestWithoutIdIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => VersionManifest.Parse("""{ "assetIndex": { "id": "32" } }"""));
        Assert.Throws<InvalidDataException>(() => VersionManifest.Parse("not json at all"));
    }

    [Fact]
    public void ManifestFileThatDoesNotExistReportsThePath()
    {
        var path = Path.Combine(TempRoot(), "missing-version.json");

        var exception = Assert.Throws<FileNotFoundException>(() => VersionManifest.ReadFile(path));

        Assert.Contains("missing-version.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("versions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchingJarPassesVerification()
    {
        var root = TempRoot();
        var jar = WriteSyntheticJar(root, "client.jar", ["assets/minecraft/textures/block/stone.png"]);
        var expected = JarVerifier.ComputeSha1(jar);

        var actual = JarVerifier.Verify(jar, expected, new FileInfo(jar).Length);

        Assert.Equal(expected, actual);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void TamperedJarFailsWithBothHashes()
    {
        var root = TempRoot();
        var jar = WriteSyntheticJar(root, "client.jar", ["assets/minecraft/textures/block/stone.png"]);
        var expected = JarVerifier.ComputeSha1(jar);

        // Flip one byte: size stays the same, so only the hash can catch it.
        using (var stream = new FileStream(jar, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = 10;
            var value = stream.ReadByte();
            stream.Position = 10;
            stream.WriteByte((byte)(value ^ 0xFF));
        }

        var exception = Assert.Throws<JarVerificationException>(() => JarVerifier.Verify(jar, expected));

        Assert.Equal(expected, exception.ExpectedSha1);
        Assert.NotNull(exception.ActualSha1);
        Assert.NotEqual(expected, exception.ActualSha1);
        Assert.Contains("no override", exception.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void WrongSizeIsReportedBeforeHashing()
    {
        var root = TempRoot();
        var jar = WriteSyntheticJar(root, "client.jar", ["assets/minecraft/textures/block/stone.png"]);

        var exception = Assert.Throws<JarVerificationException>(
            () => JarVerifier.Verify(jar, "0000000000000000000000000000000000000000", expectedSize: 1));

        Assert.Contains("bytes but the manifest expects", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.ActualSha1);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void MissingJarReportsAnActionablePath()
    {
        var path = Path.Combine(TempRoot(), "absent.jar");

        var exception = Assert.Throws<JarVerificationException>(() => JarVerifier.Verify(path, "abc"));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never downloads", exception.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HashOfAStreamMatchesTheFileHash()
    {
        var root = TempRoot();
        var jar = WriteSyntheticJar(root, "client.jar", ["version.json"]);

        var fromFile = JarVerifier.ComputeSha1(jar);
        string fromStream;
        using (var stream = File.OpenRead(jar))
        {
            fromStream = JarVerifier.ComputeSha1(stream);
        }

        Assert.Equal(fromFile, fromStream);

        Directory.Delete(root, recursive: true);
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcs-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Builds a tiny jar-shaped zip with deterministic contents.</summary>
    private static string WriteSyntheticJar(string root, string name, string[] entries)
    {
        var path = Path.Combine(root, name);

        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var entry in entries)
        {
            var zipEntry = archive.CreateEntry(entry, CompressionLevel.Optimal);
            using var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8);
            writer.Write($"payload for {entry}");
        }

        return path;
    }
}
