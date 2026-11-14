// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The three buffer operations the frame's geometry and uniform components actually use.
/// </summary>
/// <remarks>
/// <see cref="IRenderBackend"/> has around fifty five members, which makes it impossible to stand in for in a
/// test without either a graphics device or a very large fake. None of the components that only move bytes
/// need more than these three, so they depend on this instead and a small fake becomes enough to test them.
/// <para>
/// The real backend implements it, so call sites pass one unchanged.
/// </para>
/// </remarks>
public interface IGeometryBackend
{
    /// <summary>Allocates a buffer.</summary>
    /// <param name="desc">Size and usage.</param>
    /// <returns>The new buffer.</returns>
    BufferHandle CreateBuffer(in BufferDesc desc);

    /// <summary>Writes bytes into a buffer at an offset.</summary>
    /// <param name="buffer">Destination.</param>
    /// <param name="data">Bytes to write.</param>
    /// <param name="offset">Byte offset within the buffer.</param>
    void WriteBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, ulong offset);

    /// <summary>Releases a buffer.</summary>
    /// <param name="buffer">Buffer to release.</param>
    void Destroy(BufferHandle buffer);
}
