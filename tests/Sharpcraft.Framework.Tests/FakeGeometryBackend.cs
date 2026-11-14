// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Stands in for the three buffer operations so the components that only move bytes can be tested without a
/// graphics device.
/// </summary>
internal sealed class FakeGeometryBackend : IGeometryBackend
{
    private readonly List<BufferHandle> _created = [];
    private readonly List<BufferHandle> _destroyed = [];
    private readonly List<ulong> _written = [];

    /// <summary>Buffers created, in order.</summary>
    public IReadOnlyList<BufferHandle> Created => _created;

    /// <summary>Buffers released, in order.</summary>
    public IReadOnlyList<BufferHandle> Destroyed => _destroyed;

    /// <summary>Byte counts of every write, in order.</summary>
    public IReadOnlyList<ulong> Written => _written;

    /// <inheritdoc />
    public BufferHandle CreateBuffer(in BufferDesc desc)
    {
        var handle = new BufferHandle(new object());
        _created.Add(handle);
        return handle;
    }

    /// <inheritdoc />
    public void WriteBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, ulong offset)
    {
        _written.Add((ulong)data.Length);
    }

    /// <inheritdoc />
    public void Destroy(BufferHandle buffer) => _destroyed.Add(buffer);
}
