// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Core vanilla mesh DSL bake tests (T-670). Pins that <c>VanillaMeshBake.Bake</c> flattens a declarative
/// <c>VanillaMeshDefinition</c> into exactly the same <c>EntityModelPart</c> values the legacy
/// <c>LegacyModelPartFactory.FromVanilla</c>/<c>FromVanillaChild</c> helpers produce, and that the tree order, parent
/// links, inherited texture metadata and malformed-input guards behave as specified.
/// </summary>
public sealed class VanillaMeshDefinitionTests
{
    [Fact]
    public void BakeProducesTheSamePartsAsTheLegacyHumanoidDefinitions()
    {
        var overlays = new[] { new EntityModelLayer("jacket", 0.25f, 16, 32, Kind: EntityGeometryLayerKind.Translucent) };

        // Legacy expectations: a root "body" and a "head" attached to it, with non-trivial pose and metadata.
        var expectedBody = LegacyModelPartFactory.FromVanilla(
            "body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
            mirror: true, overlays: overlays,
            rotationX: 0.1f, rotationY: -0.2f, rotationZ: 0.3f,
            grow: 0.5f, kind: EntityGeometryLayerKind.Translucent);
        var expectedHead = LegacyModelPartFactory.FromVanillaChild(
            "head", "body", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
            rotationX: 0.1f, rotationY: -0.2f, rotationZ: 0.3f);

        // Declarative equivalent: root group -> "body" (root part) -> "head" (child part).
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero));
        var body = mesh.Root!.AddOrReplaceChild(
            "body",
            VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0.1f, -0.2f, 0.3f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f,
                Grow: 0.5f, Mirror: true, Overlays: overlays, Kind: EntityGeometryLayerKind.Translucent));
        body.AddOrReplaceChild(
            "head",
            VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0.1f, -0.2f, 0.3f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f));

        // No mesh-level texture dims -> parts keep the legacy 0/0 and full record equality holds.
        var parts = VanillaMeshBake.Bake(mesh, 0, 0);

        Assert.Equal(2, parts.Count);
        Assert.Equal(expectedBody, parts[0]);
        Assert.Equal(expectedHead, parts[1]);

        // Explicit re-checks of the fields the spec calls out (parent / texture / UV / pivots / rotations).
        Assert.Equal("body", parts[0].Name);
        Assert.Null(parts[0].Parent);
        Assert.Equal(16, parts[0].TextureX);
        Assert.Equal(16, parts[0].TextureY);
        Assert.Equal(0.1f, parts[0].RotationX);
        Assert.Equal(-0.2f, parts[0].RotationY);
        Assert.Equal(0.3f, parts[0].RotationZ);
        Assert.Equal("head", parts[1].Name);
        Assert.Equal("body", parts[1].Parent);
        Assert.Equal(-0.1f, parts[1].RotationX);   // child negates X/Y rotations
        Assert.Equal(0.2f, parts[1].RotationY);
        Assert.Equal(0.3f, parts[1].RotationZ);
    }

    [Fact]
    public void BakePreservesChildOrderAndParentLinks()
    {
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero));
        var body = mesh.Root!.AddOrReplaceChild(
            "body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        body.AddOrReplaceChild("first", VanillaPartPose.Zero, new VanillaCubeDefinition("first", 0, 0, 0f, 0f, 0f, 2f, 2f, 2f));
        var second = body.AddOrReplaceChild("second", VanillaPartPose.Zero, new VanillaCubeDefinition("second", 0, 0, 0f, 0f, 0f, 2f, 2f, 2f));
        body.AddOrReplaceChild("third", VanillaPartPose.Zero, new VanillaCubeDefinition("third", 0, 0, 0f, 0f, 0f, 2f, 2f, 2f));
        second.AddOrReplaceChild("nested", VanillaPartPose.Zero, new VanillaCubeDefinition("nested", 0, 0, 0f, 0f, 0f, 1f, 1f, 1f));

        var parts = VanillaMeshBake.Bake(mesh, 64, 64);

        // Pre-order (reference order): a part's own cuboids first, then its children depth-first.
        var names = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            names[i] = parts[i].Name;
        }

        Assert.Equal(new[] { "body", "first", "second", "nested", "third" }, names);
        Assert.Null(parts[0].Parent);
        Assert.Equal("body", parts[1].Parent);
        Assert.Equal("body", parts[2].Parent);
        Assert.Equal("second", parts[3].Parent);
        Assert.Equal("body", parts[4].Parent);
    }

    [Fact]
    public void BakeUsesTheMeshDefaultTextureWhenPartsDoNotOverrideIt()
    {
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero));
        mesh.Root!.AddOrReplaceChild(
            "body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));   // no per-cube override
        mesh.Root.AddOrReplaceChild(
            "hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, TexturePath: "entity/override"));

        var parts = VanillaMeshBake.Bake(mesh, 64, 32, texturePath: "entity/default");

        Assert.Equal("entity/default", parts[0].TexturePath);
        Assert.Equal("entity/override", parts[1].TexturePath);
        // Mesh-level texture dimensions are inherited by every part.
        Assert.Equal(64, parts[0].TextureWidth);
        Assert.Equal(32, parts[0].TextureHeight);
        Assert.Equal(64, parts[1].TextureWidth);
        Assert.Equal(32, parts[1].TextureHeight);
    }

    [Fact]
    public void MalformedInputThrowsClearExceptions()
    {
        // Duplicate child name: a repeated sibling name is rejected by the tree API with a descriptive message.
        var mesh = new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero));
        var body = mesh.Root!.AddOrReplaceChild(
            "body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 0, 0f, 0f, 0f, 2f, 2f, 2f));
        body.AddOrReplaceChild("dup", VanillaPartPose.Zero);
        var duplicate = Assert.Throws<InvalidOperationException>(() =>
        {
            body.AddOrReplaceChild("dup", VanillaPartPose.Zero);
        });
        Assert.Contains("dup", duplicate.Message);

        // Missing root: baking a mesh with no root part throws a clear exception.
        var noRoot = new VanillaMeshDefinition();
        var missing = Assert.Throws<InvalidOperationException>(() =>
        {
            VanillaMeshBake.Bake(noRoot, 64, 64);
        });
        Assert.Contains("root", missing.Message);
    }
}
