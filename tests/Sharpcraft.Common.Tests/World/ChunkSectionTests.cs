// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Single state sections (docs/specs/render/occlusion-culling.md O3.2b-2b): a filled section must not claim to be empty, or
/// the mesher skips it and the scene renders as nothing.
/// </summary>
public sealed class ChunkSectionTests
{
    [Fact]
    public void AFilledSectionIsNotEmpty()
    {
        var section = new ChunkSection(singleState: 1, sectionY: 0);

        Assert.False(section.IsEmpty);
        Assert.Equal(ChunkSection.Volume, section.NonAirCount);
    }

    [Fact]
    public void ASectionOfAirIsEmpty()
    {
        var section = new ChunkSection(singleState: 0, sectionY: 0);

        Assert.True(section.IsEmpty);
    }

    [Fact]
    public void TheAirStateIsRespected()
    {
        var section = new ChunkSection(singleState: 7, sectionY: 0, airState: 7);

        Assert.True(section.IsEmpty);
    }
}
