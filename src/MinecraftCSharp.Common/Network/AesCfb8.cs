// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Security.Cryptography;

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// AES/CFB8 stream as used by the login phase: one continuous stream per connection,
/// initialised with the shared secret as both key and IV
/// (docs/specs/network/encryption.md).
/// </summary>
/// <remarks>
/// CFB8 is <b>stateful</b>: the register carries across packets, so this type must live for
/// the whole connection and transform data in arrival order. Treating each packet as an
/// independent encryption would silently corrupt the stream after the first packet.
/// </remarks>
public sealed class AesCfb8 : IDisposable
{
    /// <summary>Block size of AES in bytes.</summary>
    public const int BlockSize = 16;

    private readonly Aes _aes;
    private readonly byte[] _register = new byte[BlockSize];
    private readonly byte[] _keystreamBlock = new byte[BlockSize];
    private bool _disposed;

    /// <summary>Creates a stream cipher; pass <paramref name="decrypt"/> to reverse directions.</summary>
    public AesCfb8(ReadOnlySpan<byte> key, bool decrypt)
    {
        if (key.Length != BlockSize)
        {
            throw new ArgumentException($"AES-128 needs a {BlockSize}-byte key, got {key.Length}", nameof(key));
        }

        _aes = Aes.Create();
        _aes.Key = key.ToArray();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        Decrypting = decrypt;

        // Vanilla uses the key itself as the IV.
        key.CopyTo(_register);
    }

    /// <summary>True when this instance decrypts inbound bytes.</summary>
    public bool Decrypting { get; }

    /// <summary>Transforms <paramref name="source"/> into <paramref name="destination"/>.</summary>
    /// <returns>Number of bytes written (always the source length).</returns>
    public int Transform(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (destination.Length < source.Length)
        {
            throw new ArgumentException("destination too small", nameof(destination));
        }

        for (var i = 0; i < source.Length; i++)
        {
            _aes.EncryptEcb(_register, _keystreamBlock, PaddingMode.None);
            var keystream = _keystreamBlock[0];
            var input = source[i];
            var output = (byte)(input ^ keystream);
            destination[i] = output;

            // Shift the register by one byte and feed it the ciphertext byte.
            _register.AsSpan(1).CopyTo(_register);
            _register[BlockSize - 1] = Decrypting ? input : output;
        }

        return source.Length;
    }

    /// <summary>Transforms in place.</summary>
    public void TransformInPlace(Span<byte> buffer) => Transform(buffer, buffer);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _aes.Dispose();
        CryptographicOperations.ZeroMemory(_register);
        CryptographicOperations.ZeroMemory(_keystreamBlock);
    }
}
