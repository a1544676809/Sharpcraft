// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text.Json;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.EntityModelSandbox;

/// <summary>Small local bridge for Face UV Studio: page POSTs per-face state and polls a rendered PNG.</summary>
internal static class SandboxServer
{
    private const string Prefix = "http://127.0.0.1:19876/";
    private static byte[] _skinPng = [];

    public static void Run()
    {
        var skin = LoadSkin();
        var state = new Dictionary<string, FaceState>();
        var current = Render(skin, state);

        using var listener = new HttpListener();
        listener.Prefixes.Add(Prefix);
        listener.Start();
        Console.WriteLine($"sandbox server listening on {Prefix}");

        while (true)
        {
            var context = listener.GetContext();
            try
            {
                Handle(context, skin, state, ref current);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"sandbox request failed: {exception.Message}");
                try { context.Response.Abort(); } catch { }
            }
        }
    }

    private static void Handle(HttpListenerContext context, TextureData skin, Dictionary<string, FaceState> state, ref byte[] current)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (path == "/state" && context.Request.HttpMethod == "POST")
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var json = reader.ReadToEnd();
            state.Clear();
            using var document = JsonDocument.Parse(json);
            foreach (var part in document.RootElement.EnumerateObject())
            {
                foreach (var face in part.Value.EnumerateObject())
                {
                    var value = face.Value;
                    state[part.Name + "." + face.Name] = new FaceState(
                        value.GetProperty("rot").GetInt32(),
                        value.GetProperty("flipH").GetBoolean(),
                        value.GetProperty("flipV").GetBoolean());
                }
            }

            current = Render(skin, state);
            TryWrite(context, 200, "application/json", "{\"ok\":true}"u8.ToArray());
            return;
        }

        if (path == "/frame.png")
        {
            TryWrite(context, 200, "image/png", current);
            return;
        }

        if (path == "/skin.png")
        {
            TryWrite(context, 200, "image/png", _skinPng);
            return;
        }

        if (path == "/" || path == "/index.html")
        {
            var htmlPath = Path.Combine("tools", "FaceUvStudio", "index.html");
            var html = File.Exists(htmlPath) ? File.ReadAllText(htmlPath) : "<h1>FaceUvStudio/index.html not found</h1>";
            TryWrite(context, 200, "text/html; charset=utf-8", System.Text.Encoding.UTF8.GetBytes(html));
            return;
        }

        TryWrite(context, 404, "text/plain", "not found"u8.ToArray());
    }

    private static void TryWrite(HttpListenerContext context, int status, string contentType, byte[] body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        context.Response.ContentLength64 = body.Length;
        context.Response.OutputStream.Write(body);
        context.Response.OutputStream.Close();
    }

    private static TextureData LoadSkin()
    {
        var custom = Environment.GetEnvironmentVariable("MC_SANDBOX_SKIN") ?? @"E:\Users\14947\Downloads\my_skin.png";
        if (File.Exists(custom))
        {
            var image = PngDecoder.Decode(File.ReadAllBytes(custom));
            _skinPng = PngEncoder.EncodeRgba(image.Width, image.Height, image.Pixels);
            return new TextureData(image.Width, image.Height, image.Pixels);
        }

        var pack = Environment.GetEnvironmentVariable("MC_ASSETS_PACK")
            ?? @"C:\Users\14947\AppData\Local\MinecraftCSharp\packs\minecraft-26.3-a51bffa6a64c.pack.zip";
        using var archive = System.IO.Compression.ZipFile.OpenRead(pack);
        var entry = archive.GetEntry("assets/minecraft/textures/entity/player/wide/steve.png")
            ?? throw new InvalidDataException("player skin entry missing from pack");
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var fallback = PngDecoder.Decode(memory.ToArray());
        _skinPng = PngEncoder.EncodeRgba(fallback.Width, fallback.Height, fallback.Pixels);
        return new TextureData(fallback.Width, fallback.Height, fallback.Pixels);
    }

    private static byte[] Render(TextureData skin, IReadOnlyDictionary<string, FaceState> states)
    {
        var transforms = new Dictionary<string, FaceUvTransform>();
        foreach (var pair in states)
        {
            transforms[pair.Key] = new FaceUvTransform(pair.Value.Rot, pair.Value.FlipH, pair.Value.FlipV);
        }

        var sprite = new SpriteRect(0, 0, skin.Width, skin.Height, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
        var entity = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var mesher = new EntityModelMesher();
        var geometry = mesher.Build([entity], sprite, skin.Width, skin.Height, transforms);
        var request = new FrameRequest(
            Width: 512,
            Height: 512,
            Vertices: geometry.Vertices.AsSpan(0, geometry.VertexFloats).ToArray(),
            Indices: geometry.Indices.AsSpan(0, geometry.IndexCount).ToArray(),
            ClearColour: new System.Numerics.Vector4(0.16f, 0.19f, 0.24f, 1f),
            CameraPosition: new System.Numerics.Vector3(0f, 1.1f, -3f),
            CameraTarget: new System.Numerics.Vector3(0f, 1.0f, 0f),
            FieldOfViewDegrees: 70f)
        {
            Atlas = skin,
            SkipGradientSky = true,
            NearestTextureFilter = true
        };
        var frame = OffscreenRenderer.Render(request);
        return PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
    }

    private sealed record FaceState(int Rot, bool FlipH, bool FlipV);
}
