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
/// mesher stays an array index. A state has an entry when its block's model resolves, including the
/// empty model of a block entity block: the entry is then an empty quad list, so <see cref="Has"/> is
/// true (the cube path must leave it alone) while <see cref="Quads"/> returns no geometry. States with
/// no entry keep the cube path: a plain cube, an unreadable model, or a model that resolved to no
/// drawable quads.
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

    /// <summary>
    /// Whether this state has an entry, meaning the cube path must skip it. True both for element
    /// models and for empty models; check <see cref="Quads"/> for what to draw.
    /// </summary>
    /// <param name="stateId">Block state id.</param>
    public bool Has(int stateId) => (uint)stateId < (uint)_quads.Length && _quads[stateId] is not null;

    /// <summary>
    /// The quads of one state: an empty array for an empty model, or null when the state has no entry
    /// and belongs on the cube path.
    /// </summary>
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
    /// <param name="quadSamples">Per-quad corner samples for ambient occlusion, in the order of <paramref name="quads"/>.</param>
    /// <param name="ambientOcclusion">False keeps every vertex at full brightness while still smoothing the light.</param>
    /// <returns>How many quads were emitted.</returns>
    internal static int Emit(
        MeshBuffer buffer,
        TexturedQuad[] quads,
        int x,
        int y,
        int z,
        (float Sky, float Block) light,
        Func<Identifier, SpriteRect?> spriteFor,
        Func<string, bool>? isCulled = null,
        (float R, float G, float B)? tint = null,
        ReadOnlySpan<(float Sky, float Block)> faceLights = default,
        ReadOnlySpan<ChunkMesher.QuadCornerSamples> quadSamples = default,
        bool ambientOcclusion = true)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(spriteFor);

        var emitted = 0;

        // Hoisted out of the quad loop so a model with several faces does not grow the stack per quad (CA2014).
        Span<ChunkMesher.VertexLighting> perVertex = stackalloc ChunkMesher.VertexLighting[4];
        Span<(float R, float G, float B, float Sky, float Block)> corners = stackalloc (float, float, float, float, float)[4];

        for (var index = 0; index < quads.Length; index++)
        {
            var textured = quads[index];
            if (spriteFor(textured.Texture) is not { } sprite)
            {
                continue;
            }

            var quad = textured.Quad;
            if (quad.CullFace is { } cullFace && isCulled is not null && isCulled(cullFace))
            {
                continue;
            }

            // The reference tints exactly the faces whose model declares a tint index, which is why the model path
            // honours the per-face index rather than a per-block rule: a grass block tints its top and its side
            // overlay but not its dirt below. Issue 227 was this path dropping the tint entirely.
            var (tr, tg, tb) = tint is { } colour && quad.TintIndex >= 0 ? colour : (1f, 1f, 1f);

            // The reference lights a face from the block the face looks at, not from the block it belongs to: an
            // opaque block has an unlit cell of its own, so reading that cell drew every model block black
            // (issue 227). Callers without a neighbourhood fall back to the single supplied light.
            var faceIndex = FaceIndex(quad.Direction);
            var fallbackSky = faceIndex >= 0 && faceIndex < faceLights.Length ? faceLights[faceIndex].Sky : light.Sky;
            var fallbackBlock = faceIndex >= 0 && faceIndex < faceLights.Length ? faceLights[faceIndex].Block : light.Block;

            // Issue 255: a model face is lit per corner exactly like a cube face. The samples come from the caller,
            // which has world access, and the quad's own bounds decide which branch and which weights apply; the four
            // values arrive in the baked quad's vertex order, so each of this quad's corners is matched by position.
            var lit = index < quadSamples.Length && faceIndex >= 0;
            var direction = DirectionOfFace(faceIndex);
            var bounds = lit ? Bounds(quad) : default;
            if (lit)
            {
                ChunkMesher.ResolveQuadLighting(direction, quadSamples[index], bounds, ambientOcclusion, perVertex);
            }

            var shade = Shade(quad.Direction);
            var (uAxis, vAxis) = ChunkMesher.InPlaneAxes(direction);

            ReadOnlySpan<ModelVertex> vertices = [quad.V0, quad.V1, quad.V2, quad.V3];
            for (var corner = 0; corner < 4; corner++)
            {
                if (!lit)
                {
                    corners[corner] = (shade * tr, shade * tg, shade * tb, fallbackSky, fallbackBlock);
                    continue;
                }

                var position = vertices[corner].Position;
                var bakedVertex = ChunkMesher.BakedVertexAt(
                    direction,
                    Component(position, uAxis) > ((Component(bounds, uAxis, true) + Component(bounds, uAxis, false)) * 0.5f),
                    Component(position, vAxis) > ((Component(bounds, vAxis, true) + Component(bounds, vAxis, false)) * 0.5f));

                var value = perVertex[bakedVertex];
                corners[corner] = (
                    shade * tr * value.Brightness,
                    shade * tg * value.Brightness,
                    shade * tb * value.Brightness,
                    value.SkyLight,
                    value.BlockLight);
            }

            buffer.AppendQuad(
                (quad.V0.Position.X + x, quad.V0.Position.Y + y, quad.V0.Position.Z + z),
                (quad.V1.Position.X + x, quad.V1.Position.Y + y, quad.V1.Position.Z + z),
                (quad.V2.Position.X + x, quad.V2.Position.Y + y, quad.V2.Position.Z + z),
                (quad.V3.Position.X + x, quad.V3.Position.Y + y, quad.V3.Position.Z + z),
                (corners[0].R, corners[0].G, corners[0].B),
                (corners[1].R, corners[1].G, corners[1].B),
                (corners[2].R, corners[2].G, corners[2].B),
                (corners[3].R, corners[3].G, corners[3].B),
                (corners[0].Sky, corners[0].Block),
                (corners[1].Sky, corners[1].Block),
                (corners[2].Sky, corners[2].Block),
                (corners[3].Sky, corners[3].Block),
                (quad.V0.Uv.X, quad.V0.Uv.Y),
                (quad.V1.Uv.X, quad.V1.Uv.Y),
                (quad.V2.Uv.X, quad.V2.Uv.Y),
                (quad.V3.Uv.X, quad.V3.Uv.Y),
                sprite);

            emitted++;
        }

        return emitted;
    }

    /// <summary>Our face direction constant for one entry of <see cref="FaceIndex"/>.</summary>
    private static int DirectionOfFace(int faceIndex) => faceIndex switch
    {
        0 => 2,  // down
        1 => 3,  // up
        2 => 4,  // north
        3 => 5,  // south
        4 => 0,  // west
        _ => 1   // east
    };

    /// <summary>Bounds of a quad inside its block along each axis, which the shape test and the corner match read.</summary>
    private static (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) Bounds(ModelQuad quad)
    {
        var min = quad.V0.Position;
        var max = quad.V0.Position;

        foreach (var vertex in (ReadOnlySpan<ModelVertex>)[quad.V1, quad.V2, quad.V3])
        {
            min = System.Numerics.Vector3.Min(min, vertex.Position);
            max = System.Numerics.Vector3.Max(max, vertex.Position);
        }

        return (min.X, max.X, min.Y, max.Y, min.Z, max.Z);
    }

    private static float Component(System.Numerics.Vector3 value, int axis) => axis switch
    {
        0 => value.X,
        1 => value.Y,
        _ => value.Z
    };

    private static float Component(
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds, int axis, bool maximum)
    {
        if (axis == 0)
        {
            return maximum ? bounds.MaxX : bounds.MinX;
        }

        return axis == 1
            ? (maximum ? bounds.MaxY : bounds.MinY)
            : (maximum ? bounds.MaxZ : bounds.MinZ);
    }

    /// <summary>Index of a face direction inside the per-face light table, or -1 when the direction is unknown.</summary>
    public static int FaceIndex(string direction) => direction switch
    {
        "down" => 0,
        "up" => 1,
        "north" => 2,
        "south" => 3,
        "west" => 4,
        "east" => 5,
        _ => -1
    };

    /// <summary>Neighbour offset of a face direction, in the order <see cref="FaceIndex"/> defines.</summary>
    public static (int X, int Y, int Z) FaceOffset(int index) => index switch
    {
        0 => (0, -1, 0),
        1 => (0, 1, 0),
        2 => (0, 0, -1),
        3 => (0, 0, 1),
        4 => (-1, 0, 0),
        5 => (1, 0, 0),
        _ => (0, 0, 0)
    };

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
