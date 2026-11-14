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
    /// <summary>
    /// Whether a section's palette names any state that draws from element geometry. A direct palette
    /// has no small list to consult, so it is treated as possibly containing one.
    /// </summary>
    private static bool SectionHasElementModel(ChunkSection section, BlockModelSet models)
    {
        var states = section.States;
        if (states.IsDirect)
        {
            return true;
        }

        var palette = states.Palette;
        for (var index = 0; index < palette.Count; index++)
        {
            if (models.Has(palette[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static void EmitSectionModels(
        in Neighbourhood neighbourhood,
        ChunkSection section,
        int baseX,
        int baseY,
        int baseZ,
        MeshBuffer buffer,
        BlockSpriteMap sprites,
        BlockModelSet models)
    {
        var nb = neighbourhood;

        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var state = section.Get(x, y, z);
                    if (models.Quads(state) is not { } quads)
                    {
                        continue;
                    }

                    nb.TryGetLight(baseX + x, baseY + y, baseZ + z, out var sky, out var block);
                    BlockModelSet.Emit(
                        buffer,
                        quads,
                        baseX + x,
                        baseY + y,
                        baseZ + z,
                        (sky / 15f, block / 15f),
                        sprites.ForTexture);
                }
            }
        }
    }

    private static void EmitSectionFluids(
        in Neighbourhood neighbourhood,
        ChunkSection section,
        int baseX,
        int baseY,
        int baseZ,
        MeshBuffer opaque,
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

        // A fluid column's height at one cell, in the units the reference getHeight uses. Coordinates are explicit so the
        // merge prepass below can ask about cells other than the one currently being emitted.
        float CellHeight(int cbx, int cby, int cbz, string cfluid, int ddx, int ddz)
        {
            var sample = nb.GetBlockState(cbx + ddx, cby, cbz + ddz, 0);
            if (layout.BlockAt(sample) is { } sampleBlock)
            {
                if (sampleBlock.Path == cfluid)
                {
                    // The reference getHeight returns 1.0 when the same fluid is directly above, so a flowing
                    // block next to a continuous column must use 1.0 here for its corner to rise and meet it.
                    if (layout.BlockAt(nb.GetBlockState(cbx + ddx, cby + 1, cbz + ddz, 0)) is { } aboveRef && aboveRef.Path == cfluid)
                    {
                        return 1f;
                    }

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

        // The four surface heights of one fluid cell. Coordinates are explicit for the same reason CellHeight takes
        // them: the merge prepass has to ask about cells it is not currently emitting, and sharing this mapping is
        // what keeps the prepass and the emit loop from drifting apart.
        (float K00, float K01, float K10, float K11) CellCorners(int cbx, int cby, int cbz, string cfluid, bool above, float self)
        {
            if (above)
            {
                return (1f, 1f, 1f, 1f);
            }

            var north = CellHeight(cbx, cby, cbz, cfluid, 0, -1);
            var south = CellHeight(cbx, cby, cbz, cfluid, 0, 1);
            var west = CellHeight(cbx, cby, cbz, cfluid, -1, 0);
            var east = CellHeight(cbx, cby, cbz, cfluid, 1, 0);
            var northWest = CellHeight(cbx, cby, cbz, cfluid, -1, -1);
            var northEast = CellHeight(cbx, cby, cbz, cfluid, 1, -1);
            var southWest = CellHeight(cbx, cby, cbz, cfluid, -1, 1);
            var southEast = CellHeight(cbx, cby, cbz, cfluid, 1, 1);

            return (
                FluidSurface.AverageCorner(self, west, north, northWest),
                FluidSurface.AverageCorner(self, west, south, southWest),
                FluidSurface.AverageCorner(self, east, north, northEast),
                FluidSurface.AverageCorner(self, east, south, southEast));
        }

        // Flat, still water tops that agree on block state, light and biome tint collapse into one quad per rectangle.
        // The shader tiles the sprite through the fractional part of uv, so a merged quad samples exactly what the
        // individual ones would; what it saves is the 256 separate top quads a section used to pay for in each of the
        // three reference transparency geometry passes. Only the rectangle is decided here: the emit stays in the main
        // loop so a merged quad goes through the same light, tint, sprite and lava handling as a single one.
        // Corner heights come out of an average over the four neighbours, so a perfectly level surface does not
        // reproduce its own height bit for bit; comparing exactly rejected every ocean cell and the merge never ran.
        // One thousandth of a block is the same magnitude as the depth inset used when emitting, and far below
        // anything a pixel can resolve, so a surface flatter than that is flat for every purpose the merge cares about.
        const float LevelTolerance = 0.001f;
        void MergeWaterTops(int layer, Span<bool> consumed, Span<byte> spanWidth, Span<byte> spanDepth)
        {
            Span<bool> eligible = stackalloc bool[256];
            Span<float> heightOf = stackalloc float[256];
            Span<float> skyOf = stackalloc float[256];
            Span<float> blockOf = stackalloc float[256];
            Span<float> redOf = stackalloc float[256];
            Span<float> greenOf = stackalloc float[256];
            Span<float> blueOf = stackalloc float[256];

            var by = baseY + layer;

            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var cell = (z * 16) + x;
                    var state = section.Get(x, layer, z);
                    if (layout.BlockAt(state) is not { Path: "water" })
                    {
                        continue;
                    }

                    var fluid = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(state, layout);
                    if (fluid.IsEmpty)
                    {
                        continue;
                    }

                    var bx = baseX + x;
                    var bz = baseZ + z;

                    // A cell with the same fluid directly above has no top face to merge.
                    if (layout.BlockAt(nb.GetBlockState(bx, by + 1, bz, 0)) is { Path: "water" })
                    {
                        continue;
                    }

                    var self = fluid.OwnHeight;

                    // Level surfaces only: averaging a slope into one flat quad would flatten a visible slope.
                    var (k00, k01, k10, k11) = CellCorners(bx, by, bz, "water", false, self);
                    if (MathF.Abs(k00 - self) > LevelTolerance || MathF.Abs(k01 - self) > LevelTolerance ||
                        MathF.Abs(k10 - self) > LevelTolerance || MathF.Abs(k11 - self) > LevelTolerance)
                    {
                        continue;
                    }

                    // Flowing water rotates its uv toward the downhill direction, which a merged quad cannot express.
                    if (CellHeight(bx, by, bz, "water", 1, 0) != self || CellHeight(bx, by, bz, "water", -1, 0) != self ||
                        CellHeight(bx, by, bz, "water", 0, 1) != self || CellHeight(bx, by, bz, "water", 0, -1) != self)
                    {
                        continue;
                    }

                    nb.TryGetLight(bx, by, bz, out var s0, out var b0);
                    nb.TryGetLight(bx, by + 1, bz, out var s1, out var b1);

                    var tr = 1f;
                    var tg = 1f;
                    var tb = 1f;
                    if (tint is not null)
                    {
                        var (cr, cg, cb) = BiomeTint.ToMultiplier(tint.ColourAt(BlockTintKind.Water, bx, by, bz));
                        tr = cr;
                        tg = cg;
                        tb = cb;
                    }

                    eligible[cell] = true;
                    heightOf[cell] = self;
                    skyOf[cell] = Math.Max(s0, s1) / 15f;
                    blockOf[cell] = Math.Max(b0, b1) / 15f;
                    redOf[cell] = tr;
                    greenOf[cell] = tg;
                    blueOf[cell] = tb;
                }
            }

            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var origin = (z * 16) + x;
                    if (!eligible[origin] || consumed[origin])
                    {
                        continue;
                    }

                    var originHeight = heightOf[origin];
                    var originSky = skyOf[origin];
                    var originBlock = blockOf[origin];
                    var originRed = redOf[origin];
                    var originGreen = greenOf[origin];
                    var originBlue = blueOf[origin];

                    var w = 1;
                    while (x + w < 16)
                    {
                        var probe = origin + w;
                        if (!eligible[probe] || consumed[probe] || heightOf[probe] != originHeight ||
                            skyOf[probe] != originSky || blockOf[probe] != originBlock ||
                            redOf[probe] != originRed || greenOf[probe] != originGreen || blueOf[probe] != originBlue)
                        {
                            break;
                        }

                        w++;
                    }

                    var d = 1;
                    while (z + d < 16)
                    {
                        var rowFits = true;
                        for (var k = 0; k < w; k++)
                        {
                            var probe = ((z + d) * 16) + x + k;
                            if (!eligible[probe] || consumed[probe] || heightOf[probe] != originHeight ||
                                skyOf[probe] != originSky || blockOf[probe] != originBlock ||
                                redOf[probe] != originRed || greenOf[probe] != originGreen || blueOf[probe] != originBlue)
                            {
                                rowFits = false;
                                break;
                            }
                        }

                        if (!rowFits)
                        {
                            break;
                        }

                        d++;
                    }

                    for (var dz = 0; dz < d; dz++)
                    {
                        for (var dx = 0; dx < w; dx++)
                        {
                            consumed[((z + dz) * 16) + x + dx] = true;
                        }
                    }

                    spanWidth[origin] = (byte)w;
                    spanDepth[origin] = (byte)d;
                }
            }
        }

        Span<bool> mergedTop = stackalloc bool[256];
        Span<byte> mergedWidth = stackalloc byte[256];
        Span<byte> mergedDepth = stackalloc byte[256];

        for (var y = 0; y < 16; y++)
        {
            mergedTop.Clear();
            mergedWidth.Clear();
            mergedDepth.Clear();
            MergeWaterTops(y, mergedTop, mergedWidth, mergedDepth);

            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var state = section.Get(x, y, z);
                    var blockRef = layout.BlockAt(state);
                    if (blockRef is not { } fluidBlockRef ||
                        (fluidBlockRef.Path != "water" && fluidBlockRef.Path != "lava"))
                    {
                        continue;
                    }

                    var fluid = Sharpcraft.Common.World.FluidStateCodec.FromBlockState(state, layout);
                    if (fluid.IsEmpty)
                    {
                        continue;
                    }

                    var fluidPath = fluidBlockRef.Path;
                    var sink = fluidPath == "lava" ? opaque : translucent;
                    var lavaSink = fluidPath == "lava";

                    // Light levels of this fluid cell (set just below, before any Emit call); captured by the local Emit.
                    float skyLevel = 0f;
                    float blockLevel = 0f;

                    void Emit(
                        (float X, float Y, float Z) a, (float X, float Y, float Z) b,
                        (float X, float Y, float Z) c, (float X, float Y, float Z) d,
                        (float R, float G, float B) colourA, (float R, float G, float B) colourB,
                        (float R, float G, float B) colourC, (float R, float G, float B) colourD,
                        (float U, float V) uvA, (float U, float V) uvB, (float U, float V) uvC, (float U, float V) uvD,
                        SpriteRect? sprite)
                    {
                        if (lavaSink)
                        {
                            sink.AppendQuadDoubleSided(a, b, c, d, colourA, colourB, colourC, colourD, (skyLevel, blockLevel), (skyLevel, blockLevel), (skyLevel, blockLevel), (skyLevel, blockLevel), uvA, uvB, uvC, uvD, sprite);
                        }
                        else
                        {
                            sink.AppendQuad(a, b, c, d, colourA, colourB, colourC, colourD, (skyLevel, blockLevel), (skyLevel, blockLevel), (skyLevel, blockLevel), (skyLevel, blockLevel), uvA, uvB, uvC, uvD, sprite);
                        }
                    }

                    var bx = baseX + x;
                    var by = baseY + y;
                    var bz = baseZ + z;
                    var fx = (float)bx;
                    var fz = (float)bz;
                    var lowY = (float)by;

                    var aboveSame = layout.BlockAt(nb.GetBlockState(bx, by + 1, bz, 0)) is { } aboveBlockRef && aboveBlockRef.Path == fluidPath;
                    var selfHeight = aboveSame ? 1f : fluid.OwnHeight;

                    nb.TryGetLight(bx, by, bz, out var s0, out var b0);
                    nb.TryGetLight(bx, by + 1, bz, out var s1, out var b1);
                    // Light levels go to the shader; the lightmap is computed per fragment so day/night needs no re-mesh.
                    skyLevel = Math.Max(s0, s1) / 15f;
                    blockLevel = Math.Max(b0, b1) / 15f;

                    var tr = 1f;
                    var tg = 1f;
                    var tb = 1f;
                    if (fluidPath != "lava" && tint is not null)
                    {
                        var (r, g, b) = BiomeTint.ToMultiplier(tint.ColourAt(BlockTintKind.Water, bx, by, bz));
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
                        if (layout.BlockAt(neighbourState) is { } flowBlockRef && flowBlockRef.Path == fluidPath)
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

                    (float R, float G, float B) Face(float shade) => (tr * shade, tg * shade, tb * shade);

                    bool IsSameFluid(int wx, int wy, int wz) => layout.BlockAt(nb.GetBlockState(wx, wy, wz, 0)) is { } sameRef && sameRef.Path == fluidPath;

                    // A fluid face is hidden only by a block that actually occludes light. A translucent neighbour (the
                    // glass family, ice) must NOT hide it: the reference FluidRenderer keeps a fluid's side face unless the
                    // neighbour is the same fluid, so water behind glass stays visible. Culling against any non-air block
                    // (the old name-based test) dropped the water face in front of glass and made the water vanish.
                    bool IsOccluding(int wx, int wy, int wz)
                    {
                        var neighbourState = nb.GetBlockState(wx, wy, wz, 0);
                        if (neighbourState == 0 || layout?.BlockAt(neighbourState) is not { } neighbourBlock)
                        {
                            return neighbourState != 0;
                        }

                        // Explicit translucent materials (glass, ice, fluids) never occlude, whatever the atlas says.
                        if (BlockRenderLayers.IsTranslucent(neighbourBlock))
                        {
                            return false;
                        }

                        if (sprites?.TransparencyOf(neighbourBlock) is not { } transparency)
                        {
                            return true;
                        }

                        return !transparency.HasTransparent && !transparency.HasTranslucent;
                    }
                    var (k00, k01, k10, k11) = CellCorners(bx, by, bz, fluidPath, aboveSame, selfHeight);

                    const float epsilon = 0.001f;

                    // Top: only when no water directly above. Corner heights so the surface follows neighbours.
                    var mergeCell = (z * 16) + x;
                    var meetWidth = mergedWidth[mergeCell];
                    var meetDepth = mergedDepth[mergeCell];

                    // A covered cell emits nothing here unless it is the rectangle origin, which is the one place the
                    // merged quad is written.
                    if (!aboveSame && (meetWidth > 0 || !mergedTop[mergeCell]))
                    {
                        var colour = Face(1.0f);
                        if (meetWidth > 0)
                        {
                            // Every corner of every covered cell equals the surface height, so the rectangle is flat and
                            // one quad whose uv runs to the rectangle bounds tiles the sprite exactly as the individual
                            // quads did.
                            Emit(
                                (fx, lowY + k00 - epsilon, fz),
                                (fx, lowY + k00 - epsilon, fz + meetDepth),
                                (fx + meetWidth, lowY + k00 - epsilon, fz + meetDepth),
                                (fx + meetWidth, lowY + k00 - epsilon, fz),
                                colour, colour, colour, colour,
                                (0f, 0f), (0f, meetDepth), (meetWidth, meetDepth), (meetWidth, 0f),
                                topSprite);
                        }
                        else if (flowSprite is { } moving)
                        {
                            // Flow UV rotated toward the downhill direction (reference: angle atan2(z,x) - pi/2, radius
                            // 0.25 around the sprite centre).
                            var angle = MathF.Atan2(flowZ, flowX) - (MathF.PI / 2f);
                            var s = MathF.Sin(angle) * 0.25f;
                            var c = MathF.Cos(angle) * 0.25f;
                            Emit(
                                (fx, lowY + k00 - epsilon, fz),
                                (fx, lowY + k01 - epsilon, fz + 1f),
                                (fx + 1f, lowY + k11 - epsilon, fz + 1f),
                                (fx + 1f, lowY + k10 - epsilon, fz),
                                colour, colour, colour, colour,
                                (0.5f + (-c - s), 0.5f + (-c + s)),
                                (0.5f + (-c + s), 0.5f + (c + s)),
                                (0.5f + (c + s), 0.5f + (c - s)),
                                (0.5f + (c - s), 0.5f + (-c - s)),
                                moving);
                        }
                        else
                        {
                            Emit(
                                (fx, lowY + k00 - epsilon, fz),
                                (fx, lowY + k01 - epsilon, fz + 1f),
                                (fx + 1f, lowY + k11 - epsilon, fz + 1f),
                                (fx + 1f, lowY + k10 - epsilon, fz),
                                colour, colour, colour, colour,
                                (0f, 0f), (0f, 1f), (1f, 1f), (1f, 0f),
                                topSprite);
                        }
                    }

                    // Bottom: only when the cell below is neither water nor solid.
                    if (!IsSameFluid(bx, by - 1, bz) && !IsOccluding(bx, by - 1, bz))
                    {
                        var colour = Face(0.5f);
                        var yb = lowY + epsilon;
                        Emit(
                            (fx, yb, fz), (fx + 1f, yb, fz), (fx + 1f, yb, fz + 1f), (fx, yb, fz + 1f),
                            colour, colour, colour, colour,
                            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f),
                            sideSprite);
                    }

                    // Side faces use the same corner heights as the top face so the rim is continuous, and (as in the
                    // reference FluidRenderer) sample the flowing sprite with V mapped to the exposed height so the texture
                    // reads as water falling down the drop. Corner assignment and the +/-0.001 inset follow the reference.
                    var c00 = lowY + k00;
                    var c01 = lowY + k01;
                    var c10 = lowY + k10;
                    var c11 = lowY + k11;
                    var nz = fz + epsilon;
                    var sz = fz + 1f - epsilon;
                    var wx = fx + epsilon;
                    var ex = fx + 1f - epsilon;
                    var sideFlowSprite = sprites?.FluidFlow(blockId) ?? sideSprite;

                    // -Z (z = bz): corners (0,0) and (1,0)
                    if (!IsSameFluid(bx, by, bz - 1) && !IsOccluding(bx, by, bz - 1))
                    {
                        var colour = Face(0.8f);
                        Emit(
                            (fx, c00, nz), (fx + 1f, c10, nz), (fx + 1f, lowY, nz), (fx, lowY, nz),
                            colour, colour, colour, colour,
                            (0f, (1f - k00) * 0.5f),
                            (0.5f, (1f - k10) * 0.5f),
                            (0.5f, 0.5f),
                            (0f, 0.5f),
                            sideFlowSprite);
                    }

                    // +Z (z = bz + 1): corners (1,1) and (0,1)
                    if (!IsSameFluid(bx, by, bz + 1) && !IsOccluding(bx, by, bz + 1))
                    {
                        var colour = Face(0.8f);
                        Emit(
                            (fx + 1f, c11, sz), (fx, c01, sz), (fx, lowY, sz), (fx + 1f, lowY, sz),
                            colour, colour, colour, colour,
                            (0f, (1f - k11) * 0.5f),
                            (0.5f, (1f - k01) * 0.5f),
                            (0.5f, 0.5f),
                            (0f, 0.5f),
                            sideFlowSprite);
                    }

                    // -X (x = bx): corners (0,1) and (0,0)
                    if (!IsSameFluid(bx - 1, by, bz) && !IsOccluding(bx - 1, by, bz))
                    {
                        var colour = Face(0.6f);
                        Emit(
                            (wx, c01, fz + 1f), (wx, c00, fz), (wx, lowY, fz), (wx, lowY, fz + 1f),
                            colour, colour, colour, colour,
                            (0f, (1f - k01) * 0.5f),
                            (0.5f, (1f - k00) * 0.5f),
                            (0.5f, 0.5f),
                            (0f, 0.5f),
                            sideFlowSprite);
                    }

                    // +X (x = bx + 1): corners (1,0) and (1,1)
                    if (!IsSameFluid(bx + 1, by, bz) && !IsOccluding(bx + 1, by, bz))
                    {
                        var colour = Face(0.6f);
                        Emit(
                            (ex, c10, fz), (ex, c11, fz + 1f), (ex, lowY, fz + 1f), (ex, lowY, fz),
                            colour, colour, colour, colour,
                            (0f, (1f - k10) * 0.5f),
                            (0.5f, (1f - k11) * 0.5f),
                            (0.5f, 0.5f),
                            (0f, 0.5f),
                            sideFlowSprite);
                    }
                }
            }
        }
    }
}
