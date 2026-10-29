// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
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
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;
    private bool _captureRequested;

    private InteractiveSession(PlayerEntity player) => _player = player;

    /// <summary>Creates a session with the player standing on the highest block under the view centre.</summary>
    /// <param name="world">World the player walks in.</param>
    /// <param name="request">Frame request, used for the spawn area and the vertex count.</param>
    public static InteractiveSession Create(ClientWorld world, FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);

        var adapter = new EntityWorldAdapter(world);
        var spawn = FindSpawn(world, request);
        return new InteractiveSession(new PlayerEntity(adapter, spawn));
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
        _camera.ApplyLook(frame.LookDelta, _mapper.LookSensitivity);

        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;

        _loop.Advance(elapsed, () => _player.Tick(frame.Forward, frame.Strafe, frame.Jump, frame.Sprint, _camera.Yaw));

        if (frame.ReleaseCursor)
        {
            window.SetMouseCaptured(false);
            _captureRequested = false;
        }

        var eye = _player.EyePosition;
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
}
