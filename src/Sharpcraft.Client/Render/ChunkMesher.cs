// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

using Sharpcraft.Client.Assets;

// `BlockFace` here always means the atlas texture bucket (Top/Side/Bottom); the six-direction
// BlockFace lives in Sharpcraft.Common.World and is not referenced by this file.
using BlockFace = Sharpcraft.Client.Assets.BlockFace;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Turns blocks into triangle geometry. This is the CPU half of rendering: the GPU layer only
/// consumes the float arrays it produces (docs/architecture/boundaries.md).
/// </summary>
/// <remarks>
/// Meshing is greedy: inside one section and one face direction, runs of faces that share both
/// state and lighting are merged into a single larger quad, so a flat plain costs one quad per
/// direction instead of one per block. Merging only happens across identical keys, which keeps
/// shading per-quad uniform and the result visually identical to the per-block version.
/// <para>
/// Geometry is written into a reusable <see cref="MeshBuffer"/> rather than a <c>List</c>, and the
/// world's loaded chunks are enumerated directly instead of scanning a fixed coordinate window.
/// </para>
/// </remarks>
public static partial class ChunkMesher
{
    /// <summary>Floats per vertex: position (3), packed colour (1), uv (2), sprite rect (4), packed light (1).</summary>
    /// <remarks>Six float slots, twenty four bytes, matching <c>VertexLayout.Stride</c> (T-832). The two packed words are stored as their bit patterns, so the buffer stays a float array and vertex counts keep dividing cleanly.</remarks>
    public const int FloatsPerVertex = 6;

    /// <summary>Vertices per quad.</summary>
    public const int VerticesPerQuad = 4;

    /// <summary>Indices per quad (two triangles).</summary>
    public const int IndicesPerQuad = 6;

    /// <summary>Shading applied to faces looking up.</summary>
    public const float TopShade = 1.0f;

    /// <summary>Shading applied to faces looking down.</summary>
    public const float BottomShade = 0.5f;

    /// <summary>Shading applied to the four horizontal facings.</summary>
    public const float SideShade = 0.75f;

    // Face order matches the classic six-direction list: -X, +X, -Y, +Y, -Z, +Z.
    private const int NegativeX = 0;
    private const int PositiveX = 1;
    private const int NegativeY = 2;
    private const int PositiveY = 3;
    private const int NegativeZ = 4;
    private const int PositiveZ = 5;

    /// <summary>Meshes every loaded chunk of a world into a freshly allocated mesh.</summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="airState">Block state treated as empty.</param>
    /// <param name="maxBlocks">Safety cap so a huge world cannot exhaust memory.</param>
    /// <param name="sprites">Atlas mapping; without it quads carry the whole-atlas rect.</param>
    /// <param name="layout">Block state layout, needed with <paramref name="tint"/> to colour grass and leaves.</param>
    /// <param name="tint">Biome tint, or null for none. A caller without biome data passes none and gets untinted faces.</param>
    public static MeshData Mesh(
        ClientWorld world,
        int airState = 0,
        int maxBlocks = 2_000_000,
        BlockSpriteMap? sprites = null,
        bool merge = true,
        bool ambientOcclusion = true,
        BlockModelSet? models = null,
        Sharpcraft.Common.Registries.BlockStateLayout? layout = null,
        BiomeTint? tint = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        // Merging cuts the quad count by an order of magnitude, so a smaller guess is enough.
        using var buffer = new MeshBuffer(Math.Min(maxBlocks, world.LoadedChunkCount * 512));
        var blocks = Mesh(world, buffer, airState, maxBlocks, sprites, merge, ambientOcclusion, models, layout, tint);
        return buffer.ToMeshData(blocks, world.MinSectionY);
    }

