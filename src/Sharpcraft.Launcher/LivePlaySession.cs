// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Concurrent;
using System.Net.Sockets;
using Sharpcraft.Auth;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Launcher;

/// <summary>
/// Keeps a play connection alive while the client runs (docs/src/sharpcraft-launcher/live-session.md).
/// Receiving happens on a background task that only enqueues packets; every send and every game-state
/// change stays on the caller's thread, so the player entity is never touched concurrently.
/// </summary>
internal sealed class LivePlaySession : IDisposable
{
    private readonly MinecraftConnection _connection;
    private readonly ConcurrentQueue<byte[]> _incoming = new();
    private readonly CancellationTokenSource _stop = new();
    private Task _pump = Task.CompletedTask;
    private volatile string _failure = string.Empty;

    private LivePlaySession(MinecraftConnection connection) => _connection = connection;

    /// <summary>Starts the receive pump on a background task.</summary>
    private void Start(CancellationToken cancellationToken) =>
        _pump = Task.Run(() => PumpAsync(cancellationToken), CancellationToken.None);

    /// <summary>Reason the connection ended, or empty while it is alive.</summary>
    internal string Failure => _failure;

    /// <summary>False once the connection died.</summary>
    internal bool IsAlive => _failure.Length == 0;

    /// <summary>True when the player is signed in and in the play phase.</summary>
    internal static async Task<LivePlaySession> ConnectAsync(
        string address,
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var signedIn = await AuthenticationFlow.EnsureSignedInAsync(
            http,
            TokenStore.CreateDefault(),
            forceInteractive: false,
            onChallenge: (challenge, _) =>
            {
                Console.WriteLine($"Open {challenge.VerificationUri} and enter the code: {challenge.UserCode}");
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Signed in as {signedIn.Profile.Name}");

        var join = new SessionServerClient(http);
        var (outcome, connection) = await MinecraftLoginClient.LoginAsync(
            address,
            (signedIn.Profile.Name, signedIn.Profile.Id),
            signedIn.Session.AccessToken,
            (token, hash, ct) => join.JoinAsync(signedIn.Session, signedIn.Profile.Id, hash, ct),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!outcome.Success || connection is null)
        {
            throw new InvalidOperationException($"server refused the login: {outcome.Reason}");
        }

        var probe = await ConfigurationProbe.RunAsync(
            connection,
            TimeSpan.FromSeconds(20),
            cancellationToken).ConfigureAwait(false);

        if (!probe.ReachedPlay)
        {
            connection.Dispose();
            throw new InvalidOperationException($"did not reach play: {probe.Reason}");
        }

        var session = new LivePlaySession(connection);
        session.Start(cancellationToken);
        return session;
    }

    /// <summary>Takes the next received packet body, if any.</summary>
    /// <param name="body">Packet body without the length prefix.</param>
    internal bool TryDequeue(out byte[] body) => _incoming.TryDequeue(out body!);

    /// <summary>Sends one packet body; failures mark the session dead instead of throwing.</summary>
    /// <param name="packet">Packet body to send.</param>
    internal bool Send(ReadOnlySpan<byte> packet)
    {
        if (_failure.Length != 0)
        {
            return false;
        }

        try
        {
            _connection.Send(packet);
            return true;
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            _failure = exception.Message;
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _connection.Dispose();

        try
        {
            _pump.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // The pump ends by exception when the socket closes; that is the normal shutdown path.
        }

        _stop.Dispose();
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var body = await _connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                _incoming.Enqueue(body);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            _failure = exception.Message;
        }
    }
}
