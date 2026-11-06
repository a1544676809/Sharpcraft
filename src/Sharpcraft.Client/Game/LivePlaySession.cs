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
    private readonly BoundedReceiveQueue _incoming = new();
    private readonly CancellationTokenSource _stop = new();
    private Task _pump = Task.CompletedTask;
    private volatile string _failure = string.Empty;

    /// <summary>Message used when the bounded receive queue rejects a packet.</summary>
    internal const string QueueOverflowMessage = "receive queue exceeded 64 MiB";

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
        // session applies them exactly as if it had owned the connection from the start. A deferred packet that would
        // overflow the bounded queue marks the session dead instead of growing it without bound.
        if (deferredPackets is not null)
        {
            foreach (var body in deferredPackets)
            {
                if (!session.TryEnqueueReceived(body))
                {
                    break;
                }
            }
        }

        if (session.IsAlive)
        {
            session.Start(cancellationToken);
        }

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

    /// <summary>Queues a received packet body, marking the session dead when the bounded queue is full.</summary>
    private bool TryEnqueueReceived(byte[] body)
    {
        if (_incoming.TryEnqueue(body))
        {
            return true;
        }

        _failure = _incoming.OverflowReason ?? QueueOverflowMessage;
        _stop.Cancel();
        return false;
    }

    /// <summary>Formats a receive-pump fault so protocol/decode failures are surfaced instead of silently faulting the task.</summary>
    internal static string DescribePumpFailure(Exception exception) =>
        $"receive pump failed: {exception.GetType().Name}: {exception.Message}";

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var body = await _connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!TryEnqueueReceived(body))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (OutOfMemoryException)
        {
            // Resource exhaustion is not a recoverable connection fault; let it propagate.
            throw;
        }
        catch (Exception exception)
        {
            // Any other failure (protocol/decode, socket, disposal) must surface through IsAlive/Failure.
            _failure = DescribePumpFailure(exception);
        }
    }
}

/// <summary>
/// Bounded, thread-safe receive queue used by <see cref="LivePlaySession"/>. The receive pump is the only producer (plus
/// the one-shot deferred replay in <see cref="LivePlaySession.Adopt"/>), so a single atomic byte counter is enough to
/// enforce the cap without locks.
/// </summary>
internal sealed class BoundedReceiveQueue
{
    /// <summary>Default cap: 64 MiB of buffered packet bodies.</summary>
    public const long MaxBufferedBytes = 64L * 1024 * 1024;

    private readonly ConcurrentQueue<byte[]> _queue = new();
    private readonly long _maxBufferedBytes;
    private long _bufferedBytes;
    private int _overflowed;
    private string? _overflowReason;

    internal BoundedReceiveQueue(long maxBufferedBytes = MaxBufferedBytes)
    {
        if (maxBufferedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBufferedBytes), maxBufferedBytes, "queue cap must be positive");
        }

        _maxBufferedBytes = maxBufferedBytes;
    }

    /// <summary>Current buffered byte count across all queued bodies.</summary>
    public long BufferedBytes => Interlocked.Read(ref _bufferedBytes);

    /// <summary>The configured cap in bytes.</summary>
    public long MaxBytes => _maxBufferedBytes;

    /// <summary>True once an enqueue was rejected or would have exceeded the cap; sticky for the queue's lifetime.</summary>
    public bool Overflowed => Volatile.Read(ref _overflowed) != 0;

    /// <summary>The first overflow reason, or null while nothing overflowed.</summary>
    public string? OverflowReason => Volatile.Read(ref _overflowReason);

    /// <summary>
    /// Enqueues <paramref name="body"/> if it fits under the cap. Returns false and records the overflow reason when the
    /// body would exceed the cap or the queue already overflowed (further enqueues stay rejected).
    /// </summary>
    public bool TryEnqueue(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (Overflowed)
        {
            return false;
        }

        var length = (long)body.Length;
        long current;
        do
        {
            current = Interlocked.Read(ref _bufferedBytes);
            if (current + length > _maxBufferedBytes)
            {
                MarkOverflow($"receive queue exceeded {FormatLimit(_maxBufferedBytes)}");
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _bufferedBytes, current + length, current) != current);

        _queue.Enqueue(body);
        return true;
    }

    /// <summary>Removes the next body and subtracts its length from the byte count.</summary>
    public bool TryDequeue(out byte[] body)
    {
        if (_queue.TryDequeue(out body!))
        {
            Interlocked.Add(ref _bufferedBytes, -(long)body.Length);
            return true;
        }

        body = null!;
        return false;
    }

    private void MarkOverflow(string reason)
    {
        if (Interlocked.Exchange(ref _overflowed, 1) == 0)
        {
            Volatile.Write(ref _overflowReason, reason);
        }
    }

    private static string FormatLimit(long limit) =>
        limit % (1024 * 1024) == 0 ? $"{limit / (1024 * 1024)} MiB" : $"{limit} bytes";
}
