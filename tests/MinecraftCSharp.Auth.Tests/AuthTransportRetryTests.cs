// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Net;
using System.Text;
using MinecraftCSharp.Auth;
using Xunit;

namespace MinecraftCSharp.Auth.Tests;

/// <summary>
/// Transport robustness for the sign-in chain: transient failures are retried a bounded number of
/// times, and a permanent failure still surfaces as a classified error. No network is touched.
/// </summary>
public sealed class AuthTransportRetryTests
{
    [Fact]
    public async Task TransientFailureIsRetriedThenSucceeds()
    {
        var handler = new FlakyHandler(failuresBeforeSuccess: 2);
        using var http = new HttpClient(handler);

        using var response = await AuthHttp.SendWithRetryAsync(
            "POST",
            new Uri("https://example.invalid/authorize"),
            () => new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/authorize"),
            http,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task PermanentFailureReportsTheAttemptCount()
    {
        var handler = new FlakyHandler(failuresBeforeSuccess: int.MaxValue);
        using var http = new HttpClient(handler);

        var exception = await Assert.ThrowsAsync<AuthException>(() => AuthHttp.SendWithRetryAsync(
            "POST",
            new Uri("https://example.invalid/authorize"),
            () => new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/authorize"),
            http,
            CancellationToken.None));

        Assert.Equal(AuthError.Transport, exception.Error);
        Assert.Contains($"after {AuthHttp.TransportAttempts} attempts", exception.Message, StringComparison.Ordinal);
        Assert.Equal(AuthHttp.TransportAttempts, handler.Attempts);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task CallerCancellationIsNotRetried()
    {
        var handler = new FlakyHandler(failuresBeforeSuccess: int.MaxValue);
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AuthHttp.SendWithRetryAsync(
            "POST",
            new Uri("https://example.invalid/authorize"),
            () => new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/authorize"),
            http,
            cancellation.Token));

        Assert.Equal(0, handler.Attempts);
    }

    private sealed class FlakyHandler(int failuresBeforeSuccess) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;

            if (Attempts <= failuresBeforeSuccess)
            {
                throw new HttpRequestException("simulated transport failure");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
