// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;
using WgpuBuffer = Brew.Wgpu.Buffer;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Culls the chunk draw list on the GPU: a compute pass reads each chunk's bounds and the view frustum and zeroes the
/// index count of the chunks that are entirely outside it.
/// </summary>
/// <remarks>
/// The arena keeps one indirect draw record per chunk plus a copy of the untouched records; this pass writes the visible
/// ones back and blanks the rest. It is an optimisation, not a requirement: if the pipeline or its buffers cannot be
/// created, <see cref="Available"/> stays false and the renderer falls back to the CPU frustum test.
/// </remarks>
public sealed class ChunkCuller : IDisposable
{
    /// <summary>Bytes of one bounds record: minimum and maximum as four floats each.</summary>
    public const int BoundsStride = 32;

    /// <summary>Bytes of one indirect draw record.</summary>
    public const int ArgsStride = 20;

    private const ulong PlaneBytes = 6 * 16;

    private readonly Device _device;
    private readonly Queue _queue;
    private readonly int _slots;

    private readonly WgpuBuffer? _bounds;
    private readonly WgpuBuffer? _baseArgs;
    private readonly WgpuBuffer? _args;
    private readonly WgpuBuffer? _planes;
    private readonly BindGroupLayout? _layout;
    private readonly PipelineLayout? _pipelineLayout;
    private readonly ShaderModule? _shader;
    private readonly ComputePipeline? _pipeline;
    private readonly BindGroup? _bindGroup;

