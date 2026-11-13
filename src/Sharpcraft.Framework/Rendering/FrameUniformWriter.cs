// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Writes the three uniform blocks a frame depends on: the camera, the lightmap and the fog.
/// </summary>
/// <remarks>
/// All three are small and fully rewritten every frame, which is why the staging buffers they write into are
/// double buffered — recording frame N+1 must not scribble over the block frame N is still reading. Keeping the
/// writes here means that staging type has exactly one writer.
/// </remarks>
internal sealed class FrameUniformWriter
{
    private readonly IGeometryBackend _backend;
    private readonly BufferHandle _cameraBuffer;
    private readonly BufferHandle _lightmapBuffer;
    private readonly BufferHandle _fogBuffer;
    private readonly FrameUniformStaging _staging;

    /// <summary>Captures the buffers and the staging block the uniforms are written through.</summary>
    /// <param name="backend">Backend to upload through.</param>
    /// <param name="cameraBuffer">Destination of the camera block.</param>
    /// <param name="lightmapBuffer">Destination of the lightmap block.</param>
    /// <param name="fogBuffer">Destination of the fog block.</param>
    /// <param name="staging">Double buffered staging bytes.</param>
    public FrameUniformWriter(
        IGeometryBackend backend,
        BufferHandle cameraBuffer,
        BufferHandle lightmapBuffer,
        BufferHandle fogBuffer,
        FrameUniformStaging staging)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _cameraBuffer = cameraBuffer;
        _lightmapBuffer = lightmapBuffer;
        _fogBuffer = fogBuffer;
        _staging = staging ?? throw new ArgumentNullException(nameof(staging));
    }

    /// <summary>Writes the camera, lightmap and fog blocks for one frame.</summary>
    /// <param name="cameraBlock">Scratch the camera matrix is composed into.</param>
    /// <param name="eye">Where the camera is this frame.</param>
    /// <param name="target">What it looks at.</param>
    /// <param name="fieldOfViewDegrees">Vertical field of view.</param>
    /// <param name="width">Framebuffer width in pixels.</param>
    /// <param name="height">Framebuffer height in pixels.</param>
    /// <param name="lightmap">Day/night state the fragment shader samples.</param>
    /// <param name="fog">Fog colour, alpha included.</param>
    /// <param name="fogStart">Distance the fade begins at.</param>
    /// <param name="fogEnd">Distance it reaches full strength at.</param>
    public void Write(
        Span<float> cameraBlock,
        Vector3 eye,
        Vector3 target,
        float fieldOfViewDegrees,
        int width,
        int height,
        LightmapUniform lightmap,
        Vector4 fog,
        float fogStart,
        float fogEnd)
    {
        CameraUniform.Write(cameraBlock, eye, target, fieldOfViewDegrees, width, height);
        var cameraBytes = _staging.Camera;
        for (var index = 0; index < 36; index++)
        {
            BitConverter.TryWriteBytes(cameraBytes.AsSpan(index * sizeof(float)), cameraBlock[index]);
        }

        _backend.WriteBuffer(_cameraBuffer, cameraBytes, 0);

        LightmapUniformCodec.Write(lightmap, _staging.Lightmap);
        _backend.WriteBuffer(_lightmapBuffer, _staging.Lightmap, 0);

        var fogValues = _staging.FogValues;
        fogValues[0] = fog.X;
        fogValues[1] = fog.Y;
        fogValues[2] = fog.Z;
        fogValues[3] = fog.W;
        fogValues[4] = fogStart;
        fogValues[5] = fogEnd;
        System.Runtime.InteropServices.MemoryMarshal
            .AsBytes(fogValues.AsSpan())
            .CopyTo(_staging.Fog);
        _backend.WriteBuffer(_fogBuffer, _staging.Fog, 0);
    }
}