    /// <summary>
    /// Meshes every loaded chunk into <paramref name="buffer"/>, which is cleared first. A warm
    /// buffer performs no steady-state allocation.
    /// </summary>
    /// <returns>Number of solid blocks that contributed faces.</returns>
    public static int Mesh(
        ClientWorld world,
        MeshBuffer buffer,
        int airState = 0,
        int maxBlocks = 2_000_000,
        BlockSpriteMap? sprites = null,
        bool merge = true,
        bool ambientOcclusion = true,
        BlockModelSet? models = null,
        Sharpcraft.Common.Registries.BlockStateLayout? layout = null,
        BiomeTint? tint = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(buffer);

        buffer.Clear();
        var blocks = 0;

        foreach (var chunk in world.GetChunks())
        {
            if (blocks >= maxBlocks)
            {
                break;
            }

            blocks += MeshChunk(world, chunk, airState, buffer, maxBlocks - blocks, sprites, layout: layout, tint: tint, merge: merge, ambientOcclusion: ambientOcclusion, models: models);
        }

        return blocks;
    }

    /// <summary>Meshes one chunk, using the world for neighbour lookups across chunk borders.</summary>
    /// <returns>Number of solid blocks that contributed at least one face.</returns>
    public static int MeshChunk(
        ClientWorld world,
        LevelChunk chunk,
        int airState,
        MeshBuffer buffer,
        int maxBlocks,
        BlockSpriteMap? sprites = null,
        MeshBuffer? translucent = null,
        Sharpcraft.Common.Registries.BlockStateLayout? layout = null,
        MeshBuffer? cutout = null,
        BiomeTint? tint = null,
        StateCullTable? cull = null,
        StateVisualCache? visuals = null,
        bool merge = true,
        bool ambientOcclusion = true,
        BlockModelSet? models = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(buffer);

        var baseX = chunk.ChunkX * 16;
        var baseZ = chunk.ChunkZ * 16;
        var solidBlocks = 0;

        Span<bool> consumed = stackalloc bool[256];

            var neighbourhood = new Neighbourhood(world, chunk);

        buffer.ReserveSections(chunk.SectionCount);

        // Scratch for the per section sight test, hoisted so the loop allocates nothing (T-833).
        Span<ulong> blocking = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ulong> sightVisited = stackalloc ulong[SectionVisibilityCompute.CellWordCount];
        Span<ushort> sightQueue = stackalloc ushort[SectionVisibilityCompute.CellCount];

        for (var sectionIndex = 0; sectionIndex < chunk.SectionCount; sectionIndex++)
        {
            // Marked before the skip so the ranges stay aligned with the section index: an empty section simply
            // contributes a zero length run (T-829).
            var section = chunk.GetSection(chunk.MinSectionY + sectionIndex);
            var sectionBaseY = (chunk.MinSectionY + sectionIndex) * 16;
            var sight = section is null || section.IsEmpty
                ? SectionVisibility.AllVisible
                : ResolveSectionVisibility(
                    neighbourhood, baseX, sectionBaseY, baseZ, sprites, layout, blocking, sightVisited, sightQueue);
            buffer.MarkSection(sight);

            if (section is null || section.IsEmpty)
            {
                continue;
            }

            if (solidBlocks >= maxBlocks)
            {
                return solidBlocks;
            }

            var baseY = sectionBaseY;
            solidBlocks += section.NonAirCount;

            for (var direction = 0; direction < 6; direction++)
            {
                consumed.Clear();
                MergeSectionFaces(neighbourhood, section, direction, baseX, baseY, baseZ, airState, buffer, consumed, sprites, translucent, layout, cutout, tint, cull, visuals, merge, ambientOcclusion, models);
            }

            if (translucent is not null)
            {
                EmitSectionFluids(neighbourhood, section, baseX, baseY, baseZ, buffer, translucent, sprites, layout, tint);
            }

            if (models is not null && sprites is not null)
            {
                EmitSectionModels(neighbourhood, section, baseX, baseY, baseZ, buffer, sprites, models, tint, layout, ambientOcclusion);
            }
        }

        return solidBlocks;
    }

