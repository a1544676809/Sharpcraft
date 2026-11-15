// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Reference;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Scratch owned by whoever builds a frame's block entity geometry, plus the result of the last
/// build and a version that changes whenever that result does.
/// </summary>
/// <remarks>
/// The reference keeps the visible block entity list on its per-frame render state
/// (<c>LevelRenderState.blockEntityRenderStates</c>) and reuses it instead of allocating a new one
/// every frame. This is the same idea: the buffers live in an object with one owner rather than in
/// static fields, so there is no shared mutable state between callers.
///
/// The geometry instance is reused across frames, so identity cannot signal that it changed. The
/// version does: it advances on every build, including builds that produce nothing.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.state.LevelRenderState", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/state/LevelRenderState.java")]
public sealed class BlockEntityFrameState
{
    /// <summary>Visible block entity states for this frame; cleared at the start of each build.</summary>
    public List<BlockEntityRenderState> States { get; } = [];

    /// <summary>Position lookup used to pair the halves of a double chest; cleared at each build.</summary>
    public Dictionary<long, BlockEntityRenderState> ByPosition { get; } = [];

    /// <summary>Mesher reused across frames; its geometry is cleared at the start of each build.</summary>
    public ChestMesher Mesher { get; } = new();

    /// <summary>Geometry produced by the last build, or null when there was nothing to draw.</summary>
    public DynamicGeometry? Geometry { get; private set; }

    /// <summary>Advances on every build, so a consumer can tell a rebuild from a repeat.</summary>
    public int Version { get; private set; }

    /// <summary>Records the result of a build and marks the version as changed.</summary>
    /// <param name="geometry">Geometry built this frame, or null when nothing was built.</param>
    public void Publish(DynamicGeometry? geometry)
    {
        Geometry = geometry;
        Version++;
    }
}
