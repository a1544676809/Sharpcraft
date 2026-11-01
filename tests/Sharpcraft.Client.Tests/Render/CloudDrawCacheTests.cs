// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>CloudDrawCache rebuilds only when the centre cell or camera side changes (docs/modules/clouds.md).</summary>
public sealed class CloudDrawCacheTests
{
    [Fact]
    public void ReusesGeometryForTheSameCell()
    {
        var cells = new long[5 * 5];
        cells[2 + (2 * 5)] = CloudMeshBuilder.PackCell(unchecked((int)0xFFFFFFFF), true, true, true, true);
        var cache = new CloudDrawCache();

        var first = cache.Get(cells, 5, 5, 2, 2, 1, 100f, CloudCameraSide.BelowClouds);
        var second = cache.Get(cells, 5, 5, 2, 2, 1, 100f, CloudCameraSide.BelowClouds);

        Assert.Same(first.Vertices, second.Vertices);
        Assert.Same(first.Indices, second.Indices);

        var moved = cache.Get(cells, 5, 5, 3, 2, 1, 100f, CloudCameraSide.BelowClouds);
        Assert.NotSame(first.Vertices, moved.Vertices);
    }
}
