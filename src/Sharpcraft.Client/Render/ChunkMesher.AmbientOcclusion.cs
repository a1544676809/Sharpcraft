// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per-quad ambient occlusion and smooth lighting, following the reference block model lighter
/// (ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/BlockModelLighter.java, prepareQuadAmbientOcclusion).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps two things apart and smooths them separately: an occlusion <em>brightness</em> that multiplies
/// the vertex colour, and <em>light coordinates</em> that become the lightmap lookup. The implementation this replaces
/// averaged four cells into one scalar per corner and multiplied that by a hand-rolled 0.2/1.0 occlusion term, which is
/// the pre-1.15 shape of the algorithm: it agrees with the reference on a plain corner and disagrees on a diagonal step,
/// because it never blends a corner's own shade with its diagonal.
/// </para>
/// <para>
/// Tables are transcribed from the reference and re-checked by tests that rebuild them from the reference's own rules.
/// Rows are in our direction order (-X, +X, -Y, +Y, -Z, +Z), so a face's row is at <c>direction * 4</c>.
/// </para>
/// </remarks>
public static partial class ChunkMesher
{
    /// <summary>How many size entries the weight tables index.</summary>
    public const int SizeInfoCount = 12;

    // SizeInfo indices, ref BlockModelLighter.java:699-713: the six axes then their FLIP_ mirror.
    private const int SizeDown = 0;
    private const int SizeUp = 1;
    private const int SizeNorth = 2;
    private const int SizeSouth = 3;
    private const int SizeWest = 4;
    private const int SizeEast = 5;
    private const int SizeFlipDown = 6;
    private const int SizeFlipUp = 7;
    private const int SizeFlipNorth = 8;
    private const int SizeFlipSouth = 9;
    private const int SizeFlipWest = 10;
    private const int SizeFlipEast = 11;

    /// <summary>
    /// Bounds of the full cube face of one direction, which is what a merged cube quad always covers.
    /// </summary>
    /// <remarks>
    /// The bounds are the coordinate ranges of the quad, so the axis the face looks along has min equal to max at the
    /// block edge. That equality is what the reference tests for <c>faceCubic</c>
    /// (ref BlockModelLighter.java:264-272).
    /// </remarks>
    public static (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) CubeFaceBoundsOf(int direction) =>
        direction switch
        {
            NegativeY => (0f, 1f, 0f, 0f, 0f, 1f),
            PositiveY => (0f, 1f, 1f, 1f, 0f, 1f),
            NegativeZ => (0f, 1f, 0f, 1f, 0f, 0f),
            PositiveZ => (0f, 1f, 0f, 1f, 1f, 1f),
            NegativeX => (0f, 0f, 0f, 1f, 0f, 1f),
            _ => (1f, 1f, 0f, 1f, 0f, 1f)
        };

    /// <summary>
    /// The four corner neighbour directions of each face, as our direction constants.
    /// </summary>
    /// <remarks>
    /// ref BlockModelLighter.java:289 (DOWN), 334 (UP), 379 (NORTH), 424 (SOUTH), 469 (WEST), 514 (EAST).
    /// </remarks>
    private static ReadOnlySpan<byte> CornerNeighbours =>
    [
        3, 2, 4, 5,   // -X west:  up, down, north, south
        2, 3, 4, 5,   // +X east:  down, up, north, south
        0, 1, 4, 5,   // -Y down:  west, east, north, south
        1, 0, 4, 5,   // +Y up:    east, west, north, south
        3, 2, 1, 0,   // -Z north: up, down, east, west
        0, 1, 2, 3    // +Z south: west, east, down, up
    ];

