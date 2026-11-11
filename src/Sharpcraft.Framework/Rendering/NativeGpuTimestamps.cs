// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>Where a GPU timestamp is written in the frame. The order is the recording order.</summary>
internal enum GpuTimingSlot
{
    /// <summary>First marker of the frame, before any pass.</summary>
    FrameBegin,

    /// <summary>After the main scene pass: sky, opaque terrain, cutout and entities.</summary>
    Scene,

    /// <summary>After the depth-bounds pass.</summary>
    OitBounds,

    /// <summary>After the opaque-depth cull pass.</summary>
    OitCull,

    /// <summary>After the transmittance passes (terrain and clouds).</summary>
    OitTransmit,

    /// <summary>After the accumulate passes (terrain and clouds).</summary>
    OitAccumulate,

    /// <summary>After the composite pass.</summary>
    OitComposite,

    /// <summary>After the cloud pass.</summary>
    Clouds,

    /// <summary>After the interface pass.</summary>
    Ui,

    /// <summary>After the present copy is recorded.</summary>
    PresentCopy,

    /// <summary>Last marker of the frame, after every pass.</summary>
    FrameEnd,

    /// <summary>Number of slots, which is the query set size.</summary>
    Count
}

/// <summary>
/// GPU timestamp queries for one frame range: diagnostic only, created only when the host asks for them.
/// </summary>
/// <remarks>
/// <para>
/// Delete this file together with <c>FrameDiagnostics.GpuTimings</c>, the <c>MC_GPU_TIMINGS</c> mapping and the four
/// <c>IGpuTimingMarkers</c> call sites, and the renderer behaves exactly as before: nothing here runs unless the switch is
/// set, no query set is created, and no timestamp is written (principle 13).
/// </para>
/// <para>
/// The managed binding has no query set type, so the native calls are made directly and the handle is owned by
/// <see cref="NativeQuerySet"/>. Queries are resolved into a small buffer and read back with the seam's blocking read,
/// which costs one stall per report; the report therefore covers the frames before it, not the frame it runs on.
/// </para>
/// </remarks>
internal interface IGpuTimingMarkers
{
    /// <summary>Writes the timestamp for one slot.</summary>
    /// <param name="encoder">Encoder the frame is being recorded into.</param>
    /// <param name="slot">Slot to stamp.</param>
    void Mark(CommandEncoderHandle encoder, GpuTimingSlot slot);
}

/// <summary>A native query set with its timestamps resolved into a readback buffer.</summary>
internal sealed unsafe class NativeQuerySet : IDisposable
{
    /// <summary>Native handle, released on dispose.</summary>
    public WGPUQuerySetImpl* Handle { get; private set; }

    /// <summary>Creates a timestamp query set.</summary>
    /// <param name="device">Device that owns it.</param>
    /// <param name="count">Number of timestamps it holds.</param>
    public NativeQuerySet(Device device, uint count)
    {
        var descriptor = new WGPUQuerySetDescriptor { type = WGPUQueryType.Timestamp, count = count };
        Handle = WGPU.wgpuDeviceCreateQuerySet(device.Handle, &descriptor);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Handle is not null)
        {
            WGPU.wgpuQuerySetRelease(Handle);
            Handle = null;
        }
    }
}

/// <summary>The three native calls a timestamp pass needs, with no state of their own.</summary>
internal static unsafe class NativeGpuTimestamps
{
    /// <summary>
    /// Whether encoder level timestamps can be used with this binding at all.
    /// </summary>
    /// <remarks>
    /// wgpu accepts <c>wgpuCommandEncoderWriteTimestamp</c> only when the instance was created with the
    /// <c>TIMESTAMP_QUERY_INSIDE_ENCODERS</c> instance feature (device level <c>TIMESTAMP_QUERY</c> alone is not enough, and
    /// asking for it silently produces a validation error that aborts the process). Brew.Wgpu 29.0.0.5 exposes neither
    /// that instance feature value nor a way to pass required instance features to <c>Instance.Create</c>: its
    /// <c>WGPUInstanceFeatureName</c> stops at <c>MultipleDevicesPerAdapter</c> and <c>InstanceDescriptor</c> has no feature
    /// list, so the feature cannot be requested without building the instance natively.
    /// </remarks>
    public static readonly bool EncoderTimestampsAvailable = false;

