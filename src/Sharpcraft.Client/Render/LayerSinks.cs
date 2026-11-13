// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Buffers and state that take the non solid layers out of the solid mesh.
/// </summary>
/// <param name="Layout">Layout that names block states, needed to decide the layer.</param>
/// <param name="Cutout">Buffer that receives alpha tested faces.</param>
/// <param name="Translucent">Buffer that receives blended faces.</param>
/// <param name="Tint">Biome tint, or null for none.</param>
public sealed record LayerSinks(
    BlockStateLayout? Layout,
    MeshBuffer? Cutout,
    MeshBuffer? Translucent,
    BiomeTint? Tint);

/// <summary>
/// Immutable meshing configuration shared by synchronous cache builds and background worker threads
/// so both paths pass identical parameters to <see cref="ChunkMesher.MeshChunk(Sharpcraft.Client.World.ClientWorld, Sharpcraft.Common.World.LevelChunk, MeshBuffer, MeshBuffer?, MeshBuffer?, in MeshingContext)"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.MeshingContext", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Sharpcraft-only immutable parameter bundle unifying synchronous and worker meshing paths")]
public readonly record struct MeshingContext(
    BlockSpriteMap? Sprites = null,
    BlockStateLayout? Layout = null,
    BiomeTint? Tint = null,
    StateCullTable? Cull = null,
    StateVisualCache? Visuals = null,
    bool MergeFaces = true,
    bool AmbientOcclusion = true,
    int AirState = 0,
    int MaxBlocks = 2_000_000);
