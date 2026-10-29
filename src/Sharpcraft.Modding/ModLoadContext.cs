// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Runtime.Loader;

namespace Sharpcraft.Modding;

/// <summary>
/// Collectible load context for one mod directory. The contract assembly is shared with the host so that
/// <see cref="ISharpcraftMod"/> keeps a single identity; every other dependency is resolved from the mod
/// directory or the default context (docs/modules/modding.md 4).
/// </summary>
public sealed class ModLoadContext : AssemblyLoadContext
{
    private readonly string _directory;

    /// <summary>Creates a collectible context rooted at a mod directory.</summary>
    /// <param name="name">Context name, shown in diagnostics.</param>
    /// <param name="directory">Directory the mod and its private dependencies live in.</param>
    public ModLoadContext(string name, string directory)
        : base(name, isCollectible: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <summary>Directory dependencies are probed in.</summary>
    public string Directory => _directory;

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The contract must be the host's copy, otherwise the mod's ISharpcraftMod would be a different
        // type and the host could never cast an instance to it.
        if (string.Equals(assemblyName.Name, typeof(ISharpcraftMod).Assembly.GetName().Name, StringComparison.Ordinal))
        {
            return null;
        }

        var candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
