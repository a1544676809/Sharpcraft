// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Net;
using System.Text;

namespace MinecraftCSharp.Auth.Tests;

/// <summary>
/// Deterministic HTTP stub: replies from a script and records request bodies so tests can
/// assert the payload shape, not just the parsing side.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    /// <summary>Bodies of every request, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Requested URIs, in order.</summary>
    public List<Uri> Uris { get; } = [];

    /// <summary>Queues a response.</summary>
    public StubHandler Respond(HttpStatusCode status, string body)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    /// <summary>Convenience for a 200 JSON reply.</summary>
    public StubHandler RespondOk(string body) => Respond(HttpStatusCode.OK, body);

    /// <summary>Number of scripted responses still unused.</summary>
    public int Remaining => _responses.Count;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Uris.Add(request.RequestUri!);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"unexpected request to {request.RequestUri}");
        }

        var (status, body) = _responses.Dequeue();
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Builds an HttpClient over this stub.</summary>
    public HttpClient CreateClient() => new(this, disposeHandler: false);
}
