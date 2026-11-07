// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using Sharpcraft.Client.Render;

namespace Sharpcraft.Architecture.Tests;

/// <summary>Keeps baked geometry construction behind the internal Client-side baker boundary.</summary>
public sealed class EntityModelPartEncapsulationTests
{
    [Fact]
    public void EntityModelPartHasNoPublicParameterizedConstructor()
    {
        var constructors = typeof(EntityModelPart)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => constructor.GetParameters().Length > 0)
            .ToArray();

        Assert.Empty(constructors);
    }

    [Fact]
    public void OnlyTheBakerAndResolverConstructBakedParts()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "EntityModelMesher.cs",
            "EntityModelResolver.cs",
            "VanillaMeshBake.cs"
        };
        var root = RepositoryLocator.Root;
        var renderRoot = Path.Combine(root, "src", "Sharpcraft.Client", "Render");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(renderRoot, "*.cs"))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("new EntityModelPart(", StringComparison.Ordinal)
                && !allowed.Contains(Path.GetFileName(file)))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "direct baked-part construction outside the baker boundary: " + string.Join(", ", offenders));
    }
}
