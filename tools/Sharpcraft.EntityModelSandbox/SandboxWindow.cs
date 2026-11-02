// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Platform;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.EntityModelSandbox;

/// <summary>
/// One-process interactive sandbox: keyboard cycles parts/faces, rotates/flips the selected face, and the window title
/// always shows the current selection. Left-drag orbits the camera. No web page or cross-process sync.
/// </summary>
internal static class SandboxWindow
{
    private static readonly string[] Faces = ["DOWN", "UP", "WEST", "NORTH", "EAST", "SOUTH"];

    public static void Run()
    {
        var skin = SandboxServer.LoadSkin();
        var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, skin.Width, skin.Height, 1)
        {
            U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
        };
        var entity = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var mesher = new EntityModelMesher();
        var state = new Dictionary<string, FaceUvTransform>();
        var partIndex = 0;
        var faceIndex = 0;
        var yaw = 0f;
        var pitch = -0.12f;
        var hasLast = false;
        Vector2 last = default;

        DynamicGeometry Build()
        {
            return mesher.Build([entity], sprite, skin.Width, skin.Height, state);
        }

        var geometry = Build();
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

        WindowedRenderer.Run(
            request,
            0,
            onFrame: (_, window) => window.Title = Title(),
            cameraPerFrame: window =>
            {
                var input = window.Input;

                if (input.JustPressed(InputKey.PageDown))
                {
                    partIndex = (partIndex + 1) % PlayerModelDefinition.AdultParts.Length;
                    faceIndex = 0;
                }
                else if (input.JustPressed(InputKey.PageUp))
                {
                    partIndex = (partIndex - 1 + PlayerModelDefinition.AdultParts.Length) % PlayerModelDefinition.AdultParts.Length;
                    faceIndex = 0;
                }

                if (input.JustPressed(InputKey.RightArrow))
                {
                    faceIndex = (faceIndex + 1) % Faces.Length;
                }
                else if (input.JustPressed(InputKey.LeftArrow))
                {
                    faceIndex = (faceIndex - 1 + Faces.Length) % Faces.Length;
                }

                var key = PlayerModelDefinition.AdultParts[partIndex].Name + "." + Faces[faceIndex];
                var current = state.TryGetValue(key, out var value) ? value : FaceUvTransform.Identity;
                if (input.JustPressed(InputKey.Space))
                {
                    current = current with { Rotation = (current.Rotation + 90) % 360 };
                }
                else if (input.JustPressed(InputKey.LeftShift))
                {
                    current = current with { FlipH = !current.FlipH };
                }
                else if (input.JustPressed(InputKey.LeftControl))
                {
                    current = current with { FlipV = !current.FlipV };
                }

                state[key] = current;

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
                var eye = target + new Vector3(
                    MathF.Sin(yaw) * MathF.Cos(pitch),
                    MathF.Sin(pitch),
                    -MathF.Cos(yaw) * MathF.Cos(pitch)) * 3.4f;
                return (eye, target);
            },
            dynamicGeometry: Build);

        string Title()
        {
            var part = PlayerModelDefinition.AdultParts[partIndex].Name;
            var face = Faces[faceIndex];
            var current = state.TryGetValue(part + "." + face, out var value) ? value : FaceUvTransform.Identity;
            return $"Sharpcraft sandbox | {part}.{face} | PageUp/Down part, Left/Right face, Space rotate, Shift flipH, Ctrl flipV | rot={current.Rotation} flipH={current.FlipH} flipV={current.FlipV}";
        }
    }
}
