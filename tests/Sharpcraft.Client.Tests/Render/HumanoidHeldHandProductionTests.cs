// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Production output check for the task-268 source-frame humanoid held path. The item model is a unit cube, so
/// ItemInHandMesher.Build emits exactly the source probe's calibration cube through SourceWorld + entity transform.
/// </summary>
public sealed class HumanoidHeldHandProductionTests
{
    private static readonly string SamplePath = Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", "humanoid-held-hand-source-vectors.reference.txt");

    [Fact]
    public void AllMesherVerticesMatchTheJarSample()
    {
        var vertices = ParseVertices();
        var matrices = ParseMatrices();
        Assert.Equal(12, matrices.Count);
        Assert.Equal(8, vertices.Keys.Count(key => !key.Contains("_attack_", StringComparison.Ordinal)));
        Assert.Equal(4, vertices.Keys.Count(key => key.Contains("_attack_", StringComparison.Ordinal)));

        var paperId = EntityModelResolver.Items?.GetId(Identifier.Parse("minecraft:paper")) ?? -1;
        Assert.True(paperId > 0, "embedded item registry must contain minecraft:paper");

        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-held-frame-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/paper.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/paper_cube"}}""");
            Write(root, "assets/minecraft/models/item/paper_cube.json",
                """
                {"textures":{"particle":"item/paper","all":"item/paper"},"elements":[
                  {"from":[0,0,0],"to":[16,16,16],"faces":{
                    "down":{"texture":"#all"},"up":{"texture":"#all"},"north":{"texture":"#all"},
                    "south":{"texture":"#all"},"west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
                """);

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "held-frame-test"));
            using var resources = new ResourceManager(stack, 1);
            var oldModels = EntityModelResolver.ItemModels;
            EntityModelResolver.ItemModels = new ItemModelResolver(resources);
            try
            {
                var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
                {
                    ["item/paper"] = new(16, 16, new byte[16 * 16 * 4]),
                });
                var definition = EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId);
                var mesher = new ItemInHandMesher();
                var checkedStates = 0;
                var skippedAttackStates = 0;
                foreach (var state in vertices.Keys)
                {
                    if (state.Contains("_attack_", StringComparison.Ordinal))
                    {
                        skippedAttackStates++;
                        continue;
                    }

                    checkedStates++;
                    var expectedSource = vertices[state];
                    Assert.Equal(24, expectedSource.Length);
                    var parts = state.Split('_');
                    var left = parts[0] == "LEFT";
                    var motion = parts[1];
                    var baby = parts[2] == "baby";
                    var typeId = EntityPlaceholderMesher.PlayerEntityTypeId;
                    var frame = default(EntityAnimationFrame) with
                    {
                        TypeId = typeId,
                        IsBaby = baby,
                        IsUsingItem = motion == "use",
                        TicksUsingItem = motion == "use" ? 10f : 0f,
                        SwingAnimation = motion == "attack" ? 0.15f : 0f,
                        SwingType = 0,
                        UseItemHand = left ? 1 : 0,
                    };
                    var pose = new EntityPose();
                    HumanoidModelPose.Apply(
                        frame,
                        pose,
                        HumanoidVariant.Plain,
                        left ? HumanoidArmPose.Item : PoseFor(motion),
                        left ? PoseFor(motion) : HumanoidArmPose.Item,
                        isHoldingBow: false,
                        rawSourceFrame: true);

                    var entity = new EntityRenderState(
                        1,
                        typeId,
                        0f, 0f, 0f, 180f, 0f,
                        IsBaby: baby,
                        MainHandItemId: left ? -1 : paperId,
                        IsUsingItem: motion == "use",
                        TicksUsingItem: motion == "use" ? 10f : 0f,
                        OffHandItemId: left ? paperId : -1,
                        SwingAnimation: motion == "attack" ? 0.15f : 0f,
                        SwingType: motion == "attack" ? SpearAnimations.StabSwingType : 0,
                        SwingHand: left ? 1 : 0);
                    var layers = mesher.Build([entity], [pose], variants: null, atlas);
                    var layer = layers.Single(entry => entry.Name == ItemInHandMesher.LayerName);
                    var actual = ReadPositions(layer.Geometry);
                    var productionModel = EntityModelMesher.ResolveEntityModel(entity, variants: null);
                    var entityMatrix = EntityModelTransform.From(entity).ToMatrix();
                    // Independent sample -> production map: sample vertices are already source render units
                    // (px/16), so only the model scale and the entity transform are applied. No
                    // HumanoidHeldHandFrame helper participates in the expectation.
                    var scale = baby ? 0.5f : 0.9f;
                    var expected = expectedSource
                        .Select(vertex => Vector3.Transform(vertex, Matrix4x4.CreateScale(scale) * entityMatrix))
                        .ToArray();
                    AssertVertexSet(expected, actual, state);
                }

                Assert.Equal(8, checkedStates);
                Assert.Equal(4, skippedAttackStates);

                // Non-humanoid raw models must still use the legacy hand adapter in the real mesher.
                var copper = new EntityRenderState(
                    2,
                    CopperGolemModelDefinition.CopperGolemEntityTypeId,
                    0f, 0f, 0f, 180f, 0f,
                    MainHandItemId: paperId);
                var copperModel = EntityModelMesher.ResolveEntityModel(copper, variants: null);
                var copperMatrix = EntityModelTransform.From(copper).ToMatrix();
                var legacyWorld = EntityModelResolver.AnchorCoreFor(offhand: false, baby: false)
                    * EntityModelMesher.LegacyHandWorldMatrix(copperModel, copper, "right_arm")
                    * copperMatrix;
                var copperCorners = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f),
                    new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 1f, 1f), new Vector3(0f, 1f, 1f),
                };
                var copperExpected = copperCorners
                    .Select(vertex => Vector3.Transform(vertex, legacyWorld))
                    .ToArray();
                var copperLayers = mesher.Build([copper], [new EntityPose()], variants: null, atlas);
                var copperLayer = copperLayers.Single(entry => entry.Name == ItemInHandMesher.LayerName);
                AssertVertexSet(copperExpected, ReadPositions(copperLayer.Geometry), "CopperGolem_right_rest");
            }
            finally
            {
                EntityModelResolver.ItemModels = oldModels;
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static readonly Vector3[] CubeCorners =
    [
        new(0f, 0f, 0f), new(1f, 0f, 0f), new(1f, 1f, 0f), new(0f, 1f, 0f),
        new(0f, 0f, 1f), new(1f, 0f, 1f), new(1f, 1f, 1f), new(0f, 1f, 1f),
    ];

    private static HumanoidArmPose PoseFor(string motion) => motion == "use" ? HumanoidArmPose.Spear : HumanoidArmPose.Item;

    private static Dictionary<string, Matrix4x4> ParseMatrices()
    {
        var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(SamplePath))
        {
            if (!line.StartsWith("#MATRIX ", StringComparison.Ordinal)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var values = parts.Skip(2).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(16, values.Length);
            result[parts[1]] = new Matrix4x4(values[0], values[1], values[2], 0f, values[4], values[5], values[6], 0f, values[8], values[9], values[10], 0f, values[12], values[13], values[14], values[15]);
        }
        return result;
    }

    private static Dictionary<string, Vector3[]> ParseVertices()
    {
        var result = new Dictionary<string, List<Vector3>>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            if (!result.TryGetValue(parts[0], out var list))
            {
                result[parts[0]] = list = [];
            }

            list.Add(new Vector3(
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture),
                float.Parse(parts[5], CultureInfo.InvariantCulture)));
        }

        return result.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }

    private static Vector3[] ReadPositions(DynamicGeometry geometry)
    {
        var positions = new List<Vector3>();
        for (var offset = 0; offset + 3 <= geometry.VertexFloats; offset += 6)
        {
            positions.Add(new Vector3(
                geometry.Vertices[offset],
                geometry.Vertices[offset + 1],
                geometry.Vertices[offset + 2]));
        }

        return [.. positions];
    }

    private static void AssertVertexSet(Vector3[] expected, Vector3[] actual, string label)
    {
        Assert.Equal(24, actual.Length);
        foreach (var want in expected)
        {
            var best = actual.Min(point => Vector3.Distance(want, point));
            Assert.True(best < 1e-5f, $"{label}: no production vertex near {want}, best {best}; first actual {actual[0]}, count {actual.Length}");
        }
    }

    private static void Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }
}
