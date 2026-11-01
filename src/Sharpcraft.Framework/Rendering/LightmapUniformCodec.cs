// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Encodes <see cref="LightmapUniform"/> into the 64-byte block the terrain shader reads (docs/decisions/0019).</summary>
public static class LightmapUniformCodec
{
    /// <summary>Bytes in one lightmap uniform block.</summary>
    public const int Bytes = 64;

    /// <summary>Writes the lightmap uniform into <paramref name="destination"/> (at least 64 bytes).</summary>
    public static void Write(LightmapUniform lightmap, System.Span<byte> destination)
    {
        if (destination.Length < Bytes)
        {
            throw new ArgumentException("lightmap uniform needs 64 bytes", nameof(destination));
        }

        var floats = new float[16];
        floats[0] = lightmap.SkyFactor;
        floats[1] = lightmap.BlockFactor;
        floats[2] = lightmap.BrightnessFactor;
        floats[3] = lightmap.Padding;
        floats[4] = lightmap.SkyLightColor.X;
        floats[5] = lightmap.SkyLightColor.Y;
        floats[6] = lightmap.SkyLightColor.Z;
        floats[7] = lightmap.SkyLightColor.W;
        floats[8] = lightmap.BlockLightTint.X;
        floats[9] = lightmap.BlockLightTint.Y;
        floats[10] = lightmap.BlockLightTint.Z;
        floats[11] = lightmap.BlockLightTint.W;
        floats[12] = lightmap.AmbientColor.X;
        floats[13] = lightmap.AmbientColor.Y;
        floats[14] = lightmap.AmbientColor.Z;
        floats[15] = lightmap.AmbientColor.W;
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(floats.AsSpan()).CopyTo(destination);
    }
}