    /// <summary>
    /// The eight size-entry indices each computed vertex weights its four corner shades by: entry 0 pairs with 1,
    /// 2 with 3, 4 with 5 and 6 with 7, and the four products are the weights of corners (3,0), (2,0), (2,1), (3,1).
    /// </summary>
    /// <remarks>
    /// ref BlockModelLighter.java:292-331 (DOWN), 337-376 (UP), 382-421 (NORTH), 427-466 (SOUTH), 472-511 (WEST),
    /// 517-556 (EAST).
    /// </remarks>
    private static ReadOnlySpan<byte> CornerWeights =>
    [
        1, 3, 1, 9, 7, 9, 7, 3,       // -X west, vertex 0
        1, 2, 1, 8, 7, 8, 7, 2,       //   vertex 1
        0, 2, 0, 8, 6, 8, 6, 2,       //   vertex 2
        0, 3, 0, 9, 6, 9, 6, 3,       //   vertex 3
        6, 3, 6, 9, 0, 9, 0, 3,       // +X east
        6, 2, 6, 8, 0, 8, 0, 2,
        7, 2, 7, 8, 1, 8, 1, 2,
        7, 3, 7, 9, 1, 9, 1, 3,
        10, 3, 10, 9, 4, 9, 4, 3,     // -Y down
        10, 2, 10, 8, 4, 8, 4, 2,
        11, 2, 11, 8, 5, 8, 5, 2,
        11, 3, 11, 9, 5, 9, 5, 3,
        5, 3, 5, 9, 11, 9, 11, 3,     // +Y up
        5, 2, 5, 8, 11, 8, 11, 2,
        4, 2, 4, 8, 10, 8, 10, 2,
        4, 3, 4, 9, 10, 9, 10, 3,
        1, 10, 1, 4, 7, 4, 7, 10,     // -Z north
        1, 11, 1, 5, 7, 5, 7, 11,
        0, 11, 0, 5, 6, 5, 6, 11,
        0, 10, 0, 4, 6, 4, 6, 10,
        1, 10, 7, 10, 7, 4, 1, 4,     // +Z south
        0, 10, 6, 10, 6, 4, 0, 4,
        0, 11, 6, 11, 6, 5, 0, 5,
        1, 11, 7, 11, 7, 5, 1, 5
    ];

    /// <summary>The reference vertex remap: which baked-quad vertex each computed corner lands on.</summary>
    /// <remarks>ref BlockModelLighter.java:597-602.</remarks>
    private static ReadOnlySpan<byte> FaceVertexRemap =>
    [
        3, 0, 1, 2,   // -X west
        1, 2, 3, 0,   // +X east
        0, 1, 2, 3,   // -Y down
        2, 3, 0, 1,   // +Y up
        3, 0, 1, 2,   // -Z north
        0, 1, 2, 3    // +Z south
    ];

    /// <summary>Our quad corner slot for each baked-quad vertex, which the reference orders by FaceInfo.</summary>
    /// <remarks>
    /// ref://mc-26.3-analysis/src/net/minecraft/client/renderer/FaceInfo.java (the four VertexInfo extents per face).
    /// Our corners are ordered by the merge's UV axes, so this names the same four world corners in our order;
    /// <c>Repro_255_AmbientOcclusionTests</c> checks it against FaceInfo and against the emitted vertex positions.
    /// </remarks>
    private static ReadOnlySpan<byte> QuadCornerForFaceVertex =>
    [
        3, 0, 1, 2,   // -X west
        2, 3, 0, 1,   // +X east
        3, 0, 1, 2,   // -Y down
        0, 1, 2, 3,   // +Y up
        2, 3, 0, 1,   // -Z north
        3, 0, 1, 2    // +Z south
    ];

    /// <summary>Corner table row used by tests and diagnostics: the four neighbour directions of a face.</summary>
    /// <param name="direction">Face direction constant.</param>
    public static ReadOnlySpan<byte> CornerDirectionsOf(int direction) => CornerNeighbours.Slice(direction * 4, 4);

    /// <summary>The eight weight indices of one computed vertex.</summary>
    /// <param name="direction">Face direction constant.</param>
    /// <param name="vertex">Computed vertex 0..3.</param>
    public static ReadOnlySpan<byte> VertexWeightsOf(int direction, int vertex) =>
        CornerWeights.Slice((direction * 32) + (vertex * 8), 8);

    /// <summary>The baked-quad vertex each computed corner lands on.</summary>
    /// <param name="direction">Face direction constant.</param>
    public static ReadOnlySpan<byte> VertexRemapOf(int direction) => FaceVertexRemap.Slice(direction * 4, 4);