    /// <summary>Whether this adapter can write timestamps at all.</summary>
    /// <param name="adapter">Adapter the device would be created from.</param>
    /// <returns>True when the timestamp feature is available.</returns>
    public static bool IsSupported(Adapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return adapter.HasFeature(WGPUFeatureName.TimestampQuery);
    }

    /// <summary>Writes one timestamp into the query set, outside any render pass.</summary>
    /// <param name="encoder">Encoder recording the frame.</param>
    /// <param name="querySet">Set to write into.</param>
    /// <param name="index">Slot index.</param>
    public static void Write(CommandEncoder encoder, NativeQuerySet querySet, uint index)
    {
        WGPU.wgpuCommandEncoderWriteTimestamp(encoder.Handle, querySet.Handle, index);
    }

    /// <summary>Resolves a range of timestamps into a buffer.</summary>
    /// <param name="encoder">Encoder recording the frame.</param>
    /// <param name="querySet">Set to resolve.</param>
    /// <param name="first">First slot.</param>
    /// <param name="count">Slot count.</param>
    /// <param name="destination">Destination buffer, usable as a query resolve destination.</param>
    /// <param name="offset">Destination byte offset.</param>
    public static void Resolve(CommandEncoder encoder, NativeQuerySet querySet, uint first, uint count, Brew.Wgpu.Buffer destination, ulong offset)
    {
        WGPU.wgpuCommandEncoderResolveQuerySet(encoder.Handle, querySet.Handle, first, count, destination.Handle, offset);
    }
}


/// <summary>
/// Collects the frame's GPU timestamps and prints them, once the host enables the diagnostic.
/// </summary>
/// <remarks>
/// Everything here exists only while <see cref="FrameDiagnostics.GpuTimings"/> is set: with the switch off no query set is
/// created, no timestamp is written and the log has no <c>gpu-timings:</c> line. Removing this type and its call sites
/// restores the previous renderer exactly (principle 13).
/// </remarks>
internal sealed class GpuTimingsRecorder : IGpuTimingMarkers, IDisposable
{
    private static readonly string[] SlotNames =
    [
        "frame-begin", "opaque-scene", "oit-bounds", "oit-cull", "oit-transmit", "oit-accumulate", "oit-composite",
        "clouds", "ui", "present-copy", "frame-end"
    ];

    private readonly IRenderBackend _backend;
    private readonly Instance _instance;
    private readonly QuerySetHandle _querySet;
    private readonly BufferHandle _resolve;
    private readonly BufferHandle _readback;
    private readonly double[] _last = new double[(int)GpuTimingSlot.Count];
    private readonly double[] _total = new double[(int)GpuTimingSlot.Count];
    private int _reports;

    private GpuTimingsRecorder(IRenderBackend backend, Instance instance, QuerySetHandle querySet, BufferHandle resolve, BufferHandle readback)
    {
        _backend = backend;
        _instance = instance;
        _querySet = querySet;
        _resolve = resolve;
        _readback = readback;
    }

    /// <summary>Creates the recorder, or returns null with a log line when the host did not ask or the GPU cannot do it.</summary>
    /// <param name="backend">Backend the queries go through.</param>
    /// <param name="instance">Instance the readback needs.</param>
    /// <param name="adapter">Adapter the device was created from.</param>
    /// <param name="enabled">Whether the host asked for GPU timings.</param>
    /// <returns>The recorder, or null.</returns>
    public static GpuTimingsRecorder? TryCreate(IRenderBackend backend, Instance instance, Adapter adapter, bool enabled)
    {
        if (!enabled)
        {
            return null;
        }

        if (!NativeGpuTimestamps.IsSupported(adapter))
        {
            Console.WriteLine("gpu-timings: unavailable on this adapter (no timestamp query feature); continuing without it");
            return null;
        }

        if (!NativeGpuTimestamps.EncoderTimestampsAvailable)
        {
            // Detected before anything is created: a timestamp written without the instance feature aborts the process, so
            // the switch has to decline here rather than discover it on the first frame.
            Console.WriteLine(
                "gpu-timings: unavailable with this binding (needs the TIMESTAMP_QUERY_INSIDE_ENCODERS instance feature, "
                + "which Brew.Wgpu 29.0.0.5 cannot request); continuing without GPU timings");
            return null;
        }

        var count = (uint)GpuTimingSlot.Count;
        var bytes = (ulong)count * sizeof(ulong);
        var querySet = backend.CreateTimestampQuerySet(count);
        // The resolve target and the readback target have to be separate buffers: a mappable buffer may only pair its map
        // usage with the opposite copy usage, so the timestamps are resolved then copied across in the same encoder.
        var resolve = backend.CreateBuffer(new BufferDesc(bytes, BufferUsages.QueryResolve | BufferUsages.CopySrc));
        var readback = backend.CreateBuffer(new BufferDesc(bytes, BufferUsages.MapRead | BufferUsages.CopyDst));
        Console.WriteLine($"gpu-timings: enabled, {count} timestamps per frame");
        return new GpuTimingsRecorder(backend, instance, querySet, resolve, readback);
    }

