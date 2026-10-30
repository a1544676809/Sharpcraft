// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// The one place resources are read from (docs/analysis/assets/resource-pack-stack.md).
/// </summary>
/// <remarks>
/// The reference has a single resource manager that builds one fallback stack per namespace and hands it to every
/// consumer, so a pack is discovered once and every loader sees the same override. Building a stack per loader, which is
/// what we did before, discovered and opened the same packs several times and would have made a reload fire more than
/// once. This type owns the stack for the whole session instead.
/// </remarks>
public sealed class ResourceManager : IDisposable
{
    private AssetStack _stack;

    private readonly Func<(AssetStack Stack, int PackCount)>? _factory;
    private readonly List<ReloadListener> _listeners = [];
    private readonly Lock _gate = new();

    /// <summary>Wraps a prepared stack.</summary>
    /// <param name="stack">Stack whose sources are owned by this manager.</param>
    /// <param name="packCount">How many installed packs were layered over the base pack.</param>
    /// <param name="factory">Rebuilds the stack for a reload; without it the manager is static.</param>
    public ResourceManager(AssetStack stack, int packCount, Func<(AssetStack Stack, int PackCount)>? factory = null)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _stack = stack;
        PackCount = packCount;
        _factory = factory;
    }

    /// <summary>Registers a listener to run after a reload swaps the stack.</summary>
    /// <param name="id">Diagnostic name.</param>
    /// <param name="order">Lower runs first; use it to keep language ahead of anything that reads messages.</param>
    /// <param name="reload">Work to redo against the new stack.</param>
    /// <remarks>
    /// Listeners, not the manager, decide what a reload means for their own caches: a font atlas and a sprite atlas free
    /// and rebuild different things. Running them in a fixed order is what stops one reading data another has not built.
    /// </remarks>
    public void AddListener(string id, int order, Action<ResourceManager> reload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(reload);
        _listeners.Add(new ReloadListener(id, order, reload));
    }

    /// <summary>
    /// Rebuilds the stack and tells every listener, leaving the old stack in place if the rebuild fails.
    /// </summary>
    /// <returns>True when the new stack was adopted.</returns>
    /// <remarks>
    /// The reference prepares off thread and applies on the main thread, and a failure aborts the whole attempt rather
    /// than leaving resources half swapped. Building the replacement first and swapping one reference gives the same
    /// all-or-nothing property, at the cost of holding both stacks briefly.
    /// </remarks>
    public bool Reload()
    {
        if (_factory is null)
        {
            return false;
        }

        (AssetStack Stack, int PackCount) rebuilt;

        try
        {
            rebuilt = _factory();
        }
        catch (IOException exception)
        {
            Console.WriteLine($"resource reload: kept the previous stack ({exception.Message})");
            return false;
        }
        catch (InvalidDataException exception)
        {
            Console.WriteLine($"resource reload: kept the previous stack ({exception.Message})");
            return false;
        }

        AssetStack previous;

        lock (_gate)
        {
            previous = _stack;
            _stack = rebuilt.Stack;
            PackCount = rebuilt.PackCount;
        }

        previous.Dispose();

        foreach (var listener in _listeners.OrderBy(entry => entry.Order))
        {
            listener.Reload(this);
        }

        return true;
    }

    /// <summary>How many installed packs are layered over the base pack.</summary>
    public int PackCount { get; private set; }

    /// <summary>The stack, for diagnostics and for a future reload.</summary>
    public AssetStack Stack => _stack;

    /// <summary>Reads a resource, taking the highest priority pack that carries it.</summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="location">Identifier.</param>
    /// <returns>The bytes, or null when no pack carries it.</returns>
    public byte[]? Read(ResourceKind kind, Identifier location) => _stack.Read(kind, location);

    /// <summary>
    /// Opens a raw pack file, taking the highest priority pack that carries it.
    /// </summary>
    /// <param name="relativePath">Path from the pack root, such as a <c>.mcmeta</c> beside a texture.</param>
    /// <returns>The stream, or null when no pack carries the path.</returns>
    public Stream? OpenFile(string relativePath)
    {
        var sources = _stack.Sources;

        for (var index = sources.Count - 1; index >= 0; index--)
        {
            if (sources[index].TryOpenPath(relativePath, out var content))
            {
                return content;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose() => _stack.Dispose();

    /// <summary>One post reload step.</summary>
    /// <param name="Id">Diagnostic name.</param>
    /// <param name="Order">Sort key; lower runs first.</param>
    /// <param name="Reload">Work to redo.</param>
    private sealed record ReloadListener(string Id, int Order, Action<ResourceManager> Reload);
}
