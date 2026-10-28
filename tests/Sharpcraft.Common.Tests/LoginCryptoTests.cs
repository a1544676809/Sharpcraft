// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using System.Text;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Login crypto tests validated against vectors captured from the reference JVM
/// (docs/specs/network/encryption.md).
/// </summary>
public sealed class LoginCryptoTests
{
    // Vectors from the Java harness: SHA-1 digest rendered by BigInteger.toString(16).
    [Theory]
    [InlineData("00", "5ba93c9db0cff93f52b521d7420e43f6eda2784f")]
    [InlineData("80", "-387142c37a5c65a69260a302d472dbf43e463edb")]
    [InlineData("ff01", "7dd9b9b6f1fe30c650ee7988643e98436c23da")]
    [InlineData("4e6f746368", "4ed1f46bbe04bc756bcb17c0c7ce3e4632f06a48")]
    public void ServerHashFormattingMatchesJavaBigInteger(string inputHex, string expectedText)
    {
        var input = Convert.FromHexString(inputHex);
        var digest = SHA1.HashData(input);

        Assert.Equal(expectedText, MinecraftLoginCrypto.FormatSignedHex(digest));
    }

    [Fact]
    public void ServerHashUsesIso88591ForTheServerId()
    {
        // "Notch" is ASCII, so ISO-8859-1 and ASCII agree; the contract is the digest itself.
        var secret = new byte[16];
        var publicKey = new byte[162];
        var expected = SHA1.HashData([.. "Notch"u8.ToArray(), .. secret, .. publicKey]);

        Assert.Equal(
            MinecraftLoginCrypto.FormatSignedHex(expected),
            MinecraftLoginCrypto.ServerHash("Notch", secret, publicKey));
    }

    [Fact]
    public void EmptyValueFormatsAsZero()
    {
        Assert.Equal("0", MinecraftLoginCrypto.FormatSignedHex([]));
        Assert.Equal("0", MinecraftLoginCrypto.FormatSignedHex([0x00, 0x00]));
    }

    [Fact]
    public void NegativeMagnitudeUsesTwosComplement()
    {
        // 0x80 as a signed 8-bit value is -128; its magnitude is 0x80 in two's complement.
        Assert.Equal("-80", MinecraftLoginCrypto.FormatSignedHex([0x80]));

        // 0xFF is -1: magnitude 0x01.
        Assert.Equal("-1", MinecraftLoginCrypto.FormatSignedHex([0xFF]));

        // 0xFF,0xFF is -1 as well, once the leading zeroes are trimmed.
        Assert.Equal("-1", MinecraftLoginCrypto.FormatSignedHex([0xFF, 0xFF]));
    }

    [Fact]
    public void AesCfb8MatchesTheReferenceCipher()
    {
        var key = Convert.FromHexString("030e19242f3a45505b66717c87929da8");
        var plain = Encoding.ASCII.GetBytes("The quick brown fox jumps over 13 lazy dogs.");
        const string expectedCipher = "3ac2e69b42d439d9879083e537d000ca93243243933cc501476ae674845b599eb09b676a977ed62222f21576";

        using var encryptor = new AesCfb8(key, decrypt: false);
        var cipher = new byte[plain.Length];
        encryptor.Transform(plain, cipher);

        Assert.Equal(expectedCipher, Convert.ToHexStringLower(cipher));
    }

    [Fact]
    public void AesCfb8DecryptsTheReferenceCipher()
    {
        var key = Convert.FromHexString("030e19242f3a45505b66717c87929da8");
        var cipher = Convert.FromHexString("3ac2e69b42d439d9879083e537d000ca93243243933cc501476ae674845b599eb09b676a977ed62222f21576");

        using var decryptor = new AesCfb8(key, decrypt: true);
        var plain = new byte[cipher.Length];
        decryptor.Transform(cipher, plain);

        Assert.Equal("The quick brown fox jumps over 13 lazy dogs.", Encoding.ASCII.GetString(plain));
    }

    [Fact]
    public void AesCfb8IsContinuousAcrossSplits()
    {
        var key = Convert.FromHexString("030e19242f3a45505b66717c87929da8");
        var plain = Encoding.ASCII.GetBytes("The quick brown fox jumps over 13 lazy dogs.");

        using var whole = new AesCfb8(key, decrypt: false);
        var wholeCipher = new byte[plain.Length];
        whole.Transform(plain, wholeCipher);

        // Feeding the same data in two chunks must produce the same stream: the register is
        // stateful, so splitting a packet boundary cannot reset it.
        using var split = new AesCfb8(key, decrypt: false);
        var splitCipher = new byte[plain.Length];
        split.Transform(plain.AsSpan(0, 10), splitCipher);
        split.Transform(plain.AsSpan(10), splitCipher.AsSpan(10));

        Assert.Equal(Convert.ToHexStringLower(wholeCipher), Convert.ToHexStringLower(splitCipher));
    }

    [Fact]
    public void AesCfb8EmptyInputProducesNoBytes()
    {
        var key = Convert.FromHexString("030e19242f3a45505b66717c87929da8");

        using var encryptor = new AesCfb8(key, decrypt: false);
        Span<byte> buffer = stackalloc byte[1];

        Assert.Equal(0, encryptor.Transform([], buffer));
    }

    [Fact]
    public void RsaEncryptionRoundTripsThroughThePrivateKey()
    {
        using var rsa = RSA.Create(1024);
        var publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
        var secret = MinecraftLoginCrypto.GenerateSharedSecret();

        var encrypted = MinecraftLoginCrypto.EncryptRsa(publicKeyDer, secret);
        var decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);

        Assert.Equal(16, secret.Length);
        Assert.Equal(128, encrypted.Length);  // 1024-bit modulus
        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void SharedSecretIsRandomEveryTime()
    {
        var first = MinecraftLoginCrypto.GenerateSharedSecret();
        var second = MinecraftLoginCrypto.GenerateSharedSecret();

        Assert.Equal(MinecraftLoginCrypto.SharedSecretLength, first.Length);
        Assert.NotEqual(first, second);
    }
}
