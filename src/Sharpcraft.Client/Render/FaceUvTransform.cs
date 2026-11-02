// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Per-face diagnostic UV transform used by the interactive Face UV Studio.</summary>
public readonly record struct FaceUvTransform(int Rotation, bool FlipH, bool FlipV)
{
    public static FaceUvTransform Identity => new(0, false, false);
}
