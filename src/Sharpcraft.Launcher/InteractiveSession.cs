// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Platform;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher;

/// <summary>
/// Glue between the window, the input mapper, the player entity and the camera. Kept here rather than in
/// the client so the client stays free of window and renderer types
/// (docs/modules/input.md 3: platform capabilities live in the engine layer).
/// </summary>
internal sealed class InteractiveSession
{
    private readonly PlayerEntity _player;
    private readonly InputMapper _mapper = new();
    private readonly CameraController _camera = new();
    private readonly TickLoop _loop = new();
    private readonly PlayerInterpolator _interpolator;
    private readonly LivePlaySession? _live;
    private readonly ClientWorld? _world;
    private bool _streamingRequested;
    private readonly PlayerSync? _sync;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;
    private bool _captureRequested;

    private InteractiveSession(PlayerEntity player, LivePlaySession? live, ClientWorld? world)
    {
        _player = player;
        _world = world;
        _interpolator = new PlayerInterpolator(player, _camera.Yaw);
        _live = live;
        _sync = live is null ? null : new PlayerSync();
    }

    /// <summary>Creates a session with the player standing on the highest block under the view centre.</summary>
    /// <param name="world">World the player walks in.</param>
    /// <param name="request">Frame request, used for the spawn area and the vertex count.</param>
    public static InteractiveSession Create(ClientWorld world, FrameRequest request, LivePlaySession? live = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);

