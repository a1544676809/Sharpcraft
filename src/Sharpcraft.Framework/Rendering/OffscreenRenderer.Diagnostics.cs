// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using System.Numerics;

namespace Sharpcraft.Framework.Rendering;


public static partial class OffscreenRenderer
{
    /// <summary>
    /// Compares sampled motion vectors against the same computation done here, pixel by pixel.
    /// </summary>
    private static void CompareMotion(
        WgpuRenderBackend backend,
        Instance instance,
        BufferHandle depthReadback,
        ulong depthSize,
        int depthPadded,
        BufferHandle motionReadback,
        System.Numerics.Matrix4x4 viewProjection,
        FrameRequest request,
        int width,
        int height)
    {
        System.Numerics.Matrix4x4.Invert(viewProjection, out var inverse);
        var shift = request.Diagnostics?.MotionShiftX ?? 0f;
        var shiftBy = new System.Numerics.Vector3(shift == 0f ? 1f : shift, 0f, 0f);
        var previous = CameraMath.BuildViewProjection(
            request.CameraPosition + shiftBy,
            request.CameraTarget + shiftBy,
            request.FieldOfViewDegrees,
            (float)request.Width / request.Height);

        var motionBytes = (ulong)(width * height * 8);

        // Both blocks come back through the seam: it maps, copies and unmaps, so the comparison only sees bytes.
        var depthMapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        var motionMapped = backend.ReadBuffer(instance, motionReadback, 0, (nuint)motionBytes);
        var samples = 0;
        var mismatches = 0;
        var worst = 0f;

        for (var y = 16; y < height; y += 31)
        {
            for (var x = 16; x < width; x += 31)
            {
                var window = BitConverter.ToSingle(depthMapped.AsSpan((y * depthPadded) + (x * 4), 4));

                // Reversed-Z: the sky / far plane is depth 0 (depth 1 is the near plane).
                if (window <= 0f)
                {
                    continue;
                }

                var u = (x + 0.5f) / width;
                var v = (y + 0.5f) / height;
                var clip = new System.Numerics.Vector4((u * 2f) - 1f, 1f - (v * 2f), window, 1f);
                var world = System.Numerics.Vector4.Transform(clip, inverse);
                var point = new System.Numerics.Vector3(world.X / world.W, world.Y / world.W, world.Z / world.W);
                var projected = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(point, 1f), previous);

                if (projected.W <= 0f)
                {
                    continue;
                }

                var previousU = ((projected.X / projected.W) * 0.5f) + 0.5f;
                var previousV = 0.5f - ((projected.Y / projected.W) * 0.5f);
                var expectedX = (previousU - u) * width;
                var expectedY = (previousV - v) * height;

                var offset = ((y * width) + x) * 8;
                var gpuX = BitConverter.ToSingle(motionMapped.AsSpan(offset, 4));
                var gpuY = BitConverter.ToSingle(motionMapped.AsSpan(offset + 4, 4));

                samples++;
                var delta = MathF.Max(MathF.Abs(gpuX - expectedX), MathF.Abs(gpuY - expectedY));

                if (delta > 0.01f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }
        }

        Console.WriteLine($"motion: {samples} samples, mismatches {mismatches}, worst delta {worst:F4} px");
    }

    private static void WriteMatrix(System.Numerics.Matrix4x4 matrix, float[] target, int offset)
    {
        // The C# matrix transforms row vectors and WGSL transforms column vectors, so the rows are uploaded as columns:
        // that transposes it, which is exactly the conversion needed.
        target[offset + 0] = matrix.M11;
        target[offset + 1] = matrix.M12;
        target[offset + 2] = matrix.M13;
        target[offset + 3] = matrix.M14;
        target[offset + 4] = matrix.M21;
        target[offset + 5] = matrix.M22;
        target[offset + 6] = matrix.M23;
        target[offset + 7] = matrix.M24;
        target[offset + 8] = matrix.M31;
        target[offset + 9] = matrix.M32;
        target[offset + 10] = matrix.M33;
        target[offset + 11] = matrix.M34;
        target[offset + 12] = matrix.M41;
        target[offset + 13] = matrix.M42;
        target[offset + 14] = matrix.M43;
        target[offset + 15] = matrix.M44;
    }

