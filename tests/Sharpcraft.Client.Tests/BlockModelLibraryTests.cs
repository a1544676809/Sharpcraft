// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Covers the model library's job: take a block, find its model, read the boxes, and resolve every
/// face's texture variable to a real path. A block whose model is a plain cube must stay on the cube
/// path rather than resolving to an empty set.
/// </summary>
public sealed class BlockModelLibraryTests
{
    private const string TorchModel = """
        {
          "textures": { "torch": "block/torch" },
          "elements": [
            {
              "from": [7, 0, 7],
              "to": [9, 10, 9],
              "faces": { "up": { "texture": "#torch" }, "north": { "texture": "#torch" } }
            }
          ]
        }
        """;

    private static BlockModelLibrary Library(
        Dictionary<string, string> models,
        Dictionary<string, string>? modelByBlock = null)
    {
        var elements = new BlockModelElements(id =>
            models.TryGetValue(id.Path, out var json) ? Encoding.UTF8.GetBytes(json) : null);

        return new BlockModelLibrary(
            block => modelByBlock is not null && modelByBlock.TryGetValue(block.Path, out var model)
                ? Identifier.Parse("minecraft:" + model)
                : Identifier.Parse("minecraft:" + block.Path),
            elements,
            (_, variable) => variable.StartsWith('#')
                ? Identifier.Parse("minecraft:block/" + variable[1..])
                : null);
    }

    /// <summary>A block with element geometry resolves to textured quads.</summary>
    [Fact]
    public void AnElementModelResolvesToTexturedQuads()
    {
        var library = Library(new() { ["torch"] = TorchModel });

        var quads = library.Resolve(Identifier.Parse("minecraft:torch"));

        Assert.NotNull(quads);
        Assert.Equal(2, quads!.Length);
        Assert.All(quads, quad => Assert.Equal(new Identifier("minecraft", "block/torch"), quad.Texture));
        Assert.Equal(1, library.ElementModelCount);
    }

    /// <summary>A plain cube block resolves to null so it stays on the cube path.</summary>
    [Fact]
    public void APlainCubeStaysOnTheCubePath()
    {
        var library = Library(new() { ["stone"] = """{"parent": "block/cube_all"}""" });

        Assert.Null(library.Resolve(Identifier.Parse("minecraft:stone")));
        Assert.Equal(0, library.ElementModelCount);
    }

    /// <summary>A block whose state names a different model follows that name.</summary>
    [Fact]
    public void TheBlockStateModelIsFollowed()
    {
        var library = Library(
            new() { ["wall_torch"] = TorchModel },
            new() { ["torch"] = "wall_torch" });

        var quads = library.Resolve(Identifier.Parse("minecraft:torch"));

        Assert.NotNull(quads);
        Assert.Equal(2, quads!.Length);
    }

    /// <summary>A face whose texture cannot be resolved is dropped rather than drawn unsampled.</summary>
    [Fact]
    public void AnUnresolvableTextureDropsTheFace()
    {
        var model = """
            {
              "elements": [
                { "from": [0, 0, 0], "to": [16, 16, 16], "faces": { "up": { "texture": "notavariable" } } }
              ]
            }
            """;
        var library = Library(new() { ["odd"] = model });

        Assert.Null(library.Resolve(Identifier.Parse("minecraft:odd")));
    }

    /// <summary>The result is cached, so a second lookup does not count the block again.</summary>
    [Fact]
    public void TheResultIsCached()
    {
        var library = Library(new() { ["torch"] = TorchModel });
        var block = Identifier.Parse("minecraft:torch");

        var first = library.Resolve(block);
        var second = library.Resolve(block);

        Assert.Same(first, second);
        Assert.Equal(1, library.ElementModelCount);
    }
}
