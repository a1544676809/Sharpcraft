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
    public void YawRotatesAroundTheEntityYAxis()
    {
        var transform = new EntityModelTransform(Vector3.Zero, 90f, 0f);

        var rotated = transform.Apply(new Vector3(0f, 0f, -1f));

        Assert.InRange(rotated.X, -1.001f, -0.999f);
        Assert.InRange(rotated.Y, -0.001f, 0.001f);
        Assert.InRange(rotated.Z, -0.001f, 0.001f);
    }
}
