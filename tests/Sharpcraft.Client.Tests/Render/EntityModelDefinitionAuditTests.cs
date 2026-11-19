// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Reflection;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Issue #229 production gate: the whole Sharpcraft.Client static-field graph is inspected, not only
/// EntityModelResolver.Definitions. Definition-bearing static fields are EntityModelDefinition,
/// EntityModelDefinition[], IEnumerable/IReadOnlyList&lt;EntityModelDefinition&gt;, and
/// Dictionary/IReadOnlyDictionary&lt;,&gt; with EntityModelDefinition as the value. Container elements are
/// expanded and the result is reference-deduplicated, then EntityModelResolver.Definitions is added.
/// The baselines below are change detectors: deliberately adding/removing a definition must update them.
/// </summary>
public sealed class EntityModelDefinitionAuditTests
{
    // static-field graph, reference-deduped: 386 in the isolated gate at impl-remove-flat 8fc6a832.
    // Full-suite execution can observe more when tests register derived definitions in client static caches,
    // so the gate asserts this documented minimum plus the strict FLAT_NO_COMPILED=0 invariant.
    private const int StaticFieldGraphBaseline = 386;

    [Fact]
    public void EveryStaticFieldGraphDefinitionHasACompiledTree()
    {
        _ = EntityModelResolver.Definitions; // initialize resolver variant caches before the static-field graph is read.
        var definitions = StaticFieldGraph(typeof(EntityModelDefinition).Assembly)
            .Concat(EntityModelResolver.Definitions)
            .Distinct(ReferenceEqualityComparer.Instance)
            .OfType<EntityModelDefinition>()
            .ToArray();
        Assert.NotEmpty(definitions);
        Assert.True(definitions.Length >= StaticFieldGraphBaseline,
            $"static-field graph, reference-deduped: {definitions.Length} definitions; minimum baseline {StaticFieldGraphBaseline}");

        var flat = definitions.Where(definition => definition.Compiled is null).Select(definition => definition.Name).ToArray();
        Assert.True(flat.Length == 0,
            $"FLAT_NO_COMPILED={flat.Length}: {string.Join(", ", flat)}");
    }

    [Fact]
    public void PartOnlyDefinitionFailsLoudlyInBuildModel()
    {
        var invalid = new EntityModelDefinition(
            "part_only",
            PlayerModelDefinition.Definition.TextureWidth,
            PlayerModelDefinition.Definition.TextureHeight,
            PlayerModelDefinition.Definition.Parts);
        Assert.Null(invalid.Compiled);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/player/steve"] = new(64, 64, new byte[64 * 64 * 4]),
        });
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var exception = Assert.Throws<InvalidOperationException>(() => new EntityModelMesher().BuildModel(
            invalid, state, atlas.Get("entity/player/steve")!, 64, 64, modelScale: 1f));
        Assert.Contains("no compiled tree", exception.Message, StringComparison.Ordinal);
        Assert.Contains("part_only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigratedSixDefinitionsRemainCoveredByKeyedSamplesOrStructure()
    {
        // 5/6 have independent keyed source samples already checked by the named test methods.
        new ChestRawReferenceSampleTests().RawGeometryMatchesSourceBindPose("chest-single-bone-raw-vectors.reference.txt", "chest_single");
        new ChestRawReferenceSampleTests().RawGeometryMatchesSourceBindPose("chest-double-left-bone-raw-vectors.reference.txt", "chest_double_left");
        new ChestRawReferenceSampleTests().RawGeometryMatchesSourceBindPose("chest-double-right-bone-raw-vectors.reference.txt", "chest_double_right");
        new ShulkerBoxRawReferenceSampleTests().RawBindGeometryMatchesSource48();
        new LivingRawReferenceSampleTests().RawBakeMatchesSourceVertices(
            "dragon-skull-raw-vectors.reference.txt", SkullModelDefinition.DragonDefinition, 256, 256,
            "entity/enderdragon/dragon", 168, 48, 120, 0);

        // placeholder has no source sample; it is the fallback box and stays a deterministic compiled tree.
        var placeholder = EntityModelResolver.Placeholder;
        Assert.NotNull(placeholder.Compiled);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/player/steve"] = new(64, 64, new byte[64 * 64 * 4]),
        });
        var geometry = new EntityModelMesher().BuildModel(
            placeholder, new EntityRenderState(1, 0, 0f, 0f, 0f, 0f, 0f),
            atlas.Get("entity/player/steve")!, 64, 64, modelScale: 1f);
        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
    }

    private static IEnumerable<EntityModelDefinition> StaticFieldGraph(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!HoldsDefinitions(field.FieldType))
                {
                    continue;
                }

                object? value;
                try
                {
                    value = field.GetValue(null);
                }
                catch (TypeInitializationException)
                {
                    continue;
                }

                foreach (var definition in Expand(value))
                {
                    yield return definition;
                }
            }
        }
    }

    private static bool HoldsDefinitions(Type type)
    {
        if (type == typeof(EntityModelDefinition))
        {
            return true;
        }

        if (type.IsArray)
        {
            return type.GetElementType() == typeof(EntityModelDefinition);
        }

        return type.IsGenericType
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            && type.GetGenericArguments().Contains(typeof(EntityModelDefinition));
    }

    private static IEnumerable<EntityModelDefinition> Expand(object? value)
    {
        if (value is EntityModelDefinition direct)
        {
            yield return direct;
            yield break;
        }

        if (value is not System.Collections.IEnumerable sequence || value is string)
        {
            yield break;
        }

        foreach (var item in sequence)
        {
            if (item is EntityModelDefinition definition)
            {
                yield return definition;
                continue;
            }

            if (item is null)
            {
                continue;
            }

            var valueProperty = item.GetType().GetProperty("Value");
            if (valueProperty?.GetValue(item) is EntityModelDefinition nested)
            {
                yield return nested;
            }
        }
    }
}
