// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using System.Text;

namespace Sharpcraft.AuthLib;

/// <summary>One profile property exactly as it arrived on the wire.</summary>
/// <param name="Name">Property name; only <c>textures</c> is consumed by the skin pipeline.</param>
/// <param name="Value">Base64 payload.</param>
/// <param name="Signature">Base64 signature, or null when the property was unsigned.</param>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.authlib.properties.Property", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Wire property value plus optional signature; authlib is an external library, not part of the 26.3 source tree")]
public readonly record struct ProfileProperty(string Name, string Value, string? Signature = null)
{
    /// <summary>True when the property carried a signature string.</summary>
    public bool HasSignature => !string.IsNullOrEmpty(Signature);
}

/// <summary>Signature state produced by the profile property verifier.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.authlib.SignatureState", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "UNSIGNED / INVALID / SIGNED")]
public enum ProfileSignatureState
{
    /// <summary>No signature was present.</summary>
    Unsigned,

    /// <summary>A signature was present but no trusted key accepted it.</summary>
    Invalid,

    /// <summary>A trusted profile-property key accepted the signature.</summary>
    Signed
}

/// <summary>One trusted RSA profile-property key.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.authlib.services.MinecraftServicesKeyInfo", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "RSA public key wrapper; discovery/download lives in MinecraftServicesKeyClient")]
public sealed class ProfileSignatureKey : IDisposable
{
    private readonly RSA _rsa;

    internal ProfileSignatureKey(RSA rsa)
    {
        _rsa = rsa;
    }

    /// <summary>Key size in bits, for diagnostics and trust checks.</summary>
    public int KeySizeBits => _rsa.KeySize;

    /// <summary>Verifies a SHA1withRSA PKCS#1 signature over the ASCII bytes of <paramref name="value"/>.</summary>
    public bool Verify(string value, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            return _rsa.VerifyData(
                Encoding.ASCII.GetBytes(value),
                signatureBytes,
                HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _rsa.Dispose();
}

/// <summary>
/// The trusted profile-property keys and the texture URL prefixes from the Minecraft services discovery document.
/// An empty set still carries the URL policy: unsigned properties are valid for the local player, but signed ones
/// become <see cref="ProfileSignatureState.Invalid"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.authlib.services.ServicesKeySet", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Immutable key set plus allowed texture URL prefixes")]
public sealed class ProfileSignatureKeySet : IDisposable
{
    private readonly ProfileSignatureKey[] _keys;
    private readonly string[] _allowedTexturePrefixes;

    internal ProfileSignatureKeySet(IReadOnlyList<ProfileSignatureKey> keys, IReadOnlyList<string> allowedTexturePrefixes)
    {
        _keys = [.. keys];
        _allowedTexturePrefixes = [.. allowedTexturePrefixes];
    }

    /// <summary>An empty key set; signed properties are invalid and no texture URL is accepted.</summary>
    public static ProfileSignatureKeySet Empty { get; } = new([], []);

    /// <summary>Texture URL prefixes, with the discovery <c>{textureId}</c> placeholder removed.</summary>
    public IReadOnlyList<string> AllowedTexturePrefixes => _allowedTexturePrefixes;

    /// <summary>Determines whether a texture URL is in the discovery document's allowed domain/prefix set.</summary>
    public bool IsAllowedTextureUrl(string url)
    {
        foreach (var prefix in _allowedTexturePrefixes)
        {
            var probe = prefix.Replace("{textureId}", string.Empty, StringComparison.Ordinal);
            if (url.StartsWith(probe, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Validates one property against every trusted key.</summary>
    public ProfileSignatureState Validate(ProfileProperty property)
    {
        if (!property.HasSignature)
        {
            return ProfileSignatureState.Unsigned;
        }

        foreach (var key in _keys)
        {
            if (key.Verify(property.Value, property.Signature))
            {
                return ProfileSignatureState.Signed;
            }
        }

        return ProfileSignatureState.Invalid;
    }

    public void Dispose()
    {
        foreach (var key in _keys)
        {
            key.Dispose();
        }
    }
}
