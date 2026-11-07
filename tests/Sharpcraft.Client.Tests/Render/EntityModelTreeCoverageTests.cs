// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Registry-wide compiled-tree and explicit layer-order invariant gate (T-741). Scans the registered model
/// definitions (base, adult variants and baby variants; decorated/equipped copies are derived and not part of the
/// registry) so that every future model migration is forced through the tree path and keeps an unambiguous layer plan.
/// </summary>
public sealed class EntityModelTreeCoverageTests
{
    /// <summary>
    /// Known, still-open migration gap (T-741 discovery, not a legitimate architecture exception):
    /// <c>BabyModelDefinitionsNether</c> declares <c>HoglinMesh</c>/<c>HoglinParts</c> for baby hoglin/zoglin but never
    /// bakes them with <c>VanillaMeshBakery</c>, so both definitions remain on the legacy flat path. T-727 migrated only
    /// the baby piglin pair in that file and the T-734..T-737 batches did not include it.
    ///
    /// This set is a ratchet, not a whitelist to grow: a newly registered flat definition fails the gate, and once a
    /// follow-up migration gives these two a compiled tree the stale baseline entry fails until it is removed. Closing
    /// the gap requires a production change, which T-741 deliberately leaves to a later task.
    /// </summary>
    private static readonly HashSet<string> KnownLegacyFlatDefinitions = new(StringComparer.Ordinal)
    {
        "baby_hoglin",
        "baby_zoglin"
    };

    [Fact]
    public void EveryRegisteredDefinitionHasACompiledTree()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var withoutTree = definitions
            .Where(definition => definition.Compiled is null)
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.Ordinal);

        var unexpected = withoutTree.Where(name => !KnownLegacyFlatDefinitions.Contains(name)).ToArray();
        var stale = KnownLegacyFlatDefinitions.Where(name => !withoutTree.Contains(name)).ToArray();
        Assert.True(unexpected.Length == 0 && stale.Length == 0,
            "new definitions on the legacy flat path: " + string.Join(", ", unexpected)
            + "\nbaseline entries that are now compiled (shrink the baseline): " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryCompiledTreeHasARootAndGeometry()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var violations = new List<string>();
        foreach (var definition in definitions)
        {
            // Only the explicitly pinned gap may be skipped; any other null tree is a regression caught by the first test.
            if (KnownLegacyFlatDefinitions.Contains(definition.Name))
            {
                continue;
            }

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