    /// <summary>Meshes one chunk using a unified <see cref="MeshingContext"/>.</summary>
    public static int MeshChunk(
        ClientWorld world,
        LevelChunk chunk,
        MeshBuffer buffer,
        MeshBuffer? cutout,
        MeshBuffer? translucent,
        in MeshingContext context) =>
        MeshChunk(
            world,
            chunk,
            context.AirState,
            buffer,
            context.MaxBlocks,
            context.Sprites,
            translucent: translucent,
            layout: context.Layout,
            cutout: cutout,
            tint: context.Tint,
            cull: context.Cull,
            visuals: context.Visuals,
            merge: context.MergeFaces,
            ambientOcclusion: context.AmbientOcclusion,
            models: context.Models);

    /// <summary>Merges the faces of one direction inside one section into as few quads as possible.</summary>
    private static void MergeSectionFaces(
        in Neighbourhood neighbourhood,
        ChunkSection section,
        int direction,
        int baseX,
        int baseY,
        int baseZ,
        int airState,
        MeshBuffer buffer,
        Span<bool> consumed,
        BlockSpriteMap? sprites,
        MeshBuffer? translucent = null,
        Sharpcraft.Common.Registries.BlockStateLayout? layout = null,
        MeshBuffer? cutout = null,
        BiomeTint? tint = null,
        StateCullTable? cull = null,
        StateVisualCache? visuals = null,
        bool merge = true,
        bool ambientOcclusion = true,
        BlockModelSet? models = null)
    {
        var (uAxis, vAxis) = InPlaneAxes(direction);

        // One pass builds the plane's faces, then the merge runs on that table. Asking the world for each neighbour again
        // while widening and growing a quad made the work quadratic in the quad size, which is exactly the flat case.
        Span<MergeKey> keys = stackalloc MergeKey[256];
        Span<bool> present = stackalloc bool[256];

        for (var slice = 0; slice < 16; slice++)
        {
            // The consumed mask is per plane: sharing it across slices would suppress cells in
            // later planes and emit quads from the wrong slice.
            consumed.Clear();

            for (var v = 0; v < 16; v++)
            {
                for (var u = 0; u < 16; u++)
                {
                    var cell = (v << 4) | u;
                    present[cell] = TryDescribe(
                        neighbourhood, section, direction, uAxis, vAxis, slice, u, v, baseX, baseY, baseZ,
                        airState, sprites, layout, cull, models, out keys[cell]);
                }
            }

            for (var v = 0; v < 16; v++)
            {
                for (var u = 0; u < 16; u++)
                {
                    var cell = (v << 4) | u;

                    if (consumed[cell] || !present[cell])
                    {
                        continue;
                    }

                    var key = keys[cell];

                    var width = 1;

                    if (merge)
                    {
                        while (u + width < 16)
                        {
                            var next = (v << 4) | (u + width);

                            if (consumed[next] || !present[next] || keys[next] != key)
                            {
                                break;
                            }

                            width++;
                        }
                    }

                    var height = 1;

                    if (merge)
                    {
                        while (v + height < 16)
                        {
                            var rowMatches = true;

                            for (var du = 0; du < width; du++)
                            {
                                var above = ((v + height) << 4) | (u + du);

                                if (consumed[above] || !present[above] || keys[above] != key)
                                {
                                    rowMatches = false;
                                    break;
                                }
                            }

                            if (!rowMatches)
                            {
                                break;
                            }

                            height++;
                        }
                    }

                    // A merged quad carries only four corner lights and AO, so merging is exact only where the whole
                    // rectangle is lit uniformly (open, same light, no occluders in or around it). Anywhere else fall
                    // back to one quad per block face, which makes the merged output equal the unmerged output exactly
                    // (the smooth-lighting variation the reference gets for free by never merging).
                    if (merge && (width > 1 || height > 1) &&
                        !RectangleIsUniformlyLit(neighbourhood, direction, uAxis, vAxis, slice, u, v, width, height, baseX, baseY, baseZ, sprites, layout, key))
                    {
                        width = 1;
                        height = 1;
                    }

                    for (var dv = 0; dv < height; dv++)
                    {
                        for (var du = 0; du < width; du++)
                        {
                            consumed[((v + dv) << 4) | (u + du)] = true;
                        }
                    }

                    EmitMergedQuad(neighbourhood, buffer, direction, slice, u, v, width, height, baseX, baseY, baseZ, key.State, sprites, translucent, layout, cutout, tint, cull, visuals, ambientOcclusion);
                }
            }
        }
    }

