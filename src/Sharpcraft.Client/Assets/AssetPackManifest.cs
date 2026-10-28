// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpcraft.Client.Assets;

/// <summary>One payload entry recorded in the pack manifest.</summary>
/// <param name="Name">Path inside the pack, identical to the jar entry name.</param>
/// <param name="Size">Uncompressed size in bytes.</param>
/// <param name="Sha256">SHA-256 of the uncompressed bytes.</param>
public sealed record AssetPackEntry(string Name, long Size, string Sha256);

/// <summary>
/// Manifest stored inside the pack: what went in, where it came from, and the hashes needed to
/// verify it later (docs/modules/assets.md §5).
/// </summary>
/// <param name="Format">Manifest format marker.</param>
/// <param name="VersionId">Version id the pack was built from.</param>
/// <param name="JarSha1">SHA-1 of the source jar.</param>
/// <param name="BuiltAtUtc">Build timestamp.</param>
/// <param name="Entries">Payload entries.</param>
public sealed record AssetPackManifest(
    string Format,
    string VersionId,
    string JarSha1,
    DateTimeOffset BuiltAtUtc,
    IReadOnlyList<AssetPackEntry> Entries)
{
    /// <summary>Current manifest format marker.</summary>
    public const string CurrentFormat = "mcs-asset-pack/1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Serializes the manifest to UTF-8 JSON.</summary>
    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions);

    /// <summary>Reads a manifest back from JSON.</summary>
    /// <exception cref="InvalidDataException">The payload is not a manifest this build understands.</exception>
    public static AssetPackManifest ReadJson(ReadOnlySpan<byte> json)
    {
        AssetPackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AssetPackManifest>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"pack manifest is not valid JSON: {exception.Message}", exception);
        }

        if (manifest is null || manifest.Format != CurrentFormat)
        {
            throw new InvalidDataException(
                $"pack manifest format '{manifest?.Format ?? "(null)"}' is not '{CurrentFormat}'");
        }

        return manifest;
    }
}

