// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using System.Text.Json;

namespace Sharpcraft.AuthLib;

/// <summary>
/// Loads the trusted profile-property keys and texture URL policy from the Minecraft services discovery document.
/// The reference authlib fetches the same data lazily from the authentication service; the shared library keeps the
/// HTTP boundary injectable so tests never reach the network.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("com.mojang.authlib.services.MinecraftServicesKeyInfo", "ref://mc-26.3-analysis/src", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Discovery + publickeys loader; no client/server game types")]
public sealed class MinecraftServicesKeyClient : IDisposable
{
    /// <summary>Production discovery endpoint used by the reference authlib.</summary>
    public static readonly Uri DefaultDiscoveryUri = new("https://discovery.minecraftservices.com/minecraft/client");

    private readonly HttpClient _http;
    private readonly Uri _discoveryUri;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProfileSignatureKeySet? _cached;

    public MinecraftServicesKeyClient(HttpClient http, Uri? discoveryUri = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _discoveryUri = discoveryUri ?? DefaultDiscoveryUri;
    }

    /// <summary>
    /// Returns the cached key set, loading it once. Offline mode deliberately returns an empty set, matching the
    /// reference client's disabled services-key path.
    /// </summary>
    public async Task<ProfileSignatureKeySet> GetAsync(bool online, CancellationToken cancellationToken = default)
    {
        if (!online)
        {
            return ProfileSignatureKeySet.Empty;
        }

        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cached ??= await LoadAsync(cancellationToken).ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _cached?.Dispose();
        _gate.Dispose();
    }

    private async Task<ProfileSignatureKeySet> LoadAsync(CancellationToken cancellationToken)
    {
        string[] prefixes = [];
        try
        {
            var discoveryText = await _http.GetStringAsync(_discoveryUri, cancellationToken).ConfigureAwait(false);
            using var discovery = JsonDocument.Parse(discoveryText);
            var root = discovery.RootElement;

            if (!TryReadPath(root, ["discovery", "authentication", "endpoints", "getPublicKeys", "uri"], out var keysUriElement) ||
                keysUriElement.ValueKind != JsonValueKind.String)
            {
                return ProfileSignatureKeySet.Empty;
            }

            if (TryReadPath(root, ["discovery", "profiles", "endpoints", "getTexture", "validUris"], out var validUris) &&
                validUris.ValueKind == JsonValueKind.Array)
            {
                prefixes = validUris.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .ToArray();
            }

            var keysUrl = keysUriElement.GetString();
            if (string.IsNullOrWhiteSpace(keysUrl) || !Uri.TryCreate(keysUrl, UriKind.Absolute, out var keysUri))
            {
                return new ProfileSignatureKeySet([], prefixes);
            }

            var keysText = await _http.GetStringAsync(keysUri, cancellationToken).ConfigureAwait(false);
            using var keysDocument = JsonDocument.Parse(keysText);
            var keys = new List<ProfileSignatureKey>();
            if (keysDocument.RootElement.TryGetProperty("profilePropertyKeys", out var keyList) &&
                keyList.ValueKind == JsonValueKind.Array)
            {
                foreach (var keyElement in keyList.EnumerateArray())
                {
                    if (keyElement.ValueKind != JsonValueKind.Object ||
                        !keyElement.TryGetProperty("publicKey", out var publicKeyElement) ||
                        publicKeyElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    try
                    {
                        var der = Convert.FromBase64String(publicKeyElement.GetString()!);
                        var rsa = RSA.Create();
                        rsa.ImportSubjectPublicKeyInfo(der, out _);
                        keys.Add(new ProfileSignatureKey(rsa));
                    }
                    catch (Exception exception) when (exception is FormatException or CryptographicException)
                    {
                        // One malformed key must not invalidate the remaining trusted keys.
                    }
                }
            }

            return new ProfileSignatureKeySet(keys, prefixes);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          exception is HttpRequestException or TaskCanceledException or JsonException or FormatException or CryptographicException)
        {
            // The reference treats a failed key fetch as an empty key set; the texture URL policy may still be
            // available if discovery succeeded before the failure.
            return prefixes.Length == 0
                ? ProfileSignatureKeySet.Empty
                : new ProfileSignatureKeySet([], prefixes);
        }
    }

    private static bool TryReadPath(JsonElement root, string[] path, out JsonElement value)
    {
        value = root;
        foreach (var part in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }
}
