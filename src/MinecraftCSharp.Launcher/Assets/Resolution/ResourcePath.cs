// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftCSharp.Common.Identifiers;

namespace MinecraftCSharp.Launcher.Assets.Resolution;

/// <summary>The resource families this client loads.</summary>
public enum ResourceKind
{
    /// <summary><c>blockstates/&lt;path&gt;.json</c>.</summary>
    BlockState,

    /// <summary><c>models/&lt;path&gt;.json</c>.</summary>
    Model,

    /// <summary><c>items/&lt;path&gt;.json</c>.</summary>
    Item,

    /// <summary><c>textures/&lt;path&gt;.png</c>.</summary>
    Texture,

    /// <summary><c>font/&lt;path&gt;.json</c>.</summary>
    Font,

    /// <summary><c>particles/&lt;path&gt;.json</c>.</summary>
    Particle,

    /// <summary><c>lang/&lt;locale&gt;.json</c>.</summary>
    Language
}

/// <summary>
/// Turns a resource location into the file path a pack stores it under
/// (docs/analysis/assets/resource-resolution.md §2). This is the only place that rule lives.
/// </summary>
public static class ResourcePath
{
    /// <summary>Client resources live under this root; server data would use <c>data</c>.</summary>
    public const string ClientRoot = "assets";

    /// <summary>Directory for a resource kind.</summary>
    public static string Directory(ResourceKind kind) => kind switch
    {
        ResourceKind.BlockState => "blockstates",
        ResourceKind.Model => "models",
        ResourceKind.Item => "items",
        ResourceKind.Texture => "textures",
        ResourceKind.Font => "font",
        ResourceKind.Particle => "particles",
        ResourceKind.Language => "lang",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown resource kind")
    };

    /// <summary>Extension for a resource kind.</summary>
    public static string Extension(ResourceKind kind) => kind switch
    {
        ResourceKind.Texture => ".png",
        _ => ".json"
    };

    /// <summary>The path a resource is stored under, for example <c>assets/minecraft/textures/block/stone.png</c>.</summary>
    public static string FilePath(ResourceKind kind, Identifier id)
    {
        if (string.IsNullOrEmpty(id.Namespace) || string.IsNullOrEmpty(id.Path))
        {
            throw new ArgumentException("resource location must have a namespace and a path", nameof(id));
        }

        return string.Concat(
            ClientRoot, "/", id.Namespace, "/", Directory(kind), "/", id.Path, Extension(kind));
    }

    /// <summary>Path prefix used to list a whole resource kind in a namespace.</summary>
    public static string Prefix(ResourceKind kind, string @namespace = "minecraft") =>
        string.Concat(ClientRoot, "/", @namespace, "/", Directory(kind), "/");

    /// <summary>Path of the <c>.mcmeta</c> sibling of a stored resource.</summary>
    public static string MetadataPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return filePath + ".mcmeta";
    }

    /// <summary>
    /// Parses a stored path back into a location, or returns false when the path does not belong to
    /// the requested kind.
    /// </summary>
    public static bool TryParse(ResourceKind kind, string filePath, out Identifier location)
    {
        location = default;

        var marker = "/" + Directory(kind) + "/";
        var index = filePath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || !filePath.StartsWith(ClientRoot + "/", StringComparison.Ordinal))
        {
            return false;
        }

        var @namespace = filePath[(ClientRoot.Length + 1)..index];
        var extension = Extension(kind);
        var path = filePath[(index + marker.Length)..];

        if (!path.EndsWith(extension, StringComparison.Ordinal))
        {
            return false;
        }

        path = path[..^extension.Length];
        return Identifier.TryParse(@namespace + ":" + path, out location);
    }
}

/// <summary>
/// Parses listed file paths back into resource locations. The prefix carries the kind implicitly
/// (<c>assets/&lt;namespace&gt;/&lt;directory&gt;/</c>), so listing code does not need the caller to
/// repeat it.
/// </summary>
public static class ResourceLookup
{
    private static readonly ResourceKind[] AllKinds =
    [
        ResourceKind.Texture,
        ResourceKind.BlockState,
        ResourceKind.Model,
        ResourceKind.Item,
        ResourceKind.Font,
        ResourceKind.Particle,
        ResourceKind.Language
    ];

    /// <summary>
    /// Turns <paramref name="filePath"/> into a location when it sits under
    /// <paramref name="prefix"/> and the prefix names a known resource directory.
    /// </summary>
    public static bool TryParseUnderPrefix(string prefix, string filePath, out Identifier location)
    {
        location = default;

        if (!filePath.StartsWith(prefix, StringComparison.Ordinal) || filePath.EndsWith(".mcmeta", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3 || segments[0] != ResourcePath.ClientRoot)
        {
            return false;
        }

        var @namespace = segments[1];
        var directory = segments[2];

        ResourceKind? kind = null;
        foreach (var candidate in AllKinds)
        {
            if (ResourcePath.Directory(candidate) == directory)
            {
                kind = candidate;
                break;
            }
        }

        if (kind is null)
        {
            return false;
        }

        var relative = filePath[prefix.Length..];
        var extension = ResourcePath.Extension(kind.Value);
        if (!relative.EndsWith(extension, StringComparison.Ordinal))
        {
            return false;
        }

        var path = relative[..^extension.Length];
        return path.Length > 0 && Identifier.TryParse(@namespace + ":" + path, out location);
    }
}

/// <summary>
/// The parts of a resource pack's <c>pack.mcmeta</c> we read today. Version compatibility is
/// deliberately not enforced yet (risk R-016).
/// </summary>
/// <param name="PackFormat">Declared pack format.</param>
/// <param name="Description">Human-readable description, when present.</param>
public sealed record PackMetadata(int PackFormat, string? Description)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Parses a <c>pack.mcmeta</c> payload.</summary>
    /// <exception cref="InvalidDataException">The payload is not a usable pack metadata document.</exception>
    public static PackMetadata Parse(ReadOnlySpan<byte> json)
    {
        MetadataDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<MetadataDocument>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"pack.mcmeta is not valid JSON: {exception.Message}", exception);
        }

        if (document?.Pack is null)
        {
            throw new InvalidDataException("pack.mcmeta has no 'pack' section");
        }

        return new PackMetadata(document.Pack.PackFormat, document.Pack.Description);
    }

    private sealed class MetadataDocument
    {
        [JsonPropertyName("pack")]
        public PackDocument? Pack { get; init; }
    }

    private sealed class PackDocument
    {
        [JsonPropertyName("pack_format")]
        public int PackFormat { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }
}
