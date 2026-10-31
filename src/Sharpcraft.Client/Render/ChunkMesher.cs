// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.World;

using Sharpcraft.Client.Assets;

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
public static class ChunkMesher
{
    /// <summary>Diagnostic switch: emit full brightness vertices to isolate shading from geometry.</summary>
    private static readonly bool NoShade = Environment.GetEnvironmentVariable("MC_FRAME_NO_SHADE") == "1";

    /// <summary>Floats per vertex: position (3) plus colour (3).</summary>
    public const int FloatsPerVertex = 12;

    /// <summary>Vertices per quad.</summary>
    public const int VerticesPerQuad = 4;

    /// <summary>Indices per quad (two triangles).</summary>
    public const int IndicesPerQuad = 6;

    /// <summary>Darkest a face can get; keeps unlit caves legible instead of pure black.</summary>
    public const float MinimumBrightness = 0.12f;

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
    public static MeshData Mesh(
        ClientWorld world,
        int airState = 0,
        int maxBlocks = 2_000_000,
        BlockSpriteMap? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        // Merging cuts the quad count by an order of magnitude, so a smaller guess is enough.
        using var buffer = new MeshBuffer(Math.Min(maxBlocks, world.LoadedChunkCount * 512));
        var blocks = Mesh(world, buffer, airState, maxBlocks, sprites);
        return buffer.ToMeshData(blocks);
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
        BlockSpriteMap? sprites = null)
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

            blocks += MeshChunk(world, chunk, airState, buffer, maxBlocks - blocks, sprites);
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
        BiomeTint? tint = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(buffer);

        var baseX = chunk.ChunkX * 16;
        var baseZ = chunk.ChunkZ * 16;
        var solidBlocks = 0;

        Span<bool> consumed = stackalloc bool[256];

        for (var sectionIndex = 0; sectionIndex < chunk.SectionCount; sectionIndex++)
        {
            var section = chunk.GetSection(chunk.MinSectionY + sectionIndex);
            if (section is null || section.IsEmpty)
            {
                continue;
            }

            if (solidBlocks >= maxBlocks)
            {
                return solidBlocks;
            }

            var baseY = (chunk.MinSectionY + sectionIndex) * 16;
            solidBlocks += section.NonAirCount;

            for (var direction = 0; direction < 6; direction++)
            {
                consumed.Clear();
                MergeSectionFaces(world, section, direction, baseX, baseY, baseZ, airState, buffer, consumed, sprites, translucent, layout, cutout, tint);
            }
        }

