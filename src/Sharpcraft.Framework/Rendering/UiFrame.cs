// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Platform;

namespace Sharpcraft.Framework.Rendering;

/// <summary>One frame's interface context: the input state and the virtual canvas it is drawn into.</summary>
/// <param name="Input">Input state for this frame.</param>
/// <param name="Width">Virtual width in interface pixels, not device pixels.</param>
/// <param name="Height">Virtual height in interface pixels.</param>
public readonly record struct UiFrame(WindowInputState Input, int Width, int Height);
