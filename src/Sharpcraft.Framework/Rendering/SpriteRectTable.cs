// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The atlas rectangle table the terrain vertex stage indexes, and the storage buffer that carries it (T-832).
/// </summary>
/// <remarks>
/// A vertex stores a sixteen bit sprite index instead of the four atlas bounds, which takes fourteen bytes off every
/// vertex. This owns the other side of that trade: turning the bounds into a buffer the shader can read, and replacing it
/// when the atlas is rebuilt, because a rebuilt atlas renumbers its sprites and indices already baked into cached meshes
/// would otherwise point at the wrong ones.
/// </remarks>
internal sealed class SpriteRectTable(IRenderBackend backend)
{
    /// <summary>Entry covering the whole texture, used when no atlas is bound so the binding always has data.</summary>
    private static readonly float[] WholeAtlas = [0f, 0f, 1f, 1f];

    private BufferHandle _buffer;
    private ulong _bytes;

    /// <summary>Buffer the bind group points at.</summary>
    public BufferHandle Buffer => _buffer;

    /// <summary>Size of <see cref="Buffer"/> in bytes, which the binding has to declare.</summary>
    public ulong Bytes => _bytes;

    /// <summary>Replaces the table, releasing the previous buffer.</summary>
    /// <param name="rects">Atlas bounds as four floats per sprite.</param>
    public void Rebuild(float[] rects)
    {
        var data = rects.Length > 0 ? rects : WholeAtlas;
        var bytes = new byte[data.Length * sizeof(float)];
        MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(bytes);

        if (_bytes > 0)
        {
            backend.Destroy(_buffer);
        }

        _buffer = backend.CreateBufferInit(
            new BufferDesc((ulong)bytes.Length, BufferUsages.Storage | BufferUsages.CopyDst),
            bytes.AsSpan());
        _bytes = (ulong)bytes.Length;
    }
}
