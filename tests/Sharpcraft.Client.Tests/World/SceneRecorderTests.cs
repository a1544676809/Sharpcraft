// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// Scene recording (docs/milestones/midterm-goal.md, condition one): a saved scene must load back to the same world,
/// proven by the fingerprint rather than by eyeballing a picture.
/// </summary>
public sealed class SceneRecorderTests
{
    [Fact]
    public void ASavedSceneLoadsBackIdentical()
    {
        var world = new ClientWorld(0, 1);
        var chunk = new LevelChunk(3, -2, 0, 1);
        chunk.SetBlockState(1, 2, 3, 7, 0);
        chunk.SetBlockState(15, 15, 15, 9, 0);
        world.AddChunk(chunk);

        var path = Path.Combine(Path.GetTempPath(), $"scene-{Guid.NewGuid():N}.bin");

        try
        {
            SceneRecorder.Save(world, path);
            var loaded = SceneRecorder.Load(path);

            Assert.Equal(SceneFingerprint.Of(world), SceneFingerprint.Of(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ANonSceneFileIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"junk-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [0, 0, 0, 0]);

        try
        {
            Assert.Throws<InvalidOperationException>(() => SceneRecorder.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
