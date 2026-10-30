// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Optional graphics capabilities a backend may or may not offer.
/// </summary>
/// <remarks>
/// This exists so a shader pack can be told "this pack needs a feature this backend does not have" before anything is
/// compiled, instead of failing halfway. The default backend is the WebGPU class one; a native backend added later would
/// report a superset here without any change above this layer.
/// </remarks>
[Flags]
public enum GraphicsCapability
{
    /// <summary>No optional capability.</summary>
    None = 0,

    /// <summary>Compute shaders (used by volumetrics, ambient occlusion, upsaclers).</summary>
    Compute = 1 << 0,

    /// <summary>Geometry shaders; the WebGPU class API has no such stage.</summary>
    GeometryShader = 1 << 1,

    /// <summary>Tessellation (control and evaluation stages).</summary>
    TessellationShader = 1 << 2,

    /// <summary>Mesh shaders.</summary>
    MeshShader = 1 << 3,

    /// <summary>A full hardware ray tracing pipeline with acceleration structures.</summary>
    RayTracingPipeline = 1 << 4,

    /// <summary>Variable rate shading.</summary>
    VariableRateShading = 1 << 5,

    /// <summary>Bindless resources through descriptor indexing.</summary>
    Bindless = 1 << 6,

    /// <summary>Sparse or partially resident resources.</summary>
    SparseResources = 1 << 7,

    /// <summary>Multiple hardware queues with timeline synchronisation.</summary>
    MultipleQueues = 1 << 8,

    /// <summary>Push constants.</summary>
    PushConstants = 1 << 9
}

/// <summary>
/// What a backend can do, plus the hard limits callers need to respect.
/// </summary>
/// <param name="Name">Backend name for logs.</param>
/// <param name="Capabilities">Optional features present.</param>
/// <param name="MaxColorAttachments">Colour targets one pass may write.</param>
/// <param name="MaxStorageBufferBindingSize">Largest storage buffer binding, in bytes.</param>
public readonly record struct BackendCapabilities(
    string Name,
    GraphicsCapability Capabilities,
    int MaxColorAttachments,
    long MaxStorageBufferBindingSize)
{
    /// <summary>True when the backend offers every requested capability.</summary>
    /// <param name="requested">Capabilities a shader pack declared as required.</param>
    public bool Supports(GraphicsCapability requested) => (Capabilities & requested) == requested;

    /// <summary>Lists the requested capabilities this backend lacks, for a readable refusal.</summary>
    /// <param name="requested">Capabilities a shader pack declared as required.</param>
    public IReadOnlyList<string> Missing(GraphicsCapability requested)
    {
        var missing = new List<string>();

        foreach (var value in Enum.GetValues<GraphicsCapability>())
        {
            if (value == GraphicsCapability.None)
            {
                continue;
            }

            if ((requested & value) == value && (Capabilities & value) != value)
            {
                missing.Add(value.ToString());
            }
        }

        return missing;
    }

    /// <summary>
    /// The capabilities of the default backend: the WebGPU class API as exposed by the bindings we use.
    /// </summary>
    /// <remarks>
    /// Deliberately conservative and stated as a constant: no geometry, tessellation or mesh stage exists in the API, the
    /// ray tracing pipeline is not exposed (only an experimental query feature), and there is no variable rate shading,
    /// sparse resources or bindless descriptor indexing. Compute and push constants are available.
    /// </remarks>
    public static BackendCapabilities Default { get; } = new(
        "wgpu",
        GraphicsCapability.Compute | GraphicsCapability.PushConstants,
        MaxColorAttachments: 8,
        MaxStorageBufferBindingSize: 128L * 1024 * 1024);
}
