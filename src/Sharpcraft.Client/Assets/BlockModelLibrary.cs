// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>A model quad whose texture variable has been resolved to the texture it names.</summary>
/// <param name="Quad">The quad, still with its variable name for diagnostics.</param>
/// <param name="Texture">The texture path the variable resolved to.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public sealed record TexturedQuad(ModelQuad Quad, Identifier Texture);

/// <summary>
/// Resolves a block's model into ready to draw quads: the boxes from
/// <see cref="BlockModelElements"/>, with every face's texture variable turned into a path by
/// <see cref="BlockModelResolver"/>. Blocks whose model is a plain cube resolve to null and stay on
/// the existing cube path.
/// </summary>
/// <remarks>
/// The result is cached per block, not per block state: the texture resolver only reads the first
/// variant of a block's blockstate file, so every state of a block currently shares one model. That
/// is the same limitation the resolver already carries and is registered in the spec.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public sealed class BlockModelLibrary
{
    private readonly Func<Identifier, Identifier?> _modelLookup;
    private readonly BlockModelElements _elements;
    private readonly Func<Identifier, string, Identifier?> _textureLookup;
    private readonly Dictionary<Identifier, TexturedQuad[]?> _cache = new();

    /// <summary>Creates a library over explicit sources, which is what tests supply.</summary>
    /// <param name="elements">Reader for model elements.</param>
    /// <param name="textureLookup">Resolves a block's texture variable to a path.</param>
    public BlockModelLibrary(
        Func<Identifier, Identifier?> modelLookup,
        BlockModelElements elements,
        Func<Identifier, string, Identifier?> textureLookup)
    {
        _modelLookup = modelLookup ?? throw new ArgumentNullException(nameof(modelLookup));
        _elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _textureLookup = textureLookup ?? throw new ArgumentNullException(nameof(textureLookup));
    }

    /// <summary>Creates a library over a resource stack.</summary>
    /// <param name="stack">Stack holding the extracted models and blockstates.</param>
    public static BlockModelLibrary FromStack(AssetStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        var resolver = new BlockModelResolver(stack);
        return new BlockModelLibrary(
            resolver.ResolveModel,
            BlockModelElements.FromStack(stack),
            resolver.ResolveTexture);
    }

    /// <summary>How many blocks were found to have element geometry.</summary>
    public int ElementModelCount { get; private set; }

    /// <summary>Builds a block's quads, or null when its model is a plain cube.</summary>
    /// <param name="blockId">Block to resolve.</param>
    public TexturedQuad[]? Resolve(Identifier blockId)
    {
        if (_cache.TryGetValue(blockId, out var cached))
        {
            return cached;
        }

        var resolved = Build(blockId);
        if (resolved is not null)
        {
            ElementModelCount++;
        }

        _cache[blockId] = resolved;
        return resolved;
    }

    /// <summary>
    /// Whether a model is nothing but one full cube with all six faces culled. The reference defines
    /// <c>block/cube</c> that way, so almost every block has element geometry on paper; treating it as
    /// an element model would route the whole world around the face merge for no visual gain.
    /// </summary>
    private static bool IsPlainCube(IReadOnlyList<ModelElement> elements)
    {
        if (elements.Count != 1)
        {
            return false;
        }

        var box = elements[0].Box;
        if (box.FromX != 0f || box.FromY != 0f || box.FromZ != 0f ||
            box.ToX != 16f || box.ToY != 16f || box.ToZ != 16f)
        {
            return false;
        }

        // A full-size box is only a plain cube when nothing about it is specialised: an element
        // rotation, an explicit texture rectangle, a face rotation or a tint slot all mean the cube
        // path would draw the wrong thing, and the six keys must be exactly the six directions.
        if (elements[0].Rotation is not null || elements[0].Faces.Count != 6)
        {
            return false;
        }

        string[] directions = ["down", "up", "north", "south", "west", "east"];
        foreach (var direction in directions)
        {
            if (!elements[0].Faces.TryGetValue(direction, out var face) ||
                face.CullFace is null ||
                face.Uv is not null ||
                face.Rotation != 0 ||
                face.TintIndex != -1)
            {
                return false;
            }
        }

        return true;
    }

    private TexturedQuad[]? Build(Identifier blockId)
    {
        // The element reader walks the parent chain itself; the texture lookup needs the block so it
        // can apply the same first variant rule the rest of the pipeline uses.
        var modelId = _modelLookup(blockId);
        var elements = modelId is { } model ? _elements.Resolve(model) : null;
        if (elements is null || elements.Count == 0 || IsPlainCube(elements))
        {
            return null;
        }

        var quads = BlockModelGeometry.Build(elements);
        var textured = new List<TexturedQuad>(quads.Count);

        foreach (var quad in quads)
        {
            if (_textureLookup(blockId, quad.Texture) is not { } texture)
            {
                continue;
            }

            textured.Add(new TexturedQuad(quad, texture));
        }

        return textured.Count == 0 ? null : textured.ToArray();
    }
}