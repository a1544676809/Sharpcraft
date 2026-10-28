// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text;
using System.Text.Json;

namespace Sharpcraft.Auth;

/// <summary>Shared JSON request/response plumbing for the authentication chain.</summary>
internal static class AuthHttp
{
    // Property names are already correct on each endpoint (snake_case for the identity
    // endpoints, PascalCase for Xbox Live), so no naming policy may be applied.
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Set MC_TRANSPORT_DEBUG=1 to log each request URI. Transport failures classify by host, and
    /// knowing which call failed is what makes them actionable.
    /// </summary>
    private static readonly bool TraceRequests =
        Environment.GetEnvironmentVariable("MC_TRANSPORT_DEBUG") is "1";

    /// <summary>Attempts per request; transport failures are usually transient on flaky networks.</summary>
    internal const int TransportAttempts = 3;

    /// <summary>Delay before each retry, in milliseconds.</summary>
    internal static readonly int[] RetryDelays = [400, 1200];

    /// <summary>
    /// Sends a request, retrying a bounded number of times on transport failures. Endpoints in this
    /// chain are safe to repeat: signing in again simply issues a fresh session.
    /// </summary>
    internal static async Task<HttpResponseMessage> SendWithRetryAsync(
        string method,
        Uri uri,
        Func<HttpRequestMessage> buildRequest,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildRequest);
        ArgumentNullException.ThrowIfNull(http);

        Exception? last = null;

        for (var attempt = 1; attempt <= TransportAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = buildRequest();
            try
            {
                Trace(method, uri);
                return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken))
            {
                last = exception;

                if (attempt == TransportAttempts)
                {
                    break;
                }

                if (TraceRequests)
                {
                    Console.Error.WriteLine(
                        $"  [debug] {method} {uri.Host} attempt {attempt} failed: {exception.Message}; retrying");
                }

                await Task.Delay(RetryDelays[attempt - 1], cancellationToken).ConfigureAwait(false);
            }
        }

        throw new AuthException(
            AuthError.Transport,
            $"request to {uri.Host} failed after {TransportAttempts} attempts",
            inner: last);
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested &&
        exception is HttpRequestException or IOException or TimeoutException ||
        (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static void Trace(string method, Uri uri)
    {
        if (TraceRequests)
        {
            Console.Error.WriteLine($"  [debug] {method} {uri}");
        }
    }

    /// <summary>Posts a JSON body and returns the parsed response document.</summary>
    internal static async Task<(HttpStatusCode Status, JsonDocument Body)> PostJsonAsync(
        HttpClient http,
        Uri uri,
        object payload,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        using var response = await SendWithRetryAsync(
            "POST",
            uri,
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            },
            http,
            cancellationToken).ConfigureAwait(false);

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, Parse(text));
    }

    /// <summary>
    /// Posts a form-urlencoded body. The Microsoft identity endpoints reject JSON with
    /// AADSTS900144 ("body must contain client_id"), so the encoding is not a preference.
    /// </summary>
    internal static async Task<(HttpStatusCode Status, JsonDocument Body)> PostFormAsync(
        HttpClient http,
        Uri uri,
        IEnumerable<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(fields);

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

    /// <summary>Extracts the provider error description, used to make failures diagnosable.</summary>
    internal static string Describe(JsonElement root, HttpStatusCode status)
    {
        var error = String(root, "error");
        var description = String(root, "error_description");
        if (string.IsNullOrEmpty(error) && string.IsNullOrEmpty(description))
        {
            return $"HTTP {(int)status}";
        }

        var code = String(root, "trace_id");
        var text = string.IsNullOrEmpty(description) ? error : $"{error}: {description}";
        return string.IsNullOrEmpty(code) ? text! : $"{text} (trace {code})";
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
