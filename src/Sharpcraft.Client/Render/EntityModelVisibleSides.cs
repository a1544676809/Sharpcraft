// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per-cube bit mask of the cube faces a raw model emits. Mirrors the reference
/// <c>CubeListBuilder.addBox(..., Set&lt;Direction&gt; visibleFaces)</c> face selection: only the flagged
/// directions produce a polygon. The default <see cref="All"/> keeps every face, so legacy and all existing
/// raw models are unchanged. Bit values follow the reference <c>Direction</c> 3D-data ordering used by the
/// source cube builder (DOWN, UP, NORTH, SOUTH, WEST, EAST).
/// Source: ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java (Cube polygon construction).
/// </summary>
[System.Flags]
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.builders.CubeListBuilder", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeListBuilder.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Per-cube face selection mask (Set<Direction> visibleFaces in CubeListBuilder.addBox)")]
public enum EntityModelVisibleSides : byte
{
    /// <summary>No faces (degenerate cube).</summary>
    None = 0x00,
    /// <summary>Downward face (y-min).</summary>
    Down = 0x01,
    /// <summary>Upward face (y-max).</summary>
    Up = 0x02,
    /// <summary>North face (z-min).</summary>
    North = 0x04,
    /// <summary>South face (z-max).</summary>
    South = 0x08,
    /// <summary>West face (x-min).</summary>
    West = 0x10,
    /// <summary>East face (x-max).</summary>
    East = 0x20,
    /// <summary>All six faces (default).</summary>
    All = Down | Up | North | South | West | East
}
