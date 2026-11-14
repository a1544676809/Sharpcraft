// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Covers the two halves of the plain-cube rule: a face's texture rotation must reach the geometry,
/// and a full-size box that is specialised in any way must not be mistaken for a plain cube.
/// </summary>
public sealed class FaceRotationAndPlainCubeTests
{
    private static BlockModelLibrary Library(Dictionary<string, string> models) =>
        new(
            block => Identifier.Parse("minecraft:" + block.Path),
            new BlockModelElements(id =>
                models.TryGetValue(id.Path, out var json) ? Encoding.UTF8.GetBytes(json) : null),
            (_, variable) => variable.StartsWith('#')
                ? Identifier.Parse("minecraft:" + variable[1..])
                : null);

    private const string PlainCubeModel = """
        {
          "textures": { "all": "block/stone" },
          "elements": [
            {
              "from": [0, 0, 0],
              "to": [16, 16, 16],
              "faces": {
                "down":  { "texture": "#all", "cullface": "down" },
                "up":    { "texture": "#all", "cullface": "up" },
                "north": { "texture": "#all", "cullface": "north" },
                "south": { "texture": "#all", "cullface": "south" },
                "west":  { "texture": "#all", "cullface": "west" },
                "east":  { "texture": "#all", "cullface": "east" }
              }
            }
          ]
        }
        """;

    /// <summary>A face rotation is read from the model.</summary>
    [Fact]
    public void AFaceRotationIsRead()
    {
        var model = PlainCubeModel.Replace(
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\" }",
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\", \"rotation\": 90 }");

        var elements = new BlockModelElements(id =>
            id.Path == "rotated" ? Encoding.UTF8.GetBytes(model) : null).Resolve(Identifier.Parse("minecraft:rotated"));

        Assert.NotNull(elements);
        Assert.Equal(90, elements![0].Faces["up"].Rotation);
        Assert.Equal(0, elements[0].Faces["down"].Rotation);
    }

    /// <summary>A quarter turn advances which corner of the rectangle each vertex reads.</summary>
    [Fact]
    public void AQuarterTurnAdvancesTheUvCorner()
    {
        var element = new ModelElement(
            new ModelBox(0, 0, 0, 16, 16, 16),
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["up"] = new ModelFace("#all", [0f, 0f, 16f, 16f], null, -1, Rotation: 90)
            },
            null);

        var quad = BlockModelGeometry.Build([element]).Single();

        // Vertex 0 now reads corner 1, which is the top-left of the rectangle.
        Assert.Equal(new System.Numerics.Vector2(0f, 1f), quad.V0.Uv);
        Assert.Equal(new System.Numerics.Vector2(1f, 1f), quad.V1.Uv);
    }

    /// <summary>An ordinary full cube stays on the fast path.</summary>
    [Fact]
    public void APlainCubeIsNotAnElementModel()
    {
        Assert.Null(Library(new() { ["stone"] = PlainCubeModel }).Resolve(Identifier.Parse("minecraft:stone")));
    }

    /// <summary>A full cube with a face rotation must take the element path, or the rotation is lost.</summary>
    [Fact]
    public void ARotatedFaceTakesTheElementPath()
    {
        var model = PlainCubeModel.Replace(
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\" }",
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\", \"rotation\": 180 }");

        var quads = Library(new() { ["odd"] = model }).Resolve(Identifier.Parse("minecraft:odd"));

        Assert.NotNull(quads);
        Assert.Equal(6, quads!.Length);
    }

    /// <summary>A full cube with an explicit rectangle must take the element path.</summary>
    [Fact]
    public void AnExplicitRectangleTakesTheElementPath()
    {
        var model = PlainCubeModel.Replace(
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\" }",
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\", \"uv\": [0, 8, 8, 16] }");

        Assert.NotNull(Library(new() { ["odd"] = model }).Resolve(Identifier.Parse("minecraft:odd")));
    }

    /// <summary>A full cube with a tint slot must take the element path.</summary>
    [Fact]
    public void ATintedFaceTakesTheElementPath()
    {
        var model = PlainCubeModel.Replace(
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\" }",
            "\"up\":    { \"texture\": \"#all\", \"cullface\": \"up\", \"tintindex\": 0 }");

        Assert.NotNull(Library(new() { ["odd"] = model }).Resolve(Identifier.Parse("minecraft:odd")));
    }

    /// <summary>A full cube whose element is rotated must take the element path.</summary>
    [Fact]
    public void ARotatedElementTakesTheElementPath()
    {
        var model = PlainCubeModel.Replace(
            "\"from\": [0, 0, 0],",
            "\"rotation\": { \"angle\": 45, \"axis\": \"y\", \"origin\": [8, 8, 8] },\n              \"from\": [0, 0, 0],");

        Assert.NotNull(Library(new() { ["odd"] = model }).Resolve(Identifier.Parse("minecraft:odd")));
    }
}
