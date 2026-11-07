// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Registry-wide compiled-tree and explicit layer-order invariant gate (T-741; the gap it found was closed by T-742).
/// Scans the registered model definitions (base, adult variants and baby variants; decorated/equipped copies are
/// derived and not part of the registry) so that every future model migration is forced through the tree path and
/// keeps an unambiguous layer plan. The gate is strict: no flat-path exception baseline remains.
/// </summary>
public sealed class EntityModelTreeCoverageTests
{
    [Fact]
    public void EveryRegisteredDefinitionHasACompiledTree()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var withoutTree = definitions
            .Where(definition => definition.Compiled is null)
            .Select(definition => definition.Name)
            .ToArray();

        // Strict gate: any newly registered definition still on the legacy flat path fails here, and no known gap
        // remains. The T-741 ratchet baseline (baby_hoglin/baby_zoglin) was removed by T-742 once those two were
        // migrated to the compiled tree path.
        Assert.True(withoutTree.Length == 0,
            "registered definitions still on the legacy flat path (no Compiled tree): " + string.Join(", ", withoutTree));
    }

    [Fact]
    public void EveryCompiledTreeHasARootAndGeometry()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var violations = new List<string>();
        foreach (var definition in definitions)
        {
            var compiled = definition.Compiled;
            Assert.True(compiled is not null, definition.Name + ": missing Compiled tree");

            // Root is a non-nullable node in the bakery contract; assert it anyway so a malformed constructor
            // cannot silently produce a tree with no pose root.
            if (compiled!.Root is null)
            {
                violations.Add(definition.Name + ": null Root");
            }

            // A registered entity model always contributes at least one part and one cube. Zero-cube *group nodes*
            // are legal inside a tree (they carry pose/visibility), but the compiled snapshot as a whole must not be
            // empty; BuildElytraLayer can legitimately return an empty tree, yet that is a derived layer model and
            // never a registered definition.
            if (compiled.Parts.Count == 0)
            {
                violations.Add(definition.Name + ": empty Parts");
            }

            if (compiled.Cubes.Count == 0)
            {
                violations.Add(definition.Name + ": empty Cubes");
            }
        }

        Assert.True(violations.Count == 0, "compiled tree geometry invariant violated:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void BabyHoglinAndZoglinShareOneCompiledTree()
    {
        Assert.NotNull(BabyModelDefinitionsNether.Hoglin.Compiled);
        Assert.NotNull(BabyModelDefinitionsNether.Zoglin.Compiled);
        Assert.Same(BabyModelDefinitionsNether.Hoglin.Compiled, BabyModelDefinitionsNether.Zoglin.Compiled);
        Assert.True(BabyModelDefinitionsNether.Hoglin.Compiled!.Cubes.Count > 0);
    }

    [Fact]
    public void RenderLayerDefinitionsAreUniqueOrderedAndIncludeCutout()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var violations = new List<string>();
        foreach (var definition in definitions)
        {
            var layers = definition.RenderLayers;
            if (layers.Count == 0)
            {
                violations.Add(definition.Name + ": no RenderLayers");
                continue;
            }

            if (!layers.Any(layer => layer.Kind == EntityGeometryLayerKind.Cutout))
            {
                violations.Add(definition.Name + ": no Cutout layer");
            }

            var names = layers.Select(layer => layer.Name).ToArray();
            if (names.Length != names.Distinct(StringComparer.Ordinal).Count())
            {
                violations.Add(definition.Name + ": duplicate layer Name");
            }

            var orders = layers.Select(layer => layer.Order).ToArray();
            if (orders.Length != orders.Distinct().Count())
            {
                violations.Add(definition.Name + ": duplicate layer Order");
            }

            if (!orders.SequenceEqual(orders.OrderBy(static order => order)))
            {
                violations.Add(definition.Name + ": RenderLayers not declared in ascending Order");
            }
        }

        Assert.True(violations.Count == 0, "render layer invariant violated:\n" + string.Join("\n", violations));
    }
}
