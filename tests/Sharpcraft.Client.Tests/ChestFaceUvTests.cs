// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the box-unwrapped face UV rectangles for single and double chests against handwritten
/// constants derived from the 64x64 chest sheet layout.
/// </summary>
public sealed class ChestFaceUvTests
{
    [Fact]
    public void SingleChestBottomFacesMatchExpectedRectangles()
    {
        Span<EntityPlaceholderMesher.FaceUvRect> buffer = stackalloc EntityPlaceholderMesher.FaceUvRect[6];
        EntityPlaceholderMesher.ComputeBoxFaceUvs(0, 19, 14, 10, 14, buffer);
        var actual = buffer.ToArray();
        EntityPlaceholderMesher.FaceUvRect[] expected =
        [
            new(28f, 33f, 42f, 43f),
            new(0f, 33f, 14f, 43f),
            new(28f, 33f, 42f, 19f),
            new(14f, 19f, 28f, 33f),
            new(42f, 33f, 56f, 43f),
            new(14f, 33f, 28f, 43f)
        ];
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 19, 14, 10, 14)]
    [InlineData(0, 0, 14, 5, 14)]
    [InlineData(0, 0, 2, 4, 1)]
    [InlineData(0, 19, 15, 10, 14)]
    [InlineData(0, 0, 15, 5, 14)]
    [InlineData(0, 0, 1, 4, 1)]
    public void BoxFaceUvsConserveAreaStayInBoundsAndAlignRows(int texU, int texV, int dx, int dy, int dz)
    {
        Span<EntityPlaceholderMesher.FaceUvRect> buffer = stackalloc EntityPlaceholderMesher.FaceUvRect[6];
        EntityPlaceholderMesher.ComputeBoxFaceUvs(texU, texV, dx, dy, dz, buffer);
        var faces = buffer.ToArray();
        var areaSum = 0f;
        foreach (var f in faces)
        {
            Assert.InRange(f.U0, 0f, 64f);
            Assert.InRange(f.U1, 0f, 64f);
            Assert.InRange(f.V0, 0f, 64f);
            Assert.InRange(f.V1, 0f, 64f);
            areaSum += MathF.Abs((f.U1 - f.U0) * (f.V1 - f.V0));
        }

        Assert.Equal(2f * ((dx * dy) + (dy * dz) + (dz * dx)), areaSum);
        Assert.Equal(faces[0].V0, faces[2].V0);
        Assert.Equal(faces[0].V0, faces[3].V1);
    }

    [Theory]
    [InlineData((byte)0, 56f / 64f)]
    [InlineData((byte)1, 58f / 64f)]
    [InlineData((byte)2, 58f / 64f)]
    public void ChestMesherEmitsUnwrappedUvsForSingleAndDoubleChests(byte chestType, float expectedMaxU)
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest, ChestType: chestType)];
        var geometry = mesher.Build(entities, new SpriteRect(0, 0, 64, 64, 1));
        var maxU = 0f;
        for (var v = 0; v < 24; v++)
        {
            var (u, _) = VertexFieldReader.Uv(geometry.Vertices, v);
            maxU = MathF.Max(maxU, u);
        }

        Assert.InRange(maxU, expectedMaxU - 1e-3f, expectedMaxU + 1e-3f);
    }
}
