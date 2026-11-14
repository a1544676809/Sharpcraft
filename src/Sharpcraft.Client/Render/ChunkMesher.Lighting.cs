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


public static partial class ChunkMesher
{
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
        BlockModelSet? models,
        out MergeKey key)
    {
        key = default;

        var (x, y, z) = FaceBlock(uAxis, vAxis, slice, u, v);
        var state = section.Get(x, y, z);
        if (models is not null && models.Has(state))
        {
            return false;
        }

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
    private static (float R, float G, float B, float Sky, float Block) CornerShade(
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

        Span<byte> skySamples = stackalloc byte[4];
        Span<byte> blockSamples = stackalloc byte[4];
        var samples = 0;

        for (var index = 0; index < 4; index++)
        {
            if (neighbourhood.TryGetLight(xs[index], ys[index], zs[index], out var sampleSky, out var sampleBlock))
            {
                skySamples[samples] = sampleSky;
                blockSamples[samples] = sampleBlock;
                samples++;
            }
        }

        (int Sky, int Block) light;
        if (samples == 4)
        {
            // Reference LightCoordsUtil.smoothBlend for a full cube face: three neighbours + centre with the
            // zero-neighbour correction (docs/analysis/render/lighting-color-space.md #3). Index 0 is the centre cell.
            light = SmoothLighting.Blend(
                skySamples[0], blockSamples[0],
                skySamples[1], blockSamples[1],
                skySamples[2], blockSamples[2],
                skySamples[3], blockSamples[3]);
        }
        else if (samples == 0)
        {
            light = (0, 0);
        }
        else
        {
            var skySum = 0;
            var blockSum = 0;
            for (var index = 0; index < samples; index++)
            {
                skySum += skySamples[index];
                blockSum += blockSamples[index];
            }

            light = (skySum / samples, blockSum / samples);
        }

        // Ambient occlusion: average shade brightness of the four cells that share this vertex on the face's outer side.
        // A full solid cell contributes 0.2, anything else 1.0, so an open corner is 1.0 and a nook drops toward 0.2.
        var occlusion = ambientOcclusion
            ? AmbientOcclusion(
                Occludes(neighbourhood.GetBlockState(xs[0], ys[0], zs[0], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[1], ys[1], zs[1], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[2], ys[2], zs[2], 0), sprites, layout),
                Occludes(neighbourhood.GetBlockState(xs[3], ys[3], zs[3], 0), sprites, layout))
            : 1f;

        // Base colour = biome tint × directional shade × AO (linear). The lightmap is computed per fragment in the shader
        // from the light levels carried alongside, so a day/night change needs no re-mesh (docs/decisions/0019).
        var scalar = shadeFactor * occlusion;
        return (red * scalar, green * scalar, blue * scalar, light.Item1 / 15f, light.Item2 / 15f);
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

    /// <summary>Resolves which faces of one 16³ section can see each other (T-833).</summary>
    /// <remarks>
    /// Without an atlas <see cref="Occludes"/> answers "solid" for everything, so a section of ordinary blocks would
    /// look like solid rock and the traversal would cull the whole world. Reporting everything visible is the safe
    /// answer when the information is missing.
    /// </remarks>
    private static SectionVisibility ResolveSectionVisibility(
        in Neighbourhood neighbourhood,
        int baseX,
        int baseY,
        int baseZ,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        Span<ulong> blocking,
        Span<ulong> visited,
        Span<ushort> queue)
    {
        if (sprites is null || layout is null)
        {
            return SectionVisibility.AllVisible;
        }

        blocking.Clear();

        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    if (!Occludes(neighbourhood.GetBlockState(baseX + x, baseY + y, baseZ + z, 0), sprites, layout))
                    {
                        continue;
                    }

                    var cell = x | (z << 4) | (y << 8);
                    blocking[cell >> 6] |= 1UL << (cell & 63);
                }
            }
        }

        return SectionVisibilityCompute.Compute(blocking, visited, queue);
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

    /// <summary>
    /// True when a state's block is a fluid (water or lava), which <see cref="EmitSectionFluids"/> meshes with proper
    /// surface geometry: water into the translucent buffer, lava into the opaque buffer.
    /// </summary>
    private static bool IsFluidState(int state, BlockStateLayout? layout) =>
        layout?.BlockAt(state) is { Path: "water" or "lava" };

    /// <summary>
    /// Emits fluid geometry for one section (docs/specs/render/fluid-rendering.md). Water and lava share one algorithm
    /// (reference FluidRenderer.tesselate): same-fluid face culling, four-corner surface heights, flow UV. Only the target
    /// layer differs - water is translucent, lava is opaque (reference: the fluid model's layer) - plus water's biome tint.
    /// </summary>
    /// <summary>
    /// Emits blocks whose model is built from element boxes. They cannot join the cube face merge
    /// because their faces are not cube faces, so they are written on their own after it.
    /// </summary>
}