    /// <summary>Creates the culler, or one whose <see cref="Available"/> is false when compute is not usable.</summary>
    /// <param name="device">Device that owns the resources.</param>
    /// <param name="queue">Queue used for the small per chunk uploads.</param>
    /// <param name="slots">Draw slots, matching the arena's indirect buffer.</param>
    public ChunkCuller(Device device, Queue queue, int slots)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slots);

        _device = device;
        _queue = queue;
        _slots = slots;

        try
        {
            // The bindings are structs wrapping handles, so the local values are used directly rather than the fields.
            var bounds = device.CreateBuffer(new BufferDescriptor
            {
                Size = (ulong)(slots * BoundsStride),
                Usage = BufferUsage.Storage | BufferUsage.CopyDst
            });

            var baseArgs = device.CreateBuffer(new BufferDescriptor
            {
                Size = (ulong)(slots * ArgsStride),
                Usage = BufferUsage.Storage | BufferUsage.CopyDst
            });

            var args = device.CreateBuffer(new BufferDescriptor
            {
                Size = (ulong)(slots * ArgsStride),
                Usage = BufferUsage.Storage | BufferUsage.Indirect | BufferUsage.CopyDst
            });

            var planes = device.CreateBuffer(new BufferDescriptor
            {
                Size = PlaneBytes,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst
            });

            var layout = device.CreateBindGroupLayout(
            [
                BindGroupLayoutEntry.Buffer(0, ShaderStage.Compute, WGPUBufferBindingType.ReadOnlyStorage, false, 0),
                BindGroupLayoutEntry.Buffer(1, ShaderStage.Compute, WGPUBufferBindingType.ReadOnlyStorage, false, 0),
                BindGroupLayoutEntry.Buffer(2, ShaderStage.Compute, WGPUBufferBindingType.Storage, false, 0),
                BindGroupLayoutEntry.Buffer(3, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, PlaneBytes)
            ]);

            var pipelineLayout = device.CreatePipelineLayout([layout]);

            var shader = device.CreateShaderModule(new ShaderModuleDescriptor
            {
                Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("chunk_cull")))
            });

            var pipeline = device.CreateComputePipeline(shader, "cs_main"u8, pipelineLayout);

            var bindGroup = device.CreateBindGroup(layout,
            [
                BindGroupEntry.Buffer(0, bounds, 0, (ulong)(slots * BoundsStride)),
                BindGroupEntry.Buffer(1, baseArgs, 0, (ulong)(slots * ArgsStride)),
                BindGroupEntry.Buffer(2, args, 0, (ulong)(slots * ArgsStride)),
                BindGroupEntry.Buffer(3, planes, 0, PlaneBytes)
            ]);

            _bounds = bounds;
            _baseArgs = baseArgs;
            _args = args;
            _planes = planes;
            _layout = layout;
            _pipelineLayout = pipelineLayout;
            _shader = shader;
            _pipeline = pipeline;
            _bindGroup = bindGroup;

            Available = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // Compute is an optimisation; the renderer keeps the CPU frustum test when it is not available.
            Available = false;
        }
    }

    /// <summary>True when the compute pass can run.</summary>
    public bool Available { get; }

    /// <summary>Indirect argument buffer the pass writes.</summary>
    public WgpuBuffer? Args => _args;

    /// <summary>Uploads one slot's bounds and draw record.</summary>
    /// <param name="slot">Draw slot.</param>
    /// <param name="minimum">Smallest corner of the chunk's world bounds.</param>
    /// <param name="maximum">Largest corner.</param>
    /// <param name="indexCount">Indices the chunk draws.</param>
    /// <param name="firstIndex">First index in the arena's index buffer.</param>
    /// <param name="baseVertex">Base vertex for the draw.</param>
    public void WriteSlot(int slot, Vector3 minimum, Vector3 maximum, uint indexCount, uint firstIndex, int baseVertex)
    {
        if (!Available || _bounds is null || _baseArgs is null || _args is null || slot < 0 || slot >= _slots)
        {
            return;
        }

        Span<float> boundsValues = stackalloc float[8];
        boundsValues[0] = minimum.X;
        boundsValues[1] = minimum.Y;
        boundsValues[2] = minimum.Z;
        boundsValues[3] = 0f;
        boundsValues[4] = maximum.X;
        boundsValues[5] = maximum.Y;
        boundsValues[6] = maximum.Z;
        boundsValues[7] = 0f;

        var boundsBytes = new byte[BoundsStride];

        for (var index = 0; index < 8; index++)
        {
            BitConverter.TryWriteBytes(boundsBytes.AsSpan(index * sizeof(float)), boundsValues[index]);
        }

        _queue.WriteBuffer(_bounds.Value, (ulong)(slot * BoundsStride), boundsBytes);

        var record = BuildRecord(indexCount, firstIndex, baseVertex);
        _queue.WriteBuffer(_baseArgs.Value, (ulong)(slot * ArgsStride), record);
        _queue.WriteBuffer(_args.Value, (ulong)(slot * ArgsStride), record);
    }

    /// <summary>Records the cull pass for this frame.</summary>
    /// <param name="encoder">Encoder the render pass will be recorded into.</param>
    /// <param name="planes">Six inward facing frustum planes.</param>
    /// <param name="slotCount">Slots to test.</param>
    public void Dispatch(CommandEncoder encoder, ReadOnlySpan<Vector4> planes, int slotCount)
    {
        if (!Available || _pipeline is null || _bindGroup is null || _planes is null || slotCount <= 0)
        {
            return;
        }

        var planeBytes = new byte[PlaneBytes];

        for (var index = 0; index < 6; index++)
        {
            var plane = planes[index];
            var offset = index * 4 * sizeof(float);
            BitConverter.TryWriteBytes(planeBytes.AsSpan(offset), plane.X);
            BitConverter.TryWriteBytes(planeBytes.AsSpan(offset + 4), plane.Y);
            BitConverter.TryWriteBytes(planeBytes.AsSpan(offset + 8), plane.Z);
            BitConverter.TryWriteBytes(planeBytes.AsSpan(offset + 12), plane.W);
        }

        _queue.WriteBuffer(_planes.Value, 0, planeBytes);

        using var pass = encoder.BeginComputePass();
        pass.SetPipeline(_pipeline.Value);
        pass.SetBindGroup(0, _bindGroup.Value);
        pass.DispatchWorkgroups((uint)((slotCount + 63) / 64));
        pass.End();
    }

    private static byte[] BuildRecord(uint indexCount, uint firstIndex, int baseVertex)
    {
        var record = new byte[ArgsStride];
        BitConverter.TryWriteBytes(record.AsSpan(0), indexCount);
        BitConverter.TryWriteBytes(record.AsSpan(4), 1u);
        BitConverter.TryWriteBytes(record.AsSpan(8), firstIndex);
        BitConverter.TryWriteBytes(record.AsSpan(12), baseVertex);
        BitConverter.TryWriteBytes(record.AsSpan(16), 0u);

        return record;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bindGroup?.Dispose();
        _pipeline?.Dispose();
        _shader?.Dispose();
        _pipelineLayout?.Dispose();
        _layout?.Dispose();
        _planes?.Dispose();
        _args?.Dispose();
        _baseArgs?.Dispose();
        _bounds?.Dispose();
    }
}
