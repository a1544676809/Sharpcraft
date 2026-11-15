// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Item-bearing block entities: the fixed NBT slot snapshot, the shared item mesher, and the
/// Shelf/Campfire placement transforms with final-vertex truth samples.
/// </summary>
public sealed class ShelfCampfireRendererTests : IDisposable
{
    private readonly string _root;
    private readonly AssetStack _stack;
    private readonly ResourceManager _resources;
    private readonly ItemModelResolver _resolver;
    private readonly ItemAtlas _atlas;

    public ShelfCampfireRendererTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpcraft-shelf-" + Guid.NewGuid().ToString("N"));
        Write("assets/minecraft/items/apple.json", """{"model":{"type":"minecraft:model","model":"minecraft:item/apple"}}""");
        Write("assets/minecraft/models/item/apple.json", """
            {"textures":{"all":"minecraft:item/apple"},"elements":[
              {"from":[0,0,0],"to":[16,16,16],"faces":{
                "down":{"texture":"#all"},"up":{"texture":"#all"},
                "north":{"texture":"#all"},"south":{"texture":"#all"},
                "west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
            """);
        _stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
            .Add(new DirectoryAssetSource(_root, "shelf-test"));
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
    public void ReadsFixedItemsFromContainerHelperNbt()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var appleId = registry.GetId(Identifier.Parse("minecraft:apple"));
        Assert.True(appleId > 0);

        var items = new NbtList(NbtTagType.Compound,
        [
            ItemEntry(0, "minecraft:apple", 2),
            ItemEntry(2, "minecraft:apple", 1),
            ItemEntry(9, "minecraft:apple", 1),
            ItemEntry(1, "not a valid identifier", 1)
        ]);
        var tag = new NbtCompound()
            .Set("Items", items)
            .Set("align_items_to_bottom", new NbtByte(1));
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.Shelf, tag);

        Assert.True(BlockEntityData.TryReadItems(record, registry, out var slots));
        Assert.Equal(appleId, slots.Slot0.ItemId);
        Assert.Equal(2, slots.Slot0.Count);
        Assert.True(slots.Slot1.IsEmpty);
        Assert.Equal(1, slots.Slot2.Count);
        Assert.True(BlockEntityData.TryReadShelfAlign(record, out var align));
        Assert.True(align);
        Assert.True(BlockEntityData.UnsupportedItemSlotCount >= 1);
        Assert.True(BlockEntityData.UnsupportedItemCount >= 1);
    }

    [Fact]
    public void EmptyShelfDoesNotDraw()
    {
        var (shelf, atlas) = BuildShelf();
        var geometry = shelf.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Shelf, Facing: 2)],
            atlas,
            atlas);

        Assert.Equal(0, shelf.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    [Fact]
    public void SingleShelfSlotEmitsFinalVerticesAndUvs()
    {
        var (shelf, atlas) = BuildShelf();
        var item = new BlockEntityItemSlots(ItemSlot(0));
        var geometry = shelf.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Shelf, Facing: 2, Items: item)],
            atlas,
            atlas);

        Assert.Equal(1, shelf.LastEntityCount);
        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        AssertVertex(geometry, 0, 0.9375f, 0.375f, 0.625f, 0f, 0f);
        AssertVertex(geometry, 1, 0.9375f, 0.375f, 0.875f, 0f, 1f);
        AssertVertex(geometry, 2, 0.6875f, 0.375f, 0.875f, 1f, 1f);
    }

    [Fact]
    public void FullShelfEmitsThreeSlotCentres()
    {
        var (shelf, atlas) = BuildShelf();
        var slots = FullShelfSlots();
        var geometry = shelf.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Shelf, Facing: 2, Items: slots)],
            atlas,
            atlas);

        Assert.Equal(1, shelf.LastEntityCount);
        Assert.Equal(72, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        AssertVertex(geometry, 0, 0.9375f, 0.375f, 0.625f, 0f, 0f);
        AssertVertex(geometry, 24, 0.625f, 0.375f, 0.625f, 0f, 0f);
        AssertVertex(geometry, 48, 0.3125f, 0.375f, 0.625f, 0f, 0f);
    }

    [Fact]
    public void ShelfFacingChangesTheSlotOrientation()
    {
        var (shelf, atlas) = BuildShelf();
        var geometry = shelf.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Shelf, Facing: 5, Items: FullShelfSlots())],
            atlas,
            atlas);

        Assert.Equal(72, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        AssertVertex(geometry, 0, 0.625f, 0.375f, 0.0625f, 0f, 0f);
    }

    [Fact]
    public void FullCampfireEmitsFourSlotFinalVertices()
    {
        var (campfire, atlas) = BuildCampfire();
        var geometry = campfire.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Campfire, Facing: 2, Items: FullShelfSlots())],
            atlas,
            atlas);

        Assert.Equal(1, campfire.LastEntityCount);
        Assert.Equal(96, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        AssertVertex(geometry, 0, 1f, 0.26172f, 1f, 0f, 0f);
        AssertVertex(geometry, 1, 1f, 0.63672f, 1f, 0f, 1f);
        AssertVertex(geometry, 24, 0f, 0.26172f, 1f, 0f, 0f);
    }

    [Fact]
    public void CampfireFacingChangesTheSlotOrientation()
    {
        var (campfire, atlas) = BuildCampfire();
        var geometry = campfire.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Campfire, Facing: 5, Items: FullShelfSlots())],
            atlas,
            atlas);

        Assert.Equal(96, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        AssertVertex(geometry, 0, 1f, 0.26172f, 0f, 0f, 0f);
    }

    [Fact]
    public void RenderersHandleShelfAndCampfireOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Shelf, renderers));
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Campfire, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    private (ShelfRenderer Shelf, ItemAtlas Atlas) BuildShelf()
    {
        return (new ShelfRenderer(new BlockEntityItemMesher(_resolver)), _atlas);
    }

    private (CampfireRenderer Campfire, ItemAtlas Atlas) BuildCampfire()
    {
        return (new CampfireRenderer(new BlockEntityItemMesher(_resolver)), _atlas);
    }

    private static BlockEntityItemSlots FullShelfSlots()
    {
        var item = ItemSlot(0);
        return new BlockEntityItemSlots(item, item, item, item);
    }

    private static BlockEntityItemSlot ItemSlot(int slot)
    {
        var appleId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:apple"));
        return new BlockEntityItemSlot(appleId, 1);
    }

    private static NbtCompound ItemEntry(int slot, string id, int count) =>
        new NbtCompound()
            .Set("Slot", new NbtByte((sbyte)slot))
            .Set("id", new NbtString(id))
            .Set("count", new NbtInt(count));

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
