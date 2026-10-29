// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Modding;

/// <summary>
/// A loaded mod. Every reference here is weak or plain value data on purpose: a handle that kept a strong
/// reference to the mod instance or its load context would make the context permanently unreclaimable, and
/// the unload check would then fail by design (docs/modules/modding.md 5).
/// </summary>
public sealed class ModHandle
{
    internal ModHandle(string id, string entryTypeName, WeakReference<object> instance, WeakReference<ModLoadContext> context)
    {
        Id = id;
        EntryTypeName = entryTypeName;
        Instance = instance;
        Context = context;
    }

    /// <summary>Mod identifier reported by the mod itself.</summary>
    public string Id { get; }

    /// <summary>Full name of the entry type, kept for diagnostics.</summary>
    public string EntryTypeName { get; }

    /// <summary>Weak reference to the mod instance.</summary>
    internal WeakReference<object> Instance { get; }

    /// <summary>Weak reference to the mod's load context.</summary>
    internal WeakReference<ModLoadContext> Context { get; }

    /// <inheritdoc />
    public override string ToString() => $"ModHandle({Id} @ {EntryTypeName})";
}