    /// <summary>
    /// True when every cell in and around the rectangle is non-occluding and shares one light level, so a single merged
    /// quad's bilinear corner shading equals the per-face shading of each block. Any occluder or light change means the
    /// merged quad would smear the variation, so the caller emits per-face quads instead.
    /// </summary>
    private static bool RectangleIsUniformlyLit(
        in Neighbourhood neighbourhood,
        int direction,
        int uAxis,
        int vAxis,
        int slice,
        int u,
        int v,
        int width,
        int height,
        int baseX,
        int baseY,
        int baseZ,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        MergeKey key)
    {
        var (dx, dy, dz) = NeighbourOffset(direction);

        for (var vv = v - 1; vv <= v + height; vv++)
        {
            for (var uu = u - 1; uu <= u + width; uu++)
            {
                var (x, y, z) = FaceBlock(uAxis, vAxis, slice, uu, vv);
                var sampleX = baseX + x + dx;
                var sampleY = baseY + y + dy;
                var sampleZ = baseZ + z + dz;

                // Without an atlas the occlusion cannot be known (Occludes reports solid for everything); treat those
                // cells as non-occluding so the untextured path still merges where the light is uniform.
                if (sprites is not null && layout is not null &&
                    Occludes(neighbourhood.GetBlockState(sampleX, sampleY, sampleZ, 0), sprites, layout))
                {
                    return false;
                }

                neighbourhood.TryGetLight(sampleX, sampleY, sampleZ, out var sky, out var block);

                if (sky != key.Sky || block != key.Block)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool RowMatches(
        in Neighbourhood neighbourhood,
        ChunkSection section,
        int direction,
        int uAxis,
        int vAxis,
        int slice,
        int u,
        int v,
        int width,
        int baseX,
        int baseY,
        int baseZ,
        int airState,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        StateCullTable? cull,
        MergeKey key,
        BlockModelSet? models,
        Span<bool> consumed)
    {
        for (var du = 0; du < width; du++)
        {
            if (consumed[(v << 4) | (u + du)])
            {
                return false;
            }

            if (!TryDescribe(neighbourhood, section, direction, uAxis, vAxis, slice, u + du, v, baseX, baseY, baseZ, airState, sprites, layout, cull, models, out var candidate) ||
                candidate != key)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The five chunks a face can reach: this one and its four horizontal neighbours.
    /// </summary>
    /// <remarks>
    /// A face's neighbour block is at most one block outside the chunk and never crosses a section boundary vertically, so
    /// these five are the only chunks involved. Resolving them once turns every block and light lookup into an array index
    /// instead of a dictionary probe, which is what made meshing take hundreds of milliseconds per chunk.

    private static (int X, int Y, int Z) FaceBlock(int uAxis, int vAxis, int slice, int u, int v)
    {
        Span<int> coordinates = stackalloc int[3];
        coordinates[uAxis] = u;
        coordinates[vAxis] = v;
        coordinates[3 - uAxis - vAxis] = slice;
        return (coordinates[0], coordinates[1], coordinates[2]);
    }

    /// <summary>The two in-plane axes used for merging, per face direction.</summary>
    internal static (int U, int V) InPlaneAxes(int direction) => direction switch
    {
        NegativeX or PositiveX => (2, 1),   // u = Z, v = Y
        NegativeY or PositiveY => (0, 2),   // u = X, v = Z
        _ => (0, 1)                         // u = X, v = Y
    };

    private static (int X, int Y, int Z) NeighbourOffset(int direction) => direction switch
    {
        NegativeX => (-1, 0, 0),
        PositiveX => (1, 0, 0),
        NegativeY => (0, -1, 0),
        PositiveY => (0, 1, 0),
        NegativeZ => (0, 0, -1),
        _ => (0, 0, 1)
    };

    private static void EmitMergedQuad(
        in Neighbourhood neighbourhood,
        MeshBuffer buffer,
        int direction,
        int slice,
        int u,
        int v,
        int width,
        int height,
        int baseX,
        int baseY,
        int baseZ,
        int state,
        BlockSpriteMap? sprites,
        MeshBuffer? translucent = null,
        Sharpcraft.Common.Registries.BlockStateLayout? layout = null,
        MeshBuffer? cutout = null,
        BiomeTint? tint = null,
        StateCullTable? cull = null,
        StateVisualCache? visuals = null,
        bool ambientOcclusion = true)
    {
        // Fluids are meshed by EmitSectionFluids into the translucent buffer; the block path must not also emit them.
        if (translucent is not null && IsFluidState(state, layout))
        {
            return;
        }

        var (uAxis, vAxis) = InPlaneAxes(direction);
        var (x, y, z) = FaceBlock(uAxis, vAxis, slice, u, v);

        // u spans width along uAxis, v spans height along vAxis.
        Span<int> uSpan = stackalloc int[3];
        uSpan[uAxis] = width;

        Span<int> vSpan = stackalloc int[3];
        vSpan[vAxis] = height;

        // Corner sequence per direction mirrors the per-block mesher exactly, so the winding that
        // the GPU culling setup expects is preserved when quads grow.
        (int X, int Y, int Z) origin;
        (int X, int Y, int Z) a;
        (int X, int Y, int Z) b;
        (int X, int Y, int Z) c;

        // Projects a corner onto the face's texture axes. The reference derives each face's coordinates from world
        // axes with a fixed flip, so expressing them the same way keeps every face aligned with the original.
        static (float U, float V) Uv(int direction, int x, int y, int z, (int X, int Y, int Z) corner) => direction switch
        {
            PositiveZ => ((float)(corner.X - x), (float)((y + 1) - corner.Y)),
            NegativeZ => ((float)((x + 1) - corner.X), (float)((y + 1) - corner.Y)),
            PositiveX => ((float)((z + 1) - corner.Z), (float)((y + 1) - corner.Y)),
            NegativeX => ((float)(corner.Z - z), (float)((y + 1) - corner.Y)),
            PositiveY => ((float)(corner.X - x), (float)(corner.Z - z)),
            _ => ((float)(corner.X - x), (float)((z + 1) - corner.Z))
        };

        // Winding per direction: the quad must be counter-clockwise seen from outside, which
        // means Cross(U, V) equals the outward normal. Where it does not, V comes first.
        switch (direction)
        {
            case NegativeX:
                origin = (x, y, z);
                a = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                b = (origin.X + uSpan[0] + vSpan[0], origin.Y + uSpan[1] + vSpan[1], origin.Z + uSpan[2] + vSpan[2]);
                c = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                break;

            case PositiveX:
                origin = (x + 1, y, z);
                a = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                b = (origin.X + vSpan[0] + uSpan[0], origin.Y + vSpan[1] + uSpan[1], origin.Z + vSpan[2] + uSpan[2]);
                c = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                break;

            case NegativeY:
                origin = (x, y, z);
                a = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                b = (origin.X + uSpan[0] + vSpan[0], origin.Y + uSpan[1] + vSpan[1], origin.Z + uSpan[2] + vSpan[2]);
                c = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                break;

            case PositiveY:
                origin = (x, y + 1, z);
                a = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                b = (origin.X + vSpan[0] + uSpan[0], origin.Y + vSpan[1] + uSpan[1], origin.Z + vSpan[2] + uSpan[2]);
                c = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                break;

            case NegativeZ:
                origin = (x, y, z);
                a = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                b = (origin.X + vSpan[0] + uSpan[0], origin.Y + vSpan[1] + uSpan[1], origin.Z + vSpan[2] + uSpan[2]);
                c = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                break;

            default:  // PositiveZ
                origin = (x, y, z + 1);
                a = (origin.X + uSpan[0], origin.Y + uSpan[1], origin.Z + uSpan[2]);
                b = (origin.X + uSpan[0] + vSpan[0], origin.Y + uSpan[1] + vSpan[1], origin.Z + uSpan[2] + vSpan[2]);
                c = (origin.X + vSpan[0], origin.Y + vSpan[1], origin.Z + vSpan[2]);
                break;
        }

        // Faces are described in section-local coordinates; shift them into chunk space before
        // they are written, otherwise sections above or below the origin land in the wrong place.
        origin = (origin.X + baseX, origin.Y + baseY, origin.Z + baseZ);
        a = (a.X + baseX, a.Y + baseY, a.Z + baseZ);
        b = (b.X + baseX, b.Y + baseY, b.Z + baseZ);
        c = (c.X + baseX, c.Y + baseY, c.Z + baseZ);

        // With textures the albedo comes from the atlas, so the vertex colour carries only light:
        // multiplying a texture by the state hash colour would tint every block (found by sampling
        // rendered pixels against the atlas cell of a forced stone world).
        var (red, green, blue) = sprites is null ? ColourOf(state) : (1f, 1f, 1f);

        // One tint decision for every path: BlockTintTable answers the colour, and the top-face-only rule the
        // reference uses for a cube face is asked for explicitly rather than reimplemented here.
        if (layout?.BlockAt(state) is { } tintedBlock
            && BlockTintTable.MultiplierForCubeFace(tint, tintedBlock, origin.X, origin.Y, origin.Z, direction == PositiveY)
                is { } tintMultiplier)
        {
            red *= tintMultiplier.R;
            green *= tintMultiplier.G;
            blue *= tintMultiplier.B;
        }
        // Directional face brightness follows the reference's CardinalLighting.DEFAULT: up 1.0, down 0.5, north/south
        // (Z axis) 0.8, west/east (X axis) 0.6. A single 0.75 for every side made side faces shade wrong.
        var shadeFactor = direction switch
        {
            PositiveY => 1.0f,
            NegativeY => 0.5f,
            NegativeZ or PositiveZ => 0.8f,
            _ => 0.6f
        };

        // Corner order follows the winding: where U came first the horizontal corner comes first,
        // where V came first the vertical one does. UVs stay in sprite repeats so a merged quad
        // tiles its texture instead of stretching it.
        // Texture axes per face, taken from the reference model baker: it derives each face's coordinates from the
        // world axes with a fixed flip per direction. Down is lower on the screen for every side face, and the
        // horizontal axis runs opposite between opposing sides, which is what makes a wrapped texture read the
        // same way round as the original rather than mirrored on half the faces
        // (docs/analysis/render/face-uv.md, ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/FaceBakery.java).
        (float U, float V) uvOrigin = Uv(direction, x, y, z, (origin.X - baseX, origin.Y - baseY, origin.Z - baseZ));
        (float U, float V) uvA = Uv(direction, x, y, z, (a.X - baseX, a.Y - baseY, a.Z - baseZ));
        (float U, float V) uvB = Uv(direction, x, y, z, (b.X - baseX, b.Y - baseY, b.Z - baseZ));
        (float U, float V) uvC = Uv(direction, x, y, z, (c.X - baseX, c.Y - baseY, c.Z - baseZ));

        var target = buffer;
        var face = FaceOf(direction);

        // The layer decides the target buffer, exactly as the reference picks a chunk section layer per material, and it is
        // decided per face from that face's own sprite: a block can have an opaque top and holed sides, and a holed sprite
        // drawn by the solid pipeline shows its holes as opaque black.
        SpriteRect? faceSprite;

        if (visuals is not null)
        {
            var visual = visuals.Get(state, face);
            faceSprite = visual.Sprite;

            if (translucent is not null)
            {
                target = visual.Layer switch
                {
                    BlockLayer.Translucent when translucent is not null => translucent,
                    BlockLayer.Cutout when cutout is not null => cutout,
                    _ => buffer
                };
            }
        }
        else
        {
            faceSprite = sprites?.ForState(state, face);

            if (translucent is not null)
            {
                var layer = faceSprite is null || layout?.BlockAt(state) is not { } faceBlock
                    ? BlockLayerRules.Of(state, sprites, layout)
                    : BlockLayerRules.OfSprite(faceBlock, faceSprite, sprites);

                target = layer switch
                {
                    BlockLayer.Translucent when translucent is not null => translucent,
                    BlockLayer.Cutout when cutout is not null => cutout,
                    _ => buffer
                };
            }
        }

        // Ambient occlusion and smooth light come from the reference's per-quad pass: it samples the four corners, the
        // four diagonals (with its own fallback rule) and the centre, then blends brightness and light separately. A
        // merged quad covers a whole block face, so the shape test of that pass sees a cubic face.
        Span<VertexLighting> lighting = stackalloc VertexLighting[4];
        PrepareQuadLighting(
            neighbourhood,
            direction,
            (x + baseX, y + baseY, z + baseZ),
            ambientOcclusion,
            sprites,
            layout,
            CubeFaceBoundsOf(direction),
            lighting);

        (float R, float G, float B, float Sky, float Block) Shaded(VertexLighting vertex)
        {
            var factor = shadeFactor * vertex.Brightness;
            return (red * factor, green * factor, blue * factor, vertex.SkyLight, vertex.BlockLight);
        }

        var cornerOrigin = Shaded(lighting[0]);
        var cornerA = Shaded(lighting[1]);
        var cornerB = Shaded(lighting[2]);
        var cornerC = Shaded(lighting[3]);

        // Still water and lava sit at fourteen sixteenths of a block, not at the block top. Without this the water surface
        // is exactly coplanar with an ice block's top face and the two fight for the same depth, which is what showed as
        // large rectangular patches on a frozen ocean. Flowing fluid heights need the block state level property and are
        // left for the block state work; a single height already removes the coplanar case.
        if (layout?.BlockAt(state) is { Path: "water" or "lava" } fluidBlock)
        {
            // Vanilla fluid surface height: amount/9 (source = 8/9), or a full block when the same fluid is directly above.
            // Flowing levels are lower. This replaces the old fixed 14/16 hack and uses the decoded block level.
            var fluid = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(state, layout);
            var aboveState = neighbourhood.GetBlockState(baseX + x, baseY + y + 1, baseZ + z, 0);
            var sameFluidAbove = layout.BlockAt(aboveState) is { } aboveBlock && aboveBlock.Path == fluidBlock.Path;
            var ownHeight = sameFluidAbove ? 1f : (fluid.IsEmpty ? 1f : fluid.OwnHeight);
            var drop = 1f - ownHeight;

            var top = Math.Max(Math.Max(origin.Y, a.Y), Math.Max(b.Y, c.Y));
            target.AppendQuad(
                Drop(origin),
                Drop(a),
                Drop(b),
                Drop(c),
                (cornerOrigin.R, cornerOrigin.G, cornerOrigin.B),
                (cornerA.R, cornerA.G, cornerA.B),
                (cornerB.R, cornerB.G, cornerB.B),
                (cornerC.R, cornerC.G, cornerC.B),
                (cornerOrigin.Sky, cornerOrigin.Block),
                (cornerA.Sky, cornerA.Block),
                (cornerB.Sky, cornerB.Block),
                (cornerC.Sky, cornerC.Block),
                uvOrigin,
                uvA,
                uvB,
                uvC,
                faceSprite);

            (float X, float Y, float Z) Drop((int X, int Y, int Z) point) =>
                (point.X, point.Y == top ? point.Y - drop : point.Y, point.Z);

            return;
        }

        target.AppendQuad(
            origin,
            a,
            b,
            c,
            (cornerOrigin.R, cornerOrigin.G, cornerOrigin.B),
            (cornerA.R, cornerA.G, cornerA.B),
            (cornerB.R, cornerB.G, cornerB.B),
            (cornerC.R, cornerC.G, cornerC.B),
            (cornerOrigin.Sky, cornerOrigin.Block),
            (cornerA.Sky, cornerA.Block),
            (cornerB.Sky, cornerB.Block),
            (cornerC.Sky, cornerC.Block),
            uvOrigin,
            uvA,
            uvB,
            uvC,
            faceSprite);
    }

    /// <summary>Maps a face direction to the atlas face bucket a block uses.</summary>
    private static BlockFace FaceOf(int direction) => direction switch
    {
        PositiveY => BlockFace.Top,
        NegativeY => BlockFace.Bottom,
        _ => BlockFace.Side
    };

    /// <summary>Deterministic colour from a block state id, so the same world always looks the same.</summary>
    public static (float Red, float Green, float Blue) ColourOf(int state)
    {
        // Mix the id into a stable hue; the exact palette does not matter yet, stability does.
        var hash = unchecked((uint)(state * 2654435761u));
        var red = 0.35f + (hash & 0xFF) / 255f * 0.6f;
        var green = 0.35f + ((hash >> 8) & 0xFF) / 255f * 0.6f;
        var blue = 0.35f + ((hash >> 16) & 0xFF) / 255f * 0.6f;
        return (red, green, blue);
    }
}

/// <summary>Geometry produced by the mesher.</summary>
/// <param name="Vertices">Interleaved position and colour floats.</param>
/// <param name="Indices">Triangle indices.</param>
/// <param name="BlockCount">Number of solid blocks that contributed faces.</param>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.blaze3d.vertex.MeshData", "ref://mc-26.3-analysis/src/com/mojang/blaze3d/vertex/MeshData.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public sealed record MeshData(float[] Vertices, uint[] Indices, int BlockCount, int[]? SectionStarts = null, SectionVisibility[]? SectionVisibility = null, int SectionMinY = 0)
{
    /// <summary>Vertex count.</summary>
    public int VertexCount => Vertices.Length / ChunkMesher.FloatsPerVertex;

    /// <summary>Triangle count.</summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>Quad count, since greedy meshing emits quads instead of per-block faces.</summary>
    public int QuadCount => Indices.Length / ChunkMesher.IndicesPerQuad;

    /// <summary>Total emitted surface area in square blocks, summed over every quad.</summary>
    public float SurfaceArea => Vertices.Length == 0 ? 0f : SurfaceAreaOf(this);

    /// <summary>An empty mesh.</summary>
    public static MeshData Empty { get; } = new([], [], 0);

    private static float SurfaceAreaOf(MeshData mesh)
    {
        var area = 0f;

        for (var quad = 0; quad < mesh.QuadCount; quad++)
        {
            var baseIndex = quad * ChunkMesher.IndicesPerQuad;
            var a = Position(mesh, mesh.Indices[baseIndex]);
            var b = Position(mesh, mesh.Indices[baseIndex + 1]);
            var c = Position(mesh, mesh.Indices[baseIndex + 2]);
            area += Vector3Area(a, b, c) * 2f;
        }

        return area;
    }

    private static System.Numerics.Vector3 Position(MeshData mesh, uint vertex)
    {
        var offset = (int)vertex * ChunkMesher.FloatsPerVertex;
        return new System.Numerics.Vector3(
            mesh.Vertices[offset],
            mesh.Vertices[offset + 1],
            mesh.Vertices[offset + 2]);
    }

    private static float Vector3Area(
        System.Numerics.Vector3 a,
        System.Numerics.Vector3 b,
        System.Numerics.Vector3 c) =>
        System.Numerics.Vector3.Cross(b - a, c - a).Length() / 2f;
}
