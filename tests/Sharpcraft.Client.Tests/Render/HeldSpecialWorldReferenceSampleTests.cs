// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// B-4 T2 (#228): trident/shield special renderers now go through the same raw hand chain as regular held items.
/// Production <see cref="ItemInHandMesher"/> output is compared against the jar held-world samples per
/// layer|part|cube|polygon|vertex, position at 1e-5 and UV half at 1e-5, for RIGHT / LEFT / RIGHT_USE and with
/// non-foil / foil glint.
/// </summary>
public sealed class HeldSpecialWorldReferenceSampleTests
{
    private const float Tolerance = 1e-5f;
    private const int FixedMillis = 55000;

    public static TheoryData<string, string, bool, bool, string> Cases => new()
    {
        { "trident-held-world.reference.txt", "TRIDENT_RIGHT", false, false, "held_special_trident_right" },
        { "trident-held-world.reference.txt", "TRIDENT_RIGHT_USE", false, true, "held_special_trident_right" },
        { "trident-held-world.reference.txt", "TRIDENT_LEFT", true, false, "held_special_trident_left" },
        { "shield-held-world.reference.txt", "SHIELD_RIGHT", false, false, "held_special_shield_right" },
        { "shield-held-world.reference.txt", "SHIELD_RIGHT_USE", false, true, "held_special_shield_right" },
        { "shield-held-world.reference.txt", "SHIELD_LEFT", true, false, "held_special_shield_left" }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ProductionHeldSpecialMatchesTheWorldSamplePerKey(string sampleFile, string sampleLayer, bool offhand,
        bool use, string layerName)
    {
        var isTrident = sampleFile.StartsWith("trident", StringComparison.Ordinal);
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse(isTrident ? "minecraft:trident" : "minecraft:shield"));
        Assert.True(itemId > 0);
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsUsingItem: use,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);

        var rows = Load(sampleFile).Where(row => row.Layer == sampleLayer).ToArray();
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Where(cube => cube.Width > 0f || cube.Height > 0f || cube.Depth > 0f)
            .Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        var consumed = 0;
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{layerName}: production cube '{cubeName}' missing");
            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            Assert.Equal(24, ordered.Length);
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var produced = ReadVertex(layer.Geometry, (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex);
                var actual = produced.Position;
                // Production held layers work in the source hand frame without the entity living flip, so the
                // world sample is mapped back through S(-1,-1,1) and T(0,-1.501,0) and scaled by the model scale.
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((actual - expected).Length() <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {actual} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }

            consumed += ordered.Length;
        }

