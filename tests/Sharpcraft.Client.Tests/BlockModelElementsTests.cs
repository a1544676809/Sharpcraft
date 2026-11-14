// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Covers the block model element reader's shape rules: elements come from the first model in the
/// parent chain that declares them, faces carry their texture variable, explicit UV and cull face,
/// and a model with no elements anywhere is a plain cube.
/// </summary>
public sealed class BlockModelElementsTests
{
    private static BlockModelElements Reader(Dictionary<string, string> models) =>
        new(id => models.TryGetValue(id.Path, out var json) ? Encoding.UTF8.GetBytes(json) : null);

    private const string TorchLike = """
        {
          "textures": { "torch": "block/torch" },
          "elements": [
            {
              "from": [7, 0, 7],
              "to": [9, 10, 9],
              "faces": {
                "up": { "texture": "#torch", "uv": [7, 6, 9, 8] },
                "north": { "texture": "#torch", "cullface": "north" }
              }
            }
          ]
        }
        """;

    private const string CubeAll = """
        {
          "parent": "block/cube_all",
          "textures": { "all": "block/stone" }
        }
        """;

    /// <summary>One box comes back with its corners in model units.</summary>
    [Fact]
    public void ABoxIsReadWithItsCorners()
    {
        var elements = Reader(new() { ["torch"] = TorchLike }).Resolve(Identifier.Parse("minecraft:torch"));

        Assert.NotNull(elements);
        var box = Assert.Single(elements).Box;
        Assert.Equal(7f, box.FromX);
        Assert.Equal(0f, box.FromY);
        Assert.Equal(7f, box.FromZ);
        Assert.Equal(9f, box.ToX);
        Assert.Equal(10f, box.ToY);
        Assert.Equal(9f, box.ToZ);
    }

    /// <summary>Faces keep their texture variable, explicit UV, cull face and tint slot.</summary>
    [Fact]
    public void FacesKeepTheirDetails()
    {
        var elements = Reader(new() { ["torch"] = TorchLike }).Resolve(Identifier.Parse("minecraft:torch"));

        Assert.NotNull(elements);
        var faces = Assert.Single(elements).Faces;
        Assert.Equal(2, faces.Count);

        Assert.Equal("#torch", faces["up"].Texture);
        var uv = faces["up"].Uv;
        Assert.NotNull(uv);
        Assert.Equal([7f, 6f, 9f, 8f], uv);
        Assert.Null(faces["up"].CullFace);
        Assert.Equal(-1, faces["up"].TintIndex);

        Assert.Equal("north", faces["north"].CullFace);
        Assert.Null(faces["north"].Uv);
    }

    /// <summary>A model without elements takes its parent's.</summary>
    [Fact]
    public void ElementsAreInheritedFromAParent()
    {
        var models = new Dictionary<string, string> { ["child"] = CubeAll, ["block/cube_all"] = TorchLike };

        var elements = Reader(models).Resolve(Identifier.Parse("minecraft:child"));

        Assert.NotNull(elements);
        Assert.Single(elements);
    }

    /// <summary>A model that declares its own elements does not inherit the parent's.</summary>
    [Fact]
    public void OwnElementsReplaceTheParents()
    {
        var own = """
            { "elements": [ { "from": [0, 0, 0], "to": [16, 16, 16], "faces": { "up": { "texture": "#all" } } } ] }
            """;
        var models = new Dictionary<string, string> { ["child"] = own, ["block/cube_all"] = TorchLike };

        var elements = Reader(models).Resolve(Identifier.Parse("minecraft:child"));

        Assert.NotNull(elements);
        var box = Assert.Single(elements).Box;
        Assert.Equal(16f, box.ToY);
    }

    /// <summary>A model chain that never declares elements is a plain cube and resolves to null.</summary>
    [Fact]
    public void APlainCubeResolvesToNothing()
    {
        Assert.Null(Reader(new() { ["stone"] = CubeAll }).Resolve(Identifier.Parse("minecraft:stone")));
    }

    /// <summary>Rotation is read when the model carries one.</summary>
    [Fact]
    public void RotationIsRead()
    {
        var rotated = """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "rotation": { "angle": 45, "axis": "y", "origin": [8, 8, 8], "rescale": true },
                  "faces": { "up": { "texture": "#all" } }
                }
              ]
            }
            """;

        var elements = Reader(new() { ["rotated"] = rotated }).Resolve(Identifier.Parse("minecraft:rotated"));

        Assert.NotNull(elements);
        var rotation = Assert.Single(elements).Rotation;
        Assert.NotNull(rotation);
        Assert.Equal(45f, rotation.Value.Angle);
        Assert.Equal("y", rotation.Value.Axis);
        Assert.Equal(8f, rotation.Value.OriginY);
        Assert.True(rotation.Value.Rescale);
    }

    /// <summary>An element without both corners is skipped rather than throwing.</summary>
    [Fact]
    public void AMalformedElementIsSkipped()
    {
        var malformed = """
            { "elements": [ { "from": [0, 0, 0] }, { "from": [1, 1, 1], "to": [2, 2, 2], "faces": {} } ] }
            """;

        var elements = Reader(new() { ["bad"] = malformed }).Resolve(Identifier.Parse("minecraft:bad"));

        Assert.NotNull(elements);
        Assert.Single(elements);
    }
}