        return solidBlocks;
    }

    /// <summary>Merges the faces of one direction inside one section into as few quads as possible.</summary>
    private static void MergeSectionFaces(
        ClientWorld world,
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
        BiomeTint? tint = null)
    {
        var (uAxis, vAxis) = InPlaneAxes(direction);

        for (var slice = 0; slice < 16; slice++)
        {
            // The consumed mask is per plane: sharing it across slices would suppress cells in
            // later planes and emit quads from the wrong slice.
            consumed.Clear();

            for (var v = 0; v < 16; v++)
            {
                for (var u = 0; u < 16; u++)
                {
                    if (consumed[(v << 4) | u])
                    {
                        continue;
                    }

                    if (!TryDescribe(world, section, direction, uAxis, vAxis, slice, u, v, baseX, baseY, baseZ, airState, out var key))
                    {
                        continue;
                    }

                    var width = 1;
                    while (u + width < 16 &&
                           !consumed[(v << 4) | (u + width)] &&
                           TryDescribe(world, section, direction, uAxis, vAxis, slice, u + width, v, baseX, baseY, baseZ, airState, out var next) &&
                           next == key)
                    {
                        width++;
                    }

                    var height = 1;
                    while (v + height < 16 &&
                           RowMatches(world, section, direction, uAxis, vAxis, slice, u, v + height, width, baseX, baseY, baseZ, airState, key, consumed))
                    {
                        height++;
                    }

                    for (var dv = 0; dv < height; dv++)
                    {
                        for (var du = 0; du < width; du++)
                        {
                            consumed[((v + dv) << 4) | (u + du)] = true;
                        }
                    }

                    EmitMergedQuad(world, buffer, direction, slice, u, v, width, height, baseX, baseY, baseZ, key.State, sprites, translucent, layout, cutout, tint);
                }
            }
        }
    }

    private static bool RowMatches(
        ClientWorld world,
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
        MergeKey key,
        Span<bool> consumed)
    {
        for (var du = 0; du < width; du++)
        {
            if (consumed[(v << 4) | (u + du)])
            {
                return false;
            }

            if (!TryDescribe(world, section, direction, uAxis, vAxis, slice, u + du, v, baseX, baseY, baseZ, airState, out var candidate) ||
                candidate != key)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>What a face contributes to a merge: the block state plus the light that shades it.</summary>
    private readonly record struct MergeKey(int State, byte Sky, byte Block);

    /// <summary>Resolves a (slice, u, v) cell of a face plane into the face it would emit.</summary>
    private static bool TryDescribe(
        ClientWorld world,
        ChunkSection section,
        int direction,
        int uAxis,
        int vAxis,
        int slice,
        int u,
        int v,
        int baseX,
        int baseY,
        int baseZ,
        int airState,
        out MergeKey key)
    {
        key = default;

        var (x, y, z) = FaceBlock(uAxis, vAxis, slice, u, v);
        var state = section.Get(x, y, z);
        if (state == airState)
        {
            return false;
        }

        var (dx, dy, dz) = NeighbourOffset(direction);
        var sampleX = baseX + x + dx;
        var sampleY = baseY + y + dy;
        var sampleZ = baseZ + z + dz;

        if (world.GetBlockState(sampleX, sampleY, sampleZ, airState) != airState)
        {
            return false;
        }

        world.TryGetLight(sampleX, sampleY, sampleZ, out var sky, out var block);
        key = new MergeKey(state, sky, block);
        return true;
    }

    private static (int X, int Y, int Z) FaceBlock(int uAxis, int vAxis, int slice, int u, int v)
    {
        Span<int> coordinates = stackalloc int[3];
        coordinates[uAxis] = u;
        coordinates[vAxis] = v;
        coordinates[3 - uAxis - vAxis] = slice;
        return (coordinates[0], coordinates[1], coordinates[2]);
    }

    /// <summary>The two in-plane axes used for merging, per face direction.</summary>
    private static (int U, int V) InPlaneAxes(int direction) => direction switch
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
        ClientWorld world,
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
        BiomeTint? tint = null)
    {
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
        (float U, float V) Uv((int X, int Y, int Z) corner) => direction switch
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

        if (NoShade)
        {
            // Diagnostic: force full brightness to tell "no geometry" from "shaded black".
            red = 1f;
            green = 1f;
            blue = 1f;
        }

        if (tint is not null && layout?.BlockAt(state) is { } tintedBlock)
        {
            // The reference tints the faces whose model carries a tint index; grass tints only its top, leaves and plants
            // tint every face.
            var kind = BlockTintTable.KindOf(tintedBlock);

            if (kind != BlockTintKind.None && (!BlockTintTable.TintsTopFaceOnly(kind) || direction == PositiveY))
            {
                var (tintRed, tintGreen, tintBlue) = BiomeTint.ToMultiplier(tint.Colour(kind));
                red *= tintRed;
                green *= tintGreen;
                blue *= tintBlue;
            }
        }
        var (dx, dy, dz) = NeighbourOffset(direction);
        var shadeFactor = direction switch
        {
            PositiveY => TopShade,
            NegativeY => BottomShade,
            _ => SideShade
        };

        var light = world.TryGetLight(baseX + x + dx, baseY + y + dy, baseZ + z + dz, out var sky, out var block)
            ? (sky, block)
            : world.TryGetLight(baseX + x, baseY + y, baseZ + z, out var selfSky, out var selfBlock)
                ? (selfSky, selfBlock)
                : ((byte)0, (byte)0);

        var shade = Brightness(light) * shadeFactor;

        // Corner order follows the winding: where U came first the horizontal corner comes first,
        // where V came first the vertical one does. UVs stay in sprite repeats so a merged quad
        // tiles its texture instead of stretching it.
        // Texture axes per face, taken from the reference model baker: it derives each face's coordinates from the
        // world axes with a fixed flip per direction. Down is lower on the screen for every side face, and the
        // horizontal axis runs opposite between opposing sides, which is what makes a wrapped texture read the
        // same way round as the original rather than mirrored on half the faces
        // (docs/analysis/render/face-uv.md, ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/FaceBakery.java).
        (float U, float V) uvOrigin = Uv((origin.X - baseX, origin.Y - baseY, origin.Z - baseZ));
        (float U, float V) uvA = Uv((a.X - baseX, a.Y - baseY, a.Z - baseZ));
        (float U, float V) uvB = Uv((b.X - baseX, b.Y - baseY, b.Z - baseZ));
        (float U, float V) uvC = Uv((c.X - baseX, c.Y - baseY, c.Z - baseZ));

        var target = buffer;

        if (translucent is not null)
        {
            // The layer decides the target buffer, exactly as the reference picks a chunk section layer per material. It is
            // decided per face from that face's own sprite: a block can have an opaque top and holed sides, and a holed
            // sprite drawn by the solid pipeline shows its holes as opaque black.
            var face = FaceOf(direction);
            var faceSprite = sprites?.ForState(state, face);
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

        target.AppendQuad(
            origin,
            a,
            b,
            c,
            red * shade,
            green * shade,
            blue * shade,
            uvOrigin,
            uvA,
            uvB,
            uvC,
            sprites?.ForState(state, FaceOf(direction)));
    }

    /// <summary>Maps a face direction to the atlas face bucket a block uses.</summary>
    private static BlockFace FaceOf(int direction) => direction switch
    {
        PositiveY => BlockFace.Top,
        NegativeY => BlockFace.Bottom,
        _ => BlockFace.Side
    };

    /// <summary>
    /// Brightness of a cell from its light levels. Sky and block light are combined with max,
    /// normalised over 0..15 and floored, which is close enough to the vanilla lightmap for a
    /// first pass without claiming to match it exactly.
    /// </summary>
    public static float Brightness((byte Sky, byte Block) light)
    {
        var level = light.Sky > light.Block ? light.Sky : light.Block;
        return MinimumBrightness + (1f - MinimumBrightness) * (level / 15f);
    }

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
public sealed record MeshData(float[] Vertices, uint[] Indices, int BlockCount)
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
