// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Text.Json;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
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
        using var resources = CreateResourceManager();
        TextureData? playerOverride = null;
        var customSkinPath = Environment.GetEnvironmentVariable("MC_SANDBOX_SKIN");
        if (customSkinPath is { Length: > 0 } && File.Exists(customSkinPath))
        {
            var image = PngDecoder.Decode(File.ReadAllBytes(customSkinPath));
            playerOverride = new TextureData(image.Width, image.Height, image.Pixels);
        }

        var skinLibrary = EntitySkinLibrary.Load(resources, playerOverride);
        var atlas = skinLibrary.Atlas ?? throw new InvalidDataException("no entity skin could be loaded from the asset pack");
        var requestedModel = Environment.GetEnvironmentVariable("MC_SANDBOX_MODEL") ?? "player";
        var typeId = EntityModelResolver.FindTypeId(requestedModel) ?? EntityPlaceholderMesher.PlayerEntityTypeId;
        var variantTable = EntityVariantTable.Builtin;
        var variantId = 0;
        if (Environment.GetEnvironmentVariable("MC_SANDBOX_VARIANT") is { Length: > 0 } requestedVariant)
        {
            if (!EntityVariantSpecs.TryGet(typeId, out var variantSpec))
            {
                throw new InvalidDataException($"model '{requestedModel}' has no variants");
            }

            variantId = -1;
            for (var i = 0; i < variantSpec.Keys.Count; i++)
            {
                if (string.Equals(variantSpec.Keys[i], requestedVariant, StringComparison.Ordinal))
                {
                    variantId = i;
                    break;
                }
            }

            if (variantId < 0)
            {
                throw new InvalidDataException($"unknown variant '{requestedVariant}' for '{requestedModel}'");
            }
        }

        var model = variantTable.Resolve(typeId, variantId);
        var sprite = atlas.Get(model.TexturePath) ?? throw new InvalidDataException($"skin {model.TexturePath} missing from atlas");
        var sandboxState = Environment.GetEnvironmentVariable("MC_SANDBOX_STATE") ?? string.Empty;
        var entity = new EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f,
            HasChest: Environment.GetEnvironmentVariable("MC_SANDBOX_CHEST") == "1",
            HasEgg: Environment.GetEnvironmentVariable("MC_SANDBOX_EGG") == "1",
            VariantId: variantId,
            IsTame: sandboxState.Contains("tame", StringComparison.OrdinalIgnoreCase),
            IsAngry: sandboxState.Contains("angry", StringComparison.OrdinalIgnoreCase),
            IsSleeping: sandboxState.Contains("sleeping", StringComparison.OrdinalIgnoreCase),
            HasNectar: sandboxState.Contains("nectar", StringComparison.OrdinalIgnoreCase));
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

        IReadOnlyList<EntityGeometryLayer> Build()
        {
            var parts = model.Parts;
            var partName = parts[partIndex % parts.Count].Name;
            var layers = mesher.BuildLayers([entity], sprite, sprite.Width, sprite.Height, state,
                onlyPart: isolateSelectedPart ? partName : null,
                excludedPart: hideSelectedPart ? partName : null,
                skinAtlas: atlas,
                variants: variantTable);
            if (!hideSelectedPart)
            {
                foreach (var layer in layers)
                {
                    if (layer.Kind == EntityGeometryLayerKind.Cutout)
                    {
                        AppendSelectionHighlight(layer.Geometry, sprite, parts[partIndex % parts.Count], Faces[faceIndex]);
                        break;
                    }
                }
            }

            return layers;
        }

        var request = new FrameRequest(
            Width: 900,
            Height: 700,
            Vertices: [],
            Indices: [],
            ClearColour: new Vector4(0.16f, 0.19f, 0.24f, 1f),
            CameraPosition: new Vector3(0f, 1.1f, -3.2f),
            CameraTarget: new Vector3(0f, 1.0f, 0f),
            FieldOfViewDegrees: 60f)
        {
            Atlas = atlas.Texture,
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
                    partIndex = (partIndex + 1) % model.Parts.Count;
                    faceIndex = 0;
                }
                else if (input.JustPressed(InputKey.PageUp))
                {
                    partIndex = (partIndex - 1 + model.Parts.Count) % model.Parts.Count;
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

                var key = model.Parts[partIndex % model.Parts.Count].Name + "." + Faces[faceIndex];
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
                    DumpState(statePath, state, model.Parts);
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
            dynamicGeometry: null,
            entityLayers: Build);

        string Title()
        {
            var part = model.Parts[partIndex % model.Parts.Count].Name;
            var face = Faces[faceIndex];
            var current = state.TryGetValue(part + "." + face, out var value)
                ? value
                : FaceUvTransform.Identity;
            return $"Sharpcraft sandbox | {part}.{face} | PageUp/Down part, Left/Right face, Space rotate, Shift flipH, Ctrl flipV, Home isolate, End hide | rot={current.Rotation} flipH={current.FlipH} flipV={current.FlipV} | isolate={isolateSelectedPart} hide={hideSelectedPart}";
        }
    }

    private static void DumpState(string path, IReadOnlyDictionary<string, FaceUvTransform> state, IReadOnlyList<EntityModelPart> parts)
    {
        var all = new SortedDictionary<string, FaceUvTransform>(StringComparer.Ordinal);
        foreach (var part in parts)
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

    private static ResourceManager CreateResourceManager()
    {
        var pack = Environment.GetEnvironmentVariable("MC_ASSETS_PACK")
            ?? throw new InvalidOperationException("MC_ASSETS_PACK must point at a Sharpcraft asset pack");
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new BuiltInFallbackSource());
        if (File.Exists(pack))
        {
            stack.Add(new ZipAssetSource(pack, "base-pack"));
        }

        return new ResourceManager(stack, 0);
    }
}
