// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Text.Json;
using MinecraftCSharp.Common.Identifiers;

namespace MinecraftCSharp.Client.Assets;

/// <summary>Texture paths for the three face buckets the mesher draws.</summary>
/// <param name="Top">Texture of the +Y face.</param>
/// <param name="Side">Texture of the four vertical faces.</param>
/// <param name="Bottom">Texture of the -Y face.</param>
public sealed record BlockFaceTextures(Identifier Top, Identifier Side, Identifier Bottom);

/// <summary>
/// Resolves a block's face textures the way vanilla does: read its <c>blockstates</c> file, take the
/// first variant's model, walk the <c>parent</c> chain while merging <c>textures</c>, then resolve
/// <c>#variable</c> references down to texture paths
/// (docs/analysis/assets/block-models.md §1–§4).
/// </summary>
/// <remarks>
/// Only the texture mapping is consumed, not element geometry: the mesher still draws whole cubes and
/// tiles the texture inside each quad. Multipart conditions, property-driven variants, overlays and
/// tint indices are out of scope and documented as such.
/// </remarks>
public sealed class BlockModelResolver
{
    private readonly AssetStack _stack;
    private readonly Dictionary<Identifier, BlockFaceTextures?> _cache = new();

    /// <summary>Creates a resolver over a resource stack.</summary>
    /// <param name="stack">Stack used to read <c>blockstates</c> and <c>models</c>.</param>
    public BlockModelResolver(AssetStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _stack = stack;
    }

    /// <summary>Number of blocks resolved so far, for diagnostics.</summary>
    public int ResolvedCount => _cache.Count;

    /// <summary>Number of blocks whose model chain produced no textures.</summary>
    public int UnresolvedCount { get; private set; }

    /// <summary>Resolves the face textures of a block, or null when its models say nothing usable.</summary>
    public BlockFaceTextures? Resolve(Identifier blockId)
    {
        if (_cache.TryGetValue(blockId, out var cached))
        {
            return cached;
        }

        var resolved = ResolveCore(blockId);
        if (resolved is null)
        {
            UnresolvedCount++;
        }

        _cache[blockId] = resolved;
        return resolved;
    }

    private BlockFaceTextures? ResolveCore(Identifier blockId)
    {
        if (FirstVariantModel(blockId) is not { } modelId)
        {
            return null;
        }

        var textures = ResolveModelTextures(modelId);
        if (textures is null || textures.Count == 0)
        {
            return null;
        }

        // cube.json style face variables; cube_all and cube_column map theirs onto these.
        var up = Lookup(textures, "up");
        var down = Lookup(textures, "down");
        var north = Lookup(textures, "north");

        var top = Lookup(textures, "top") ?? up ?? Lookup(textures, "end");
        var bottom = Lookup(textures, "bottom") ?? down ?? Lookup(textures, "end");
        var side = Lookup(textures, "side")
                   ?? north
                   ?? Lookup(textures, "all")
                   ?? top;

        if (top is null && side is null && bottom is null)
        {
            return null;
        }

        var topTexture = top ?? side ?? bottom;
        var sideTexture = side ?? top ?? bottom;
        var bottomTexture = bottom ?? side ?? top;

        return topTexture is null || sideTexture is null || bottomTexture is null
            ? null
            : new BlockFaceTextures(topTexture.Value, sideTexture.Value, bottomTexture.Value);
    }

    private Identifier? FirstVariantModel(Identifier blockId)
    {
        var bytes = _stack.Read(ResourceKind.BlockState, blockId);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);

            if (document.RootElement.TryGetProperty("variants", out var variants) &&
                variants.ValueKind == JsonValueKind.Object)
            {
                foreach (var variant in variants.EnumerateObject())
                {
                    var model = FirstModelReference(variant.Value);
                    if (model is not null)
                    {
                        return model;
                    }
                }
            }

            if (document.RootElement.TryGetProperty("multipart", out var multipart) &&
                multipart.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in multipart.EnumerateArray())
                {
                    if (part.TryGetProperty("apply", out var apply) &&
                        FirstModelReference(apply) is { } model)
                    {
                        return model;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static Identifier? FirstModelReference(JsonElement value)
    {
        // A variant is either a model entry or an array of weighted entries.
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in value.EnumerateArray())
            {
                if (FirstModelReference(entry) is { } model)
                {
                    return model;
                }
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!value.TryGetProperty("model", out var modelName) ||
            modelName.ValueKind != JsonValueKind.String ||
            modelName.GetString() is not { Length: > 0 } name)
        {
            return null;
        }

        return NormalizeModel(name);
    }

    private static Identifier NormalizeModel(string name)
    {
        // "block/stone" and "minecraft:block/stone" mean the same model.
        var text = name.Contains(':', StringComparison.Ordinal) ? name : "minecraft:" + name;

        if (!Identifier.TryParse(text, out var id))
        {
            return Identifier.Parse("minecraft:" + name);
        }

        // Models live under models/<path>.json; the stack already appends the directory.
        return id.Path.StartsWith("models/", StringComparison.Ordinal)
            ? new Identifier(id.Namespace, id.Path["models/".Length..])
            : id;
    }

    /// <summary>Walks the parent chain, merging textures child-over-parent.</summary>
    private Dictionary<string, string>? ResolveModelTextures(Identifier modelId)
    {
        var chain = new List<JsonDocument>();
        Identifier? current = modelId;
        var guard = 0;

        try
        {
            while (current is { } model && guard++ < 16)
            {
                var bytes = _stack.Read(ResourceKind.Model, model);
                if (bytes is null)
                {
                    break;
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(bytes);
                }
                catch (JsonException)
                {
                    break;
                }

                chain.Add(document);

                current = document.RootElement.TryGetProperty("parent", out var parent) &&
                          parent.ValueKind == JsonValueKind.String &&
                          parent.GetString() is { Length: > 0 } parentName
                    ? NormalizeModel(parentName)
                    : null;
            }

            if (chain.Count == 0)
            {
                return null;
            }

            // Root first, so children overwrite their parents.
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = chain.Count - 1; index >= 0; index--)
            {
                if (!chain[index].RootElement.TryGetProperty("textures", out var textures) ||
                    textures.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var entry in textures.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String &&
                        entry.Value.GetString() is { Length: > 0 } value)
                    {
                        merged[entry.Name] = value;
                    }
                }
            }

            return merged;
        }
        finally
        {
            foreach (var document in chain)
            {
                document.Dispose();
            }
        }
    }

    /// <summary>Resolves a face variable, following <c>#references</c> to a texture path.</summary>
    private static Identifier? Lookup(Dictionary<string, string> textures, string variable)
    {
        var current = variable;
        var guard = 0;

        while (guard++ < 16)
        {
            if (!textures.TryGetValue(current, out var value))
            {
                return null;
            }

            if (value.StartsWith('#'))
            {
                current = value[1..];
                continue;
            }

            var text = value.Contains(':', StringComparison.Ordinal) ? value : "minecraft:" + value;
            return Identifier.TryParse(text, out var id) ? id : null;
        }

        return null;
    }
}
