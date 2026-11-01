// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>The separable 7-tap gaussian weights used for biome blending (docs/modules/weather-and-sky-color.md 6.2/§9).</summary>
public sealed class GaussianWeightsTests
{
    [Fact]
    public void CentreWeightIsTheMiddleTapCubed()
    {
        Assert.Equal(216, GaussianWeights.Weight(0, 0, 0));
    }

    [Fact]
    public void TheWholeCubeSumsToTheTotalWeight()
    {
        var sum = 0;
        for (var dz = -GaussianWeights.Radius; dz <= GaussianWeights.Radius; dz++)
        {
            for (var dy = -GaussianWeights.Radius; dy <= GaussianWeights.Radius; dy++)
            {
                for (var dx = -GaussianWeights.Radius; dx <= GaussianWeights.Radius; dx++)
                {
                    sum += GaussianWeights.Weight(dx, dy, dz);
                }
            }
        }

        Assert.Equal(GaussianWeights.TotalWeight, sum);
    }

    [Fact]
    public void WeightsAreSymmetricAndZeroOutsideTheKernel()
    {
        Assert.Equal(GaussianWeights.Weight(1, 0, 0), GaussianWeights.Weight(-1, 0, 0));
        Assert.Equal(144, GaussianWeights.Weight(1, 0, 0));
        Assert.Equal(0, GaussianWeights.Weight(4, 0, 0));
    }
}
