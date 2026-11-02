// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Platform;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.EntityModelSandbox;

/// <summary>Opens a real WGPU window showing the player model with a left-drag orbit camera.</summary>
internal static class SandboxWindow
{
    public static void Run()
    {
        var skin = SandboxServer.LoadSkin();
        var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, skin.Width, skin.Height, 1)
        {
            U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
        };
        var entity = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var mesher = new EntityModelMesher();
        var geometry = mesher.Build([entity], sprite, skin.Width, skin.Height);
        var request = new FrameRequest(
            Width: 900,
            Height: 700,
            Vertices: geometry.Vertices.AsSpan(0, geometry.VertexFloats).ToArray(),
            Indices: geometry.Indices.AsSpan(0, geometry.IndexCount).ToArray(),
            ClearColour: new Vector4(0.16f, 0.19f, 0.24f, 1f),
            CameraPosition: new Vector3(0f, 1.1f, -3.2f),
            CameraTarget: new Vector3(0f, 1.0f, 0f),
            FieldOfViewDegrees: 60f)
        {
            Atlas = skin,
            SkipGradientSky = true,
            NearestTextureFilter = true
        };

        var yaw = 0f;
        var pitch = -0.12f;
        var hasLast = false;
        Vector2 last = default;
        WindowedRenderer.Run(
            request,
            0,
            cameraPerFrame: window =>
            {
                var input = window.Input;
                var cursor = input.CursorInCanvas;
                if (input.LeftMouseDown)
                {
                    if (hasLast)
                    {
                        yaw += (cursor.X - last.X) * 0.012f;
                        pitch = Math.Clamp(pitch + (cursor.Y - last.Y) * 0.012f, -1.2f, 1.2f);
                    }

                    last = cursor;
                    hasLast = true;
                }
                else
                {
                    hasLast = false;
                }

                var target = new Vector3(0f, 1.0f, 0f);
                var distance = 3.4f;
                var eye = target + new Vector3(
                    MathF.Sin(yaw) * MathF.Cos(pitch),
                    MathF.Sin(pitch),
                    -MathF.Cos(yaw) * MathF.Cos(pitch)) * distance;
                return (eye, target);
            },
            dynamicGeometry: () => geometry);
    }
}
