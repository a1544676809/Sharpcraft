// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Net;
using System.Text;
using System.Text.Json;

namespace MinecraftCSharp.Auth;

/// <summary>Shared JSON request/response plumbing for the authentication chain.</summary>
internal static class AuthHttp
{
    // Property names are already correct on each endpoint (snake_case for the identity
    // endpoints, PascalCase for Xbox Live), so no naming policy may be applied.
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Posts a JSON body and returns the parsed response document.</summary>
    internal static async Task<(HttpStatusCode Status, JsonDocument Body)> PostJsonAsync(
        HttpClient http,
        Uri uri,
        object payload,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new AuthException(AuthError.Transport, $"request to {uri.Host} failed", inner: exception);
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, Parse(text));
    }

    /// <summary>Parses a JSON body, tolerating empty payloads.</summary>
    internal static JsonDocument Parse(string text) =>
        string.IsNullOrWhiteSpace(text) ? JsonDocument.Parse("{}") : JsonDocument.Parse(text);

    /// <summary>Reads a string property, returning null when absent.</summary>
    internal static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Reads a numeric property, returning null when absent.</summary>
    internal static int? Int(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.GetInt32();
    }

    /// <summary>Reads a 64-bit numeric property, returning null when absent.</summary>
    internal static long? Long(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt64(out var parsed) ? parsed : null;
    }

    /// <summary>Returns the first element claim user hash from an Xbox response.</summary>
    internal static string? UserHash(JsonElement root)
    {
        if (!root.TryGetProperty("DisplayClaims", out var claims) ||
            !claims.TryGetProperty("xui", out var xui) ||
            xui.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in xui.EnumerateArray())
        {
            var uhs = String(entry, "uhs");
            if (!string.IsNullOrEmpty(uhs))
            {
                return uhs;
            }
        }

        return null;
    }
}