        var adapter = new EntityWorldAdapter(world);
        var spawn = FindSpawn(world, request);
        return new InteractiveSession(new PlayerEntity(adapter, spawn), live, world);
    }

    /// <summary>Player being simulated.</summary>
    public PlayerEntity Player => _player;

    /// <summary>
    /// Advances the simulation from the current input and returns the camera for this frame.
    /// </summary>
    /// <param name="window">Window whose input state is read.</param>
    public (Vector3 Eye, Vector3 Target) UpdateCamera(WindowHost window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var frame = _mapper.Map(window.Input);

        // Verification hook: walk forward without a keyboard so an unattended run exercises the network path.
        if (Environment.GetEnvironmentVariable("MC_WINDOW_AUTOWALK") == "1")
        {
            frame = frame with { Forward = 1f };
        }
        _camera.ApplyLook(frame.LookDelta, _mapper.LookSensitivity);

        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;

        EnsureStreaming();
        DrainIncoming();

        _loop.Advance(
            elapsed,
            () =>
            {
                _player.Tick(frame.Forward, frame.Strafe, frame.Jump, frame.Sprint, _camera.Yaw);
                _interpolator.Push(_player, _camera.Yaw);
                PublishTick();
            });

        if (frame.ReleaseCursor)
        {
            window.SetMouseCaptured(false);
            _captureRequested = false;
        }

        // Frames arrive faster than ticks; drawing the interpolated eye is what keeps walking smooth.
        var eye = _interpolator.Eye(_loop.Alpha);
        return (eye, _camera.Target(eye));
    }

    /// <summary>Captures the mouse once, and writes the diagnostic title.</summary>
    /// <param name="window">Window to configure.</param>
    /// <param name="first">True on the first frame.</param>
    public void CaptureMouse(WindowHost window, bool first)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (first && !_captureRequested)
        {
            window.SetMouseCaptured(true);
            _captureRequested = true;
        }

        var position = _player.Position;
        window.Title =
            $"Sharpcraft | {position.X:F1} {position.Y:F1} {position.Z:F1} | " +
            $"{(_player.OnGround ? "ground" : "air")} | {window.Input.PressedKeys.Count} keys";
    }

    private static Vector3 FindSpawn(ClientWorld world, FrameRequest request)
    {
        // Drop onto the highest solid block near the centre of what will be visible.
        var centre = new Vector3(
            (request.CameraTarget.X + request.CameraPosition.X) / 2f,
            0f,
            (request.CameraTarget.Z + request.CameraPosition.Z) / 2f);

        var x = MathF.Floor(centre.X);
        var z = MathF.Floor(centre.Z);

        for (var y = 319; y > -64; y--)
        {
            if (world.GetBlockState((int)x, y, (int)z) != 0)
            {
                return new Vector3(x + 0.5f, y + 1f, z + 0.5f);
            }
        }

        return new Vector3(x + 0.5f, 1f, z + 0.5f);
    }

    /// <summary>
    /// Sends this tick's movement and the end-of-tick marker. The marker is not optional: the server resets
    /// its per-tick movement guards there and disconnects a client whose second position packet arrives
    /// without it (docs/specs/network/play-movement.md 6.5).
    /// </summary>
    /// <summary>
    /// Tells the server how far we want to see, then asks for the first chunk batch. Without this the server
    /// never streams terrain and the player walks off the end of whatever was downloaded at join
    /// (docs/specs/network/play-movement.md 7.12).
    /// </summary>
    private void EnsureStreaming()
    {
        if (_live is null || _streamingRequested)
        {
            return;
        }

        _streamingRequested = true;
        _live.Send(PlayPackets.BuildClientInformation(
            locale: "en_us",
            viewDistance: 8,
            chatVisibility: 0,
            chatColors: true,
            skinParts: 0x7F,
            mainHand: 1,
            textFiltering: false,
            allowListing: true));
        _live.Send(PlayPackets.BuildChunkBatchReceived(8f));
    }

    private void PublishTick()
    {
        if (_live is null || _sync is null || !_live.IsAlive)
        {
            return;
        }

        var yawDegrees = _camera.Yaw * (180f / MathF.PI);
        var pitchDegrees = _camera.Pitch * (180f / MathF.PI);

        if (_sync.Build(_sync.Decide(_player, yawDegrees, pitchDegrees), _player, yawDegrees, pitchDegrees) is { } packet)
        {
            _live.Send(packet);
        }

        _live.Send(PlayPackets.BuildClientTickEnd());
    }

    /// <summary>
    /// Applies everything the server sent since the previous frame: keep-alives, pings, position
    /// corrections and a disconnect. Runs on the render thread so the player is never touched concurrently.
    /// </summary>
    private void DrainIncoming()
    {
        if (_live is null)
        {
            return;
        }

        while (_live.TryDequeue(out var body))
        {
            var reader = new PayloadReader(body);
            if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
            {
                continue;
            }

            switch (packetId)
            {
                case PacketIds.PlayClientbound.KeepAlive:
                    reader.TryReadLong(out var keepAliveId);
                    _live.Send(PlayPackets.BuildKeepAliveReply(keepAliveId));
                    break;

                case PacketIds.PlayClientbound.Ping:
                    reader.TryReadInt(out var pingId);
                    _live.Send(PlayPackets.BuildPongReply(pingId));
                    break;

                case PacketIds.PlayClientbound.PlayerPosition:
                    if (_sync is not null &&
                        PlayMovementPackets.TryReadPlayerPosition(body[reader.Position..], out var correction) == DecodeStatus.Success &&
                        correction is not null)
                    {
                        var outcome = _sync.ApplyServerPosition(_player, correction, isTeleport: true);
                        _interpolator.Snap(_player, _camera.Yaw);

                        if (outcome.TeleportId is { } teleportId)
                        {
                            _live.Send(PlayMovementPackets.BuildAcceptTeleportation(
                                teleportId,
                                _player.Position.X,
                                _player.Position.Y,
                                _player.Position.Z,
                                _camera.Yaw * (180f / MathF.PI),
                                _camera.Pitch * (180f / MathF.PI)));
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.LevelChunkWithLight:
                    if (_world is not null &&
                        ChunkPacketParser.TryRead(body, _world.SectionCount, _world.MinSectionY, airState: 0, out var parsed) == DecodeStatus.Success &&
                        parsed is not null)
                    {
                        _world.AddChunk(parsed);
                    }

                    break;

                case PacketIds.PlayClientbound.ChunkBatchFinished:
                    // The server waits for the acknowledgement before sending the next batch.
                    _live.Send(PlayPackets.BuildChunkBatchReceived(8f));
                    break;

                case PacketIds.PlayClientbound.Disconnect:
                    return;
            }
        }
    }
}
