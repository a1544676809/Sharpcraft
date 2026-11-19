// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 226: every vertex carries a sixteen bit sprite id and the shader resolves it through the
/// sprite rectangle table, so an atlas whose sprites do not carry a distinct id samples the wrong
/// rectangle. Entity skins are packed by their own atlas; when those sprites all defaulted to id zero,
/// every face sampled the whole sheet, which is the "geometry right, texture scrambled" report.
/// </summary>
/// <remarks>
/// The oracle rebuilds the table from the ids the sprites carry, by the same rule the shader follows,
/// instead of reading the production table: an assertion that compares an implementation with itself
/// would pass even when both are wrong. The production table is then required to match it.
/// </remarks>
public sealed class Repro_226_SpriteRectIdTests
{
    private static TextureData Image(int width, int height, byte fill) =>
        new(width, height, Filled(width * height * 4, fill));

    private static byte[] Filled(int length, byte value)
    {
        var data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    /// <summary>Builds the table the vertex stage indexes, from the ids the sprites actually carry.</summary>
    private static float[] TableFrom(IEnumerable<SpriteRect> sprites)
    {
        var list = new List<SpriteRect>(sprites);
        var highest = 0;
        foreach (var sprite in list)
        {
            highest = Math.Max(highest, sprite.RectId);
        }

        var table = new float[(highest + 1) * 4];
        table[2] = 1f;
        table[3] = 1f;
        foreach (var sprite in list)
        {
            var at = sprite.RectId * 4;
            if (at < 0 || at + 3 >= table.Length)
            {
                continue;
            }

            table[at] = sprite.U0;
            table[at + 1] = sprite.V0;
            table[at + 2] = sprite.U1;
            table[at + 3] = sprite.V1;
        }

        return table;
    }

    private static (float U0, float V0, float U1, float V1) Resolve(float[] table, SpriteRect sprite)
    {
        var at = sprite.RectId * 4;
        Assert.True(at >= 0 && at + 3 < table.Length,
            $"rect id {sprite.RectId} is outside a table of {table.Length / 4} entries");
        return (table[at], table[at + 1], table[at + 2], table[at + 3]);
    }

    private static void AssertResolvesToItself(SpriteRect sprite, float[] table, string name)
    {
        var resolved = Resolve(table, sprite);
        Assert.Equal(sprite.U0, resolved.U0, 6);
        Assert.Equal(sprite.V0, resolved.V0, 6);
        Assert.Equal(sprite.U1, resolved.U1, 6);
        Assert.Equal(sprite.V1, resolved.V1, 6);

        // The whole-sheet rectangle is what a default id resolves to; a per-cell sprite has to sit
        // strictly inside it, or the face samples every other sprite as well.
        Assert.True(resolved.U0 > 0f || resolved.U1 < 1f || resolved.V0 > 0f || resolved.V1 < 1f,
            $"{name} still resolves to the whole sheet ({resolved.U0},{resolved.V0})-({resolved.U1},{resolved.V1})");
    }

    private static void AssertDistinctIds(IEnumerable<(string Name, SpriteRect Sprite)> sprites)
    {
        var seen = new HashSet<int>();
        foreach (var (name, sprite) in sprites)
        {
            Assert.True(seen.Add(sprite.RectId), $"{name} reuses rect id {sprite.RectId}");
        }
    }

    [Fact]
    public void EntitySkinSpriteIdsResolveToTheirOwnCell()
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/zombie"] = Image(64, 64, 1),
            ["entity/creeper"] = Image(64, 64, 2),
            ["entity/cow"] = Image(64, 96, 3)
        };

        var atlas = EntitySkinAtlas.FromTextures(images);
        var sprites = new List<(string Name, SpriteRect Sprite)>();
        foreach (var path in images.Keys)
        {
            var sprite = atlas.Get(path);
            Assert.NotNull(sprite);
            sprites.Add((path, sprite!));
        }

        AssertDistinctIds(sprites);
        var table = TableFrom(sprites.Select(pair => pair.Sprite));
        foreach (var (name, sprite) in sprites)
        {
            AssertResolvesToItself(sprite, table, name);
        }

        // The table the renderer uploads has to be the one those ids index.
        Assert.Equal(table, atlas.RectTable);
        Assert.Equal(new[] { 0f, 0f, 1f, 1f }, atlas.RectTable[..4]);
    }

    [Fact]
    public void ItemAtlasSpriteIdsResolveToTheirOwnSprite()
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/apple"] = Image(16, 16, 1),
            ["item/stick"] = Image(16, 16, 2),
            ["item/copper_ingot"] = Image(16, 16, 3)
        };

        var atlas = ItemAtlas.FromTextures(images);
        var sprites = new List<(string Name, SpriteRect Sprite)>();
        foreach (var path in images.Keys)
        {
            var sprite = atlas.Get(path);
            Assert.NotNull(sprite);
            sprites.Add((path, sprite!));
        }

        AssertDistinctIds(sprites);
        foreach (var (name, sprite) in sprites)
        {
            AssertResolvesToItself(sprite, atlas.RectTable, name);
        }

        Assert.Equal(new[] { 0f, 0f, 1f, 1f }, atlas.RectTable[..4]);
    }

    [Fact]
    public void BlockAtlasSpriteIdsResolveToTheirOwnSprite()
    {
        // The block atlas is the producer the packing change converted, so it is the reference for the
        // others: same id rule, same table shape, entry zero reserved for the whole texture.
        var entries = new List<(Identifier Location, RgbaImage Image, int Frames)>
        {
            (new Identifier("minecraft", "stone"), new RgbaImage(16, 16, Filled(16 * 16 * 4, 1)), 1),
            (new Identifier("minecraft", "dirt"), new RgbaImage(16, 16, Filled(16 * 16 * 4, 2)), 1),
            (SpriteAtlas.MissingSprite, new RgbaImage(16, 16, Filled(16 * 16 * 4, 3)), 1)
        };

        var atlas = SpriteAtlasBuilder.Stitch(entries);
        var sprites = new List<(string Name, SpriteRect Sprite)>();
        foreach (var pair in atlas.Sprites)
        {
            sprites.Add((pair.Key.ToString(), pair.Value));
        }

        AssertDistinctIds(sprites);
        var table = TableFrom(sprites.Select(pair => pair.Sprite));
        foreach (var (name, sprite) in sprites)
        {
            AssertResolvesToItself(sprite, table, name);
        }

        Assert.Equal(table, atlas.RectTable);
        Assert.Equal(new[] { 0f, 0f, 1f, 1f }, atlas.RectTable[..4]);
    }
}
