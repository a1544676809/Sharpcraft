// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Modding;

/// <summary>
/// Entry point a mod implements. The host resolves exactly one public type implementing this interface,
/// calls <see cref="OnLoad"/>, and on unload calls <see cref="OnUnload"/> before dropping the load
/// context (docs/modules/modding.md 4).
/// </summary>
/// <remarks>
/// Implementations must not keep static state that outlives the context: a static field in a collectible
/// assembly keeps its type alive and the context then leaks, which the host's unload check reports as a
/// failure.
/// </remarks>
public interface ISharpcraftMod
{
    /// <summary>Mod identifier; used in diagnostics and unload bookkeeping.</summary>
    string Id { get; }

    /// <summary>Called once after the mod is loaded.</summary>
    /// <param name="context">Host-provided capabilities.</param>
    void OnLoad(IModContext context);

    /// <summary>Called once before the mod is unloaded; release everything here.</summary>
    void OnUnload();
}
