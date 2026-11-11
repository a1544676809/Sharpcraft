// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Picks the GPU adapter by calling the native interface directly, with a power preference the managed binding cannot
/// express, and reports which adapter was chosen.
/// </summary>
/// <remarks>
/// <para>
/// Why this exists: the binding's <c>Instance</c> only exposes <c>RequestAdapterBlocking(Surface?)</c>, which asks for the
/// driver's default adapter, so on a hybrid laptop the renderer silently landed on the integrated GPU. The C interface
/// behind the binding already takes <c>WGPURequestAdapterOptions</c> (with <c>powerPreference</c>) and can enumerate
/// adapters; this shim reaches those two calls and wraps the resulting handle back into the binding's <c>Adapter</c>.
/// </para>
/// <para>
/// Deletion condition (docs/decisions/0022-native-gpu-selection.md): delete this file the moment the binding ships a
/// managed overload that takes <c>WGPURequestAdapterOptions</c> or an adapter list. Nothing else depends on the shim except
/// one call site per renderer, which falls back to the binding when the shim cannot help.
/// </para>
/// <para>
/// No platform specific API is used: the calls are the cross platform C entry points that ship with the native library on
/// Windows, Linux and macOS.
/// </para>
/// </remarks>
internal static unsafe class NativeAdapterSelection
{
    /// <summary>One adapter the instance offers, with the reference handed to this code.</summary>
    /// <remarks>A struct rather than a record because a record cannot carry a native pointer field.</remarks>
    internal readonly struct Candidate
    {
        /// <summary>Creates one candidate.</summary>
        /// <param name="handle">Native adapter handle, owned by this code.</param>
        /// <param name="name">Adapter description or device name.</param>
        /// <param name="type">Discrete, integrated or CPU adapter.</param>
        /// <param name="backend">Backend the adapter was enumerated from.</param>
        internal Candidate(WGPUAdapterImpl* handle, string name, WGPUAdapterType type, WGPUBackendType backend)
        {
            Handle = handle;
            Name = name;
            Type = type;
            Backend = backend;
        }

        /// <summary>Native adapter handle, released by whoever consumes the candidate.</summary>
        internal WGPUAdapterImpl* Handle { get; }

        /// <summary>Adapter description, or its device name when the driver leaves the description empty.</summary>
        internal string Name { get; }

        /// <summary>Discrete, integrated or CPU adapter, as the driver reports it.</summary>
        internal WGPUAdapterType Type { get; }

        /// <summary>Vulkan, D3D12, Metal or another backend the adapter was enumerated from.</summary>
        internal WGPUBackendType Backend { get; }
    }

    /// <summary>All backend bits, so the enumeration covers every adapter the instance can see.</summary>
    /// <remarks>The mask is one bit per <c>WGPUBackendType</c> value, so this covers every backend the header defines.</remarks>
    private const ulong AllBackends = 0x1FE;

    /// <summary>Upper bound on a synchronous adapter request, in nanoseconds.</summary>
    private const ulong RequestTimeoutNanoseconds = 5_000_000_000;

    /// <summary>Reaches the binding's non public adapter constructor, so a native handle can become a managed adapter.</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern Adapter Wrap(WGPUAdapterImpl* handle, Instance instance);

    /// <summary>Reads the instance's native handle, which the binding keeps internal.</summary>
    /// <param name="instance">Managed instance.</param>
    /// <returns>The native handle.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Handle")]
    private static extern WGPUInstanceImpl* InstanceHandle(Instance instance);

    /// <summary>Reads the surface's native handle, which the binding keeps internal.</summary>
    /// <param name="surface">Managed surface.</param>
    /// <returns>The native handle.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Handle")]
    private static extern WGPUSurfaceImpl* SurfaceHandle(ref Surface surface);

    /// <summary>State the request callback fills in, on the stack of the caller that waits for it.</summary>
    private struct RequestState
    {
        public WGPURequestAdapterStatus Status;
        public WGPUAdapterImpl* Adapter;
    }

    /// <summary>
    /// Resolves the adapter a host option asks for.
    /// </summary>
    /// <param name="instance">Instance to ask.</param>
    /// <param name="surface">Surface the adapter has to be able to present to, or <c>default</c> for offscreen work.</param>
    /// <param name="spec">Host option: null or empty, <c>high</c>, <c>low</c>, <c>list</c>, or an adapter name substring.</param>
    /// <returns>An adapter the caller owns.</returns>
    /// <remarks>
    /// Never throws for a selection problem: an unavailable preference, an unknown name or an empty enumeration all end at
    /// the binding's own request, so a single GPU machine keeps rendering exactly as before.
    /// </remarks>
    internal static Adapter Resolve(Instance instance, Surface surface, string? spec) =>
        ResolveCore(instance, surface, SurfaceHandle(ref surface), spec);