    /// <summary>Our corner slot for each baked-quad vertex of a direction.</summary>
    /// <param name="direction">Face direction constant.</param>
    public static ReadOnlySpan<byte> QuadCornerOrderOf(int direction) => QuadCornerForFaceVertex.Slice(direction * 4, 4);

    /// <summary>
    /// The reference's <c>doNonCubicWeight</c> flag. All six faces carry true in 26.3, so the partial-face branch is
    /// chosen by the quad's shape alone; the flag stays a function so a version bump that changes it is a compile-time
    /// visible edit rather than a silent behaviour change.
    /// </summary>
    /// <param name="direction">Face direction constant.</param>
    /// <remarks>ref BlockModelLighter.java:291, 336, 381, 426, 471, 516.</remarks>
    public static bool NonCubicWeight(int direction) => direction is >= 0 and <= 5;

    /// <summary>A corner's contribution: occlusion brightness plus both light channels on the reference's 0..240 scale.</summary>
    internal readonly record struct CornerSample(float Shade, int BlockLight, int SkyLight);

    /// <summary>One vertex's lighting: brightness for the colour, sky and block for the lightmap.</summary>
    /// <param name="Brightness">Occlusion brightness in 0..1.</param>
    /// <param name="BlockLight">Block light in 0..1.</param>
    /// <param name="SkyLight">Sky light in 0..1.</param>
    internal readonly record struct VertexLighting(float Brightness, float BlockLight, float SkyLight);

