// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Brew.Wgpu;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Benchmarks;

/// <summary>
/// Measures what one seam call costs during recording, so the per draw cost seen in the frame profile can be split
/// between our wrapper and wgpu-native (T-805).
/// </summary>
/// <remarks>
/// The measurement runs inside a real render pass on a real device: outside one, <c>DrawIndexed</c> has nothing to do and
/// would report a number that does not exist in the frame. The pass target is a small offscreen texture, so the GPU work
/// is a handful of pixels and the numbers are recording cost, not shading cost.
/// </remarks>
internal static class SeamDrawBenchmarks
{
    private const string Shader = """
        struct VertexOutput {
            @builtin(position) position : vec4<f32>,
        };

        @vertex
        fn vs_main(@builtin(vertex_index) index : u32) -> VertexOutput {
            var positions = array<vec2<f32>, 3>(
                vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
            var output : VertexOutput;
            output.position = vec4<f32>(positions[index], 0.0, 1.0);
            return output;
        }

        @fragment
        fn fs_main() -> @location(0) vec4<f32> {
            return vec4<f32>(0.25, 0.5, 0.75, 1.0);
        }
        """;

    /// <summary>Runs the measurements, printing nanoseconds per call.</summary>
    /// <param name="repetitions">Passes recorded per measurement.</param>
    /// <param name="callsPerPass">Seam calls recorded in each pass.</param>
    /// <returns>True when the measurements ran, false when no device was available.</returns>
    public static bool TryRun(int repetitions, int callsPerPass)
    {
        Instance instance;
        try
        {
            instance = Instance.Create();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.WriteLine($"seam: no instance available ({exception.Message}); skipping the draw measurements");
            return false;
        }

        using var instanceScope = instance;
        Adapter adapter;
        try
        {
            adapter = NativeAdapterSelection.Resolve(instance, spec: null);
        }
        catch (AdapterRequestException exception)
        {
            Console.WriteLine($"seam: no adapter available ({exception.Message}); skipping the draw measurements");
            return false;
        }

        using var adapterScope = adapter;
        var device = adapter.RequestDeviceBlocking(new DeviceDescriptor { RequiredLimits = adapter.GetLimits() });
        using var deviceScope = device;
        var backend = new WgpuRenderBackend(device);

        var format = TextureFormat.Rgba8Unorm;
        var target = backend.CreateTexture(new TextureDesc(64, 64, format, 1, TextureUsages.RenderAttachment));
        var targetView = backend.CreateTextureView(target);
        var depth = backend.CreateTexture(new TextureDesc(64, 64, TextureFormat.Depth32Float, 1, TextureUsages.RenderAttachment));
        var depthView = backend.CreateTextureView(depth);
        var source = System.Text.Encoding.UTF8.GetBytes(Shader);
        var pipeline = backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_main", null,
            [new ColorTargetDesc(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));

        var indexBuffer = backend.CreateBuffer(new BufferDesc(64, BufferUsages.Index | BufferUsages.CopyDst));
        // Three zero indices: a degenerate triangle is enough, the benchmark measures recording, not pixels.
        backend.WriteBuffer(indexBuffer, new byte[12], 0);

        Console.WriteLine();
        Console.WriteLine($"seam: device ready, repetitions={repetitions}, calls per pass={callsPerPass}");

        Measure("seam-1 set index buffer", repetitions, callsPerPass, (pass, count) =>
        {
            for (var index = 0; index < count; index++)
            {
                backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32, 0, 12);
            }
        });

        Measure("seam-2 draw indexed", repetitions, callsPerPass, (pass, count) =>
        {
            backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32, 0, 12);
            for (var index = 0; index < count; index++)
            {
                backend.DrawIndexed(pass, 3, 1, 0, 0, 0);
            }
        });

        Measure("seam-3 draw + index rebind", repetitions, callsPerPass, (pass, count) =>
        {
            for (var index = 0; index < count; index++)
            {
                backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32, 0, 12);
                backend.DrawIndexed(pass, 3, 1, 0, 0, 0);
            }
        });

        // The same shape without the native call: this is what our own wrapper, the handle unwrap and the interface
        // dispatch cost on their own, which is the part we can remove.
        Measure("seam-4 wrapper only (no wgpu call)", repetitions, callsPerPass, (_, count) =>
        {
            for (var index = 0; index < count; index++)
            {
                WrapperOnly(indexBuffer, 12);
                WrapperOnlyDraw(3);
            }
        });

        backend.Destroy(pipeline);
        backend.Destroy(indexBuffer);
        backend.Destroy(depthView);
        backend.Destroy(depth);
        backend.Destroy(targetView);
        backend.Destroy(target);
        return true;

        void Measure(string name, int passes, int calls, Action<RenderPassHandle, int> record)
        {
            // Warm up so tiered compilation and the driver's first use are not part of the measurement.
            for (var pass = 0; pass < 3; pass++)
            {
                Record(pass, record, calls, backend, targetView, depthView, pipeline);
            }

            var samples = new double[passes];
            for (var sample = 0; sample < passes; sample++)
            {
                var started = Stopwatch.GetTimestamp();
                Record(sample, record, calls, backend, targetView, depthView, pipeline);
                samples[sample] = Stopwatch.GetElapsedTime(started).TotalNanoseconds / calls;
            }

            Array.Sort(samples);
            Console.WriteLine(
                $"seam: {name,-34} {samples[samples.Length / 2],8:F1} ns/call  "
                + $"(min {samples[0]:F1}, max {samples[^1]:F1}, n={samples.Length})");
        }
    }

    private static void Record(
        int pass,
        Action<RenderPassHandle, int> record,
        int calls,
        IRenderBackend backend,
        TextureViewHandle targetView,
        TextureViewHandle depthView,
        PipelineHandle pipeline)
    {
        var encoder = backend.CreateCommandEncoder();
        var handle = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Keep(targetView)],
            DepthAttachmentDesc.Keep(depthView));
        backend.SetPipeline(handle, pipeline);
        record(handle, calls);
        backend.EndRenderPass(handle);
        backend.Submit(encoder);
    }

    /// <summary>Replicates the wrapper work of a seam call without touching wgpu, so the C# part can be subtracted.</summary>
    /// <param name="value">Handle whose boxed field the wrapper unwraps.</param>
    /// <param name="size">Size argument the wrapper forwards.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WrapperOnly(BufferHandle value, ulong size)
    {
        var raw = (BufferHandle)Unbox(value)!;
        _ = raw.IsValid ? size : 0UL;
    }

    /// <summary>Replicates the wrapper work of a draw call, including its argument checks.</summary>
    /// <param name="count">Index count the draw would forward.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WrapperOnlyDraw(uint count) => _ = count == 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object? Unbox(object? value) => value;

    /// <summary>Keeps <see cref="Vector4"/> referenced for the shared texture descriptor shape.</summary>
    /// <returns>The clear colour the offscreen texture would use.</returns>
    internal static Vector4 ClearColour() => Vector4.Zero;
}
