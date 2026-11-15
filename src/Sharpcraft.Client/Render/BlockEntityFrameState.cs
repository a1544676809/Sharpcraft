// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Scratch owned by whoever builds a frame's block entity geometry.
/// </summary>
/// <remarks>
/// The reference keeps the visible block entity list on its per-frame render state
/// (<c>LevelRenderState.blockEntityRenderStates</c>) and reuses it instead of allocating a new one
/// every frame. This is the same idea: the buffers live in an object with one owner rather than in
/// static fields, so there is no shared mutable state between callers. The owner is expected to be
/// the thread that builds the frame.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.state.level.LevelRenderState", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/state/level/LevelRenderState.java", Status = ReferenceStatus.Partial)]
public sealed class BlockEntityFrameState
{
    /// <summary>Visible block entity states for this frame; cleared at the start of each build.</summary>
    public List<BlockEntityRenderState> States { get; } = [];

    /// <summary>Position lookup used to pair the halves of a double chest; cleared at each build.</summary>
    public Dictionary<long, BlockEntityRenderState> ByPosition { get; } = [];

    /// <summary>The one dispatcher used by the frame path; it owns every renderer's buffers.</summary>
    public BlockEntityRenderDispatcher Dispatcher { get; } = new();
}
