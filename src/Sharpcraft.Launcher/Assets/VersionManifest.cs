// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpcraft.Launcher.Assets;

/// <summary>
/// The parts of a Minecraft version manifest the asset pipeline needs: which version, which asset
/// index it uses, and the expected hash of the client jar
/// (docs/modules/assets.md, docs/decisions/0005-asset-pack-and-official-index.md).
/// </summary>
/// <param name="Id">Version id, for example <c>26.3</c>.</param>
/// <param name="AssetIndexId">Asset index id from <c>assetIndex.id</c>.</param>
/// <param name="AssetIndexSha1">Expected SHA-1 of the downloaded index.</param>
/// <param name="AssetIndexTotalSize">Index total size in bytes, or 0 when absent.</param>
/// <param name="ClientJarSha1">Expected SHA-1 of the client jar.</param>
/// <param name="ClientJarSize">Client jar size in bytes, or 0 when absent.</param>
public sealed record VersionManifest(
    string Id,
    string? AssetIndexId,
    string? AssetIndexSha1,
    long AssetIndexTotalSize,
    string? ClientJarSha1,
    long ClientJarSize)
{
    /// <summary>Reads a version manifest from a file.</summary>
    /// <exception cref="FileNotFoundException">The manifest does not exist.</exception>
    /// <exception cref="InvalidDataException">The manifest is not valid JSON or lacks an id.</exception>
    public static VersionManifest ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"version manifest not found: {path}. Point the launcher at a version JSON such as " +
                "<.minecraft>/versions/<version>/<version>.json",
                path);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>Parses a version manifest from JSON text.</summary>
    /// <exception cref="InvalidDataException">The JSON is malformed or lacks an id.</exception>
    public static VersionManifest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        ManifestDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ManifestDocument>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"version manifest is not valid JSON: {exception.Message}", exception);
        }

        if (document is null || string.IsNullOrWhiteSpace(document.Id))
        {
            throw new InvalidDataException("version manifest has no id");
        }

        return new VersionManifest(
            document.Id,
            document.AssetIndex?.Id,
            document.AssetIndex?.Sha1,
            document.AssetIndex?.TotalSize ?? 0,
            document.Downloads?.Client?.Sha1,
            document.Downloads?.Client?.Size ?? 0);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class ManifestDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("assetIndex")]
        public AssetIndexDocument? AssetIndex { get; init; }

        [JsonPropertyName("downloads")]
        public DownloadsDocument? Downloads { get; init; }
    }

    private sealed class AssetIndexDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("sha1")]
        public string? Sha1 { get; init; }

        [JsonPropertyName("totalSize")]
        public long TotalSize { get; init; }
    }

    private sealed class DownloadsDocument
    {
        [JsonPropertyName("client")]
        public ArtifactDocument? Client { get; init; }
    }

    private sealed class ArtifactDocument
    {
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
