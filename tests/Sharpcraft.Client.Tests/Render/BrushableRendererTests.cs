// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Brushable block static item slice: NBT item/hit_direction decoding, the source dust/direction
/// placement transform, and the item-mesher Fixed display path.
/// </summary>
public sealed class BrushableRendererTests : IDisposable
{
    private readonly string _root;
    private readonly AssetStack _stack;
    private readonly ResourceManager _resources;
    private readonly ItemModelResolver _resolver;
    private readonly ItemAtlas _atlas;

    public BrushableRendererTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpcraft-brushable-" + Guid.NewGuid().ToString("N"));
        Write("assets/minecraft/items/apple.json", """{"model":{"type":"minecraft:model","model":"minecraft:item/apple"}}""");
        Write("assets/minecraft/models/item/apple.json", """
            {"textures":{"all":"minecraft:item/apple"},"elements":[
              {"from":[0,0,0],"to":[16,16,16],"faces":{
                "down":{"texture":"#all"},"up":{"texture":"#all"},
                "north":{"texture":"#all"},"south":{"texture":"#all"},
                "west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
            """);
        _stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
            .Add(new DirectoryAssetSource(_root, "brushable-test"));
        _resources = new ResourceManager(_stack, 1);
        _resolver = new ItemModelResolver(_resources);
        _atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/apple"] = new(16, 16, new byte[16 * 16 * 4])
        });
    }

    public void Dispose()
    {
        _resources.Dispose();
        _stack.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ReadsBrushableItemAndHitDirectionFromNbt()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var appleId = registry.GetId(Identifier.Parse("minecraft:apple"));
        var tag = new NbtCompound()
            .Set("item", new NbtCompound()
                .Set("id", new NbtString("minecraft:apple"))
                .Set("count", new NbtInt(2)))
            .Set("hit_direction", new NbtByte(5));
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.BrushableBlock, tag);

        Assert.True(BlockEntityData.TryReadBrushable(record, registry, out var data));
        Assert.Equal(appleId, data.Item.ItemId);
        Assert.Equal(2, data.Item.Count);
        Assert.Equal(HitDirection.East, data.HitDirection);
        Assert.True(BlockEntityData.TryReadBrushableItem(record, registry, out var item));
        Assert.Equal(appleId, item.ItemId);
        Assert.True(BlockEntityData.TryReadHitDirection(record, out var direction));
        Assert.Equal(HitDirection.East, direction);
    }

    [Fact]
    public void MissingHitDirectionIsNone()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.BrushableBlock, new NbtCompound());

        Assert.False(BlockEntityData.TryReadHitDirection(record, out var direction));
        Assert.Equal(HitDirection.None, direction);
        Assert.False(BlockEntityData.TryReadBrushable(record, registry, out var data));
        Assert.Equal(HitDirection.None, data.HitDirection);
        Assert.True(data.Item.IsEmpty);
    }

    [Fact]
    public void NonBrushableItemTagIsNotParsedAsBrushable()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var tag = new NbtCompound()
            .Set("item", new NbtCompound()
                .Set("id", new NbtString("minecraft:apple"))
                .Set("count", new NbtInt(1)))
            .Set("hit_direction", new NbtByte(5));
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.DecoratedPot, tag);

        Assert.False(BlockEntityData.TryReadBrushable(record, registry, out var data));
        Assert.True(data.Item.IsEmpty);
        Assert.Equal(HitDirection.None, data.HitDirection);
        Assert.False(BlockEntityData.TryReadBrushableItem(record, registry, out var item));
        Assert.True(item.IsEmpty);
        Assert.False(BlockEntityData.TryReadHitDirection(record, out var direction));
        Assert.Equal(HitDirection.None, direction);
    }

    [Fact]
    public void WindowTextureScanIncludesBrushableItemWithoutManualSpriteInjection()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var tag = new NbtCompound()
            .Set("item", new NbtCompound()
                .Set("id", new NbtString("minecraft:apple"))
                .Set("count", new NbtInt(1)));
        var world = new ClientWorld(-4, 24);
        var chunk = world.AddChunk(0, 0);
        chunk.AddBlockEntity(new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.BrushableBlock, tag));

        var paths = WindowCommand.CollectBlockEntityItemTexturePaths(world, registry, _resolver);
        Assert.Contains("item/apple", paths);

        var textures = paths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => new TextureData(16, 16, new byte[16 * 16 * 4]), StringComparer.Ordinal);
        var atlas = ItemAtlas.FromTextures(textures);
        var renderer = BuildRenderer();
        var geometry = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.BrushableBlock, Items: new BlockEntityItemSlots(Slot0: ItemSlot()), Dusted: 1, HitDirection: HitDirection.East)],
            atlas,
            atlas);

        Assert.Equal(1, renderer.LastEntityCount);
        Assert.True(geometry.VertexFloats > 0);
        Assert.True(geometry.IndexCount > 0);
    }

    [Fact]
    public void BrushableRendererSkipsEmptyItemZeroDustAndMissingDirection()
    {
        var renderer = BuildRenderer();
        var item = ItemSlot();

        Assert.Equal(0, Build(renderer, BlockEntityItemSlot.Empty, dusted: 1, HitDirection.North).VertexFloats);
        Assert.Equal(0, Build(renderer, item, dusted: 0, HitDirection.North).VertexFloats);
        Assert.Equal(0, Build(renderer, item, dusted: 1, HitDirection.None).VertexFloats);

        var valid = Build(renderer, item, dusted: 1, HitDirection.North);
        Assert.Equal(1, renderer.LastEntityCount);
        Assert.Equal(24, valid.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, valid.IndexCount);
    }

    [Fact]
    public void HandlesBrushableOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.BrushableBlock, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    [Theory]
    [InlineData((byte)1, HitDirection.East, 1.07183f, 0.25f, 0.26805f, 0f, 0f)]
    [InlineData((byte)2, HitDirection.West, 0.36683f, 0.25f, 0.26805f, 0f, 0f)]
    [InlineData((byte)3, HitDirection.Up, 0.73195f, 0.725f, 0.76683f, 0f, 0f)]
    [InlineData((byte)1, HitDirection.Down, 0.73195f, -0.055f, 0.76683f, 0f, 0f)]
    [InlineData((byte)2, HitDirection.North, 0.73195f, 0.25f, 0.36683f, 0f, 0f)]
    [InlineData((byte)3, HitDirection.South, 0.73195f, 0.25f, 1.22183f, 0f, 0f)]
    public void DustAndDirectionEmitFinalVerticesAndUvs(
        byte dusted,
        HitDirection direction,
        float x,
        float y,
        float z,
        float u,
        float v)
    {
        var renderer = BuildRenderer();
        var geometry = Build(renderer, ItemSlot(), dusted, direction);

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        AssertVertex(geometry, 0, x, y, z, u, v);
    }

    [Fact]
    public void TranslationTableMatchesSourceDirectionOffsets()
    {
        Assert.Equal(new System.Numerics.Vector3(0.73f, 0f, 0.5f), BrushableRenderer.Translations(HitDirection.East, 0));
        Assert.Equal(new System.Numerics.Vector3(0.25f, 0f, 0.5f), BrushableRenderer.Translations(HitDirection.West, 0));
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0.25f, 0.5f), BrushableRenderer.Translations(HitDirection.Up, 0));
        Assert.Equal(new System.Numerics.Vector3(0.5f, -0.23f, 0.5f), BrushableRenderer.Translations(HitDirection.Down, 0));
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0f, 0.25f), BrushableRenderer.Translations(HitDirection.North, 0));
        Assert.Equal(new System.Numerics.Vector3(0.5f, 0f, 0.73f), BrushableRenderer.Translations(HitDirection.South, 0));
    }

    private BrushableRenderer BuildRenderer() => new(new BlockEntityItemMesher(_resolver));

    private DynamicGeometry Build(BrushableRenderer renderer, BlockEntityItemSlot item, byte dusted, HitDirection direction) =>
        renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.BrushableBlock, Items: new BlockEntityItemSlots(Slot0: item), Dusted: dusted, HitDirection: direction)],
            _atlas,
            _atlas);

    private static BlockEntityItemSlot ItemSlot()
    {
        var appleId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:apple"));
        return new BlockEntityItemSlot(appleId, 1);
    }

    private static void AssertVertex(DynamicGeometry geometry, int vertex, float x, float y, float z, float u, float v)
    {
        var position = VertexFieldReader.Position(geometry.Vertices, vertex);
        var uv = VertexFieldReader.Uv(geometry.Vertices, vertex);
        Assert.Equal(x, position.X, 3);
        Assert.Equal(y, position.Y, 3);
        Assert.Equal(z, position.Z, 3);
        Assert.Equal(u, uv.U, 3);
        Assert.Equal(v, uv.V, 3);
    }

    private void Write(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
