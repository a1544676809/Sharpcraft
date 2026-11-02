// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Text.Json;
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
        var logicalWidth = skin.Width == 1600 ? 64 : skin.Width;
        var logicalHeight = skin.Height == 1600 ? 64 : skin.Height;
        var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, skin.Width, skin.Height, 1)
        {
            U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
        };
        var entity = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var mesher = new EntityModelMesher();
        var state = new Dictionary<string, FaceUvTransform>();
        var statePath = Path.Combine(Path.GetTempPath(), "sharpcraft-face-state.json");
        var partIndex = 0;
        var faceIndex = 0;
        var isolateSelectedPart = false;
        var hideSelectedPart = false;
        var yaw = 0f;
        var pitch = -0.12f;
        var hasLast = false;
        Vector2 last = default;

        DynamicGeometry Build()
        {
            var partName = PlayerModelDefinition.AdultParts[partIndex].Name;
            var geometry = mesher.Build([entity], sprite, logicalWidth, logicalHeight, state,
                onlyPart: isolateSelectedPart ? partName : null,
                excludedPart: hideSelectedPart ? partName : null);
            if (!hideSelectedPart)
            {
                AppendSelectionHighlight(geometry, sprite, PlayerModelDefinition.AdultParts[partIndex], Faces[faceIndex]);
            }

            return geometry;
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

                if (input.JustPressed(InputKey.Home))
                {
                    isolateSelectedPart = !isolateSelectedPart;
                }
                else if (input.JustPressed(InputKey.End))
                {
                    hideSelectedPart = !hideSelectedPart;
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

                var changed = !current.Equals(state.TryGetValue(key, out var previous) ? previous : FaceUvTransform.Identity);
                state[key] = current;
                if (changed || input.JustPressed(InputKey.T))
                {
                    DumpState(statePath, state);
                }

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
            var current = state.TryGetValue(part + "." + face, out var value)
                ? value
                : FaceUvTransform.Identity;
            return $"Sharpcraft sandbox | {part}.{face} | PageUp/Down part, Left/Right face, Space rotate, Shift flipH, Ctrl flipV, Home isolate, End hide | rot={current.Rotation} flipH={current.FlipH} flipV={current.FlipV} | isolate={isolateSelectedPart} hide={hideSelectedPart}";
        }
    }

    private static void DumpState(string path, IReadOnlyDictionary<string, FaceUvTransform> state)
    {
        var all = new SortedDictionary<string, FaceUvTransform>(StringComparer.Ordinal);
        foreach (var part in PlayerModelDefinition.AdultParts)
        {
            foreach (var face in Faces)
            {
                var key = part.Name + "." + face;
                all[key] = state.TryGetValue(key, out var value) ? value : FaceUvTransform.Identity;
            }
        }

        var json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Console.WriteLine($"face state written to {path}");
    }

    private static void AppendSelectionHighlight(DynamicGeometry geometry, Sharpcraft.Client.Assets.SpriteRect sprite, EntityModelPart part, string face)
    {
        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        var centreX = part.CentreX * px;
        var bottom = part.Bottom * px;
        var halfW = part.Width * px * 0.5f;
        var halfD = part.Depth * px * 0.5f;
        var min = new Vector3(centreX - halfW, bottom, -halfD);
        var max = new Vector3(centreX + halfW, bottom + (part.Height * px), halfD);
        var t0 = new Vector3(min.X, min.Y, min.Z);
        var t1 = new Vector3(max.X, min.Y, min.Z);
        var t2 = new Vector3(max.X, max.Y, min.Z);
        var t3 = new Vector3(min.X, max.Y, min.Z);
        var l0 = new Vector3(min.X, min.Y, max.Z);
        var l1 = new Vector3(max.X, min.Y, max.Z);
        var l2 = new Vector3(max.X, max.Y, max.Z);
        var l3 = new Vector3(min.X, max.Y, max.Z);
        var (a, b, c, d, normal) = face switch
        {
            "DOWN" => (l1, l0, t0, t1, new Vector3(0, -1, 0)),
            "UP" => (t2, t3, l3, l2, new Vector3(0, 1, 0)),
            "WEST" => (t0, l0, l3, t3, new Vector3(-1, 0, 0)),
            "NORTH" => (t1, t0, t3, t2, new Vector3(0, 0, -1)),
            "EAST" => (l1, t1, t2, l2, new Vector3(1, 0, 0)),
            _ => (l0, l1, l2, l3, new Vector3(0, 0, 1))
        };
        a += normal * 0.01f;
        b += normal * 0.01f;
        c += normal * 0.01f;
        d += normal * 0.01f;
        var first = (uint)(geometry.VertexFloats / 14);
        AppendVertex(geometry, a, sprite);
        AppendVertex(geometry, b, sprite);
        AppendVertex(geometry, c, sprite);
        AppendVertex(geometry, d, sprite);
        geometry.Indices[geometry.IndexCount++] = first;
        geometry.Indices[geometry.IndexCount++] = first + 1;
        geometry.Indices[geometry.IndexCount++] = first + 2;
        geometry.Indices[geometry.IndexCount++] = first;
        geometry.Indices[geometry.IndexCount++] = first + 2;
        geometry.Indices[geometry.IndexCount++] = first + 3;
    }

    private static void AppendVertex(DynamicGeometry geometry, Vector3 p, Sharpcraft.Client.Assets.SpriteRect sprite)
    {
        var o = geometry.VertexFloats;
        geometry.Vertices[o++] = p.X;
        geometry.Vertices[o++] = p.Y;
        geometry.Vertices[o++] = p.Z;
        geometry.Vertices[o++] = 0.2f;
        geometry.Vertices[o++] = 1f;
        geometry.Vertices[o++] = 0.2f;
        geometry.Vertices[o++] = 0.5f;
        geometry.Vertices[o++] = 0.5f;
        geometry.Vertices[o++] = sprite.U0;
        geometry.Vertices[o++] = sprite.V0;
        geometry.Vertices[o++] = sprite.U1;
        geometry.Vertices[o++] = sprite.V1;
        geometry.Vertices[o++] = 1f;
        geometry.Vertices[o++] = 0f;
        geometry.VertexFloats = o;
    }
}