        Assert.Equal(rows.Length, consumed);
    }

    public static TheoryData<string, string, string, string, string, bool, bool> PosedCases => new()
    {
        { "trident-held-world-stab.reference.txt", "TRIDENT_RIGHT_STAB", "trident", "stab", "held_special_trident_right", false, false },
        { "trident-held-world-throw-use.reference.txt", "TRIDENT_RIGHT_USE_REAL", "trident", "throw", "held_special_trident_right", true, false },
        { "shield-held-world-use.reference.txt", "SHIELD_RIGHT_USE", "shield", "use", "held_special_shield_right", true, false },
        { "shield-held-world-block.reference.txt", "SHIELD_RIGHT_BLOCK", "shield", "block", "held_special_shield_right", false, false },
        { "trident-held-world-left-stab.reference.txt", "TRIDENT_LEFT_STAB", "trident", "stab", "held_special_trident_left", false, true },
        { "shield-held-world-left-use.reference.txt", "SHIELD_LEFT_USE", "shield", "use", "held_special_shield_left", true, true }
    };

    [Theory]
    [MemberData(nameof(PosedCases))]
    public void ProductionHeldSpecialMatchesRealPoseSamplesPerKey(string sampleFile, string sampleLayer, string itemName,
        string poseName, string layerName, bool usingItem, bool offhand)
    {
        var isTrident = itemName == "trident";
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        var stab = poseName == "stab";
        var frame = default(EntityAnimationFrame) with
        {
            TypeId = EntityPlaceholderMesher.PlayerEntityTypeId,
            SwingAnimation = stab ? 0.15f : 0f,
            SwingType = stab ? 2 : 0,
            SwingHand = offhand ? 1 : 0,
            MainHandItemId = offhand ? -1 : itemId,
            OffHandItemId = offhand ? itemId : -1,
            IsUsingItem = usingItem,
            TicksUsingItem = usingItem ? 10f : 0f,
            UseItemHand = offhand ? 1 : 0
        };
        var poseForHand = poseName switch
        {
            "stab" => HumanoidArmPose.Spear,
            "throw" => HumanoidArmPose.ThrowTrident,
            "use" or "block" => HumanoidArmPose.Block,
            _ => HumanoidArmPose.Empty
        };
        var rightPose = offhand ? HumanoidArmPose.Empty : poseForHand;
        var leftPose = offhand ? poseForHand : HumanoidArmPose.Empty;
        var pose = new EntityPose();
        HumanoidModelPose.Apply(frame, pose, HumanoidVariant.Plain, rightPose, leftPose,
            isHoldingBow: false, rawSourceFrame: true);

        var state = new EntityRenderState(3, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsUsingItem: usingItem,
            TicksUsingItem: usingItem ? 10f : 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1,
            SwingAnimation: stab ? 0.15f : 0f,
            SwingType: stab ? 2 : 0,
            SwingHand: offhand ? 1 : 0,
            UseItemHand: offhand ? 1 : 0);
        var layers = new ItemInHandMesher().Build([state], [pose], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);
        var rows = Load(sampleFile).Where(row => row.Layer == sampleLayer).ToArray();
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        Assert.Equal(rows.Length, layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{sampleLayer}: production cube '{cubeName}' missing");
            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var produced = ReadVertex(layer.Geometry, (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex);
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((produced.Position - expected).Length() <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {produced.Position} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }
        }
    }
    [Theory]
    [InlineData("chest", false)]
    [InlineData("chest", true)]
    [InlineData("skeleton_skull", false)]
    [InlineData("skeleton_skull", true)]
    [InlineData("white_banner", false)]
    [InlineData("white_banner", true)]
    public void HeldSpecialTexturesArePrimedForTheAtlas(string itemName, bool offhand)
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        var state = new EntityRenderState(5, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1);
        var texture = EntityModelResolver.ResolveHeldItemSpecialTexture(state, offhand);
        Assert.NotNull(texture);
        Assert.Contains(texture, ItemInHandMesher.ResolveTextures(state));
    }
    [Fact]
    public void ShieldBaseColorComponentFlowsThroughTheEquipmentPipelineIntoThePatternTint()
    {
        using var fixture = new ItemFixture();
        var shieldId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        var baseColor = DataComponentTypes.GetId("base_color");
        var bannerPatterns = DataComponentTypes.GetId("banner_patterns");
        Assert.True(shieldId > 0 && baseColor >= 0 && bannerPatterns >= 0);

        var body = EquipmentPacketBody(b =>
        {
            b.Add(0);                          // main hand, no continuation
            b.AddRange(VarInt(1));             // count
            b.AddRange(VarInt(shieldId));
            b.AddRange(VarInt(2));             // two added components
            b.AddRange(VarInt(0));             // no removals
            b.AddRange(VarInt(baseColor));
            b.AddRange(VarInt(9));             // cyan
            b.AddRange(VarInt(bannerPatterns));
            b.AddRange(VarInt(1));             // one layer
            b.AddRange(VarInt(1));             // square_bottom_left
            b.AddRange(VarInt(14));            // red dye
        });
        var items = new EquipmentItem[SetEquipmentPackets.MaxSlots];
        Assert.Equal(DecodeStatus.Success, SetEquipmentPackets.TryRead(body, out var entityId, items, out var count, out _));
        var tracker = new EntityTracker();
        Assert.True(tracker.Add(new AddedEntity(entityId, Guid.Empty, EntityPlaceholderMesher.PlayerEntityTypeId, 0, 0, 0, 0f, 0f, 0f, 0f, 180f, 0f, 0)));
        Assert.True(tracker.ApplyEquipment(entityId, items[..count]));

        var state = tracker.Interpolate(0f)[0];
        Assert.Equal(shieldId, state.MainHandItemId);
        Assert.Equal(9, state.EquipmentBannerBaseColorAt(0));
        var patterns = state.EquipmentBannerPatternsAt(0);
        Assert.NotNull(patterns);
        Assert.Single(patterns!);
        Assert.Equal("square_bottom_left", patterns![0].PatternAssetId);
        Assert.Equal(14, patterns[0].DyeColorId);

        var atlas = ShieldPatternAtlas(state, offhand: false);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var baseLayer = layers.Single(layer => layer.Name == "held_special_shield_pattern_base_right");
        AssertLayerMatchesRows(SpecialItemModelDefinition.Shield, baseLayer.Geometry,
            Load("shield-patterns-held-world.reference.txt").Where(row => row.Layer == "SHIELD_PATTERN_BASE_RIGHT").ToArray(),
            "SHIELD_PATTERN_BASE_RIGHT");
        Assert.Equal(DyeColors.DiffuseAt(9), ReadColor(baseLayer.Geometry, 0));
        var patternLayer = layers.Single(layer => layer.Name == "held_special_shield_pattern_0_right");
        AssertLayerMatchesRows(SpecialItemModelDefinition.Shield, patternLayer.Geometry,
            Load("shield-patterns-held-world.reference.txt").Where(row => row.Layer == "SHIELD_PATTERN_0_RIGHT").ToArray(),
            "SHIELD_PATTERN_0_RIGHT");
        Assert.Equal(DyeColors.DiffuseAt(14), ReadColor(patternLayer.Geometry, 0));
    }

    [Fact]
    public void ShieldWithoutBaseColorFallsBackToWhitePatternTint()
    {
        using var fixture = new ItemFixture();
        var shieldId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        var bannerPatterns = DataComponentTypes.GetId("banner_patterns");
        var body = EquipmentPacketBody(b =>
        {
            b.Add(0);
            b.AddRange(VarInt(1));
            b.AddRange(VarInt(shieldId));
            b.AddRange(VarInt(1));
            b.AddRange(VarInt(0));
            b.AddRange(VarInt(bannerPatterns));
            b.AddRange(VarInt(1));
            b.AddRange(VarInt(2));             // stripe_bottom
            b.AddRange(VarInt(11));            // blue dye
        });
        var items = new EquipmentItem[SetEquipmentPackets.MaxSlots];
        Assert.Equal(DecodeStatus.Success, SetEquipmentPackets.TryRead(body, out var entityId, items, out var count, out _));
        var tracker = new EntityTracker();
        Assert.True(tracker.Add(new AddedEntity(entityId, Guid.Empty, EntityPlaceholderMesher.PlayerEntityTypeId, 0, 0, 0, 0f, 0f, 0f, 0f, 180f, 0f, 0)));
        Assert.True(tracker.ApplyEquipment(entityId, items[..count]));
        var state = tracker.Interpolate(0f)[0];
        Assert.Null(state.EquipmentBannerBaseColorAt(0));

        var atlas = ShieldPatternAtlas(state, offhand: false);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var baseLayer = layers.Single(layer => layer.Name == "held_special_shield_pattern_base_right");
        Assert.Equal(DyeColors.DiffuseAt(0), ReadColor(baseLayer.Geometry, 0));
        AssertLayerMatchesRows(SpecialItemModelDefinition.Shield, baseLayer.Geometry,
            Load("shield-patterns-held-world.reference.txt").Where(row => row.Layer == "SHIELD_PATTERN_BASE_RIGHT").ToArray(),
            "SHIELD_PATTERN_BASE_RIGHT");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllSixteenPatternLayersAreEmitted(bool banner)
    {
        using var fixture = new ItemFixture();
        var itemName = banner ? "red_banner" : "shield";
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        var allLayers = new Sharpcraft.Common.Network.BannerPatternLayer[SpecialItemModelDefinition.MaxBannerPatternLayers];
        for (var index = 0; index < allLayers.Length; index++)
        {
            allLayers[index] = new Sharpcraft.Common.Network.BannerPatternLayer(index % 16, BannerPatternTable.AssetAt(index + 1));
        }

        var slots = new IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[] { allLayers, allLayers };
        var state = new EntityRenderState(13, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: itemId,
            EquipmentHasBannerPatterns: [true, true],
            EquipmentBannerPatternLayers: slots,
            EquipmentBannerBaseColors: [12, 12]);
        var atlas = banner ? BannerPatternAtlas(allLayers) : ShieldPatternAtlas(state, offhand: false, allLayers);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var prefix = banner ? "held_special_banner_pattern_" : "held_special_shield_pattern_";
        Assert.Contains(layers, layer => layer.Name == prefix + "base_right");
        for (var index = 0; index < SpecialItemModelDefinition.MaxBannerPatternLayers; index++)
        {
            Assert.Contains(layers, layer => layer.Name == prefix + index + "_right");
        }
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

    private static byte[] EquipmentPacketBody(Action<List<byte>> slot)
    {
        var bytes = new List<byte>();
        bytes.AddRange(VarInt(PacketIds.PlayClientbound.SetEquipment));
        bytes.AddRange(VarInt(42));
        slot(bytes);
        return bytes.ToArray();
    }

    private static ItemAtlas BannerPatternAtlas(Sharpcraft.Common.Network.BannerPatternLayer[] layers)
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SpecialItemModelDefinition.BannerTexture] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/banner/base"] = new(64, 64, new byte[64 * 64 * 4])
        };
        foreach (var layer in layers)
        {
            textures["entity/banner/" + layer.PatternAssetId] = new(64, 64, new byte[64 * 64 * 4]);
        }

        return ItemAtlas.FromTextures(textures);
    }
    [Fact]
    public void PatternSpritesArePrimedForTheAtlas()
    {
        using var fixture = new ItemFixture();
        var slots = new IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[] { PatternLayers, PatternLayers };
        var bannerId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:red_banner"));
        var shieldId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        var banner = new EntityRenderState(11, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: bannerId, EquipmentHasBannerPatterns: [true, true],
            EquipmentBannerPatternLayers: slots);
        var shield = new EntityRenderState(12, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            OffHandItemId: shieldId, EquipmentHasBannerPatterns: [true, true],
            EquipmentBannerPatternLayers: slots, EquipmentBannerBaseColors: [12, 12]);
        var bannerTextures = ItemInHandMesher.ResolveTextures(banner).ToArray();
        Assert.Contains("entity/banner/base", bannerTextures);
        Assert.Contains("entity/banner/square_bottom_left", bannerTextures);
        Assert.Contains("entity/banner/stripe_top", bannerTextures);
        var shieldTextures = ItemInHandMesher.ResolveTextures(shield).ToArray();
        Assert.Contains("entity/shield/base", shieldTextures);
        Assert.Contains("entity/shield/square_bottom_left", shieldTextures);
        Assert.Contains("entity/shield/stripe_top", shieldTextures);
    }
    [Fact]
    public void MigratedSpecialLayersAreNotOnTheResolver()
    {
        // B-4 T3: chest/skull/banner body (and the banner flag) are emitted by ItemInHandMesher; the resolver keeps only
        // the banner pattern overlay among the held special layers migrated here.
        var layers = EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId).RenderLayers!;
        foreach (var name in new[] { "held_special_head_right", "held_special_head_left",
                     "held_special_chest_right", "held_special_chest_left",
                     "held_special_banner_right", "held_special_banner_left",
                     "held_special_banner_flag_right", "held_special_banner_flag_left",
                     "held_special_banner_patterns_right", "held_special_banner_patterns_left",
                     "held_special_shield_patterns_right", "held_special_shield_patterns_left" })
        {
            Assert.DoesNotContain(layers, layer => layer.Name == name);
        }
    }
    public static TheoryData<string, string, string, string> ChestSkullBannerCases => new()
    {
        { "chest-held-world.reference.txt", "CHEST_RIGHT", "chest", "held_special_chest_right" },
        { "chest-held-world.reference.txt", "CHEST_LEFT", "chest", "held_special_chest_left" },
        { "skull-held-world.reference.txt", "SKULL_RIGHT", "skeleton_skull", "held_special_head_right" },
        { "skull-held-world.reference.txt", "SKULL_LEFT", "skeleton_skull", "held_special_head_left" },
        { "banner-held-world.reference.txt", "BANNER_RIGHT", "white_banner", "held_special_banner_right" },
        { "banner-held-world.reference.txt", "BANNER_LEFT", "white_banner", "held_special_banner_left" }
    };

    [Theory]
    [MemberData(nameof(ChestSkullBannerCases))]
    public void ProductionChestSkullBannerMatchesTheWorldSamplePerKey(string sampleFile, string sampleLayer, string itemName,
        string layerName)
    {
        var offhand = layerName.EndsWith("_left", StringComparison.Ordinal);
        var model = itemName switch
        {
            "chest" => SpecialItemModelDefinition.ChestRaw,
            "skeleton_skull" => SpecialItemModelDefinition.SkullRaw,
            _ => SpecialItemModelDefinition.BannerStanding
        };
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        var state = new EntityRenderState(4, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1);
        var texture = EntityModelResolver.ResolveHeldItemSpecialTexture(state, offhand);
        Assert.NotNull(texture);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4])
        });
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);
        Assert.DoesNotContain(layers, entry => entry.Name == layerName + "_glint");

        var rows = Load(sampleFile).Where(row => row.Layer == sampleLayer).ToArray();
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        Assert.Equal(rows.Length, layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{sampleLayer}: production cube '{cubeName}' missing");
            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            Assert.Equal(24, ordered.Length);
            foreach (var row in ordered)
            {
                var produced = ReadVertex(layer.Geometry, (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex);
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((produced.Position - expected).Length() <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {produced.Position} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }
        }
    }
    private static readonly Sharpcraft.Common.Network.BannerPatternLayer[] PatternLayers =
    [
        new(14, "square_bottom_left"),
        new(11, "stripe_top")
    ];

    [Theory]
    [InlineData(false, "BANNER_FLAG_RIGHT", "held_special_banner_flag_right")]
    [InlineData(true, "BANNER_FLAG_LEFT", "held_special_banner_flag_left")]
    public void BannerFlagMatchesTheWorldSamplePerKey(bool offhand, string sampleLayer, string layerName)
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:white_banner"));
        var state = new EntityRenderState(6, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1);
        var atlas = BannerAtlas();
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);
        var rows = Load("banner-held-world-flag.reference.txt").Where(row => row.Layer == sampleLayer).ToArray();
        AssertLayerMatchesRows(SpecialItemModelDefinition.BannerFlagHeld, layer.Geometry, rows, sampleLayer);
    }

    [Theory]
    [InlineData(false, 0, "BANNER_PATTERN_BASE_RIGHT", "held_special_banner_pattern_base_right")]
    [InlineData(false, 1, "BANNER_PATTERN_0_RIGHT", "held_special_banner_pattern_0_right")]
    [InlineData(false, 2, "BANNER_PATTERN_1_RIGHT", "held_special_banner_pattern_1_right")]
    [InlineData(true, 0, "BANNER_PATTERN_BASE_LEFT", "held_special_banner_pattern_base_left")]
    [InlineData(true, 1, "BANNER_PATTERN_0_LEFT", "held_special_banner_pattern_0_left")]
    [InlineData(true, 2, "BANNER_PATTERN_1_LEFT", "held_special_banner_pattern_1_left")]
    public void BannerPatternLayerMatchesTheWorldSamplePerKey(bool offhand, int layerIndex, string sampleLayer, string layerName)
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:red_banner"));
        var slots = new IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[] { PatternLayers, PatternLayers };
        var state = new EntityRenderState(7, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1,
            EquipmentHasBannerPatterns: [true, true],
            EquipmentBannerPatternLayers: slots,
            EquipmentBannerBaseColors: [null, null]);
        var atlas = BannerAtlas();
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);
        var rows = Load("banner-patterns-held-world.reference.txt").Where(row => row.Layer == sampleLayer).ToArray();
        AssertLayerMatchesRows(SpecialItemModelDefinition.BannerFlagHeld, layer.Geometry, rows, sampleLayer);

        var tint = layerIndex == 0 ? DyeColors.DiffuseAt(14) : DyeColors.DiffuseAt(PatternLayers[layerIndex - 1].DyeColorId);
        Assert.Equal(tint, ReadColor(layer.Geometry, 0));
    }

    [Theory]
    [InlineData(false, 0, "SHIELD_PATTERN_BASE_RIGHT", "held_special_shield_pattern_base_right")]
    [InlineData(false, 1, "SHIELD_PATTERN_0_RIGHT", "held_special_shield_pattern_0_right")]
    [InlineData(false, 2, "SHIELD_PATTERN_1_RIGHT", "held_special_shield_pattern_1_right")]
    [InlineData(true, 0, "SHIELD_PATTERN_BASE_LEFT", "held_special_shield_pattern_base_left")]
    [InlineData(true, 1, "SHIELD_PATTERN_0_LEFT", "held_special_shield_pattern_0_left")]
    [InlineData(true, 2, "SHIELD_PATTERN_1_LEFT", "held_special_shield_pattern_1_left")]
    public void ShieldPatternLayerMatchesTheWorldSamplePerKey(bool offhand, int layerIndex, string sampleLayer, string layerName)
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        var slots = new IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[] { PatternLayers, PatternLayers };
        var state = new EntityRenderState(8, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1,
            EquipmentHasBannerPatterns: [true, true],
            EquipmentBannerPatternLayers: slots,
            EquipmentBannerBaseColors: [12, 12]);
        var atlas = ShieldPatternAtlas(state, offhand);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);
        var rows = Load("shield-patterns-held-world.reference.txt").Where(row => row.Layer == sampleLayer).ToArray();
        AssertLayerMatchesRows(SpecialItemModelDefinition.Shield, layer.Geometry, rows, sampleLayer);

        var tint = layerIndex == 0 ? DyeColors.DiffuseAt(12) : DyeColors.DiffuseAt(PatternLayers[layerIndex - 1].DyeColorId);
        Assert.Equal(tint, ReadColor(layer.Geometry, 0));
    }

    [Fact]
    public void UnpatternedBannerHasOnlyTheBasePatternLayer()
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:white_banner"));
        var state = new EntityRenderState(9, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: itemId);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, BannerAtlas());
        Assert.Contains(layers, layer => layer.Name == "held_special_banner_pattern_base_right");
        Assert.DoesNotContain(layers, layer => layer.Name.StartsWith("held_special_banner_pattern_0", StringComparison.Ordinal));
    }

    [Fact]
    public void UnpatternedShieldHasNoPatternLayer()
    {
        using var fixture = new ItemFixture();
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        var state = new EntityRenderState(10, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            MainHandItemId: itemId);
        var texture = EntityModelResolver.ResolveHeldItemSpecialTexture(state, offhand: false);
        Assert.Equal(SpecialItemModelDefinition.ShieldTexture, texture);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture!] = new(64, 64, new byte[64 * 64 * 4])
        });
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        Assert.DoesNotContain(layers, layer => layer.Name.StartsWith("held_special_shield_pattern_", StringComparison.Ordinal));
    }

    private static ItemAtlas BannerAtlas() =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [SpecialItemModelDefinition.BannerTexture] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/banner/base"] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/banner/square_bottom_left"] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/banner/stripe_top"] = new(64, 64, new byte[64 * 64 * 4])
        });

    private static ItemAtlas ShieldPatternAtlas(EntityRenderState state, bool offhand,
        Sharpcraft.Common.Network.BannerPatternLayer[]? extraLayers = null)
    {
        var body = EntityModelResolver.ResolveHeldItemSpecialTexture(state, offhand);
        Assert.NotNull(body);
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [body!] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/shield/base"] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/shield/square_bottom_left"] = new(64, 64, new byte[64 * 64 * 4]),
            ["entity/shield/stripe_top"] = new(64, 64, new byte[64 * 64 * 4])
        };
        if (extraLayers is not null)
        {
            foreach (var layer in extraLayers)
            {
                textures["entity/shield/" + layer.PatternAssetId] = new(64, 64, new byte[64 * 64 * 4]);
            }
        }

        return ItemAtlas.FromTextures(textures);
    }

    private static void AssertLayerMatchesRows(EntityModelDefinition model, DynamicGeometry geometry, Row[] rows, string context)
    {
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{context}: production cube '{cubeName}' missing");
            foreach (var row in group)
            {
                var produced = ReadVertex(geometry, (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex);
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((produced.Position - expected).Length() <= Tolerance,
                    $"{context}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {produced.Position} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{context}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }
        }
    }

    private static Vector3 ReadColor(DynamicGeometry geometry, int vertex)
    {
        var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]);
        return new Vector3((packed & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, ((packed >> 16) & 0xFF) / 255f);
    }
    [Theory]
    [InlineData("trident", "TRIDENT_RIGHT", "held_special_trident_right")]
    [InlineData("shield", "SHIELD_RIGHT", "held_special_shield_right")]
    public void FoilAddsEntityScaleGlintOnTopOfTheHeldSpecial(string itemName, string sampleLayer, string layerName)
    {
        var isTrident = itemName == "trident";
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        GlintClock.SetFixedSourceMillis(FixedMillis);
        try
        {
            var plain = new EntityRenderState(2, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
                MainHandItemId: itemId);
            var plainLayers = new ItemInHandMesher().Build([plain], [new EntityPose()], variants: null, atlas);
            Assert.Single(plainLayers, layer => layer.Name == layerName);
            Assert.DoesNotContain(plainLayers, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal));

            var foil = plain with { MainHandFoil = ItemFoilType.Standard };
            var foilLayers = new ItemInHandMesher().Build([foil], [new EntityPose()], variants: null, atlas);
            var @base = foilLayers.Single(layer => layer.Name == layerName);
            var glint = foilLayers.Single(layer => layer.Name == layerName + "_glint");
            Assert.Equal(@base.Geometry.VertexFloats, glint.Geometry.VertexFloats);
            Assert.Equal(BasePositions(@base.Geometry), BasePositions(glint.Geometry));
            for (var i = 0; i < @base.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
            {
                var baseUv = ReadVertex(@base.Geometry, i);
                var glintUv = ReadVertex(glint.Geometry, i);
                var expected = GlintTextureTransform.Apply(baseUv.U, baseUv.V, FixedMillis, GlintTextureTransform.EntityScale);
                Assert.Equal((float)(Half)expected.U, glintUv.U, 3);
                Assert.Equal((float)(Half)expected.V, glintUv.V, 3);
            }

            // The world sample layer still has full geometry coverage for this hand.
            Assert.Contains(Load(isTrident ? "trident-held-world.reference.txt" : "shield-held-world.reference.txt"),
                row => row.Layer == sampleLayer);
        }
        finally
        {
            GlintClock.SetProvider(null);
        }
    }

    private static List<Vector3> BasePositions(DynamicGeometry geometry)
    {
        var positions = new List<Vector3>(geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
        {
            positions.Add(ReadVertex(geometry, i).Position);
        }

        return positions;
    }

    private static (Vector3 Position, float U, float V) ReadVertex(DynamicGeometry geometry, int vertex)
    {
        var offset = vertex * ChunkMesher.FloatsPerVertex;
        var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[offset + 4]);
        return (
            new Vector3(geometry.Vertices[offset], geometry.Vertices[offset + 1], geometry.Vertices[offset + 2]),
            (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16)));
    }

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    private static List<Row> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            rows.Add(new Row(fields[0], fields[1], int.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture), int.Parse(fields[4], CultureInfo.InvariantCulture),
                new Vector3(float.Parse(fields[5], CultureInfo.InvariantCulture), float.Parse(fields[6], CultureInfo.InvariantCulture),
                    float.Parse(fields[7], CultureInfo.InvariantCulture)),
                float.Parse(fields[8], CultureInfo.InvariantCulture), float.Parse(fields[9], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private readonly record struct Row(string Layer, string Part, int Cube, int Polygon, int Vertex, Vector3 Position, float U, float V);

    /// <summary>Temp resource stack with the trident/shield base item models and special wrappers.</summary>
    private sealed class ItemFixture : IDisposable
    {
        private readonly string _root;
        private readonly ItemModelResolver _previous;
        private readonly ResourceManager _resources;

        public ItemFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sharpcraft-held-special-" + Guid.NewGuid().ToString("N"));
            Write("assets/minecraft/items/trident.json",
                """{"model":{"type":"minecraft:condition","property":"minecraft:using_item","on_false":{"type":"minecraft:special","base":"minecraft:item/trident_in_hand","model":{"type":"minecraft:trident"},"transformation":{"scale":[1,-1,-1]}},"on_true":{"type":"minecraft:special","base":"minecraft:item/trident_throwing","model":{"type":"minecraft:trident"},"transformation":{"scale":[1,-1,-1]}}}}""");
            Write("assets/minecraft/items/shield.json",
                """{"model":{"type":"minecraft:condition","property":"minecraft:using_item","on_false":{"type":"minecraft:special","base":"minecraft:item/shield","model":{"type":"minecraft:shield"},"transformation":{"scale":[1,-1,-1]}},"on_true":{"type":"minecraft:special","base":"minecraft:item/shield_blocking","model":{"type":"minecraft:shield"},"transformation":{"scale":[1,-1,-1]}}}}""");
            Write("assets/minecraft/items/chest.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/chest","model":{"type":"minecraft:chest","texture":"minecraft:normal"}}}""");
            Write("assets/minecraft/items/skeleton_skull.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/template_skull","model":{"type":"minecraft:head","kind":"skeleton"},"transformation":{"translation":[0.5,0,0.5],"scale":[1,1,1]}}}""");
            Write("assets/minecraft/items/white_banner.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/template_banner","model":{"type":"minecraft:banner","color":"white"},"transformation":{"translation":[0.5,0,0.5],"scale":[0.6666667,-0.6666667,-0.6666667]}}}""");
            Write("assets/minecraft/items/red_banner.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/template_banner","model":{"type":"minecraft:banner","color":"red"},"transformation":{"translation":[0.5,0,0.5],"scale":[0.6666667,-0.6666667,-0.6666667]}}}""");
            WriteModel("chest", "[75,315,0]", "[0,2.5,0]", "[75,315,0]", "[0,2.5,0]", scale: "[0.375,0.375,0.375]");
            WriteModel("template_skull", "[45,45,0]", "[0,3,0]", "[45,45,0]", "[0,3,0]", scale: "[0.5,0.5,0.5]");
            WriteModel("template_banner", "[0,90,0]", "[0,2,0.5]", "[0,90,0]", "[0,2,0.5]", scale: "[0.375,0.375,0.375]");
            WriteModel("trident_in_hand", "[0,60,0]", "[11,17,-2]", "[0,60,0]", "[11,17,-2]");
            WriteModel("trident_throwing", "[0,90,180]", "[8,-17,9]", "[0,90,180]", "[8,-17,9]");
            WriteModel("shield", "[0,90,0]", "[10,6,-4]", "[0,90,0]", "[10,6,-4]");
            WriteModel("shield_blocking", "[45,155,0]", "[-3.49,11,-2]", "[45,155,0]", "[-3.49,11,-2]");
            var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(_root, "held-special-test"));
            _resources = new ResourceManager(stack, 1);
            _previous = EntityModelResolver.ItemModels!;
            EntityModelResolver.ItemModels = new ItemModelResolver(_resources);
        }

        public void Dispose()
        {
            EntityModelResolver.ItemModels = _previous;
            _resources.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private void WriteModel(string name, string rightRotation, string rightTranslation, string leftRotation,
            string leftTranslation, string scale = "[1,1,1]") =>
            Write($"assets/minecraft/models/item/{name}.json",
                "{\"display\":{\"thirdperson_righthand\":{\"rotation\":" + rightRotation
                + ",\"translation\":" + rightTranslation
                + ",\"scale\":" + scale + "},\"thirdperson_lefthand\":{\"rotation\":" + leftRotation
                + ",\"translation\":" + leftTranslation
                + ",\"scale\":" + scale + "}}}");

        private void Write(string relative, string json)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
