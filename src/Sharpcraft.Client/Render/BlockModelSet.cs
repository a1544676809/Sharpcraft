// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per state lookup for blocks whose model is built from element boxes rather than a plain cube.
/// The mesher asks this first: a state that resolves here must not enter the cube face merge, and
/// is emitted on its own after the merge passes.
/// </summary>
/// <remarks>
/// Resolution happens once at build time and is stored in flat arrays so the per block test in the
/// mesher stays an array index. Blocks that are plain cubes, or whose model could not be read, keep
/// the cube path and are false here.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public sealed class BlockModelSet
{
    private readonly TexturedQuad[]?[] _quads;

    private BlockModelSet(TexturedQuad[]?[] quads)
    {
        _quads = quads;
    }

    /// <summary>Number of states that carry element geometry.</summary>
    public int ElementStateCount { get; private init; }

    /// <summary>Builds the lookup, or null when no block in the layout has element geometry.</summary>
    /// <param name="library">Model library to resolve each block through.</param>
    /// <param name="layout">Block state layout giving each block its state range.</param>
    public static BlockModelSet? Build(BlockModelLibrary library, BlockStateLayout layout)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(layout);

        var quads = new TexturedQuad[]?[layout.TotalStates];
        var count = 0;

        foreach (var entry in layout.Blocks)
        {
            // A block whose model has no elements contributes nothing here, exactly like the reference;
            // anything it draws comes from its block entity renderer.
            var resolved = library.Resolve(entry.Id);
            if (resolved is null)
            {
                continue;
            }

            for (var offset = 0; offset < entry.StateCount; offset++)
            {
                var state = entry.BaseId + offset;
                if ((uint)state < (uint)quads.Length)
                {
                    quads[state] = resolved;
                    count++;
                }
            }
        }

        return count == 0 ? null : new BlockModelSet(quads) { ElementStateCount = count };
    }

    /// <summary>Whether this state draws from element geometry.</summary>
    /// <param name="stateId">Block state id.</param>
    /// <summary>
    /// Whether a block is drawn by its own block entity renderer, so the cube path must leave it alone.
    /// </summary>
    private static bool IsBlockEntityRendered(Identifier blockId) => blockId.Path switch
    {
        "chest" or "trapped_chest" or "ender_chest" => true,
        "copper_chest" or "waxed_copper_chest" => true,
        "exposed_copper_chest" or "waxed_exposed_copper_chest" => true,
        "weathered_copper_chest" or "waxed_weathered_copper_chest" => true,
        "oxidized_copper_chest" or "waxed_oxidized_copper_chest" => true,
        _ => false
    };

    public bool Has(int stateId) => (uint)stateId < (uint)_quads.Length && _quads[stateId] is not null;

    /// <summary>The quads of one state, or null when it is a plain cube.</summary>
    /// <param name="stateId">Block state id.</param>
    public TexturedQuad[]? Quads(int stateId) => (uint)stateId < (uint)_quads.Length ? _quads[stateId] : null;

    /// <summary>
    /// Emits one block's quads into a buffer, translated to its position.
    /// </summary>
    /// <param name="buffer">Buffer to append to.</param>
    /// <param name="quads">Quads of the block state, in model space.</param>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <param name="light">Packed sky and block light, each zero to one.</param>
    /// <param name="spriteFor">Resolves a texture path to its atlas cell.</param>
    /// <returns>How many quads were emitted.</returns>
    public static int Emit(
        MeshBuffer buffer,
        TexturedQuad[] quads,
        int x,
        int y,
        int z,
        (float Sky, float Block) light,
        Func<Identifier, SpriteRect?> spriteFor,
        Func<string, bool>? isCulled = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(spriteFor);

        var emitted = 0;

        foreach (var textured in quads)
        {
            if (spriteFor(textured.Texture) is not { } sprite)
            {
                continue;
            }

            var quad = textured.Quad;
            if (quad.CullFace is { } cullFace && isCulled is not null && isCulled(cullFace))
            {
                continue;
            }

            var shade = Shade(quad.Direction);
            buffer.AppendQuad(
                (quad.V0.Position.X + x, quad.V0.Position.Y + y, quad.V0.Position.Z + z),
                (quad.V1.Position.X + x, quad.V1.Position.Y + y, quad.V1.Position.Z + z),
                (quad.V2.Position.X + x, quad.V2.Position.Y + y, quad.V2.Position.Z + z),
                (quad.V3.Position.X + x, quad.V3.Position.Y + y, quad.V3.Position.Z + z),
                (shade, shade, shade),
                (shade, shade, shade),
                (shade, shade, shade),
                (shade, shade, shade),
                light,
                light,
                light,
                light,
                (quad.V0.Uv.X, quad.V0.Uv.Y),
                (quad.V1.Uv.X, quad.V1.Uv.Y),
                (quad.V2.Uv.X, quad.V2.Uv.Y),
                (quad.V3.Uv.X, quad.V3.Uv.Y),
                sprite);

            emitted++;
        }

        return emitted;
    }

    /// <summary>
    /// Brightness the reference gives each face direction before light is applied. A box that is not
    /// a cube would otherwise look flat, because every face would share one brightness.
    /// </summary>
    public static float Shade(string direction) => direction switch
    {
        "down" => 0.5f,
        "up" => 1f,
        "north" => 0.8f,
        "south" => 0.8f,
        "west" => 0.6f,
        "east" => 0.6f,
        _ => 1f
    };
}