    /// <summary>Resolves an adapter for work that does not present to a surface.</summary>
    /// <param name="instance">Instance to ask.</param>
    /// <param name="spec">Host option, as in the surface overload.</param>
    /// <returns>An adapter the caller owns.</returns>
    internal static Adapter Resolve(Instance instance, string? spec) =>
        ResolveCore(instance, default, null, spec);

    private static Adapter ResolveCore(Instance instance, Surface surface, WGPUSurfaceImpl* nativeSurface, string? spec)
    {
        var requested = string.IsNullOrWhiteSpace(spec) ? "high" : spec.Trim();

        if (requested.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            PrintCandidates(Enumerate(instance));
            requested = "high";
        }

        if (requested.Equals("high", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("low", StringComparison.OrdinalIgnoreCase))
        {
            var preference = requested.Equals("low", StringComparison.OrdinalIgnoreCase)
                ? WGPUPowerPreference.LowPower
                : WGPUPowerPreference.HighPerformance;

            if (RequestByPreference(instance, nativeSurface, preference) is { } preferred)
            {
                PrintSelected(preferred, $"preference={requested.ToLowerInvariant()}");
                return preferred;
            }

            Console.WriteLine($"gpu: preference={requested.ToLowerInvariant()} returned no adapter; using the binding's default request");
            return Default(instance, surface, $"preference={requested.ToLowerInvariant()} (unavailable)");
        }

        var candidates = Enumerate(instance);
        if (candidates.Count == 0)
        {
            Console.WriteLine($"gpu: no adapter to match \"{requested}\"; using the binding's default request");
            return Default(instance, surface, "preference=default (no adapters)");
        }

        var picked = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].Name.Contains(requested, StringComparison.OrdinalIgnoreCase))
            {
                picked = index;
                break;
            }
        }

        if (picked < 0)
        {
            Console.WriteLine($"gpu: no adapter name contains \"{requested}\"; available: {Describe(candidates)}");
            PrintCandidates(candidates);
            foreach (var candidate in candidates)
            {
                WGPU.wgpuAdapterRelease(candidate.Handle);
            }

            return Default(instance, surface, $"preference={requested} (not found)");
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            if (index != picked)
            {
                WGPU.wgpuAdapterRelease(candidates[index].Handle);
            }
        }

        var adapter = Wrap(candidates[picked].Handle, instance);
        PrintSelected(adapter, $"preference=name:{requested}");
        return adapter;
    }

    /// <summary>Asks for an adapter with an explicit power preference, waiting for the callback.</summary>
    /// <param name="instance">Instance to ask.</param>
    /// <param name="surface">Native surface the adapter must present to, or null.</param>
    /// <param name="preference">Power preference to request.</param>
    /// <returns>The adapter, or null when the request failed, timed out or produced no adapter.</returns>
    private static Adapter? RequestByPreference(Instance instance, WGPUSurfaceImpl* surface, WGPUPowerPreference preference)
    {
        var state = default(RequestState);
        var options = new WGPURequestAdapterOptions
        {
            featureLevel = WGPUFeatureLevel.Core,
            powerPreference = preference,
            compatibleSurface = surface
        };

        var callbackInfo = new WGPURequestAdapterCallbackInfo
        {
            mode = WGPUCallbackMode.AllowProcessEvents,
            callback = &OnAdapter,
            userdata1 = &state
        };

        _ = WGPU.wgpuInstanceRequestAdapter(InstanceHandle(instance), &options, callbackInfo);

        var deadline = Environment.TickCount64 + (long)(RequestTimeoutNanoseconds / 1_000_000);
        while (state.Status == WGPURequestAdapterStatus.Force32 && Environment.TickCount64 < deadline)
        {
            instance.ProcessEvents();
            Thread.Sleep(1);
        }

        if (state.Status != WGPURequestAdapterStatus.Success || state.Adapter is null)
        {
            return null;
        }

        return Wrap(state.Adapter, instance);
    }

    /// <summary>Callback that receives the adapter the native request produced.</summary>
    /// <param name="status">Request status.</param>
    /// <param name="adapter">Adapter handle, owned by the caller on success.</param>
    /// <param name="message">Driver message, unused.</param>
    /// <param name="userdata1">The <see cref="RequestState"/> the request was started with.</param>
    /// <param name="userdata2">Unused second slot.</param>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnAdapter(
        WGPURequestAdapterStatus status,
        WGPUAdapterImpl* adapter,
        WGPUStringView message,
        void* userdata1,
        void* userdata2)
    {
        var state = (RequestState*)userdata1;
        state->Status = status;
        state->Adapter = adapter;
    }

    /// <summary>Asks the instance to enumerate every adapter it can see.</summary>
    /// <param name="instance">Instance to ask.</param>
    /// <returns>The adapters, with the references owned by the caller.</returns>
    private static List<Candidate> Enumerate(Instance instance)
    {
        var options = new WGPUInstanceEnumerateAdapterOptions { backends = AllBackends };
        var count = WGPU.wgpuInstanceEnumerateAdapters(InstanceHandle(instance), &options, null);
        if (count == 0)
        {
            return [];
        }

        var handles = new WGPUAdapterImpl*[(int)count];
        fixed (WGPUAdapterImpl** slots = handles)
        {
            var written = WGPU.wgpuInstanceEnumerateAdapters(InstanceHandle(instance), &options, slots);
            if (written < count)
            {
                count = written;
            }
        }

        var candidates = new List<Candidate>((int)count);
        for (var index = 0; index < (int)count; index++)
        {
            var handle = handles[index];
            var info = default(WGPUAdapterInfo);
            if (WGPU.wgpuAdapterGetInfo(handle, &info) != WGPUStatus.Success)
            {
                WGPU.wgpuAdapterRelease(handle);
                continue;
            }

            var name = Decode(info.description);
            if (name.Length == 0)
            {
                name = Decode(info.device);
            }

            if (name.Length == 0)
            {
                name = Decode(info.vendor);
            }

            candidates.Add(new Candidate(handle, name, info.adapterType, info.backendType));
            WGPU.wgpuAdapterInfoFreeMembers(info);
        }

        return candidates;
    }

    /// <summary>Runs the binding's own request and reports what it picked.</summary>
    /// <param name="instance">Instance to ask.</param>
    /// <param name="surface">Surface to make compatible, or <c>default</c>.</param>
    /// <param name="policy">Policy text for the log line.</param>
    /// <returns>The adapter the binding picked.</returns>
    private static Adapter Default(Instance instance, Surface surface, string policy)
    {
        var adapter = instance.RequestAdapterBlocking(surface);
        PrintSelected(adapter, policy);
        return adapter;
    }

    /// <summary>Prints the adapter that was chosen, which is the line that was missing when the integrated GPU was used.</summary>
    /// <param name="adapter">Chosen adapter.</param>
    /// <param name="policy">Policy text, for example <c>preference=high</c>.</param>
    private static void PrintSelected(Adapter adapter, string policy)
    {
        var info = adapter.GetInfo();
        Console.WriteLine($"gpu: selected {DisplayName(info.Description, info.Device)} ({info.Type}, {info.Backend}) {policy}");
    }

    /// <summary>Picks the adapter name worth printing.</summary>
    /// <param name="description">Driver description, which some Vulkan drivers fill with a version number.</param>
    /// <param name="device">Device name, which those drivers fill with the card name.</param>
    /// <returns>The more descriptive of the two.</returns>
    private static string DisplayName(string description, string device)
    {
        static bool IsName(string text) => text.Length > 0 && text.Any(char.IsLetter);

        if (IsName(description))
        {
            return description;
        }

        return IsName(device) ? device : description.Length == 0 ? "unknown adapter" : description;
    }

    /// <summary>Prints every enumerated adapter, for the <c>list</c> option and for unmatched names.</summary>
    /// <param name="candidates">Adapters to print.</param>
    private static void PrintCandidates(List<Candidate> candidates)
    {
        if (candidates.Count == 0)
        {
            Console.WriteLine("gpu: no adapters enumerated");
            return;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            Console.WriteLine($"gpu: adapter {index + 1}/{candidates.Count}: {candidate.Name} ({candidate.Type}, {candidate.Backend})");
        }
    }

    /// <summary>Joins the adapter names for a one line warning.</summary>
    /// <param name="candidates">Adapters to name.</param>
    /// <returns>Comma separated names.</returns>
    private static string Describe(List<Candidate> candidates) =>
        candidates.Count == 0 ? "none" : string.Join(", ", candidates.Select(candidate => candidate.Name));

    /// <summary>Decodes a native string view into a managed string.</summary>
    /// <param name="view">String view the driver filled in.</param>
    /// <returns>The decoded text, or the empty string.</returns>
    private static string Decode(WGPUStringView view)
    {
        if (view.data is null || view.length == 0)
        {
            return string.Empty;
        }

        return System.Text.Encoding.UTF8.GetString((byte*)view.data, (int)view.length);
    }
}