    /// <summary>
    /// Computes the reference ambient occlusion and smooth light for one quad, written out in our corner order.
    /// </summary>
    /// <param name="neighbourhood">World access for block and light samples.</param>
    /// <param name="direction">Face direction, one of our six constants.</param>
    /// <param name="block">The block cell the face belongs to.</param>
    /// <param name="ambientOcclusion">False keeps brightness at one and still smooths the light.</param>
    /// <param name="sprites">Atlas mapping, used to decide occluders.</param>
    /// <param name="layout">Block state layout, used to resolve blocks.</param>
    /// <param name="bounds">Quad bounds inside its block along each axis; a full cube face passes <see cref="CubeFaceBounds"/>.</param>
    /// <param name="slots">Receives four entries indexed by our quad corner slot.</param>
    internal static void PrepareQuadLighting(
        in Neighbourhood neighbourhood,
        int direction,
        (int X, int Y, int Z) block,
        bool ambientOcclusion,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds,
        Span<VertexLighting> slots)
    {
        var nb = neighbourhood;
        var cubic = FaceCubic(direction, bounds, block, nb, sprites, layout);
        var samples = SampleQuadCorners(nb, direction, block, cubic, sprites, layout);

        // The reference remaps its computed corners onto the baked quad's vertex order before writing them; ours is a
        // different order for the same four world corners, so the baked order is mapped onto our slots here.
        Span<VertexLighting> baked = stackalloc VertexLighting[4];
        ResolveQuadLighting(direction, samples, bounds, ambientOcclusion, baked);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            slots[QuadCornerForFaceVertex[(direction * 4) + vertex]] = baked[vertex];
        }
    }

    /// <summary>
    /// The nine cells the reference samples for one face: four corner neighbours, four diagonals and the centre.
    /// </summary>
    /// <remarks>
    /// Sampling does not depend on the quad's shape, only on the direction and whether the face sits on the block's
    /// outer surface, so a model with several quads on one face can share one set.
    /// </remarks>
    internal readonly struct QuadCornerSamples(
        CornerSample corner0,
        CornerSample corner1,
        CornerSample corner2,
        CornerSample corner3,
        CornerSample diagonal02,
        CornerSample diagonal03,
        CornerSample diagonal12,
        CornerSample diagonal13,
        CornerSample center)
    {
        public CornerSample Corner0 { get; } = corner0;
        public CornerSample Corner1 { get; } = corner1;
        public CornerSample Corner2 { get; } = corner2;
        public CornerSample Corner3 { get; } = corner3;
        public CornerSample Diagonal02 { get; } = diagonal02;
        public CornerSample Diagonal03 { get; } = diagonal03;
        public CornerSample Diagonal12 { get; } = diagonal12;
        public CornerSample Diagonal13 { get; } = diagonal13;
        public CornerSample Center { get; } = center;
    }

    /// <summary>Whether a face of the given shape sits on the block's outer surface.</summary>
    internal static bool QuadFaceCubic(
        int direction,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds,
        (int X, int Y, int Z) block,
        in Neighbourhood neighbourhood,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout) =>
        FaceCubic(direction, bounds, block, neighbourhood, sprites, layout);

    /// <summary>Samples the nine cells of one face (ref BlockModelLighter.java:44-126).</summary>
    internal static QuadCornerSamples SampleQuadCorners(
        in Neighbourhood nb,
        int direction,
        (int X, int Y, int Z) block,
        bool cubic,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout)
    {
        var (nx, ny, nz) = NeighbourOffset(direction);

        // A cubic face samples from the cell it looks at and takes its centre shade from there; a cut-out face samples
        // from its own cell (ref BlockModelLighter.java:41-43, 120-126).
        var baseX = cubic ? block.X + nx : block.X;
        var baseY = cubic ? block.Y + ny : block.Y;
        var baseZ = cubic ? block.Z + nz : block.Z;

        var o0 = CornerOffset(direction, 0);
        var o1 = CornerOffset(direction, 1);
        var o2 = CornerOffset(direction, 2);
        var o3 = CornerOffset(direction, 3);

        var c0 = SampleCorner(nb, baseX + o0.X, baseY + o0.Y, baseZ + o0.Z, sprites, layout);
        var c1 = SampleCorner(nb, baseX + o1.X, baseY + o1.Y, baseZ + o1.Z, sprites, layout);
        var c2 = SampleCorner(nb, baseX + o2.X, baseY + o2.Y, baseZ + o2.Z, sprites, layout);
        var c3 = SampleCorner(nb, baseX + o3.X, baseY + o3.Y, baseZ + o3.Z, sprites, layout);

        // One cell further out along the face normal decides whether a diagonal has to be sampled at all.
        var permeable0 = LightPermeable(nb.GetBlockState(baseX + o0.X + nx, baseY + o0.Y + ny, baseZ + o0.Z + nz, 0), sprites, layout);
        var permeable1 = LightPermeable(nb.GetBlockState(baseX + o1.X + nx, baseY + o1.Y + ny, baseZ + o1.Z + nz, 0), sprites, layout);
        var permeable2 = LightPermeable(nb.GetBlockState(baseX + o2.X + nx, baseY + o2.Y + ny, baseZ + o2.Z + nz, 0), sprites, layout);
        var permeable3 = LightPermeable(nb.GetBlockState(baseX + o3.X + nx, baseY + o3.Y + ny, baseZ + o3.Z + nz, 0), sprites, layout);

        // When both sides of a diagonal are opaque the diagonal is not sampled and corner zero's value is reused, for
        // all four diagonals (ref BlockModelLighter.java:83-119).
        var corner02 = !permeable2 && !permeable0 ? c0 : SampleCorner(nb, baseX + o0.X + o2.X, baseY + o0.Y + o2.Y, baseZ + o0.Z + o2.Z, sprites, layout);
        var corner03 = !permeable3 && !permeable0 ? c0 : SampleCorner(nb, baseX + o0.X + o3.X, baseY + o0.Y + o3.Y, baseZ + o0.Z + o3.Z, sprites, layout);
        var corner12 = !permeable2 && !permeable1 ? c0 : SampleCorner(nb, baseX + o1.X + o2.X, baseY + o1.Y + o2.Y, baseZ + o1.Z + o2.Z, sprites, layout);
        var corner13 = !permeable3 && !permeable1 ? c0 : SampleCorner(nb, baseX + o1.X + o3.X, baseY + o1.Y + o3.Y, baseZ + o1.Z + o3.Z, sprites, layout);

        // The centre light has its own rule (ref BlockModelLighter.java:114-119): a cubic face reads the cell it looks
        // at, but a partial face still reads the cell in front of the block whenever that cell is not a solid render.
        // Missing that condition lit a plant or a partial model from the block's own cell instead.
        var center = SampleCorner(nb, baseX, baseY, baseZ, sprites, layout);
        if (!cubic)
        {
            var frontX = block.X + nx;
            var frontY = block.Y + ny;
            var frontZ = block.Z + nz;
            if (!SolidRender(nb.GetBlockState(frontX, frontY, frontZ, 0), sprites, layout))
            {
                var front = SampleCorner(nb, frontX, frontY, frontZ, sprites, layout);
                center = new CornerSample(center.Shade, front.BlockLight, front.SkyLight);
            }
        }

        return new QuadCornerSamples(c0, c1, c2, c3, corner02, corner03, corner12, corner13, center);
    }

    /// <summary>
    /// Turns the nine samples into four vertex values, written in the baked quad's vertex order
    /// (ref BlockModelLighter.java:128-206).
    /// </summary>
    internal static void ResolveQuadLighting(
        int direction,
        in QuadCornerSamples samples,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds,
        bool ambientOcclusion,
        Span<VertexLighting> baked)
    {
        Span<VertexLighting> computed = stackalloc VertexLighting[4];
        if (IsPartialFace(direction, bounds) && NonCubicWeight(direction))
        {
            PreparePartialFace(
                direction,
                samples.Corner0, samples.Corner1, samples.Corner2, samples.Corner3,
                samples.Diagonal02, samples.Diagonal03, samples.Diagonal12, samples.Diagonal13,
                samples.Center, bounds, ambientOcclusion, computed);
        }
        else
        {
            PrepareCubeFace(
                samples.Corner0, samples.Corner1, samples.Corner2, samples.Corner3,
                samples.Diagonal02, samples.Diagonal03, samples.Diagonal12, samples.Diagonal13,
                samples.Center, ambientOcclusion, computed);
        }

        for (var vertex = 0; vertex < 4; vertex++)
        {
            baked[FaceVertexRemap[(direction * 4) + vertex]] = computed[vertex];
        }
    }

    /// <summary>
    /// Which baked-quad vertex a corner belongs to, from the corner's position inside the quad alone.
    /// </summary>
    /// <param name="direction">Face direction constant.</param>
    /// <param name="uMax">Whether the corner is at the high end of the face's first in-plane axis.</param>
    /// <param name="vMax">Whether the corner is at the high end of the face's second in-plane axis.</param>
    /// <remarks>
    /// The reference orders a quad's vertices by the box extents it calls FaceInfo, so the position inside the quad is
    /// the whole answer. This is what lets the element model path, whose vertices come straight from the model file and
    /// are not in our merged order, light each corner correctly without a hand-written table per model.
    /// </remarks>
    internal static int BakedVertexAt(int direction, bool uMax, bool vMax) =>
        FaceVertexAtCorner[(direction * 4) + ((vMax ? 2 : 0) | (uMax ? 1 : 0))];

    /// <summary>Baked-quad vertex for each corner of a face, indexed by two sign bits: bit 0 is high u, bit 1 high v.</summary>
    /// <remarks>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/FaceInfo.java, the four VertexInfo per face.</remarks>
    private static ReadOnlySpan<byte> FaceVertexAtCorner =>
    [
        1, 2, 0, 3,   // -X west:  u = Z, v = Y
        2, 1, 3, 0,   // +X east
        1, 2, 0, 3,   // -Y down:  u = X, v = Z
        0, 3, 1, 2,   // +Y up
        2, 1, 3, 0,   // -Z north: u = X, v = Y
        1, 2, 0, 3    // +Z south
    ];

    /// <summary>The full-cube branch: the four corner values pass straight through, one per vertex.</summary>
    private static void PrepareCubeFace(
        CornerSample c0, CornerSample c1, CornerSample c2, CornerSample c3,
        CornerSample corner02, CornerSample corner03, CornerSample corner12, CornerSample corner13,
        CornerSample center,
        bool ambientOcclusion,
        Span<VertexLighting> raw)
    {
        Span<float> shade = stackalloc float[4];
        shade[0] = (c3.Shade + c0.Shade + corner03.Shade + center.Shade) * 0.25f;
        shade[1] = (c2.Shade + c0.Shade + corner02.Shade + center.Shade) * 0.25f;
        shade[2] = (c2.Shade + c1.Shade + corner12.Shade + center.Shade) * 0.25f;
        shade[3] = (c3.Shade + c1.Shade + corner13.Shade + center.Shade) * 0.25f;

        var light0 = SmoothBlend(c3, c0, corner03, center);
        var light1 = SmoothBlend(c2, c0, corner02, center);
        var light2 = SmoothBlend(c2, c1, corner12, center);
        var light3 = SmoothBlend(c3, c1, corner13, center);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var light = vertex switch { 0 => light0, 1 => light1, 2 => light2, _ => light3 };
            raw[vertex] = new VertexLighting(
                ambientOcclusion ? Math.Clamp(shade[vertex], 0f, 1f) : 1f,
                TruncatedToLevel(light.Block) / 240f,
                TruncatedToLevel(light.Sky) / 240f);
        }
    }

    /// <summary>
    /// Drops the sub-level part of a blended light value, which is what the reference does by reading the result back
    /// as a four-bit level for a whole-face vertex (ref LightCoordsUtil.block/sky). The partial-face branch keeps the
    /// fraction because it feeds the value straight into the weighted blend.
    /// </summary>
    private static int TruncatedToLevel(int value240) => (value240 >> 4) << 4;

    /// <summary>
    /// The partial-face branch: each corner's shade is weighted by how much of the quad it covers, and the four blended
    /// light samples are weighted identically.
    /// </summary>
    private static void PreparePartialFace(
        int direction,
        CornerSample c0, CornerSample c1, CornerSample c2, CornerSample c3,
        CornerSample corner02, CornerSample corner03, CornerSample corner12, CornerSample corner13,
        CornerSample center,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds,
        bool ambientOcclusion,
        Span<VertexLighting> raw)
    {
        Span<float> shape = stackalloc float[SizeInfoCount];
        FaceShape(bounds, shape);

        Span<float> shade = stackalloc float[4];
        shade[0] = (c3.Shade + c0.Shade + corner03.Shade + center.Shade) * 0.25f;
        shade[1] = (c2.Shade + c0.Shade + corner02.Shade + center.Shade) * 0.25f;
        shade[2] = (c2.Shade + c1.Shade + corner12.Shade + center.Shade) * 0.25f;
        shade[3] = (c3.Shade + c1.Shade + corner13.Shade + center.Shade) * 0.25f;

        var light0 = SmoothBlend(c3, c0, corner03, center);
        var light1 = SmoothBlend(c2, c0, corner02, center);
        var light2 = SmoothBlend(c2, c1, corner12, center);
        var light3 = SmoothBlend(c3, c1, corner13, center);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var at = (direction * 32) + (vertex * 8);
            var weight01 = shape[CornerWeights[at]] * shape[CornerWeights[at + 1]];
            var weight23 = shape[CornerWeights[at + 2]] * shape[CornerWeights[at + 3]];
            var weight45 = shape[CornerWeights[at + 4]] * shape[CornerWeights[at + 5]];
            var weight67 = shape[CornerWeights[at + 6]] * shape[CornerWeights[at + 7]];

            var light = SmoothWeightedBlend(light0, light1, light2, light3, weight01, weight23, weight45, weight67);
            var brightness = (shade[0] * weight01) + (shade[1] * weight23) + (shade[2] * weight45) + (shade[3] * weight67);

            raw[vertex] = new VertexLighting(
                ambientOcclusion ? Math.Clamp(brightness, 0f, 1f) : 1f,
                light.Block / 240f,
                light.Sky / 240f);
        }
    }

    /// <summary>
    /// The reference's <c>smoothBlend</c> on our two-channel form, in the same 0..240 space and with the same
    /// zero-neighbour correction (ref://mc-26.3-analysis/src/net/minecraft/util/LightCoordsUtil.java, smoothBlend).
    /// </summary>
    private static (int Block, int Sky) SmoothBlend(CornerSample neighbor1, CornerSample neighbor2, CornerSample neighbor3, CornerSample center)
    {
         if ((center.SkyLight / 16) > 2 || (center.BlockLight / 16) > 2)
        {
            var n1 = Corrected(neighbor1, center);
            var n2 = Corrected(neighbor2, center);
            var n3 = Corrected(neighbor3, center);
            return ((n1.Block + n2.Block + n3.Block + center.BlockLight) >> 2,
                    (n1.Sky + n2.Sky + n3.Sky + center.SkyLight) >> 2);
        }

        return ((neighbor1.BlockLight + neighbor2.BlockLight + neighbor3.BlockLight + center.BlockLight) >> 2,
                (neighbor1.SkyLight + neighbor2.SkyLight + neighbor3.SkyLight + center.SkyLight) >> 2);
    }

    /// <summary>
    /// The reference's zero-neighbour correction: a neighbour with no value at all takes the centre's, and one with no
    /// sky takes the centre's sky.
    /// </summary>
    private static (int Block, int Sky) Corrected(CornerSample neighbour, CornerSample center) =>
        neighbour.BlockLight == 0 && neighbour.SkyLight == 0
            ? (center.BlockLight, center.SkyLight)
            : neighbour.SkyLight == 0
                ? (neighbour.BlockLight, center.SkyLight)
                : (neighbour.BlockLight, neighbour.SkyLight);

    /// <summary>The reference's <c>smoothWeightedBlend</c>: float weights, truncated per channel.</summary>
    private static (int Block, int Sky) SmoothWeightedBlend(
        (int Block, int Sky) coords1, (int Block, int Sky) coords2, (int Block, int Sky) coords3, (int Block, int Sky) coords4,
        float weight1, float weight2, float weight3, float weight4) =>
        (
            (int)((coords1.Block * weight1) + (coords2.Block * weight2) + (coords3.Block * weight3) + (coords4.Block * weight4)),
            (int)((coords1.Sky * weight1) + (coords2.Sky * weight2) + (coords3.Sky * weight3) + (coords4.Sky * weight4)));

    /// <summary>The twelve size entries of a quad, which the weight table indexes.</summary>
    private static void FaceShape((float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds, Span<float> shape)
    {
        shape[SizeDown] = bounds.MinY;
        shape[SizeUp] = bounds.MaxY;
        shape[SizeNorth] = bounds.MinZ;
        shape[SizeSouth] = bounds.MaxZ;
        shape[SizeWest] = bounds.MinX;
        shape[SizeEast] = bounds.MaxX;
        shape[SizeFlipDown] = 1f - bounds.MinY;
        shape[SizeFlipUp] = 1f - bounds.MaxY;
        shape[SizeFlipNorth] = 1f - bounds.MinZ;
        shape[SizeFlipSouth] = 1f - bounds.MaxZ;
        shape[SizeFlipWest] = 1f - bounds.MinX;
        shape[SizeFlipEast] = 1f - bounds.MaxX;
    }

    /// <summary>True when the quad does not cover the whole block face (ref BlockModelLighter.java:255-262).</summary>
    public static bool IsPartialFace(
        int direction,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds) => direction switch
    {
        NegativeY or PositiveY =>
            bounds.MinX >= 1.0e-4f || bounds.MinZ >= 1.0e-4f || bounds.MaxX <= 0.9999f || bounds.MaxZ <= 0.9999f,
        NegativeZ or PositiveZ =>
            bounds.MinX >= 1.0e-4f || bounds.MinY >= 1.0e-4f || bounds.MaxX <= 0.9999f || bounds.MaxY <= 0.9999f,
        _ =>
            bounds.MinY >= 1.0e-4f || bounds.MinZ >= 1.0e-4f || bounds.MaxY <= 0.9999f || bounds.MaxZ <= 0.9999f
    };

    /// <summary>
    /// True when the quad sits on the block's outer face, which is what decides whether samples come from the cell the
    /// face looks at (ref BlockModelLighter.java:264-272).
    /// </summary>
    private static bool FaceCubic(
        int direction,
        (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) bounds,
        (int X, int Y, int Z) block,
        in Neighbourhood neighbourhood,
        BlockSpriteMap? sprites,
        BlockStateLayout? layout)
    {
        var full = CollisionFullBlock(neighbourhood.GetBlockState(block.X, block.Y, block.Z, 0), sprites, layout);

        return direction switch
        {
            NegativeY => bounds.MinY == bounds.MaxY && (bounds.MinY < 1.0e-4f || full),
            PositiveY => bounds.MinY == bounds.MaxY && (bounds.MaxY > 0.9999f || full),
            NegativeZ => bounds.MinZ == bounds.MaxZ && (bounds.MinZ < 1.0e-4f || full),
            PositiveZ => bounds.MinZ == bounds.MaxZ && (bounds.MaxZ > 0.9999f || full),
            NegativeX => bounds.MinX == bounds.MaxX && (bounds.MinX < 1.0e-4f || full),
            _ => bounds.MinX == bounds.MaxX && (bounds.MaxX > 0.9999f || full)
        };
    }

    /// <summary>
    /// Whether the reference calls a state collision-shape full, which is what the 0.2 / 1.0 shade and the cubic face
    /// test read. Guessing from the sprite alpha gets glass, ice and leaves backwards: they are collision-full while
    /// their occlusion shape is empty.
    /// </summary>
    private static bool CollisionFullBlock(int state, BlockSpriteMap? sprites, BlockStateLayout? layout) =>
        layout?.BlockAt(state) is { } block && VanillaBlockOcclusion.TryGet(block, out var info)
            ? info.CollisionFullBlock
            : Occludes(state, sprites, layout);

    /// <summary>Whether the reference calls a state solid render, which decides where a partial face reads its light.</summary>
    private static bool SolidRender(int state, BlockSpriteMap? sprites, BlockStateLayout? layout) =>
        layout?.BlockAt(state) is { } block && VanillaBlockOcclusion.TryGet(block, out var info)
            ? info.SolidRender
            : Occludes(state, sprites, layout);

    /// <summary>
    /// Whether light passes through a state, which is what the two-sided diagonal rule reads. The reference answers with
    /// <c>!isSolidRender || lightDampening == 0</c>, not with the texture.
    /// </summary>
    private static bool LightPermeable(int state, BlockSpriteMap? sprites, BlockStateLayout? layout) =>
        layout?.BlockAt(state) is { } block && VanillaBlockOcclusion.TryGet(block, out var info)
            ? info.LightPermeable
            : !Occludes(state, sprites, layout);

    private static CornerSample SampleCorner(
        in Neighbourhood neighbourhood, int x, int y, int z, BlockSpriteMap? sprites, BlockStateLayout? layout)
    {
        var shade = CollisionFullBlock(neighbourhood.GetBlockState(x, y, z, 0), sprites, layout) ? 0.2f : 1.0f;
        if (!neighbourhood.TryGetLight(x, y, z, out var sky, out var block))
        {
            return new CornerSample(shade, 0, 0);
        }

        // The reference keeps light on a 0..240 scale after its nibble packing, which is level * 16.
        return new CornerSample(shade, block * 16, sky * 16);
    }

    /// <summary>Neighbour offset of one of the four corners of a face.</summary>
    private static (int X, int Y, int Z) CornerOffset(int direction, int corner) =>
        NeighbourOffset(CornerNeighbours[(direction * 4) + corner]);

    /// <summary>Test access to the reference blend, in the same 0..240 channel form the pass uses.</summary>
    internal static (int Block, int Sky) SmoothBlendForTest(
        int block1, int sky1, int block2, int sky2, int block3, int sky3, int centerBlock, int centerSky) =>
        SmoothBlend(
            new CornerSample(1f, block1, sky1),
            new CornerSample(1f, block2, sky2),
            new CornerSample(1f, block3, sky3),
            new CornerSample(1f, centerBlock, centerSky));

    /// <summary>Test access to the reference weighted blend.</summary>
    internal static (int Block, int Sky) SmoothWeightedBlendForTest(
        int block1, int sky1, int block2, int sky2, int block3, int sky3, int block4, int sky4,
        float weight1, float weight2, float weight3, float weight4) =>
        SmoothWeightedBlend(
            (block1, sky1), (block2, sky2), (block3, sky3), (block4, sky4),
            weight1, weight2, weight3, weight4);

    /// <summary>Test access to the whole-face level truncation.</summary>
    internal static int TruncatedToLevelForTest(int value240) => TruncatedToLevel(value240);
}