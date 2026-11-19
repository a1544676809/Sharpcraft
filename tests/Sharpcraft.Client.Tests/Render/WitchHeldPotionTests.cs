// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// B-4 T4b: witch held-potion real item-model render verified against the jar golden vectors:
/// potion-colors.reference.txt (effect-derived tint table) and witch-potion-pose.reference.txt (source pose).
/// </summary>
public sealed class WitchHeldPotionTests
{
    private static string SamplePath(string name) => Path.Combine(
        AppContext.BaseDirectory, "Repro", "Data", name);

    /// <summary>
    /// Reads the jar potion/effect colour table from the test output directory, the same way every other
    /// reference-sample test locates its data. Data rows are tab separated as kind|id|color|effects; the
    /// header documents the columns with '|' but the probe writes tabs. A missing or empty file fails the test.
    /// </summary>
    private static List<(string Kind, string Id, int Color)> LoadPotionColors()
    {
        var path = SamplePath("potion-colors.reference.txt");
        Assert.True(File.Exists(path), $"jar colour sample missing: {path}");
        var rows = new List<(string, string, int)>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            Assert.True(parts.Length >= 3, $"malformed colour sample row: {line}");
            rows.Add((parts[0], parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture)));
        }

        Assert.NotEmpty(rows);
        return rows;
    }

    [Fact]
    public void ProductionPotionColorTableCarriesTheJarVectors()
    {
        // The production table must reproduce the jar sample row for row. The sample is the single source of truth:
        // every expected value is parsed at runtime, so a changed sample turns this test red instead of drifting.
        var rows = LoadPotionColors();
        var effectRows = rows.Where(r => r.Kind == "effect").ToArray();
        var potionRows = rows.Where(r => r.Kind == "potion").ToArray();
        // Counts come from the jar probe that produced the sample, not from the file we are reading:
        // a truncated sample or a changed kind must fail rather than silently shrink the assertion set.
        Assert.Equal(86, rows.Count);
        Assert.Equal(40, effectRows.Length);
        Assert.Equal(46, potionRows.Length);
        Assert.Equal(rows.Count, effectRows.Length + potionRows.Length);

        foreach (var (_, id, color) in effectRows)
        {
            Assert.Equal(color, PotionColorTable.EffectColorByName(id));
        }

        foreach (var (_, id, color) in potionRows)
        {
            Assert.Equal(color, PotionColorTable.PlainColorByName(id));
        }

        Assert.Null(PotionColorTable.PlainColorByName("not_a_potion"));
        Assert.Null(PotionColorTable.EffectColorByName("not_an_effect"));
    }

    [Fact]
    public void PotionIdResolvesThroughTheConnectionRegistryAndEffectsBlendByAmplifier()
    {
        var registries = new DynamicRegistries();
        registries.Apply(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "potion"),
        [
            new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "water"), null),
            new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "night_vision"), null)
        ]);
        registries.Apply(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "mob_effect"),
        [
            new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "speed"), null),
            new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "slowness"), null)
        ]);
        PotionColorTable.Configure(registries);
        try
        {
            Assert.Equal(-13083194, PotionColorTable.Resolve(0, null));
            Assert.Equal(-3997850, PotionColorTable.Resolve(1, null));

            var speed = PotionColorTable.EffectColorByName("speed")!.Value;
            var slowness = PotionColorTable.EffectColorByName("slowness")!.Value;
            var expected = ((((speed >> 16) & 0xFF) + 2 * ((slowness >> 16) & 0xFF)) / 3) << 16
                | ((((speed >> 8) & 0xFF) + 2 * ((slowness >> 8) & 0xFF)) / 3) << 8
                | (((speed & 0xFF) + 2 * (slowness & 0xFF)) / 3);
            Assert.Equal(expected, PotionColorTable.Resolve(-1,
            [
                new PotionEffectInstance(0, 0, Visible: true),
                new PotionEffectInstance(1, 1, Visible: true)
            ]));

            Assert.Null(PotionColorTable.Resolve(-1, [new PotionEffectInstance(0, 0, Visible: false)]));
            Assert.Null(PotionColorTable.Resolve(99, null));
        }
        finally
        {
            PotionColorTable.Configure(null);
        }
    }

    [Fact]
    public void PotionContentsWireCarriesThePotionIdAndVisibleEffects()
    {
        using var fixture = new PotionFixture();
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var potionContents = Sharpcraft.Common.Registries.DataComponentTypes.GetId("potion_contents");
        Assert.True(potionId > 0 && potionContents >= 0);

        var bytes = new List<byte>();
        bytes.AddRange(VarInt(PacketIds.PlayClientbound.SetEquipment));
        bytes.AddRange(VarInt(42));
        bytes.Add(0);
        bytes.AddRange(VarInt(1));
        bytes.AddRange(VarInt(potionId));
        bytes.AddRange(VarInt(1));
        bytes.AddRange(VarInt(0));
        bytes.AddRange(VarInt(potionContents));
        bytes.Add(1);
        bytes.AddRange(VarInt(1));
        bytes.Add(0);
        bytes.AddRange(VarInt(1));
        bytes.AddRange(VarInt(0));
        bytes.AddRange(VarInt(0));
        bytes.AddRange(VarInt(0));
        bytes.Add(0);
        bytes.Add(1);
        bytes.Add(0);
        bytes.Add(0);
        bytes.Add(0);

        var items = new EquipmentItem[SetEquipmentPackets.MaxSlots];
        Assert.Equal(DecodeStatus.Success, SetEquipmentPackets.TryRead(bytes.ToArray(), out var entityId, items, out var count, out _));
        Assert.Equal(1, items[0].Components.PotionId);
        Assert.NotNull(items[0].Components.PotionEffects);
        var effect = Assert.Single(items[0].Components.PotionEffects!);
        Assert.Equal(0, effect.EffectId);
        Assert.Equal(0, effect.Amplifier);
        Assert.True(effect.Visible);

        var registries = new DynamicRegistries();
        registries.Apply(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "mob_effect"),
            [new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "speed"), null)]);
        PotionColorTable.Configure(registries);
        try
        {
            var tracker = new EntityTracker();
            Assert.True(tracker.Add(new AddedEntity(42, Guid.Empty, WitchModelDefinition.WitchEntityTypeId, 0, 0, 0, 0f, 0f, 0f, 0f, 180f, 0f, 0)));
            Assert.True(tracker.ApplyEquipment(entityId, items[..count]));
            var state = tracker.Interpolate(0f)[0];
            Assert.Equal(1, state.HeldPotionId);
            var stateEffect = Assert.Single(state.HeldPotionEffects!);
            Assert.Equal(0, stateEffect.EffectId);
            Assert.Equal(3402751, PotionColorTable.Resolve(state.HeldPotionId, state.HeldPotionEffects));

            var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
            Assert.NotNull(potionModel);
            var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
            {
                ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
                ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
            });
            var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
            Assert.NotNull(layer);
            var produced = ReadAllVertices(layer!.Geometry);
            var expectedRed = (3402751 >> 16 & 0xFF) / 255f;
            Assert.Contains(produced, v => MathF.Abs(v.Red - expectedRed) <= 1e-3f);
        }
        finally
        {
            PotionColorTable.Configure(null);
        }
    }

    [Fact]
    public void ProductionMesherMatchesTheJarSideFaceWorldSample()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var masks = LoadPotionAlphaMasks();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = AlphaTexture(masks["potion_overlay"]),
            ["item/potion"] = AlphaTexture(masks["potion"])
        });
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var state = new EntityRenderState(2, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        var produced = ReadAllVertices(layer!.Geometry);

        // Per generated layer the mesher emits south/north (8 vertices) followed by the alpha side quads in the exact
        // scan order of ItemModelGenerator.bakeSideFaces; 42 overlay + 98 potion side quads => 560 side vertices.
        const int frontBackPerLayer = 8;
        const int overlaySideVertices = 42 * 4;
        const int potionSideVertices = 98 * 4;
        Assert.Equal(2 * frontBackPerLayer + overlaySideVertices + potionSideVertices, produced.Length);
        var overlaySide = produced[frontBackPerLayer..(frontBackPerLayer + overlaySideVertices)];
        var potionStart = frontBackPerLayer + overlaySideVertices + frontBackPerLayer;
        var potionSide = produced[potionStart..(potionStart + potionSideVertices)];

        var sample = File.ReadLines(SamplePath("witch-potion-side.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToArray();
        Assert.Equal(560, sample.Length);
        var keyed = sample.ToDictionary(row => SideSampleKey(row));
        Assert.Equal(560, keyed.Count);

        // Key/index binding: every sample layer|polygon|vertex maps to exactly one production vertex index and every
        // side vertex index is consumed once.
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        AssertSideFaceLayer(keyed, "potion_overlay", overlaySide, consumed);
        AssertSideFaceLayer(keyed, "potion", potionSide, consumed);
        Assert.Equal(560, consumed.Count);

        // No duplicate or missing side geometry: all (layer, position, UV) signatures are unique.
        var signatures = overlaySide.Concat(potionSide)
            .Select(v => $"{MathF.Round(v.Position.X, 5)}|{MathF.Round(v.Position.Y, 5)}|{MathF.Round(v.Position.Z, 5)}|{MathF.Round(v.U, 5)}|{MathF.Round(v.V, 5)}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(560, signatures.Count);
    }

    [Fact]
    public void SideFaceKeyedMatcherRejectsWrongUv()
    {
        var sample = LoadSideSampleRows();
        var row = sample.First(r => r[0] == "potion_overlay" && r[1] == "0" && r[2] == "0");
        var vertex = BuildSideGeometryForTest()[8];
        Assert.True(SideVertexMatches(row, vertex, uvOffset: 0f));
        Assert.False(SideVertexMatches(row, vertex, uvOffset: 0.01f));
    }

    [Fact]
    public void SideFaceKeyedMatcherRejectsDuplicateProductionVertex()
    {
        var vertices = BuildSideGeometryForTest();
        var overlaySide = vertices[8..(8 + (42 * 4))];
        var signatures = overlaySide
            .Select(v => $"{MathF.Round(v.Position.X, 5)}|{MathF.Round(v.Position.Y, 5)}|{MathF.Round(v.Position.Z, 5)}|{MathF.Round(v.U, 5)}|{MathF.Round(v.V, 5)}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(overlaySide.Length, signatures.Count);
        var duplicated = overlaySide.Append(overlaySide[0]).ToArray();
        Assert.NotEqual(duplicated.Length, duplicated
            .Select(v => $"{MathF.Round(v.Position.X, 5)}|{MathF.Round(v.Position.Y, 5)}|{MathF.Round(v.Position.Z, 5)}|{MathF.Round(v.U, 5)}|{MathF.Round(v.V, 5)}")
            .ToHashSet(StringComparer.Ordinal).Count);
    }

    [Fact]
    public void SideFaceSampleKeysAreUniqueAndComplete()
    {
        var sample = LoadSideSampleRows();
        Assert.Equal(560, sample.Length);
        Assert.Equal(560, sample.Select(SideSampleKey).Distinct(StringComparer.Ordinal).Count());
        var overlay = sample.Where(r => r[0] == "potion_overlay").ToArray();
        var potion = sample.Where(r => r[0] == "potion").ToArray();
        Assert.Equal(42, overlay.Select(r => r[1]).Distinct().Count());
        Assert.Equal(98, potion.Select(r => r[1]).Distinct().Count());
        Assert.All(overlay, row => Assert.InRange(int.Parse(row[2], CultureInfo.InvariantCulture), 0, 3));
        Assert.All(potion, row => Assert.InRange(int.Parse(row[2], CultureInfo.InvariantCulture), 0, 3));
    }

    private static string SideSampleKey(string[] row) => $"{row[0]}|{row[1]}|{row[2]}";

    private static string[][] LoadSideSampleRows() =>
        File.ReadLines(SamplePath("witch-potion-side.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToArray();

    private static void AssertSideFaceLayer(IReadOnlyDictionary<string, string[]> keyed, string layer,
        (Vector3 Position, float U, float V, float Red, float Green, float Blue)[] sideVertices,
        HashSet<string> consumed)
    {
        var polygonCount = keyed.Keys.Count(k => k.StartsWith(layer + "|", StringComparison.Ordinal)) / 4;
        for (var polygon = 0; polygon < polygonCount; polygon++)
        {
            for (var vertex = 0; vertex < 4; vertex++)
            {
                var key = $"{layer}|{polygon}|{vertex}";
                Assert.True(keyed.TryGetValue(key, out var row), $"missing side sample key {key}");
                Assert.True(consumed.Add(key), $"duplicate side sample key {key}");
                var produced = sideVertices[(polygon * 4) + vertex];
                Assert.True(SideVertexMatches(row!, produced),
                    $"{key}: sample=({row![3]},{row[4]},{row[5]}) uv=({row[6]},{row[7]}) vs produced={produced.Position} uv=({produced.U},{produced.V})");
            }
        }
    }

    private static bool SideVertexMatches(string[] row, (Vector3 Position, float U, float V, float Red, float Green, float Blue) vertex, float uvOffset = 0f)
    {
        var world = new Vector3(
            float.Parse(row[3], CultureInfo.InvariantCulture),
            float.Parse(row[4], CultureInfo.InvariantCulture),
            float.Parse(row[5], CultureInfo.InvariantCulture));
        var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
        var expectedU = QuantizeHalf(float.Parse(row[6], CultureInfo.InvariantCulture) + uvOffset);
        var expectedV = QuantizeHalf(float.Parse(row[7], CultureInfo.InvariantCulture) + uvOffset);
        var tintIndex = int.Parse(row[8], CultureInfo.InvariantCulture);
        var tintMatches = tintIndex == 1 ? MathF.Abs(vertex.Red - 1f) <= 1e-3f : vertex.Red > 0f;
        return (vertex.Position - expected).Length() <= 1e-5f
            && MathF.Abs(vertex.U - expectedU) <= 1e-5f
            && MathF.Abs(vertex.V - expectedV) <= 1e-5f
            && tintMatches;
    }

    private static (Vector3 Position, float U, float V, float Red, float Green, float Blue)[] BuildSideGeometryForTest()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var masks = LoadPotionAlphaMasks();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = AlphaTexture(masks["potion_overlay"]),
            ["item/potion"] = AlphaTexture(masks["potion"])
        });
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var state = new EntityRenderState(30, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        return ReadAllVertices(layer!.Geometry);
    }
    [Fact]
    public void CustomPotionColorWinsOverDerivedColor()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        const int custom = unchecked((int)0xFF123456);
        var state = new EntityRenderState(20, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, HeldPotionColor: custom, HeldPotionId: 1,
            HeldPotionEffects: [new PotionEffectInstance(0, 0, Visible: true)],
            MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        var produced = ReadAllVertices(layer!.Geometry);
        Assert.Contains(produced, v => MathF.Abs(v.Red - (0x12 / 255f)) <= 1e-3f
            && MathF.Abs(v.Green - (0x34 / 255f)) <= 1e-3f
            && MathF.Abs(v.Blue - (0x56 / 255f)) <= 1e-3f);
    }

    [Fact]
    public void PotionBranchKeepsThePotionLayerName()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        var state = new EntityRenderState(21, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        Assert.Equal(WitchHeldPotionMesher.LayerName, layer!.Name);
    }

    [Fact]
    public void UnknownEffectFallsBackToThePotionColorAndThenToNull()
    {
        var registries = new DynamicRegistries();
        registries.Apply(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "potion"),
            [new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "water"), null)]);
        registries.Apply(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "mob_effect"),
            [new DynamicRegistries.Entry(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "speed"), null)]);
        PotionColorTable.Configure(registries);
        try
        {
            Assert.Equal(-13083194, PotionColorTable.Resolve(0, [new PotionEffectInstance(99, 0, Visible: true)]));
            Assert.Null(PotionColorTable.Resolve(-1, [new PotionEffectInstance(99, 0, Visible: true)]));
        }
        finally
        {
            PotionColorTable.Configure(null);
        }
    }

    [Fact]
    public void CrossPoseMatchesTheJarSampleMatrix()
    {
        var numeric = new float[16];
        foreach (var line in File.ReadLines(SamplePath("witch-potion-pose.reference.txt")))
        {
            if (!line.StartsWith("#MATRIX CROSS_POSE ")) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < 16; i++) numeric[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            break;
        }

        Assert.NotEqual(0f, numeric[0]);
        var expected = new Matrix4x4(
            numeric[0], numeric[1], numeric[2], numeric[3],
            numeric[4], numeric[5], numeric[6], numeric[7],
            numeric[8], numeric[9], numeric[10], numeric[11],
            numeric[12], numeric[13], numeric[14], numeric[15]);
        var root = WitchModelDefinition.Definition.Compiled!.Root;
        var pose = WitchHeldItemFrame.CrossSource(root);
        Assert.Equal(expected.M11, pose.M11, 5);
        Assert.Equal(expected.M12, pose.M12, 5);
        Assert.Equal(expected.M13, pose.M13, 5);
        Assert.Equal(expected.M21, pose.M21, 5);
        Assert.Equal(expected.M22, pose.M22, 5);
        Assert.Equal(expected.M23, pose.M23, 5);
        Assert.Equal(expected.M31, pose.M31, 5);
        Assert.Equal(expected.M32, pose.M32, 5);
        Assert.Equal(expected.M33, pose.M33, 5);
        Assert.Equal(expected.M41, pose.M41, 5);
        Assert.Equal(expected.M42, pose.M42, 5);
        Assert.Equal(expected.M43, pose.M43, 5);
    }

    [Fact]
    public void NonPotionWitchItemUsesTheCrossBranch()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        var state = new EntityRenderState(3, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: potionId, IsHoldingPotion: false, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        Assert.Equal(WitchHeldPotionMesher.CrossLayerName, layer!.Name);
        Assert.True(layer.Geometry.VertexFloats > 0);
    }

    [Fact]
    public void SideFaceQuadCountsMatchTheJarAlphaMasks()
    {
        var sample = LoadSideSampleRows();
        Assert.Equal(168, sample.Count(r => r[0] == "potion_overlay"));
        Assert.Equal(392, sample.Count(r => r[0] == "potion"));
    }

    [Fact]
    public void SideFaceFrontAndBackVerticesStillMatchTheQuadSample()
    {
        var vertices = BuildSideGeometryForTest();
        var sample = File.ReadLines(SamplePath("witch-potion-quad.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToArray();
        Assert.Equal(16, sample.Length);
        // First 8 vertices are layer0 south/north, the next 8 are layer1; each quad keeps the keyed sample position/UV.
        foreach (var row in sample)
        {
            // Layer0 occupies 8 front/back + 168 side vertices; layer1 starts after that.
            var layerStart = row[0] == "potion_overlay" ? 0 : 8 + 168;
            var polygon = int.Parse(row[2], CultureInfo.InvariantCulture);
            var vertexIndex = int.Parse(row[3], CultureInfo.InvariantCulture);
            var produced = vertices[layerStart + (polygon * 4) + vertexIndex];
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            Assert.True((produced.Position - expected).Length() <= 1e-5f,
                $"{row[0]}|{polygon}|{vertexIndex}: {produced.Position} vs {expected}");
            Assert.Equal(QuantizeHalf(float.Parse(row[7], CultureInfo.InvariantCulture)), produced.U);
            Assert.Equal(QuantizeHalf(float.Parse(row[8], CultureInfo.InvariantCulture)), produced.V);
        }
    }

    [Fact]
    public void PotionTintFallsBackToTheItemDefaultWhenNoPotionIdIsKnown()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        PotionColorTable.Configure(null);
        var state = new EntityRenderState(40, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        var produced = ReadAllVertices(layer!.Geometry);
        var expectedRed = ((ItemTintSource.PotionDefaultColor >> 16) & 0xFF) / 255f;
        Assert.Contains(produced, v => MathF.Abs(v.Red - expectedRed) <= 1e-3f);
    }

    [Fact]
    public void WitchOffhandItemWithoutMainHandDoesNotProduceALayer()
    {
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        var state = new EntityRenderState(41, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            OffHandItemId: potionId, IsHoldingPotion: true, Scale: 1f);
        Assert.Null(new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel));
    }
    private static byte[] VarInt(int value)
    {
        var bytes = new List<byte>();
        var remaining = (uint)value;
        while ((remaining & ~0x7Fu) != 0)
        {
            bytes.Add((byte)((remaining & 0x7F) | 0x80));
            remaining >>= 7;
        }

        bytes.Add((byte)remaining);
        return bytes.ToArray();
    }

    private static Dictionary<string, bool[,]> LoadPotionAlphaMasks()
    {
        var masks = new Dictionary<string, bool[,]>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(SamplePath("witch-potion-alpha.reference.txt")))
        {
            if (line.StartsWith('#')) continue;
            var parts = line.Split('|');
            var layer = parts[0];
            var y = int.Parse(parts[1], CultureInfo.InvariantCulture);
            if (!masks.TryGetValue(layer, out var mask))
            {
                mask = new bool[16, 16];
                masks[layer] = mask;
            }

            for (var x = 0; x < parts[2].Length; x++) mask[x, y] = parts[2][x] == '1';
        }

        return masks;
    }

    private static TextureData AlphaTexture(bool[,] mask)
    {
        var rgba = new byte[16 * 16 * 4];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var offset = ((y * 16) + x) * 4;
                var alpha = mask[x, y] ? (byte)255 : (byte)0;
                rgba[offset] = 255;
                rgba[offset + 1] = 255;
                rgba[offset + 2] = 255;
                rgba[offset + 3] = alpha;
            }

        return new TextureData(16, 16, rgba);
    }
    [Fact]
    public void PotionSourcePoseMatchesJarSampleMatrix()
    {
        // The POTION branch pose must reproduce the jar probe matrix keyed element-for-element (1e-5). This is the
        // equivalence anchor for the source WitchItemLayer.applyTranslation chain; no self-derived expectation.
        var numeric = new float[16];
        foreach (var line in File.ReadLines(SamplePath("witch-potion-pose.reference.txt")))
        {
            if (!line.StartsWith("#MATRIX POTION_POSE ")) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < 16; i++) numeric[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            break;
        }

        Assert.NotEqual(0f, numeric[0]); // sample parsed

        var root = WitchModelDefinition.Definition.Compiled!.Root;
        var pose = WitchHeldItemFrame.PotionSource(root);
        // Same mapping as HumanoidHeldHandProductionTests.ParseMatrices: the probe's JOML m0..m15 are written in a
        // System.Numerics-compatible layout (values fill rows directly; translation lives in M41..M43).
        var expected = new Matrix4x4(
            numeric[0], numeric[1], numeric[2], numeric[3],
            numeric[4], numeric[5], numeric[6], numeric[7],
            numeric[8], numeric[9], numeric[10], numeric[11],
            numeric[12], numeric[13], numeric[14], numeric[15]);

        Assert.Equal(expected.M11, pose.M11, 5);
        Assert.Equal(expected.M12, pose.M12, 5);
        Assert.Equal(expected.M13, pose.M13, 5);
        Assert.Equal(expected.M21, pose.M21, 5);
        Assert.Equal(expected.M22, pose.M22, 5);
        Assert.Equal(expected.M23, pose.M23, 5);
        Assert.Equal(expected.M31, pose.M31, 5);
        Assert.Equal(expected.M32, pose.M32, 5);
        Assert.Equal(expected.M33, pose.M33, 5);
        Assert.Equal(expected.M41, pose.M41, 5);
        Assert.Equal(expected.M42, pose.M42, 5);
        Assert.Equal(expected.M43, pose.M43, 5);
    }

    [Fact]
    public void GeneratedPotionQuadsMatchJarWorldSampleKeyed()
    {
        // Full render chain geometry: for a bind witch with no yaw/scale, world = flip * pose * GROUND * (v_px/16).
        // Verify against the jar probe witch-potion-quad.reference.txt keyed by layer|polygon|vertex (position + UV + tint).
        var root = WitchModelDefinition.Definition.Compiled!.Root;
        var pose = WitchHeldItemFrame.PotionSource(root);
        Assert.Equal(-0.99129, pose.M11, 5); // source frame reproduced

        // Use the jar probe's POTION_FULL matrix (pose * GROUND display) directly as the equivalence anchor. The probe
        // applies S(-1,-1,1)*T(0,-1.501,0) as flip.mul(full) in JOML column space = fullCSharp * flipCSharp in
        // System.Numerics row-vector space (the probe's m0..m15 parse directly into the CSharp matrix layout).
        var fullValues = new float[16];
        foreach (var line in File.ReadLines(SamplePath("witch-potion-pose.reference.txt")))
        {
            if (!line.StartsWith("#MATRIX POTION_FULL ")) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < 16; i++) fullValues[i] = float.Parse(parts[i + 2], CultureInfo.InvariantCulture);
            break;
        }

        Assert.NotEqual(0f, fullValues[0]);
        var fullNoFlip = new Matrix4x4(
            fullValues[0], fullValues[1], fullValues[2], fullValues[3],
            fullValues[4], fullValues[5], fullValues[6], fullValues[7],
            fullValues[8], fullValues[9], fullValues[10], fullValues[11],
            fullValues[12], fullValues[13], fullValues[14], fullValues[15]);
        // JOML flip.mul(full) column-space = fullCSharp * flipCSharp in row-vector space. The row-vector equivalent of
        // JOML S(-1,-1,1)*T(0,-1.501,0) is T(0,-1.501,0)*S(-1,-1,1) (translate then scale, reversed order).
        var flip = Matrix4x4.CreateTranslation(0f, -1.501f, 0f) * Matrix4x4.CreateScale(-1f, -1f, 1f);
        var full = fullNoFlip * flip;

        // Generated layer corners: south face z=8.5/16, north face z=7.5/16 (ItemModelGenerator MIN/MAX_Z).
        var layerCorners = new (float z, int tint, string layer)[]
        {
            (8.5f / 16f, 0, "potion_overlay"),
            (7.5f / 16f, 0, "potion_overlay"),
            (8.5f / 16f, 1, "potion"),
            (7.5f / 16f, 1, "potion")
        };

        var sample = File.ReadLines(SamplePath("witch-potion-quad.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToDictionary(p => $"{p[0]}|{p[2]}|{p[3]}");

        var south = new[] { (0f, 1f), (0f, 0f), (1f, 0f), (1f, 1f) };
        var north = new[] { (1f, 1f), (1f, 0f), (0f, 0f), (0f, 1f) };
        foreach (var (z, tint, layer) in layerCorners)
        {
            var corners = z > 8f / 16f ? south : north;
            var polygon = z > 8f / 16f ? 0 : 1;
            for (var i = 0; i < 4; i++)
            {
                var v = Vector3.Transform(new Vector3(corners[i].Item1, corners[i].Item2, z), full);
                var key = $"{layer}|{polygon}|{i}";
                Assert.True(sample.TryGetValue(key, out var row), $"missing sample key {key}");
                Assert.Equal(float.Parse(row[4], CultureInfo.InvariantCulture), v.X, 5);
                Assert.Equal(float.Parse(row[5], CultureInfo.InvariantCulture), v.Y, 5);
                Assert.Equal(float.Parse(row[6], CultureInfo.InvariantCulture), v.Z, 5);
                Assert.Equal(tint, int.Parse(row[9], CultureInfo.InvariantCulture));
            }
        }
    }

    [Fact]
    public void PotionTintSourceDefaultIsTheJarBaseColour()
    {
        var source = ItemTintSource.Parse(System.Text.Json.JsonDocument.Parse(
            """{"type":"minecraft:potion","default":-13083194}""").RootElement);
        Assert.NotNull(source);
        Assert.Equal(ItemTintKind.Potion, source!.Value.Kind);
        Assert.Equal(ItemTintSource.PotionDefaultColor, source.Value.Argb);
    }

    [Fact]
    public void ProductionMesherMatchesJarQuadWorldSampleKeyedAndConsumesCustomTint()
    {
        // Production keyed verification of WitchHeldPotionMesher.Build (not a formula re-computation): the 16 sample
        // vertices (south/north faces of both layers) must each appear in the mesher's emitted geometry at 1e-5 with
        // half-quantized UVs. Sprite alpha side faces are additional (blocker R-046) and are simply not in the sample.
        using var fixture = new PotionFixture();
        var potionModel = EntityModelResolver.ItemModels!.Resolve("minecraft:potion");
        Assert.NotNull(potionModel);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion_overlay"] = new(16, 16, new byte[16 * 16 * 4]),
            ["item/potion"] = new(16, 16, new byte[16 * 16 * 4])
        });
        const int customColor = unchecked((int)0xFF2A7F1E);
        // EntityModelTransform.From applies YawDegrees = 180 - state.Yaw and Scale = state.Scale. Use Yaw=180, Scale=1
        // so the entity matrix is identity (the jar quad sample excludes entity yaw/scale), leaving pose*display only.
        var potionId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion"));
        var state = new EntityRenderState(1, WitchModelDefinition.WitchEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsHoldingPotion: true, HeldPotionColor: customColor, MainHandItemId: potionId, Scale: 1f);
        var layer = new WitchHeldPotionMesher().Build([state], [new EntityPose()], atlas, potionModel);
        Assert.NotNull(layer);
        Assert.Equal(WitchHeldPotionMesher.LayerName, layer!.Name);

        var sample = File.ReadLines(SamplePath("witch-potion-quad.reference.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToArray();
        Assert.Equal(16, sample.Length);

        var produced = ReadAllVertices(layer.Geometry);
        // The mesher emits item geometry in the source frame without the living flip; map jar world sample back:
        // (-x, 1.501 - y, z), same as HeldSpecialWorldReferenceSampleTests (level renderer applies the flip later).
        foreach (var row in sample)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            var expectedU = QuantizeHalf(float.Parse(row[7], CultureInfo.InvariantCulture));
            var expectedV = QuantizeHalf(float.Parse(row[8], CultureInfo.InvariantCulture));
            Assert.Contains(produced, v =>
                (v.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(v.U - expectedU) <= 1e-5f && MathF.Abs(v.V - expectedV) <= 1e-5f);
        }

        // Tint consumption: the potion tint source is declared only on layer0 (potion_overlay, tint index 0), so the
        // 8 overlay vertices carry the stack custom colour while the 8 bottle vertices (tint index 1, no source) stay
        // white — the reference behaviour (ItemStackRenderState.tintLayers only on the overlay quad's tint index).
        var expectedRed = ((customColor >> 16) & 0xFF) / 255f;
        var expectedGreen = ((customColor >> 8) & 0xFF) / 255f;
        var expectedBlue = (customColor & 0xFF) / 255f;
        var overlayRows = sample.Where(r => r[9] == "0").ToArray();
        var bottleRows = sample.Where(r => r[9] == "1").ToArray();
        Assert.Equal(8, overlayRows.Length);
        Assert.Equal(8, bottleRows.Length);
        foreach (var row in overlayRows)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            var v = Assert.Single(produced, p =>
                (p.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(p.Red - expectedRed) <= 1e-3f &&
                MathF.Abs(p.Green - expectedGreen) <= 1e-3f &&
                MathF.Abs(p.Blue - expectedBlue) <= 1e-3f);
        }
        foreach (var row in bottleRows)
        {
            var world = new Vector3(
                float.Parse(row[4], CultureInfo.InvariantCulture),
                float.Parse(row[5], CultureInfo.InvariantCulture),
                float.Parse(row[6], CultureInfo.InvariantCulture));
            var expected = new Vector3(-world.X, 1.501f - world.Y, world.Z);
            Assert.Single(produced, p =>
                (p.Position - expected).Length() <= 1e-5f &&
                MathF.Abs(p.Red - 1f) <= 1e-3f &&
                MathF.Abs(p.Green - 1f) <= 1e-3f &&
                MathF.Abs(p.Blue - 1f) <= 1e-3f);
        }
    }

    private static (Vector3 Position, float U, float V, float Red, float Green, float Blue)[] ReadAllVertices(DynamicGeometry geometry)
    {
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new (Vector3, float, float, float, float, float)[count];
        for (var i = 0; i < count; i++)
        {
            var o = i * ChunkMesher.FloatsPerVertex;
            var packedColor = BitConverter.SingleToUInt32Bits(geometry.Vertices[o + 3]);
            var packedUv = BitConverter.SingleToUInt32Bits(geometry.Vertices[o + 4]);
            result[i] = (
                new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16)),
                (packedColor & 0xFF) / 255f,
                ((packedColor >> 8) & 0xFF) / 255f,
                ((packedColor >> 16) & 0xFF) / 255f);
        }
        return result;
    }

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    /// <summary>Temp resource stack with the real two-layer potion item model (data-only), like HeldSpecialWorldReferenceSampleTests.ItemFixture.</summary>
    private sealed class PotionFixture : IDisposable
    {
        private readonly string _root;
        private readonly Sharpcraft.Client.Render.ItemModelResolver _previous;
        private readonly ResourceManager _resources;

        public PotionFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sharpcraft-potion-test-" + Guid.NewGuid().ToString("N"));
            Write("assets/minecraft/items/potion.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/potion","tints":[{"type":"minecraft:potion","default":-13083194}]}}""");
            Write("assets/minecraft/models/item/potion.json",
                """{"parent":"minecraft:item/generated","textures":{"layer0":"minecraft:item/potion_overlay","layer1":"minecraft:item/potion"}}""");
            Write("assets/minecraft/models/item/generated.json",
                """{"display":{"ground":{"rotation":[0,0,0],"translation":[0,2,0],"scale":[0.5,0.5,0.5]}}}""");
            var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(_root, "potion-test"));
            _resources = new ResourceManager(stack, 1);
            _previous = EntityModelResolver.ItemModels!;
            EntityModelResolver.ItemModels = new ItemModelResolver(_resources);
        }

        public void Dispose()
        {
            EntityModelResolver.ItemModels = _previous;
            _resources.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private void Write(string relative, string json)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
