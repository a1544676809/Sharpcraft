// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Login-phase crypto: the server hash, RSA key transport and the AES/CFB8 stream
/// (docs/specs/network/encryption.md).
/// </summary>
public static class MinecraftLoginCrypto
{
    /// <summary>Length of the shared secret in bytes (AES-128).</summary>
    public const int SharedSecretLength = 16;

    /// <summary>Creates a random 128-bit shared secret.</summary>
    public static byte[] GenerateSharedSecret() => RandomNumberGenerator.GetBytes(SharedSecretLength);

    /// <summary>
    /// Computes the server hash: SHA-1 over <c>serverId | sharedSecret | publicKey</c>, rendered
    /// as a <b>signed</b> two's complement hex string like Java's <c>BigInteger.toString(16)</c>.
    /// </summary>
    /// <param name="serverId">Server id string, hashed as ISO-8859-1 bytes.</param>
    /// <param name="sharedSecret">Raw shared secret.</param>
    /// <param name="publicKey">DER-encoded public key exactly as the server sent it.</param>
    public static string ServerHash(string serverId, ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> publicKey)
    {
        ArgumentNullException.ThrowIfNull(serverId);

        var serverIdBytes = Encoding.Latin1.GetBytes(serverId);
        var digest = SHA1.HashData([.. serverIdBytes, .. sharedSecret, .. publicKey]);
        return FormatSignedHex(digest);
    }

    /// <summary>
    /// Formats bytes as Java's <c>BigInteger.toString(16)</c> would: lowercase, no leading zeros,
    /// and a leading <c>'-'</c> with a two's complement magnitude when the high bit is set.
    /// </summary>
    public static string FormatSignedHex(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return "0";
        }

        var negative = (value[0] & 0x80) != 0;
        Span<byte> magnitude = value.Length <= 64 ? stackalloc byte[value.Length] : new byte[value.Length];
        value.CopyTo(magnitude);

        if (negative)
        {
            // Two's complement negation over the whole width.
            var carry = 1;
            for (var i = magnitude.Length - 1; i >= 0; i--)
            {
                var sum = (~magnitude[i] & 0xFF) + carry;
                magnitude[i] = (byte)sum;
                carry = sum >> 8;
            }
        }

        var text = Convert.ToHexStringLower(magnitude).TrimStart('0');
        if (text.Length == 0)
        {
            text = "0";
        }

        return negative ? "-" + text : text;
    }

    /// <summary>Encrypts data with an RSA public key (DER SubjectPublicKeyInfo) using PKCS#1 v1.5.</summary>
    /// <exception cref="CryptographicException">The key is not a valid RSA public key.</exception>
    public static byte[] EncryptRsa(ReadOnlySpan<byte> publicKeyDer, ReadOnlySpan<byte> data)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKeyDer, out var read);
        if (read != publicKeyDer.Length)
        {
            throw new CryptographicException($"trailing bytes after RSA public key ({publicKeyDer.Length - read})");
        }

        return rsa.Encrypt(data.ToArray(), RSAEncryptionPadding.Pkcs1);
    }

    /// <summary>Formats a byte array as lowercase hex, for diagnostics and logs.</summary>
    public static string ToHex(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(value);

    /// <summary>Parses lowercase or uppercase hex.</summary>
    public static byte[] FromHex(string value) =>
        Convert.FromHexString(value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>Parses a port-like number with invariant culture, for tests and diagnostics.</summary>
    internal static int ParseInvariant(string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