    /// <inheritdoc />
    public void Mark(CommandEncoderHandle encoder, GpuTimingSlot slot) => _backend.WriteTimestamp(encoder, _querySet, (uint)slot);

    /// <summary>Resolves, reads back and prints the current frame's timestamps.</summary>
    /// <param name="windowFrames">Frames since the previous report, printed as <c>n=</c>.</param>
    public void Report(int windowFrames)
    {
        var resolveEncoder = _backend.CreateCommandEncoder();
        _backend.ResolveQuerySet(resolveEncoder, _querySet, 0, (uint)GpuTimingSlot.Count, _resolve, 0);
        _backend.CopyBufferToBuffer(resolveEncoder, _resolve, 0, _readback, 0, (ulong)((int)GpuTimingSlot.Count * sizeof(ulong)));
        _backend.Submit(resolveEncoder);

        var bytes = _backend.ReadBuffer(_instance, _readback, 0, (nuint)((int)GpuTimingSlot.Count * sizeof(ulong)));
        var ticks = new ulong[(int)GpuTimingSlot.Count];
        for (var index = 0; index < ticks.Length; index++)
        {
            ticks[index] = BitConverter.ToUInt64(bytes, index * sizeof(ulong));
        }

        // The device reports the two ends of the frame; every other slot is the boundary after that stage.
        var totalMs = TicksToMilliseconds(ticks[(int)GpuTimingSlot.FrameEnd] - ticks[(int)GpuTimingSlot.FrameBegin]);
        var builder = new System.Text.StringBuilder("gpu-timings:");
        builder.Append(" n=").Append(windowFrames).Append(" total=").Append(totalMs.ToString("F3"));

        for (var index = 1; index < (int)GpuTimingSlot.FrameEnd; index++)
        {
            var ms = TicksToMilliseconds(ticks[index] - ticks[index - 1]);
            _last[index] = ms;
            _total[index] += ms;
            builder.Append(' ').Append(SlotNames[index]).Append('=').Append(ms.ToString("F3"));
        }

        Console.WriteLine(builder.ToString());
        _reports++;
    }

    /// <summary>Prints the mean of every stage over all reports, at shut down.</summary>
    public void WriteSummary()
    {
        if (_reports == 0)
        {
            return;
        }

        var builder = new System.Text.StringBuilder($"gpu-timings: mean over {_reports} reports:");
        for (var index = 1; index < (int)GpuTimingSlot.FrameEnd; index++)
        {
            builder.Append(' ').Append(SlotNames[index]).Append('=').Append((_total[index] / _reports).ToString("F3"));
        }

        Console.WriteLine(builder.ToString());
    }

    /// <summary>Converts a timestamp delta to milliseconds, using wgpu's nanosecond period.</summary>
    /// <param name="ticks">Timestamp delta in nanoseconds.</param>
    /// <returns>The same duration in milliseconds.</returns>
    private static double TicksToMilliseconds(ulong ticks) => ticks / 1_000_000d;

    /// <inheritdoc />
    public void Dispose()
    {
        _backend.Destroy(_readback);
        _backend.Destroy(_resolve);
        _backend.Destroy(_querySet);
    }
}