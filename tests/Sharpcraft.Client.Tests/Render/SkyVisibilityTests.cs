// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>The dark bottom sky disc shows only below the horizon and not underwater (SkyRenderer.shouldRenderDarkDisc).</summary>
public sealed class SkyVisibilityTests
{
    [Fact]
    public void DarkDiscRendersBelowTheHorizon()
    {
        Assert.True(SkyVisibility.ShouldRenderDarkDisc(eyeY: 50f, horizonY: 63f, underwater: false));
    }

    [Fact]
    public void NoDarkDiscAboveTheHorizon()
    {
        Assert.False(SkyVisibility.ShouldRenderDarkDisc(eyeY: 70f, horizonY: 63f, underwater: false));
    }

    [Fact]
    public void NoDarkDiscExactlyAtTheHorizon()
    {
        // The reference test is strictly less than zero.
        Assert.False(SkyVisibility.ShouldRenderDarkDisc(eyeY: 63f, horizonY: 63f, underwater: false));
    }

    [Fact]
    public void UnderwaterSuppressesTheDarkDisc()
    {
        Assert.False(SkyVisibility.ShouldRenderDarkDisc(eyeY: 50f, horizonY: 63f, underwater: true));
    }
}
