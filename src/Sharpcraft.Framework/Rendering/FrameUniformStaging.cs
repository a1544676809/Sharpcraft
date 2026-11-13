// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The uniform bytes a frame rewrites before it submits: the camera block, the lightmap block, the fog
/// block and the floats that make up the fog block.
/// </summary>
/// <remarks>
/// These were created inside the frame loop and thrown away each time — allocations per frame, for buffers
/// that are fully overwritten before they are read. Holding them here allocates once instead.
/// <para>
/// Each one has two copies and <see cref="Advance"/> alternates between them. That is what makes recording
/// a frame while the previous one is still in flight safe: without it, the second frame's writes would land
/// in a block the GPU has not read yet. Nothing records from a second thread yet, so alternating currently
/// changes nothing observable — but it is the change that has to be in place before anything does, and doing
/// it here keeps it out of a two thousand line method (T-837).
/// </para>
/// </remarks>
internal sealed class FrameUniformStaging
{
    private const int Slots = 2;

    private readonly byte[][] _camera = new byte[Slots][];
    private readonly byte[][] _lightmap = new byte[Slots][];
    private readonly byte[][] _fog = new byte[Slots][];
    private readonly float[][] _fogValues = new float[Slots][];

    private int _slot;

    /// <summary>Allocates both copies of every block.</summary>
    public FrameUniformStaging()
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            _camera[slot] = new byte[VertexLayout.CameraUniformSize];
            _lightmap[slot] = new byte[Shaders.ShaderBindings.LightmapBytes];
            _fog[slot] = new byte[Shaders.ShaderBindings.FogBytes];
            _fogValues[slot] = new float[8];
        }
    }

    /// <summary>The copy the current frame writes into.</summary>
    public int Slot => _slot;

    /// <summary>Camera uniform bytes, rewritten every frame from the eye and the projection.</summary>
    public byte[] Camera => _camera[_slot];

    /// <summary>Lightmap uniform bytes, rewritten every frame from the day/night state.</summary>
    public byte[] Lightmap => _lightmap[_slot];

    /// <summary>Fog uniform bytes, rewritten every frame from the fog colour and range.</summary>
    public byte[] Fog => _fog[_slot];

    /// <summary>The eight floats that <see cref="Fog"/> is a view of.</summary>
    public float[] FogValues => _fogValues[_slot];

    /// <summary>Moves to the other copy. Call once per frame, before anything writes.</summary>
    public void Advance() => _slot ^= 1;
}
