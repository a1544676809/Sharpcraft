// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class EntityModelTransformTests
{
    [Fact]
    public void TranslationMovesTheEntityOrigin()
    {
        var transform = new EntityModelTransform(new Vector3(10f, 64f, -3f), 0f, 0f);

        Assert.Equal(new Vector3(10f, 64f, -3f), transform.Apply(Vector3.Zero));
    }

    [Fact]
    public void ScaleAppliesBeforeTranslation()
    {
        var transform = new EntityModelTransform(new Vector3(10f, 0f, 0f), 0f, 0f, 2f);

        Assert.Equal(new Vector3(12f, 0f, 0f), transform.Apply(new Vector3(1f, 0f, 0f)));
    }

    [Fact]
    public void CubeSizeScalesTheWholeModel()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, 120, 0, 0, 0, 0, 0, CubeSize: 4);
        var transform = EntityModelTransform.From(state);

        Assert.Equal(new Vector3(4f, 4f, 4f), transform.Apply(new Vector3(1f, 1f, 1f)));
    }

    [Fact]
    public void SquishWidensAndShortensTheModel()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, 120, 0, 0, 0, 0, 0, Squish: 1f, CubeSize: 1);
        var transform = EntityModelTransform.From(state);

        // size 1, ss = 1 / 1.5 = 0.6667, w = 0.6 -> x/z 0.6, y 1.6667.
        var result = transform.Apply(new Vector3(1f, 1f, 1f));
        Assert.Equal(0.6f, result.X, 4);
        Assert.Equal(1f / 0.6f, result.Y, 4);
        Assert.Equal(0.6f, result.Z, 4);
    }

    [Fact]
    public void AttachFaceRotatesAroundTheBlockPivot()
    {
        // Attached to the up face -> reference uses the opposite (down) rotation, i.e. 180 degrees around X at y=0.5.
        var state = new Sharpcraft.Client.World.EntityRenderState(1, 115, 0, 0, 0, 0, 0, AttachFace: 1);
        var transform = EntityModelTransform.From(state);

        Assert.Equal(new Vector3(0f, 0.5f, 0f), transform.Apply(new Vector3(0f, 0.5f, 0f)));
        var top = transform.Apply(new Vector3(0f, 1f, 0f));
        Assert.Equal(0f, top.Y, 4);
    }

    [Fact]
    public void NoAttachFaceLeavesTheModelUnchanged()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, 120, 0, 0, 0, 0, 0);
        var transform = EntityModelTransform.From(state);

        Assert.Equal(new Vector3(0f, 1f, 0f), transform.Apply(new Vector3(0f, 1f, 0f)));
    }

    [Fact]
    public void ScaleAttributeMultipliesTheModel()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, 159, 0, 0, 0, 0, 0, Scale: 2f);
        var transform = EntityModelTransform.From(state);
        Assert.Equal(new Vector3(2f, 2f, 2f), transform.Apply(new Vector3(1f, 1f, 1f)));
    }

    [Fact]
    public void YawRotatesAroundTheEntityYAxis()
    {
        var transform = new EntityModelTransform(Vector3.Zero, 90f, 0f);

        var rotated = transform.Apply(new Vector3(0f, 0f, -1f));

        Assert.InRange(rotated.X, -1.001f, -0.999f);
        Assert.InRange(rotated.Y, -0.001f, 0.001f);
        Assert.InRange(rotated.Z, -0.001f, 0.001f);
    }
}
