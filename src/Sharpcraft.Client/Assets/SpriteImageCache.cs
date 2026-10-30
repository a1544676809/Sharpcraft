// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Reuses decoded sprite images whose bytes have not changed.
/// </summary>
/// <remarks>
/// Decoding every PNG on every atlas rebuild costs about half a second on a full pack, and a reload usually changes one
/// file. Content hashes decide what to decode, which is cheap because the whole pack is only a couple of megabytes.
/// The cache holds <em>decoded</em> pixels, so it is capped: at 256x a full pack would be hundreds of megabytes and the
/// cache is dropped instead of growing without bound.
/// </remarks>
public sealed class SpriteImageCache
{
    private const long MaxCachedBytes = 64L * 1024 * 1024;

    private readonly Dictionary<Identifier, (byte[] Hash, RgbaImage Image)> _entries = [];
    private readonly Lock _gate = new();
    private long _cachedBytes;

    /// <summary>How many images are cached.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Returns the decoded image for these bytes, decoding only when the content changed.
    /// </summary>
    /// <param name="location">Sprite identifier.</param>
    /// <param name="bytes">File contents.</param>
    /// <param name="decode">Decoder to call when the bytes are new or changed.</param>
    public RgbaImage GetOrDecode(Identifier location, byte[] bytes, Func<byte[], RgbaImage> decode)
    {
        ArgumentNullException.ThrowIfNull(decode);

        var hash = System.Security.Cryptography.SHA256.HashData(bytes);

        lock (_gate)
        {
            if (_entries.TryGetValue(location, out var cached) && cached.Hash.AsSpan().SequenceEqual(hash))
            {
                return cached.Image;
            }
        }

        var image = decode(bytes);
        var size = (long)image.Pixels.Length;

        lock (_gate)
        {
            if (_entries.TryGetValue(location, out var previous))
            {
                _cachedBytes -= previous.Image.Pixels.Length;
            }

            if (_cachedBytes + size > MaxCachedBytes)
            {
                // A high resolution pack can dwarf the cap; dropping everything beats holding hundreds of megabytes.
                _entries.Clear();
                _cachedBytes = 0;
                return image;
            }

            _entries[location] = (hash, image);
            _cachedBytes += size;
        }

        return image;
    }
}
