// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Concurrent;
using System.Net.Sockets;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.Game;

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

    private LivePlaySession(MinecraftConnection connection, Sharpcraft.Common.Network.DynamicRegistries? registries = null)
    {
        _connection = connection;
        Registries = registries;
    }

    /// <summary>
    /// Registries the server sent during configuration, which is where the chat type decorations live.
    /// </summary>
    /// <remarks>
    /// Entry ids are positional and per connection, so this has to come from the same connection that carries the
    /// messages, not from an earlier probe of the same server.
    /// </remarks>
    internal Sharpcraft.Common.Network.DynamicRegistries? Registries { get; }

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
        GameIdentity identity,
        CancellationToken cancellationToken)
    {
        var (outcome, connection) = await GameConnector
            .LoginAsync(address, http, identity, cancellationToken)
            .ConfigureAwait(false);

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

        var session = new LivePlaySession(connection, probe.Registries);
        session.Start(cancellationToken);
        return session;
    }

    /// <summary>
    /// Takes over a connection that already finished its login and configuration, so the startup does not join
    /// the world twice (docs/specs/network/play-movement.md 7.32).
    /// </summary>
    /// <param name="connection">A connection already in the play phase; ownership passes to the session.</param>
    /// <param name="cancellationToken">Cancels the receive pump.</param>
    internal static LivePlaySession Adopt(
        MinecraftConnection connection,
        CancellationToken cancellationToken,
        Sharpcraft.Common.Network.DynamicRegistries? registries = null,
        IReadOnlyList<byte[]>? deferredPackets = null)
    {
        var session = new LivePlaySession(connection, registries);

        // Packets the preload consumed (join clock sync, weather game events) are replayed into the queue so the live
        // session applies them exactly as if it had owned the connection from the start.
        if (deferredPackets is not null)
        {
            foreach (var body in deferredPackets)
            {
                session._incoming.Enqueue(body);
            }
        }

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
