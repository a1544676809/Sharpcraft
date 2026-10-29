// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;

namespace Sharpcraft.Modding;

/// <summary>
/// Loads mod assemblies into collectible contexts and unloads them. The host never keeps a reference to
/// the context, to a mod instance, or to a type from a mod: doing so would pin the context and the unload
/// check would fail (docs/modules/modding.md 5).
/// </summary>
public sealed class ModHost
{
    private readonly List<WeakReference<ModLoadContext>> _contexts = [];
    private readonly Action<string> _log;

    /// <summary>Creates a host.</summary>
    /// <param name="gameVersion">Game version reported to mods.</param>
    /// <param name="log">Diagnostic sink; defaults to standard error.</param>
    public ModHost(string gameVersion, Action<string>? log = null)
    {
        GameVersion = gameVersion ?? throw new ArgumentNullException(nameof(gameVersion));
        _log = log ?? (message => Console.Error.WriteLine(message));
    }

    /// <summary>Game version reported to mods.</summary>
    public string GameVersion { get; }

    /// <summary>Number of contexts loaded and not yet unloaded.</summary>
    public int LoadedCount => _contexts.Count(context => context.TryGetTarget(out _));

    /// <summary>
    /// Loads the mod assembly at a path and instantiates its entry point.
    /// </summary>
    /// <param name="assemblyPath">Path to the mod assembly.</param>
    /// <returns>A handle to pass to <see cref="Unload"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The file is missing, exposes no <see cref="ISharpcraftMod"/>, or exposes more than one.
    /// </exception>
    public ModHandle Load(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"mod assembly not found: {fullPath}");
        }

        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new InvalidOperationException($"mod assembly has no directory: {fullPath}");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        var context = new ModLoadContext(name, directory);

        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            var entryType = FindEntryType(assembly);
            var modContext = new HostContext(name, GameVersion);

            var instance = (ISharpcraftMod)Activator.CreateInstance(entryType)!;
            instance.OnLoad(modContext);
            _log($"[modding] loaded {instance.Id} from {fullPath}");

            _contexts.Add(new WeakReference<ModLoadContext>(context));

            // The outgoing handle deliberately carries only value data and weak references, so returning
            // it does not pin either the instance or its context.
            return new ModHandle(
                instance.Id,
                entryType.FullName ?? entryType.Name,
                new WeakReference<object>(instance),
                new WeakReference<ModLoadContext>(context));
        }
        catch
        {
            // A half-loaded context must not survive a failure.
            UnloadQuietly(context);
            throw;
        }
    }

    /// <summary>Unloads a mod: calls its unload hook, then marks the context collectible.</summary>
    /// <param name="handle">Handle returned by <see cref="Load"/>.</param>
    /// <returns>True when the context was marked for unload.</returns>
    public bool Unload(ModHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (handle.Instance.TryGetTarget(out var instance) && instance is ISharpcraftMod mod)
        {
            try
            {
                mod.OnUnload();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // An unload hook that throws must not keep the context alive.
                _log($"[modding] {handle.Id} unload hook threw: {exception.Message}");
            }
        }

        // If the context is already gone there is nothing left to unload.
        if (!handle.Context.TryGetTarget(out var context))
        {
            return false;
        }

        _contexts.RemoveAll(reference =>
            !reference.TryGetTarget(out var loaded) || ReferenceEquals(loaded, context));

        return UnloadQuietly(context);
    }

    /// <summary>True when every loaded context has been collected; used by unload verification.</summary>
    public bool AllContextsCollected() => !_contexts.Any(reference => reference.TryGetTarget(out _));

    /// <summary>Forces collection and reports whether every context died. Tests only.</summary>
    /// <param name="attempts">Collection rounds to attempt.</param>
    public bool VerifyAllCollected(int attempts = 8)
    {
        for (var attempt = 0; attempt < attempts && !AllContextsCollected(); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return AllContextsCollected();
    }

    private static Type FindEntryType(Assembly assembly)
    {
        var candidates = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           typeof(ISharpcraftMod).IsAssignableFrom(type))
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"{assembly.GetName().Name} exposes no {nameof(ISharpcraftMod)} implementation"),
            _ => throw new InvalidOperationException(
                $"{assembly.GetName().Name} exposes {candidates.Length} {nameof(ISharpcraftMod)} implementations; exactly one is allowed")
        };
    }

    private static bool UnloadQuietly(ModLoadContext context)
    {
        try
        {
            context.Unload();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class HostContext(string modId, string gameVersion) : IModContext
    {
        public string ModId { get; } = modId;

        public string GameVersion { get; } = gameVersion;

        public int ContractVersion => 1;

        public void Log(string message) => Console.Error.WriteLine($"[mod:{ModId}] {message}");
    }
}
