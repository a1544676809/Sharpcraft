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
/// Vault static display item slice: shared_data.display_item decoding, GROUND placement, empty-item
/// gating, and production item-atlas pre-scan coverage.
/// </summary>
public sealed class VaultRendererTests : IDisposable
{
    private readonly string _root;
    private readonly AssetStack _stack;
    private readonly ResourceManager _resources;
    private readonly ItemModelResolver _resolver;
    private readonly ItemAtlas _atlas;

    public VaultRendererTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sharpcraft-vault-" + Guid.NewGuid().ToString("N"));
        Write("assets/minecraft/items/apple.json", """{"model":{"type":"minecraft:model","model":"minecraft:item/apple"}}""");
        Write("assets/minecraft/models/item/apple.json", """
            {"textures":{"all":"minecraft:item/apple"},"elements":[
              {"from":[0,0,0],"to":[16,16,16],"faces":{
                "down":{"texture":"#all"},"up":{"texture":"#all"},
                "north":{"texture":"#all"},"south":{"texture":"#all"},
                "west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
            """);
        _stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
            .Add(new DirectoryAssetSource(_root, "vault-test"));
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
    public void ReadsDisplayItemFromVaultSharedData()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var appleId = registry.GetId(Identifier.Parse("minecraft:apple"));
        var record = VaultRecord(count: 3);

        Assert.True(BlockEntityData.TryReadVaultDisplayItem(record, registry, out var item));
        Assert.Equal(appleId, item.ItemId);
        Assert.Equal(3, item.Count);
    }

    [Fact]
    public void MissingDisplayItemIsEmpty()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.Vault, new NbtCompound()
            .Set("shared_data", new NbtCompound()));

        Assert.False(BlockEntityData.TryReadVaultDisplayItem(record, registry, out var item));
        Assert.True(item.IsEmpty);
    }

    [Fact]
    public void SharedDataOnNonVaultTypeIsNotParsed()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var tag = new NbtCompound()
            .Set("shared_data", new NbtCompound()
                .Set("display_item", new NbtCompound()
                    .Set("id", new NbtString("minecraft:apple"))
                    .Set("count", new NbtInt(1))));
        var record = new BlockEntityRecord(0, 0, 0, VanillaBlockEntityTypes.DecoratedPot, tag);

        Assert.False(BlockEntityData.TryReadVaultDisplayItem(record, registry, out var item));
        Assert.True(item.IsEmpty);
    }

    [Fact]
    public void EmptyDisplayItemDoesNotDraw()
    {
        var renderer = BuildRenderer();
        var geometry = build(renderer, BlockEntityItemSlot.Empty, 0f, 0f, 0f);

        Assert.Equal(0, renderer.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    [Fact]
    public void HandlesVaultOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Vault, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(0f, 2f, 0f)]
    [InlineData(0f, 0f, 3f)]
    [InlineData(2f, 3f, 4f)]
    public void DisplayItemEmitsFinalVerticesAndUvs(float x, float y, float z)
    {
        var renderer = BuildRenderer();
        var geometry = build(renderer, ItemSlot(), x, y, z);

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(36, geometry.IndexCount);
        // GROUND display transform centres the item under the vault's (0.5, 0.4, 0.5) pose.
        AssertVertex(geometry, 0, x, y - 0.1f, z + 1f, 0f, 0f);
    }

    [Fact]
    public void ProductionAtlasScanIncludesVaultDisplayItem()
    {
        var registry = ItemRegistry.LoadEmbedded();
        var world = new ClientWorld(-4, 24);
        var chunk = world.AddChunk(0, 0);
        chunk.AddBlockEntity(VaultRecord(count: 1));

        var paths = WindowCommand.CollectBlockEntityItemTexturePaths(world, registry, _resolver);
        Assert.Contains("item/apple", paths);

        var textures = paths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => new TextureData(16, 16, new byte[16 * 16 * 4]), StringComparer.Ordinal);
        var atlas = ItemAtlas.FromTextures(textures);
        var renderer = BuildRenderer();
        var geometry = renderer.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Vault, Items: new BlockEntityItemSlots(Slot0: ItemSlot()))],
            atlas,
            atlas);

        Assert.Equal(1, renderer.LastEntityCount);
        Assert.True(geometry.VertexFloats > 0);
        Assert.True(geometry.IndexCount > 0);
    }

    private VaultRenderer BuildRenderer() => new(new BlockEntityItemMesher(_resolver));

    private DynamicGeometry build(VaultRenderer renderer, BlockEntityItemSlot item, float x, float y, float z) =>
        renderer.Build(
            [new BlockEntityRenderState(x, y, z, VanillaBlockEntityTypes.Vault, Items: new BlockEntityItemSlots(Slot0: item))],
            _atlas,
            _atlas);

    private static BlockEntityItemSlot ItemSlot()
    {
        var appleId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:apple"));
        return new BlockEntityItemSlot(appleId, 1);
    }

    private static BlockEntityRecord VaultRecord(int count) =>
        new(0, 0, 0, VanillaBlockEntityTypes.Vault, new NbtCompound()
            .Set("shared_data", new NbtCompound()
                .Set("display_item", new NbtCompound()
                    .Set("id", new NbtString("minecraft:apple"))
                    .Set("count", new NbtInt(count)))));

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
