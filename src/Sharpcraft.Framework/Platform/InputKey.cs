// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Platform;

/// <summary>
/// Platform-neutral key identifiers. The window layer translates the backend's key codes into these, so
/// game code never sees a GLFW or Win32 constant (docs/modules/input.md 3).
/// </summary>
public enum InputKey
{
    /// <summary>W — move forward.</summary>
    W,

    /// <summary>A — move left.</summary>
    A,

    /// <summary>S — move back.</summary>
    S,

    /// <summary>D — move right.</summary>
    D,

    /// <summary>Space — jump.</summary>
    Space,

    /// <summary>Left shift — sneak.</summary>
    LeftShift,

    /// <summary>Left control — sprint.</summary>
    LeftControl,

    /// <summary>Escape — release the cursor.</summary>
    Escape
}
