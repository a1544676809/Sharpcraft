// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>#231: TexturePaths is a set-like enumeration; no path may be yielded twice.</summary>
public sealed class EntityModelTexturePathTests
{
    [Fact]
    public void EveryRegisteredDefinitionExposesUniqueTexturePaths()
    {
        var duplicates = new List<string>();
        foreach (var definition in EntityModelResolver.Definitions)
        {
            var paths = definition.TexturePaths.ToArray();
            var repeated = paths.GroupBy(path => path, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (repeated.Length > 0)
            {
                duplicates.Add(definition.Name + ": " + string.Join(", ", repeated));
            }
        }

        Assert.True(duplicates.Count == 0, string.Join("\n", duplicates));
    }

    [Fact]
    public void MainEmissiveAdditionalAndCubePathsAreYieldedOnce()
    {
        var cube = new EntityModelCube(
            "body", 0f, 0f, 0f, 1f, 1f, 1f,
            TexturePath: "entity/test/main", EmissiveTexturePath: "entity/test/emissive");
        var node = new EntityModelPartNode(
            "body", null, 0f, 0f, 0f, 0f, 0f, 0f, [cube], []);
        var root = new EntityModelPartNode("root", null, 0f, 0f, 0f, 0f, 0f, 0f, [], [node]);
        var definition = new EntityModelDefinition(
            "dedupe", 16, 16,
            texturePath: "entity/test/main",
            emissiveTexturePath: "entity/test/emissive",
            additionalTexturePaths: ["entity/test/main", "entity/test/emissive", "entity/test/extra"],
            compiled: new CompiledEntityModel(root, [node], [cube]));

        Assert.Equal(
            ["entity/test/main", "entity/test/emissive", "entity/test/extra"],
            definition.TexturePaths.ToArray());
        Assert.Equal(definition.TexturePaths.Count(), definition.TexturePaths.Distinct(StringComparer.Ordinal).Count());
    }
}
