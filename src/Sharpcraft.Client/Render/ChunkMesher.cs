// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Registries;
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
    /// <summary>Floats per vertex: position (3) plus colour (3).</summary>
    public const int FloatsPerVertex = 12;

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
    public static MeshData Mesh(
        ClientWorld world,
        int airState = 0,
        int maxBlocks = 2_000_000,
        BlockSpriteMap? sprites = null,
        bool merge = true,
        bool ambientOcclusion = true)
    {
        ArgumentNullException.ThrowIfNull(world);

        // Merging cuts the quad count by an order of magnitude, so a smaller guess is enough.
        using var buffer = new MeshBuffer(Math.Min(maxBlocks, world.LoadedChunkCount * 512));
        var blocks = Mesh(world, buffer, airState, maxBlocks, sprites, merge, ambientOcclusion);
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
        BlockSpriteMap? sprites = null,
        bool merge = true,
        bool ambientOcclusion = true)
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

            blocks += MeshChunk(world, chunk, airState, buffer, maxBlocks - blocks, sprites, merge: merge, ambientOcclusion: ambientOcclusion);
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
        bool ambientOcclusion = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(buffer);

        var baseX = chunk.ChunkX * 16;
        var baseZ = chunk.ChunkZ * 16;
        var solidBlocks = 0;

        Span<bool> consumed = stackalloc bool[256];

            var neighbourhood = new Neighbourhood(world, chunk);

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
                MergeSectionFaces(neighbourhood, section, direction, baseX, baseY, baseZ, airState, buffer, consumed, sprites, translucent, layout, cutout, tint, cull, visuals, merge, ambientOcclusion);
            }

            if (translucent is not null)
            {
                EmitSectionFluids(neighbourhood, section, baseX, baseY, baseZ, translucent, sprites, layout, tint);
            }
        }

        return solidBlocks;
    }

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
        bool ambientOcclusion = true)
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
                        airState, sprites, layout, cull, out keys[cell]);
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
        Span<bool> consumed)
    {
        for (var du = 0; du < width; du++)
        {
            if (consumed[(v << 4) | (u + du)])
            {
                return false;
            }

            if (!TryDescribe(neighbourhood, section, direction, uAxis, vAxis, slice, u + du, v, baseX, baseY, baseZ, airState, sprites, layout, cull, out var candidate) ||
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
    /// </remarks>
    private readonly struct Neighbourhood
    {
        private readonly LevelChunk _centre;
        private readonly LevelChunk? _west;
        private readonly LevelChunk? _east;
        private readonly LevelChunk? _north;
        private readonly LevelChunk? _south;

        public Neighbourhood(ClientWorld world, LevelChunk centre)
        {
            _centre = centre;
            _west = world.GetChunk(centre.ChunkX - 1, centre.ChunkZ);
            _east = world.GetChunk(centre.ChunkX + 1, centre.ChunkZ);
            _north = world.GetChunk(centre.ChunkX, centre.ChunkZ - 1);
            _south = world.GetChunk(centre.ChunkX, centre.ChunkZ + 1);
        }

        public int GetBlockState(int x, int y, int z, int fallback)
        {
            var chunk = Resolve(x, z);

            return chunk is null ? fallback : chunk.GetBlockState(x & 15, y, z & 15, fallback);
        }

        public bool TryGetLight(int x, int y, int z, out byte sky, out byte block)
        {
            var chunk = Resolve(x, z);

            if (chunk is null)
            {
                sky = 0;
                block = 0;
                return false;
            }

            sky = chunk.GetSkyLight(x & 15, y, z & 15);
            block = chunk.GetBlockLight(x & 15, y, z & 15);
            return true;
        }

        private LevelChunk? Resolve(int x, int z)
        {
            var offsetX = (x >> 4) - _centre.ChunkX;
            var offsetZ = (z >> 4) - _centre.ChunkZ;

            return offsetX switch
            {
                0 => offsetZ switch { 0 => _centre, -1 => _north, 1 => _south, _ => null },
                -1 when offsetZ == 0 => _west,
                1 when offsetZ == 0 => _east,
                _ => null
            };
        }
    }

    /// <summary>What a face contributes to a merge: the block state plus the light that shades it.</summary>
    private readonly record struct MergeKey(int State, byte Sky, byte Block);

    /// <summary>Resolves a (slice, u, v) cell of a face plane into the face it would emit.</summary>
    private static bool TryDescribe(
        in Neighbourhood neighbourhood,
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
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        StateCullTable? cull,
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

        var neighbour = neighbourhood.GetBlockState(sampleX, sampleY, sampleZ, airState);

        if (neighbour != airState && ShouldCullFace(state, neighbour, sprites, layout, cull))
        {
            return false;
        }

        neighbourhood.TryGetLight(sampleX, sampleY, sampleZ, out var sky, out var block);
        key = new MergeKey(state, sky, block);
        return true;
    }

    /// <summary>
    /// True when a block hides the face of its neighbour: fully opaque, so a hole or a blend cannot show through.
    /// </summary>
    /// <param name="state">Neighbouring block state.</param>
    /// <param name="sprites">Sprite map, for the alpha content.</param>
    /// <param name="layout">Layout used to name the state.</param>
    /// <summary>
    /// True when the neighbouring block hides this face.
    /// </summary>
    /// <param name="state">Block owning the face.</param>
    /// <param name="neighbour">Block on the other side.</param>
    /// <param name="sprites">Sprite map, for the alpha content.</param>
    /// <param name="layout">Layout used to name the states.</param>
    /// <remarks>
    /// Follows the reference's per family rules: a fully opaque neighbour always hides the face; leaves keep their inner
    /// faces while fancy leaves are on, which is what makes a canopy read as solid from the inside; glass, ice and fluids
    /// hide only against the same block or fluid
    /// (ref://mc-26.3-analysis/src/net/minecraft/world/level/block/LeavesBlock.java:45,
    /// ref://.../block/HalfTransparentBlock.java:13, ref://.../block/LiquidBlock.java:112).
    /// </remarks>
    private static bool ShouldCullFace(int state, int neighbour, BlockSpriteMap? sprites, BlockStateLayout? layout, StateCullTable? cull = null)
    {
        if (cull is not null)
        {
            if (cull.IsOpaque(neighbour))
            {
                return true;
            }

            if (cull.IsLeaves(state))
            {
                return false;
            }

            return cull.SameFamily(state, neighbour) || state == neighbour;
        }

        if (Occludes(neighbour, sprites, layout))
        {
            return true;
        }

        var selfPath = layout?.BlockAt(state)?.Path;
        var neighbourPath = layout?.BlockAt(neighbour)?.Path;

        if (selfPath is null || neighbourPath is null)
        {
            return state == neighbour;
        }

        // Fancy leaves: a leaf face next to another leaf block is drawn, so the inside of a canopy is visible.
        if (selfPath.EndsWith("_leaves", StringComparison.Ordinal))
        {
            return false;
        }

        // Ice and glass carry noOcclusion in the reference, so a face between two of them is still drawn and the inside of
        // the mass reads through the block instead of turning into a black hole. Fluids are the exception: the fluid
        // renderer culls the shared face between two cells of the same fluid, which keeps a lake from doubling its surfaces.
        var sameFamily = selfPath == neighbourPath;

        return sameFamily && selfPath is "water" or "lava";
    }


    /// <summary>
    /// Lit colour of one quad corner, from smooth lighting and ambient occlusion.
    /// </summary>
    /// <remarks>
    /// The reference computes light per vertex: it averages the light of the four cells that share the corner on the visible
    /// side, then darkens by the occluders around that corner (one quarter per occluder, and half when both sides are
    /// blocked). Doing this per corner instead of once per quad is what removes the flat patches a merged quad used to
    /// show, and it keeps neighbouring quads continuous across chunk borders because the samples are world positions.
    /// </remarks>
    private static (float R, float G, float B) CornerShade(
        in Neighbourhood neighbourhood,
        int direction,
        (int X, int Y, int Z) corner,
        int uAxis,
        int vAxis,
        float shadeFactor,
        float red,
        float green,
        float blue,
        BlockSpriteMap? sprites,
        Sharpcraft.Common.Registries.BlockStateLayout? layout,
        bool ambientOcclusion = true)
    {
        var (nx, ny, nz) = NeighbourOffset(direction);
        Span<int> u = stackalloc int[3];
        u[uAxis] = 1;
        Span<int> v = stackalloc int[3];
        v[vAxis] = 1;

        // `corner` is already a face vertex (the face plane), so the four cells that share it on the outer side sit at
        // corner - (0 or 1) per axis: at corner for a positive normal axis, at corner - 1 for a negative one. Adding the
        // raw normal overshot by one cell on +X/+Y/+Z, so a top face read the light above the block above it and a side
        // face read one block too far out.
        var bx = corner.X + Math.Min(nx, 0);
        var by = corner.Y + Math.Min(ny, 0);
        var bz = corner.Z + Math.Min(nz, 0);

        Span<int> xs = stackalloc int[4] { bx, bx - u[0], bx - v[0], bx - u[0] - v[0] };
        Span<int> ys = stackalloc int[4] { by, by - u[1], by - v[1], by - u[1] - v[1] };
        Span<int> zs = stackalloc int[4] { bz, bz - u[2], bz - v[2], bz - u[2] - v[2] };

        var skySum = 0;
        var blockSum = 0;
        var samples = 0;

        for (var index = 0; index < 4; index++)
        {
            if (neighbourhood.TryGetLight(xs[index], ys[index], zs[index], out var sky, out var block))
            {
                skySum += sky;
                blockSum += block;
                samples++;
            }
        }

        var light = samples == 0
            ? ((byte)0, (byte)0)
            : ((byte)(skySum / samples), (byte)(blockSum / samples));

        // Ambient occlusion: average shade brightness of the four cells that share this vertex on the face's outer side.
        // A full solid cell contributes 0.2, anything else 1.0, so an open corner is 1.0 and a nook drops toward 0.2.
        var occlusion = ambientOcclusion
            ? AmbientOcclusion(
                Occludes(neighbourhood.GetBlockState(xs[0], ys[0], zs[0], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[1], ys[1], zs[1], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[2], ys[2], zs[2], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[3], ys[3], zs[3], 0), sprites, layout))
            : 1f;

        // Vanilla lightmap multiplier (linear RGB) from the averaged light, times the directional shade and AO. The
        // terrain shader decodes the texel, multiplies by this, and re-encodes (docs/specs/render/lighting.md 3-5).
        var lightmap = VanillaLightmap.Colour(light.Item1, light.Item2);
        var scalar = shadeFactor * occlusion;

        return (red * lightmap.X * scalar, green * lightmap.Y * scalar, blue * lightmap.Z * scalar);
    }

    /// <summary>
    /// Ambient-occlusion coefficient for one vertex: the average of the four cells that share it, a full solid cell
    /// counting as 0.2 and anything else as 1.0. Includes the reference corner-softening rule: if both edge cells are
    /// solid the diagonal is treated as occluded by them (use the edge's shade) instead of its own cell, which keeps an
    /// open diagonal from brightening an otherwise enclosed nook.
    /// </summary>
    public static float AmbientOcclusion(bool center, bool sideA, bool sideB, bool diagonal)
    {
        var corner = sideA && sideB ? sideA : diagonal;
        return ((center ? 0.2f : 1f) + (sideA ? 0.2f : 1f) + (sideB ? 0.2f : 1f) + (corner ? 0.2f : 1f)) * 0.25f;
    }

    private static bool Occludes(int state, BlockSpriteMap? sprites, BlockStateLayout? layout)
    {
        // Air never occludes. State 0 is the air state throughout this client (every Mesh/MeshChunk call passes airState 0),
        // and without this guard an air cell the layout could not resolve fell through to "opaque", collapsing the 4-cell AO
        // average to its 0.2 floor on almost every vertex - which darkened the whole world and is why AO-off looked right.
        if (state == 0)
        {
            return false;
        }

        if (sprites is null || layout?.BlockAt(state) is not { } block)
        {
            // Without an atlas or a layout the alpha cannot be read, so treat it as opaque, which is what the caller that
            // passes no atlas expects (untextured geometry is cube shaped in every current test and demo).
            return true;
        }

        // A block that renders in the translucent layer can never fully occlude, whatever its texture alpha looks like:
        // the reference marks ice, water and the glass family noOcclusion for exactly this reason. Treating ice as an
        // occluder culled its neighbours' faces and made the ambient occlusion darken a whole ice field.
        if (BlockRenderLayers.IsTranslucent(block))
        {
            return false;
        }

        var transparency = sprites.TransparencyOf(block);
        return !transparency.HasTransparent && !transparency.HasTranslucent;
    }

    /// <summary>True when a state's block is a fluid (water/lava); fluids are meshed by <see cref="EmitSectionFluids"/>.</summary>
    private static bool IsFluidState(int state, BlockStateLayout? layout) =>
        layout?.BlockAt(state) is { Path: "water" or "lava" };

    /// <summary>
    /// Emits water geometry for one section into the translucent buffer (docs/specs/render/fluid-rendering.md). Faces are
    /// culled against the same fluid (an adjacent water block hides the shared face), so a plain water section emits only
    /// its outer surface rather than every internal face. The top face uses the reference four-corner height average.
    /// </summary>
    private static void EmitSectionFluids(
        in Neighbourhood neighbourhood,
        ChunkSection section,
        int baseX,
        int baseY,
        int baseZ,
        MeshBuffer translucent,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        BiomeTint? tint)
    {
        if (layout is null)
        {
            return;
        }

        var nb = neighbourhood;

        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var state = section.Get(x, y, z);
                    if (layout.BlockAt(state) is not { Path: "water" } fluidBlockRef)
                    {
                        continue;
                    }

                    var fluid = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(state, layout);
                    if (fluid.IsEmpty)
                    {
                        continue;
                    }

                    var bx = baseX + x;
                    var by = baseY + y;
                    var bz = baseZ + z;
                    var fx = (float)bx;
                    var fz = (float)bz;
                    var lowY = (float)by;

                    var aboveWater = layout.BlockAt(nb.GetBlockState(bx, by + 1, bz, 0)) is { Path: "water" };
                    var selfHeight = aboveWater ? 1f : fluid.OwnHeight;

                    nb.TryGetLight(bx, by, bz, out var s0, out var b0);
                    nb.TryGetLight(bx, by + 1, bz, out var s1, out var b1);
                    var lm = VanillaLightmap.Colour(Math.Max(s0, s1), Math.Max(b0, b1));

                    var tr = 1f;
                    var tg = 1f;
                    var tb = 1f;
                    if (tint is not null)
                    {
                        var (r, g, b) = BiomeTint.ToMultiplier(tint.Colour(BlockTintKind.Water));
                        tr = r;
                        tg = g;
                        tb = b;
                    }

                    var blockId = fluidBlockRef;
                    var topSprite = sprites?.ForState(state, BlockFace.Top);
                    var sideSprite = sprites?.ForState(state, BlockFace.Side);

                    var flowX = 0f;
                    var flowZ = 0f;

                    void AddFlow(int ddx, int ddz)
                    {
                        var neighbourState = nb.GetBlockState(bx + ddx, by, bz + ddz, 0);
                        if (layout.BlockAt(neighbourState) is { Path: "water" })
                        {
                            var neighbour = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(neighbourState, layout);
                            var difference = selfHeight - (neighbour.IsEmpty ? 0f : neighbour.OwnHeight);
                            flowX += difference * ddx;
                            flowZ += difference * ddz;
                        }
                    }

                    AddFlow(-1, 0);
                    AddFlow(1, 0);
                    AddFlow(0, -1);
                    AddFlow(0, 1);
                    var flowing = ((flowX * flowX) + (flowZ * flowZ)) > 1e-7f;
                    var flowSprite = flowing ? sprites?.FluidFlow(blockId) : null;

                    (float R, float G, float B) Face(float shade) => (lm.X * tr * shade, lm.Y * tg * shade, lm.Z * tb * shade);

                    bool IsWater(int wx, int wy, int wz) => layout.BlockAt(nb.GetBlockState(wx, wy, wz, 0)) is { Path: "water" };

                    bool IsSolid(int wx, int wy, int wz)
                    {
                        var block = layout.BlockAt(nb.GetBlockState(wx, wy, wz, 0));
                        return block is { } value && value.Path != "air" && value.Path != "water";
                    }

                    float CellHeight(int ddx, int ddz)
                    {
                        var sample = nb.GetBlockState(bx + ddx, by, bz + ddz, 0);
                        if (layout.BlockAt(sample) is { } sampleBlock)
                        {
                            if (sampleBlock.Path == "water")
                            {
                                var neighbour = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(sample, layout);
                                return neighbour.IsEmpty ? 0f : neighbour.OwnHeight;
                            }

                            if (sampleBlock.Path != "air")
                            {
                                return -1f;
                            }
                        }

                        return 0f;
                    }

                    var north = CellHeight(0, -1);
                    var south = CellHeight(0, 1);
                    var west = CellHeight(-1, 0);
                    var east = CellHeight(1, 0);
                    var northWest = CellHeight(-1, -1);
                    var northEast = CellHeight(1, -1);
                    var southWest = CellHeight(-1, 1);
                    var southEast = CellHeight(1, 1);

                    float Corner(int cx, int cz)
                    {
                        var n1 = cx == 0 ? west : east;
                        var n2 = cz == 0 ? north : south;
                        var diagonal = cx == 0 ? (cz == 0 ? northWest : southWest) : (cz == 0 ? northEast : southEast);
                        return FluidSurface.AverageCorner(selfHeight, n1, n2, diagonal);
                    }

                    const float epsilon = 0.001f;

                    // Top: only when no water directly above. Corner heights so the surface follows neighbours.
                    if (!aboveWater)
                    {
                        var colour = Face(1.0f);
                        if (flowSprite is { } moving)
                        {
                            // Flow UV rotated toward the downhill direction (reference: angle atan2(z,x) - pi/2, radius
                            // 0.25 around the sprite centre).
                            var angle = MathF.Atan2(flowZ, flowX) - (MathF.PI / 2f);
                            var s = MathF.Sin(angle) * 0.25f;
                            var c = MathF.Cos(angle) * 0.25f;
                            translucent.AppendQuad(
                                (fx, lowY + Corner(0, 0) - epsilon, fz),
                                (fx, lowY + Corner(0, 1) - epsilon, fz + 1f),
                                (fx + 1f, lowY + Corner(1, 1) - epsilon, fz + 1f),
                                (fx + 1f, lowY + Corner(1, 0) - epsilon, fz),
                                colour, colour, colour, colour,
                                (0.5f + (-c - s), 0.5f + (-c + s)),
                                (0.5f + (-c + s), 0.5f + (c + s)),
                                (0.5f + (c + s), 0.5f + (c - s)),
                                (0.5f + (c - s), 0.5f + (-c - s)),
                                moving);
                        }
                        else
                        {
                            translucent.AppendQuad(
                                (fx, lowY + Corner(0, 0) - epsilon, fz),
                                (fx, lowY + Corner(0, 1) - epsilon, fz + 1f),
                                (fx + 1f, lowY + Corner(1, 1) - epsilon, fz + 1f),
                                (fx + 1f, lowY + Corner(1, 0) - epsilon, fz),
                                colour, colour, colour, colour,
                                (0f, 0f), (0f, 1f), (1f, 1f), (1f, 0f),
                                topSprite);
                        }
                    }

                    // Bottom: only when the cell below is neither water nor solid.
                    if (!IsWater(bx, by - 1, bz) && !IsSolid(bx, by - 1, bz))
                    {
                        var colour = Face(0.5f);
                        var yb = lowY + epsilon;
                        translucent.AppendQuad(
                            (fx, yb, fz), (fx + 1f, yb, fz), (fx + 1f, yb, fz + 1f), (fx, yb, fz + 1f),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }

                    // Side faces use the same corner heights as the top face so the rim is continuous (flat selfHeight here
                    // left a step/gap between the side top edge and the corner-height top face). Corner assignment and the
                    // +/-0.001 inset on the normal axis follow the reference FluidRenderer.
                    var c00 = lowY + Corner(0, 0);
                    var c01 = lowY + Corner(0, 1);
                    var c10 = lowY + Corner(1, 0);
                    var c11 = lowY + Corner(1, 1);
                    var nz = fz + epsilon;
                    var sz = fz + 1f - epsilon;
                    var wx = fx + epsilon;
                    var ex = fx + 1f - epsilon;

                    // -Z (z = bz): corners (0,0) and (1,0)
                    if (!IsWater(bx, by, bz - 1) && !IsSolid(bx, by, bz - 1))
                    {
                        var colour = Face(0.8f);
                        translucent.AppendQuad(
                            (fx, c00, nz), (fx + 1f, c10, nz), (fx + 1f, lowY, nz), (fx, lowY, nz),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }

                    // +Z (z = bz + 1): corners (1,1) and (0,1)
                    if (!IsWater(bx, by, bz + 1) && !IsSolid(bx, by, bz + 1))
                    {
                        var colour = Face(0.8f);
                        translucent.AppendQuad(
                            (fx + 1f, c11, sz), (fx, c01, sz), (fx, lowY, sz), (fx + 1f, lowY, sz),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }

                    // -X (x = bx): corners (0,1) and (0,0)
                    if (!IsWater(bx - 1, by, bz) && !IsSolid(bx - 1, by, bz))
                    {
                        var colour = Face(0.6f);
                        translucent.AppendQuad(
                            (wx, c01, fz + 1f), (wx, c00, fz), (wx, lowY, fz), (wx, lowY, fz + 1f),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }

                    // +X (x = bx + 1): corners (1,0) and (1,1)
                    if (!IsWater(bx + 1, by, bz) && !IsSolid(bx + 1, by, bz))
                    {
                        var colour = Face(0.6f);
                        translucent.AppendQuad(
                            (ex, c10, fz), (ex, c11, fz + 1f), (ex, lowY, fz + 1f), (ex, lowY, fz),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }
                }
            }
        }
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
        // (docs/analysis/render/face-uv.md, ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/FaceBakery.java).
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

        var cornerOrigin = CornerShade(neighbourhood, direction, origin, uAxis, vAxis, shadeFactor, red, green, blue, sprites, layout, ambientOcclusion);
        var cornerA = CornerShade(neighbourhood, direction, a, uAxis, vAxis, shadeFactor, red, green, blue, sprites, layout, ambientOcclusion);
        var cornerB = CornerShade(neighbourhood, direction, b, uAxis, vAxis, shadeFactor, red, green, blue, sprites, layout, ambientOcclusion);
        var cornerC = CornerShade(neighbourhood, direction, c, uAxis, vAxis, shadeFactor, red, green, blue, sprites, layout, ambientOcclusion);

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
                cornerOrigin,
                cornerA,
                cornerB,
                cornerC,
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
            cornerOrigin,
            cornerA,
            cornerB,
            cornerC,
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
