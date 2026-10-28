// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Model resolution (docs/analysis/assets/block-models.md): blockstate to model, parent chain merging,
/// <c>#variable</c> resolution and the fallbacks when a block has no usable model.
/// </summary>
public sealed class BlockModelResolverTests
{
    [Fact]
    public void CubeAllChainResolvesEveryFaceToTheSameTexture()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/stone.json", """
            {"variants": {"": {"model": "minecraft:block/stone"}}}
            """);
        fixture.Add("assets/minecraft/models/block/stone.json", """
            {"parent": "minecraft:block/cube_all", "textures": {"all": "minecraft:block/stone"}}
            """);
        fixture.Add("assets/minecraft/models/block/cube_all.json", """
            {"parent": "minecraft:block/cube",
             "textures": {"particle": "#all", "down": "#all", "up": "#all",
                          "north": "#all", "east": "#all", "south": "#all", "west": "#all"}}
            """);
        fixture.Add("assets/minecraft/models/block/cube.json", """
            {"textures": {"up": "#up", "down": "#down", "north": "#north",
                          "south": "#south", "east": "#east", "west": "#west"}}
            """);

        var textures = fixture.Resolve("minecraft:stone");

        Assert.NotNull(textures);
        var expected = new Identifier("minecraft", "block/stone");
        Assert.Equal(expected, textures!.Top);
        Assert.Equal(expected, textures.Side);
        Assert.Equal(expected, textures.Bottom);
    }

    [Fact]
    public void ColumnModelsUseEndForTopAndBottomAndSideForWalls()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/oak_wood.json", """
            {"variants": {"axis=y": {"model": "minecraft:block/oak_wood"}}}
            """);
        fixture.Add("assets/minecraft/models/block/oak_wood.json", """
            {"parent": "minecraft:block/cube_column",
             "textures": {"end": "minecraft:block/oak_log_top", "side": "minecraft:block/oak_log"}}
            """);
        fixture.Add("assets/minecraft/models/block/cube_column.json", """
            {"parent": "minecraft:block/cube",
             "textures": {"end": "#end", "side": "#side",
                          "up": "#end", "down": "#end", "north": "#side",
                          "south": "#side", "east": "#side", "west": "#side"}}
            """);

        var textures = fixture.Resolve("minecraft:oak_wood");

        Assert.NotNull(textures);
        Assert.Equal(new Identifier("minecraft", "block/oak_log_top"), textures!.Top);
        Assert.Equal(new Identifier("minecraft", "block/oak_log"), textures.Side);
        Assert.Equal(new Identifier("minecraft", "block/oak_log_top"), textures.Bottom);
    }

    [Fact]
    public void ChainedVariableReferencesResolveToATexture()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/grass_block.json", """
            {"variants": {"snowy=false": {"model": "block/grass_block"}}}
            """);
        fixture.Add("assets/minecraft/models/block/grass_block.json", """
            {"textures": {"top": "block/grass_block_top", "side": "block/grass_block_side",
                          "bottom": "block/dirt"}}
            """);

        var textures = fixture.Resolve("minecraft:grass_block");

        Assert.NotNull(textures);
        Assert.Equal(new Identifier("minecraft", "block/grass_block_top"), textures!.Top);
        Assert.Equal(new Identifier("minecraft", "block/grass_block_side"), textures.Side);
        Assert.Equal(new Identifier("minecraft", "block/dirt"), textures.Bottom);
    }

    [Fact]
    public void ArrayVariantUsesItsFirstModel()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/sand.json", """
            {"variants": {"": [
                {"model": "minecraft:block/sand_first"},
                {"model": "minecraft:block/sand_second"}
            ]}}
            """);
        fixture.Add("assets/minecraft/models/block/sand_first.json", """
            {"textures": {"all": "block/sand"}}
            """);
        fixture.Add("assets/minecraft/models/block/sand_second.json", """
            {"textures": {"all": "block/red_sand"}}
            """);

        var textures = fixture.Resolve("minecraft:sand");

        Assert.NotNull(textures);
        Assert.Equal(new Identifier("minecraft", "block/sand"), textures!.Top);
    }

    [Fact]
    public void MissingBlockStateResolvesToNull()
    {
        using var fixture = new ModelFixture();

        Assert.Null(fixture.Resolve("minecraft:nonexistent"));
        Assert.Equal(1, fixture.Resolver.UnresolvedCount);
    }

    [Fact]
    public void MalformedJsonIsReportedAsUnresolvedInsteadOfThrowing()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/broken.json", "{ not json");

        Assert.Null(fixture.Resolve("minecraft:broken"));
    }

    [Fact]
    public void ModelTexturesWinOverTheNameHeuristic()
    {
        using var fixture = new ModelFixture();
        fixture.Add("assets/minecraft/blockstates/oak_wood.json", """
            {"variants": {"": {"model": "minecraft:block/oak_wood"}}}
            """);
        fixture.Add("assets/minecraft/models/block/oak_wood.json", """
            {"textures": {"up": "block/oak_log", "down": "block/oak_log",
                          "north": "block/oak_log", "south": "block/oak_log",
                          "east": "block/oak_log", "west": "block/oak_log"}}
            """);
        // A texture named after the block exists too, but the model must take precedence.
        fixture.AddTexture("block/oak_wood", 1, 2, 3);
        fixture.AddTexture("block/oak_log", 4, 5, 6);

        var sprite = fixture.SpriteForOakWood();

        var log = fixture.Atlas.Sprites[new Identifier("minecraft", "block/oak_log")];
        Assert.Equal(log, sprite);
        Assert.NotEqual(fixture.Atlas.Sprites[new Identifier("minecraft", "block/oak_wood")], sprite);
    }

    private sealed class ModelFixture : IDisposable
    {
        private readonly List<(string Path, byte[] Content)> _entries = [];

        public ModelFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-models-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public AssetStack? Stack { get; private set; }

        public BlockModelResolver Resolver => _resolver ??= Build();

        public SpriteAtlas Atlas => _atlas ??= BuildAtlas();

        private BlockModelResolver? _resolver;
        private SpriteAtlas? _atlas;

        public void Add(string path, string json) =>
            _entries.Add((path, Encoding.UTF8.GetBytes(json)));

        public void AddTexture(string path, byte r, byte g, byte b)
        {
            var pixels = new byte[16 * 16 * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
                pixels[i + 3] = 255;
            }

            _entries.Add(("assets/minecraft/textures/" + path + ".png",
                PngEncoder.EncodeRgba(16, 16, pixels)));
        }

        public BlockFaceTextures? Resolve(string blockId) => Resolver.Resolve(Identifier.Parse(blockId));

        public SpriteRect SpriteForOakWood() =>
            new BlockSpriteMap(Atlas, Layout(), Resolver).ForBlock(new Identifier("minecraft", "oak_wood"), BlockFace.Side);

        private static BlockStateLayout Layout() => new(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "oak_wood"), 1)
        ]);

        private BlockModelResolver Build()
        {
            Stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new ZipAssetSource(WritePack(), "fixture"));

            return new BlockModelResolver(Stack);
        }

        private SpriteAtlas BuildAtlas()
        {
            var textures = _entries
                .Where(entry => entry.Path.StartsWith("assets/minecraft/textures/", StringComparison.Ordinal))
                .Select(entry =>
                {
                    var id = Identifier.Parse("minecraft:" + entry.Path["assets/minecraft/textures/".Length..^4]);
                    return (id, PngDecoder.Decode(entry.Content), 1);
                })
                .ToList();

            var missing = new byte[16 * 16 * 4];
            for (var i = 0; i < missing.Length; i += 4)
            {
                missing[i] = 255;
                missing[i + 2] = 255;
                missing[i + 3] = 255;
            }

            textures.Add((SpriteAtlas.MissingSprite, new RgbaImage(16, 16, missing), 1));
            return SpriteAtlasBuilder.Stitch(textures);
        }

        private string WritePack()
        {
            var path = Path.Combine(Root, "fixture.zip");
            using var stream = File.Create(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var (entryPath, content) in _entries)
            {
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }

            return path;
        }

        public void Dispose()
        {
            Stack?.Dispose();

            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Cleanup must never fail a test run.
            }
        }
    }
}
