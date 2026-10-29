// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Interleaved geometry handed to the renderer. Framework code cannot depend on the client's mesh type, so
/// this is the neutral shape both sides agree on (docs/specs/network/play-movement.md 7.25).
/// </summary>
/// <param name="Vertices">Interleaved vertex floats, <see cref="VertexLayout.Stride"/> bytes per vertex.</param>
/// <param name="Indices">Triangle indices, zero based for this piece of geometry.</param>
public readonly record struct GeometryData(float[] Vertices, uint[] Indices);