    /// <summary>Reads the depth buffer back and prints its range, proving depth is available for the occlusion pass.</summary>
    private static float[]? ReportDepth(WgpuRenderBackend backend, Instance instance, BufferHandle depthReadback, ulong depthSize, int depthPadded, int width, int height)
    {
        byte[] mapped;
        try
        {
            mapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("depth: readback failed");
            return null;
        }

        var depths = new float[width * height];
        // Reversed-Z: depth 1 is the near plane and 0 the far plane, so the physically nearest pixel carries the LARGEST
        // raw depth. Track both ends of the raw range and convert each to a distance for the report.
        var minRaw = float.MaxValue;
        var maxRaw = 0f;
        var centre = BitConverter.ToSingle(mapped.AsSpan(((height / 2) * depthPadded) + ((width / 2) * 4), 4));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = BitConverter.ToSingle(mapped.AsSpan((y * depthPadded) + (x * 4), 4));
                depths[(y * width) + x] = value;

                if (value < minRaw)
                {
                    minRaw = value;
                }

                if (value > maxRaw)
                {
                    maxRaw = value;
                }
            }
        }

        var near = 0.1f;
        var far = 2000f;
        Console.WriteLine(
            $"depth: {width}x{height} nearest {DepthLinearisation.ToDistance(maxRaw, near, far):F1} blocks (raw {maxRaw:F4}) " +
            $"farthest {DepthLinearisation.ToDistance(minRaw, near, far):F1} blocks (raw {minRaw:F4}) " +
            $"centre {DepthLinearisation.ToDistance(centre, near, far):F1} blocks");

        return depths;
    }

    /// <summary>
    /// Compares every pyramid level against the CPU reference, level by level, and prints the totals.
    /// </summary>
    private static void ComparePyramidChain(
        WgpuRenderBackend backend,
        Instance instance,
        BufferHandle depthReadback,
        ulong depthSize,
        int depthPadded,
        IReadOnlyList<BufferHandle> readbacks,
        IReadOnlyList<(int Width, int Height)> sizes,
        int width,
        int height)
    {
        byte[] depthMapped;
        try
        {
            depthMapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("pyramid: depth map failed");
            return;
        }

        var depth = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                depth[(y * width) + x] = DepthLinearisation.ToDistance(
                    BitConverter.ToSingle(depthMapped.AsSpan((y * depthPadded) + (x * 4), 4)),
                    0.1f,
                    2000f);
            }
        }

        var reference = DepthPyramid.Build(depth, width, height);
        var totalMismatches = 0;
        var worst = 0f;
        var lines = new List<string>();

        for (var level = 0; level < readbacks.Count; level++)
        {
            var (levelWidth, levelHeight) = sizes[level];
            var bytes = (ulong)(levelWidth * levelHeight * 4);

            byte[] mapped;
            try
            {
                mapped = backend.ReadBuffer(instance, readbacks[level], 0, (nuint)bytes);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"pyramid: level {level + 1} map failed");
                return;
            }

            var expected = reference[level + 1];
            var mismatches = 0;

            for (var index = 0; index < levelWidth * levelHeight; index++)
            {
                var delta = MathF.Abs(BitConverter.ToSingle(mapped.AsSpan(index * 4, 4)) - expected[index]);

                if (delta > 0.05f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }

            totalMismatches += mismatches;
            lines.Add($"{level + 1}:{levelWidth}x{levelHeight} mismatches {mismatches}");
        }

        Console.WriteLine($"pyramid: {readbacks.Count} levels, total mismatches {totalMismatches}, worst delta {worst:F6}; " + string.Join(", ", lines));
    }


    /// <summary>
    /// Position-colour pipeline: vertex colour times the per-draw tint, with the given reference blend mode. Sunrise uses
    /// Translucent; stars use Overlay (both match the reference pipelines' ColorTargetState).
    /// </summary>
    private static PipelineHandle CreatePositionColorPipeline(IRenderBackend backend, byte[] source, PipelineLayoutHandle layout, bool overlay, TextureFormat format = TextureFormat.Rgba8Unorm, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always, bool opaque = false)
    {
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_position_color",
            source,
            "fs_position_color",
            layout,
            [
                new ColorTargetDesc(
                    format,
                    opaque ? null : new BlendStateDesc(
                        new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, overlay ? BlendFactor.One : BlendFactor.OneMinusSrcAlpha),
                        new BlendComponentDesc(BlendOperation.Add, overlay ? BlendFactor.Zero : BlendFactor.OneMinusSrcAlpha, BlendFactor.Zero)))
            ],
            // Part of the backdrop by default: depth always passes and nothing is written, so terrain covers it. Clouds pass
            // GreaterEqual instead so terrain and other clouds occlude them (drawn after the world).
            new DepthStateDesc(TextureFormat.Depth32Float, BackendConvert.FromWgpu(depthCompare), false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.PositionColorStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.PositionColorAttributes())));
    }
}
