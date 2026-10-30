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
