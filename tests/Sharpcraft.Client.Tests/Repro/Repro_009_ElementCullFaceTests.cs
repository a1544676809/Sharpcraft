// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 9: a model face that declares <c>cullface</c> must not be emitted when a solid neighbour
/// covers that direction, otherwise the element path draws triangles that can never be seen.
/// </summary>
public sealed class Repro_009_ElementCullFaceTests
{
    private static readonly Identifier Texture = Identifier.Parse("minecraft:block/stone");

    private static TexturedQuad[] QuadsWithCullFace(string? cullFace)
    {
        var element = new ModelElement(
            new ModelBox(0, 0, 0, 16, 16, 16),
            new Dictionary<string, ModelFace>(StringComparer.Ordinal)
            {
                ["north"] = new ModelFace("#all", null, cullFace, -1)
            },
            null);

        return BlockModelGeometry.Build([element])
            .Select(quad => new TexturedQuad(quad, Texture))
            .ToArray();
    }

    private static SpriteRect Sprite(Identifier _) => new(0, 0, 16, 16, 1);

    [Fact]
    public void TheCullFaceSurvivesTheGeometryBuild()
    {
        var quads = QuadsWithCullFace("north");

        Assert.Equal("north", Assert.Single(quads).Quad.CullFace);
    }

    [Fact]
    public void ACoveredFaceIsNotEmitted()
    {
        using var buffer = new MeshBuffer();

        var emitted = BlockModelSet.Emit(
            buffer,
            QuadsWithCullFace("north"),
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 0f),
            spriteFor: Sprite,
            isCulled: direction => direction == "north");

        Assert.Equal(0, emitted);
        Assert.Equal(0, buffer.VertexCount);
    }

    [Fact]
    public void AnUncoveredFaceIsEmitted()
    {
        using var buffer = new MeshBuffer();

        var emitted = BlockModelSet.Emit(
            buffer,
            QuadsWithCullFace("north"),
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 0f),
            spriteFor: Sprite,
            isCulled: _ => false);

        Assert.Equal(1, emitted);
        Assert.Equal(4, buffer.VertexCount);
    }

    [Fact]
    public void AFaceWithoutACullFaceIsAlwaysEmitted()
    {
        using var buffer = new MeshBuffer();

        var emitted = BlockModelSet.Emit(
            buffer,
            QuadsWithCullFace(cullFace: null),
            x: 0,
            y: 0,
            z: 0,
            light: (1f, 0f),
            spriteFor: Sprite,
            isCulled: _ => true);

        Assert.Equal(1, emitted);
    }
}
