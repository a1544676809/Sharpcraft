// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sharpcraft.AuthLib;

namespace Sharpcraft.AuthLib.Tests;

/// <summary>Profile-property signature states and the discovery/public-key loader.</summary>
public sealed class ProfileSignatureTests
{
    [Fact]
    public void SignedPropertiesAreAcceptedAndTamperedOnesAreInvalid()
    {
        using var rsa = RSA.Create(2048);
        using var keySet = new ProfileSignatureKeySet([new ProfileSignatureKey(rsa)], []);
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"textures":{}}"""));
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.ASCII.GetBytes(value), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

        Assert.Equal(ProfileSignatureState.Signed, keySet.Validate(new ProfileProperty("textures", value, signature)));
        Assert.Equal(ProfileSignatureState.Invalid, keySet.Validate(new ProfileProperty("textures", value + "x", signature)));
        Assert.Equal(ProfileSignatureState.Unsigned, keySet.Validate(new ProfileProperty("textures", value)));
        Assert.Equal(ProfileSignatureState.Invalid, keySet.Validate(new ProfileProperty("textures", value, "not-base64")));
    }

    [Fact]
    public async Task DiscoveryLoadsProfileKeysAndTexturePrefixes()
    {
        using var rsa = RSA.Create(2048);
        var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"textures":{}}"""));
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.ASCII.GetBytes(value), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

        var discovery = """
            {
              "discovery": {
                "authentication": { "endpoints": { "getPublicKeys": { "uri": "https://keys.invalid/publickeys" } } },
                "profiles": { "endpoints": { "getTexture": { "validUris": [ "https://textures.minecraft.net/texture/{textureId}" ] } } }
              }
            }
            """;
        var keys = $$"""
            { "profilePropertyKeys": [ { "publicKey": "{{publicKey}}" } ] }
            """;
        using var handler = new StubHandler(discovery, keys);
        using var client = new MinecraftServicesKeyClient(handler.CreateClient(), new Uri("https://discovery.invalid/client"));

        using var keySet = await client.GetAsync(online: true);

        Assert.Equal(ProfileSignatureState.Signed, keySet.Validate(new ProfileProperty("textures", value, signature)));
        Assert.True(keySet.IsAllowedTextureUrl("https://textures.minecraft.net/texture/abc"));
        Assert.False(keySet.IsAllowedTextureUrl("https://evil.invalid/texture/abc"));
    }

    [Fact]
    public async Task OfflineModeDoesNotRequestKeys()
    {
        using var handler = new StubHandler();
        using var client = new MinecraftServicesKeyClient(handler.CreateClient(), new Uri("https://discovery.invalid/client"));

        var keySet = await client.GetAsync(online: false);

        Assert.Equal(ProfileSignatureState.Invalid,
            keySet.Validate(new ProfileProperty("textures", "value", "signature")));
        Assert.False(keySet.IsAllowedTextureUrl("https://textures.minecraft.net/texture/abc"));
        Assert.Equal(0, handler.RequestCount);
    }

    private sealed class StubHandler(string? discovery = null, string? keys = null) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = request.RequestUri!.Host == "keys.invalid" ? keys : discovery;
            return Task.FromResult(new HttpResponseMessage(body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
            });
        }
    }
}
