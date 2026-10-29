// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.Ui;
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
    private bool _geometryDirty;
    private readonly ChunkWorkerPool? _pool;
    private readonly EntityTracker _entities = new();
    private readonly PlayerListRoster _playerList = new();
    private readonly ChatInput _chat = new();
    private readonly ChatLog _chatLog = new();
    private long _chatTicks;
    private readonly bool _diagEnabled = Environment.GetEnvironmentVariable("MC_DIAG") == "1";
    private int _diagFrames;

    /// <summary>
    /// Chunks of view distance requested from the server. It bounds how much terrain can be resident, which is
    /// what the geometry arena presizes for (docs/specs/network/play-movement.md 7.27).
    /// </summary>
    public const int ViewDistanceChunks = 8;

    /// <summary>Chunks a square view of <see cref="ViewDistanceChunks"/> can hold.</summary>
    public static int ExpectedChunkCount()
    {
        var side = (2 * ViewDistanceChunks) + 1;
        return side * side;
    }

    /// <summary>Decoded chunks installed per frame; keeps one batch from stalling the frame.</summary>
    private const int ParsedChunksPerFrame = 8;
    private int _jumpPressed;
    private int _jumpAccepted;
    private int _jumpInAir;
    private int _tickCounter;
    private readonly PlayerSync? _sync;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;

    private InteractiveSession(PlayerEntity player, LivePlaySession? live, ClientWorld? world, ChunkWorkerPool? pool)
    {
        _player = player;
        _world = world;
        _pool = pool;
        _interpolator = new PlayerInterpolator(player, _camera.Yaw);
        _live = live;
        _sync = live is null ? null : new PlayerSync();
    }

    /// <summary>Creates a session with the player standing on the highest block under the view centre.</summary>
    /// <param name="world">World the player walks in.</param>
    /// <param name="request">Frame request, used for the spawn area and the vertex count.</param>
    public static InteractiveSession Create(
        ClientWorld world,
        FrameRequest request,
        LivePlaySession? live = null,
        ChunkWorkerPool? pool = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);

        var adapter = new EntityWorldAdapter(world);
        var spawn = FindSpawn(world, request);
        return new InteractiveSession(new PlayerEntity(adapter, spawn), live, world, pool);
    }

    /// <summary>
    /// True once and then false: reports that a block changed since the last call, so the renderer knows to
    /// rebuild the geometry (docs/specs/network/play-movement.md 7.17).
    /// </summary>
    /// <summary>Diagnostic counters: jump ticks seen, accepted on the ground, and ignored while airborne.</summary>
    public (int Pressed, int Accepted, int InAir) JumpStats => (_jumpPressed, _jumpAccepted, _jumpInAir);

    /// <summary>Entities the server told us about, updated as packets arrive.</summary>
    public EntityTracker Entities => _entities;

    /// <summary>The player list the server described.</summary>
    public PlayerListRoster PlayerList => _playerList;

    /// <summary>The chat prompt.</summary>
    public ChatInput Chat => _chat;

    /// <summary>The chat log on screen.</summary>
    public ChatLog ChatLog => _chatLog;

    /// <summary>Ticks elapsed, used by the chat fade.</summary>
    public long ChatTicks => _chatTicks;

    /// <summary>Fraction into the current tick, for callers that interpolate between ticks.</summary>
    public float PartialTick => _loop.Alpha;

    /// <summary>Entity render states interpolated for this frame.</summary>
    /// <param name="alpha">Fraction into the current tick.</param>
    public ReadOnlySpan<EntityRenderState> EntityStates(float alpha) => _entities.Interpolate(alpha);

    public bool TakeGeometryDirty()
    {
        if (!_geometryDirty)
        {
            return false;
        }

        _geometryDirty = false;
        return true;
    }

    /// <summary>Player being simulated.</summary>
    public PlayerEntity Player => _player;


    /// <summary>Reads the keyboard for the chat prompt and sends what the player submits.</summary>
    /// <param name="window">Window whose input state is read.</param>
    private void UpdateChat(WindowHost window)
    {
        var input = window.Input;
        var bindings = KeyBindings.Default;

        if (!_chat.IsOpen)
        {
            // Both keys open the prompt; the slash key leaves its slash behind so a command can be typed straight on.
            if (bindings.JustPressed(input, InputAction.OpenCommand))
            {
                _chat.Open("/");
                window.SetMouseCaptured(false);
            }
            else if (bindings.JustPressed(input, InputAction.OpenChat))
            {
                _chat.Open();
                window.SetMouseCaptured(false);
            }

            return;
        }

        if (bindings.JustPressed(input, InputAction.CloseScreen))
        {
            _chat.Close();
            return;
        }

        if (bindings.JustPressed(input, InputAction.SendMessage))
        {
            if (_chat.TrySubmit(out var message, out var isCommand))
            {
                SendChat(message, isCommand);
            }

            return;
        }

        if (input.JustPressed(InputKey.Backspace))
        {
            _chat.Backspace();
        }

        var typed = input.TakeText();

        if (typed.Length > 0)
        {
            _chat.Append(typed.AsSpan());
        }
    }

    /// <summary>Sends a submitted line as either a chat message or a command.</summary>
    /// <param name="message">Line the player submitted.</param>
    /// <param name="isCommand">True when it starts with a slash.</param>
    private void SendChat(string message, bool isCommand)
    {
        if (_live is null)
        {
            return;
        }

        _live.Send(isCommand
            ? PlayPackets.BuildChatCommand(message)
            : PlayPackets.BuildChatMessage(message, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    /// <summary>
    /// Advances the simulation from the current input and returns the camera for this frame.
    /// </summary>
    /// <param name="window">Window whose input state is read.</param>
    public (Vector3 Eye, Vector3 Target) UpdateCamera(WindowHost window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Chat runs before movement so a key that opened the prompt cannot also move the player on the same frame.
        UpdateChat(window);

        var frame = _mapper.Map(window.Input);

        if (_chat.IsOpen)
        {
            // While typing, the player does not move or look: the reference gives the screen the keyboard, and a
            // stray step while writing a message is the sort of thing that gets people killed.
            frame = FrameInput.None with { LookDelta = Vector2.Zero };
        }

        // Verification hook: walk forward without a keyboard so an unattended run exercises the network path.
        if (Environment.GetEnvironmentVariable("MC_WINDOW_AUTOWALK") == "1")
        {
            frame = frame with { Forward = 1f };
        }
        // Objective record of the input state, so a capture problem can be diagnosed from the log instead of from
        // impressions (docs/modules/input.md 7).
        if (_diagEnabled && (_diagFrames++ % 60) == 0)
        {
            Console.WriteLine(
                $"look diag: captured={window.Input.MouseCaptured} focus={window.Input.HasFocus} "
                + $"delta=({frame.LookDelta.X:F2},{frame.LookDelta.Y:F2}) release={frame.ReleaseCursor}");
        }

        _camera.ApplyLook(frame.LookDelta, _mapper.LookSensitivity);

        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;

        EnsureStreaming();
        DrainIncoming();
        ApplyParsedChunks();

        _loop.Advance(
            elapsed,
            () =>
            {
                if (frame.Jump)
                {
                    _jumpPressed++;
                    if (_player.OnGround)
                    {
                        _jumpAccepted++;
                    }
                    else
                    {
                        _jumpInAir++;
                    }
                }

                _tickCounter++;

                // Verification hook: print the tracked entities so their positions can be compared against the
                // other client's own report of where it is (docs/specs/network/entity-sync.md 7).
                if (Environment.GetEnvironmentVariable("MC_ENTITY_LOG") == "1" && _tickCounter % 20 == 0)
                {
                    var states = _entities.Interpolate(0f);
                    var self = _player.Position;
                    var stamp = Environment.TickCount64;
                    Console.WriteLine(
                        $"entities: {states.Length} tracked at={stamp} self=({self.X:F3},{self.Y:F3},{self.Z:F3})");

                    foreach (var state in states)
                    {
                        Console.WriteLine(
                            $"entity {state.Id} type={state.TypeId} at={stamp} "
                            + $"pos=({state.X:F3},{state.Y:F3},{state.Z:F3}) yaw={state.Yaw:F1}");
                    }
                }

                // Console writes block for tens of milliseconds on Windows, and this runs on the tick thread,
                // where a stall shows up as a hitch once per interval. Diagnostics are therefore opt in, so the
                // shipping path never performs I/O while ticking.
                if (Environment.GetEnvironmentVariable("MC_DIAG") == "1" && _tickCounter % 40 == 0)
                {
                    var (pressed, accepted, inAir) = JumpStats;
                    Console.WriteLine(
                        $"diag: tick={_tickCounter} fwd={frame.Forward:F0} sprint={frame.Sprint} sneak={frame.Sneak} "
                        + $"jump presses={pressed} accepted={accepted} inAir={inAir} ground={_player.OnGround} "
                        + $"y={_player.Position.Y:F2} vy={_player.DeltaMovement.Y:F3}");
                }

                // Diagnostic hook: sweep the pitch so the head direction can be checked from a second client
                // without driving two windows at once.
                if (Environment.GetEnvironmentVariable("MC_CAMERA_SWEEP") == "1")
                {
                    var sweep = MathF.Sin(_tickCounter * 0.05f * 0.9f) * (80f * (MathF.PI / 180f));
                    _camera.SetLook(_camera.Yaw, sweep);
                }

                _player.Tick(frame.Forward, frame.Strafe, frame.Jump, frame.Sprint, _camera.Yaw, frame.Sneak);

                // Remote entities approach the position the server reported over a few ticks, so the tracker
                // has to advance once per tick (docs/specs/network/entity-sync.md 9.4).
                _entities.Tick();
            _chatTicks++;
                _interpolator.Push(_player, _camera.Yaw);
                PublishTick();
            });

        if (frame.ReleaseCursor && window.Input.MouseCaptured)
        {
            // Escape hands the cursor back; the camera stops following because the input layer only accumulates
            // movement while captured.
            window.SetMouseCaptured(false);
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

        // Capture on the first frame only when the window already has focus, and afterwards only when the player
        // clicks inside it. Capturing without focus would lock the pointer into a window they are not using.
        if (!window.Input.MouseCaptured && window.Input.HasFocus && (first || window.Input.TakeClick()))
        {
            window.SetMouseCaptured(true);
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
            viewDistance: ViewDistanceChunks,
            chatVisibility: 0,
            chatColors: true,
            skinParts: 0x7F,
            mainHand: 1,
            textFiltering: false,
            allowListing: true));
        _live.Send(PlayPackets.BuildChunkBatchReceived(8f));
    }

    /// <summary>
    /// Adopts the rotation the server just sent. Vanilla sets yaw and pitch outright on a teleport, including
    /// the join teleport, so a client that keeps its own heading after joining reports a facing the server
    /// never gave it (docs/specs/network/play-movement.md 7.14).
    /// </summary>
    private void AdoptServerRotation(ServerPositionOutcome outcome) =>
        AdoptRotation(outcome.YawDegrees, outcome.PitchDegrees);

    /// <summary>Adopts a facing the server dictated, converting degrees to the camera's radians.</summary>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    private void AdoptRotation(float yawDegrees, float pitchDegrees)
    {
        _camera.SetLook(LookConvention.FromWireYawDegrees(yawDegrees), LookConvention.FromWirePitchDegrees(pitchDegrees));
    }

    private void PublishTick()
    {
        if (_live is null || _sync is null || !_live.IsAlive)
        {
            return;
        }

        var yawDegrees = LookConvention.ToWireYawDegrees(_camera.Yaw);
        var pitchDegrees = LookConvention.ToWirePitchDegrees(_camera.Pitch);

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
    /// <summary>
    /// Installs the chunks the pool decoded, a few per frame so a burst cannot stall the frame. Installing is
    /// the only world mutation here, and it happens on the game thread.
    /// </summary>
    private void ApplyParsedChunks()
    {
        if (_pool is null || _world is null)
        {
            return;
        }

        for (var installed = 0; installed < ParsedChunksPerFrame && _pool.TryTakeParsed(out var parsed); installed++)
        {
            _world.AddChunk(parsed);
            _geometryDirty = true;
        }
    }

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
                        AdoptServerRotation(outcome);
                        _interpolator.Snap(_player, _camera.Yaw);

                        if (outcome.TeleportId is { } teleportId)
                        {
                            _live.Send(PlayMovementPackets.BuildAcceptTeleportation(
                                teleportId,
                                _player.Position.X,
                                _player.Position.Y,
                                _player.Position.Z,
                                outcome.YawDegrees,
                                outcome.PitchDegrees));
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.LevelChunkWithLight:
                    if (_world is null)
                    {
                        break;
                    }

                    // Decoding a chunk is the expensive half of streaming, so it runs on the pool. The world
                    // itself is only ever written from this thread, which then installs the decoded chunk
                    // (docs/specs/network/play-movement.md 7.24).
                    if (_pool is not null && _pool.TryEnqueueParse(body))
                    {
                        break;
                    }

                    if (ChunkPacketParser.TryRead(body, _world.SectionCount, _world.MinSectionY, airState: 0, out var parsed) == DecodeStatus.Success &&
                        parsed is not null)
                    {
                        _world.AddChunk(parsed);
                        _geometryDirty = true;
                    }

                    break;


                case PacketIds.PlayClientbound.SystemChat:
                    if (ChatPackets.TryReadSystemChat(body[reader.Position..], out var chatText, out var overlay) ==
                        DecodeStatus.Success)
                    {
                        // The overlay flag marks an action bar line, which is drawn elsewhere in the reference; until
                        // that exists, keeping it out of the log is the honest choice.
                        if (!overlay)
                        {
                            _chatLog.Add(chatText, _chatTicks);

                            if (_diagEnabled)
                            {
                                Console.WriteLine($"chat received: {chatText}");
                            }
                        }
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerInfoUpdate:
                    if (PlayerListPackets.TryReadPlayerInfoUpdate(body[reader.Position..], out _, out var profileUpdates) ==
                        DecodeStatus.Success)
                    {
                        _playerList.Apply(profileUpdates);
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerInfoRemove:
                    if (PlayerListPackets.TryReadPlayerInfoRemove(body[reader.Position..], out var removedProfiles) ==
                        DecodeStatus.Success)
                    {
                        _playerList.Remove(removedProfiles);
                    }

                    break;

                case PacketIds.PlayClientbound.AddEntity:
                    if (EntityPackets.TryReadAddEntity(body[reader.Position..], out var added) == DecodeStatus.Success &&
                        added is not null)
                    {
                        _entities.Add(added);
                    }

                    break;

                case PacketIds.PlayClientbound.RemoveEntities:
                    if (EntityPackets.TryReadRemoveEntities(body[reader.Position..], out var removedIds) == DecodeStatus.Success)
                    {
                        _entities.Remove(removedIds);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityPos:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: true, hasRotation: false, out var moved) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(moved);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityPosRot:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: true, hasRotation: true, out var movedRot) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(movedRot);
                    }

                    break;

                case PacketIds.PlayClientbound.MoveEntityRot:
                    if (EntityPackets.TryReadMoveEntity(body[reader.Position..], hasPosition: false, hasRotation: true, out var rotated) == DecodeStatus.Success)
                    {
                        _entities.ApplyMove(rotated);
                    }

                    break;

                case PacketIds.PlayClientbound.RotateHead:
                    if (EntityPackets.TryReadRotateHead(body[reader.Position..], out var headId, out var headYaw) == DecodeStatus.Success)
                    {
                        _entities.ApplyHeadYaw(headId, headYaw);
                    }

                    break;

                case PacketIds.PlayClientbound.SetEntityMotion:
                    if (EntityPackets.TryReadSetEntityMotion(body[reader.Position..], out var motion) == DecodeStatus.Success)
                    {
                        _entities.ApplyMotion(motion);
                    }

                    break;

                case PacketIds.PlayClientbound.TeleportEntity:
                    if (EntityPackets.TryReadTeleportEntity(body[reader.Position..], out var teleport) == DecodeStatus.Success)
                    {
                        _entities.ApplyTeleport(teleport);
                    }

                    break;

                case PacketIds.PlayClientbound.BlockUpdate:
                    if (_world is not null &&
                        BlockUpdatePackets.TryReadBlockUpdate(body[reader.Position..], out var block) == DecodeStatus.Success)
                    {
                        _world.SetBlockState(block.X, block.Y, block.Z, block.State);
                        _geometryDirty = true;
                    }

                    break;

                case PacketIds.PlayClientbound.SectionBlocksUpdate:
                    if (_world is not null &&
                        BlockUpdatePackets.TryReadSectionBlocksUpdate(body[reader.Position..], out var blocks) == DecodeStatus.Success)
                    {
                        foreach (var change in blocks)
                        {
                            _world.SetBlockState(change.X, change.Y, change.Z, change.State);
                        }

                        _geometryDirty = true;
                    }

                    break;

                case PacketIds.PlayClientbound.PlayerRotation:
                    // Server-driven turning, for example a forced facing: the facing is authoritative.
                    if (_sync is not null &&
                        PlayMovementPackets.TryReadPlayerRotation(body[reader.Position..], out var rotation) == DecodeStatus.Success &&
                        rotation is not null)
                    {
                        var (yaw, pitch) = _sync.ApplyServerRotation(rotation);
                        AdoptRotation(yaw, pitch);
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
