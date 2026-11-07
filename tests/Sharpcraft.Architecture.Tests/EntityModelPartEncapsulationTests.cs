// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using System.Text.RegularExpressions;
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
            "PlayerModelDefinition.cs",
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

    [Fact]
    public void BakedPartClonesOnlyLiveInTheCentralTransformer()
    {
        var root = RepositoryLocator.Root;
        var renderRoot = Path.Combine(root, "src", "Sharpcraft.Client", "Render");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(renderRoot, "*.cs"))
        {
            if (Path.GetFileName(file) == "VanillaMeshBake.cs")
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), @"\b(part|source)\s+with\b"))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "baked-part clones outside the central transformer: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ModelDefinitionsDoNotGrowNewGeometryCloneSites()
    {
        // Ratchet for T-746b: these three legacy flat-path copy helpers still use `part/source with`. They are
        // deliberately pinned; fixing one requires shrinking this set in the same commit.
        var known = new HashSet<string>(StringComparer.Ordinal);

        var root = RepositoryLocator.Root;
        var renderRoot = Path.Combine(root, "src", "Sharpcraft.Client", "Render");
        var cloneSites = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(renderRoot, "*.cs"))
        {
            var name = Path.GetFileName(file);
            if (!name.Contains("ModelDefinition", StringComparison.Ordinal)
                && !name.Contains("ModelDefinitions", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"\b(part|source)\s+with\b", RegexOptions.Multiline))
            {
                cloneSites.Add(Path.GetFileName(file));
            }
        }

        var added = cloneSites.Except(known).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var stale = known.Except(cloneSites).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.True(added.Length == 0, "new geometry clone sites in model definitions: " + string.Join(", ", added));
        Assert.True(stale.Length == 0, "clone-site ratchet can shrink: remove these entries: " + string.Join(", ", stale));
    }
}
