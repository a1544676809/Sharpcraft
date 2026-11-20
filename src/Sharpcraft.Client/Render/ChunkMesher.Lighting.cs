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
    internal readonly struct Neighbourhood
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

            // A merged rectangle samples one cell outside its own bounds, which at the top or bottom of
            // the world leaves the vertical range: asking for light there used to index past the
            // section array and abort the whole chunk mesh.
            if (y < chunk.MinSectionY * 16 || y > (chunk.MaxSectionY * 16) + 15)
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
