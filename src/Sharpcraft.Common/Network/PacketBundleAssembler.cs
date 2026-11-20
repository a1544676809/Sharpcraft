// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Buffers sub-packets between <c>bundle_delimiter</c> (id 0) markers and dispatches them
/// atomically in arrival order, matching vanilla <c>BundlerInfo.BUNDLE_SIZE_LIMIT</c> (4096).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.network.protocol.BundlerInfo", "ref://mc-26.3-analysis/src/net/minecraft/network/protocol/BundlerInfo.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public sealed class PacketBundleAssembler
{
    /// <summary>Upper bound on packets inside a single bundle, matching vanilla <c>BundlerInfo.BUNDLE_SIZE_LIMIT</c>.</summary>
    public const int MaxBundlePackets = 4096;

    private readonly List<byte[]> _buffer = new();
    private bool _inBundle;

    /// <summary>True while inside an open <c>bundle_delimiter</c> pair.</summary>
    private readonly int _delimiterPacketId;

    /// <summary>Uses the play clientbound bundle delimiter the session dispatches on.</summary>
    public PacketBundleAssembler(int delimiterPacketId) => _delimiterPacketId = delimiterPacketId;

    public bool InBundle => _inBundle;

    /// <summary>Number of sub-packets currently buffered waiting for the closing delimiter.</summary>
    public int BufferedCount => _buffer.Count;

    /// <summary>
    /// Processes one inbound packet body. Delimiters themselves are never buffered or dispatched.
    /// If an unclosed bundle reaches <see cref="MaxBundlePackets"/>, buffered packets are flushed
    /// in order so memory cannot grow unboundedly and no packet is dropped.
    /// </summary>
    public void Process(byte[] body, Action<byte[]> dispatch)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(dispatch);

        var peekReader = new PayloadReader(body);
        if (peekReader.TryReadVarInt(out var packetId) == DecodeStatus.Success &&
            packetId == _delimiterPacketId)
        {
            if (_inBundle)
            {
                FlushBuffer(dispatch);
            }
            else
            {
                _inBundle = true;
            }

            return;
        }

        if (!_inBundle)
        {
            dispatch(body);
            return;
        }

        if (_buffer.Count >= MaxBundlePackets)
        {
            FlushBuffer(dispatch);
            dispatch(body);
            return;
        }

        _buffer.Add(body);
    }

    private void FlushBuffer(Action<byte[]> dispatch)
    {
        _inBundle = false;
        try
        {
            foreach (var bundled in _buffer)
            {
                dispatch(bundled);
            }
        }
        finally
        {
            _buffer.Clear();
        }
    }
}
