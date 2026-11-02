// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// One world-space text draw: interleaved position (3), uv (2), colour (4) vertices and triangle indices.
/// A uv.x below zero selects the solid-colour path and is used for the text background quads.
/// </summary>
public sealed record WorldTextDraw(float[] Vertices, uint[] Indices);
