// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

var packPath = Environment.GetEnvironmentVariable("MC_ASSETS_PACK")
    ?? @"C:\Users\14947\AppData\Local\MinecraftCSharp\packs\minecraft-26.3-a51bffa6a64c.pack.zip";
using var archive = ZipFile.OpenRead(packPath);
var entry = archive.GetEntry("assets/minecraft/textures/entity/player/wide/steve.png")
    ?? throw new InvalidDataException("player skin entry missing from pack");
using var stream = entry.Open();
using var memory = new MemoryStream();
stream.CopyTo(memory);
var image = PngDecoder.Decode(memory.ToArray());
var skin = new TextureData(image.Width, image.Height, image.Pixels);
var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, image.Width, image.Height, 1)
{
    U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f
};

var states = new[]
{
    new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f)
};
var mesher = new EntityModelMesher();
var geometry = mesher.Build(states, sprite, image.Width, image.Height);
var views = new (string Name, System.Numerics.Vector3 Eye, System.Numerics.Vector3 Target)[]
{
    ("front", new(0f, 1.1f, -3f), new(0f, 1.0f, 0f)),
    ("back", new(0f, 1.1f, 3f), new(0f, 1.0f, 0f)),
    ("left", new(-3f, 1.1f, 0f), new(0f, 1.0f, 0f)),
    ("right", new(3f, 1.1f, 0f), new(0f, 1.0f, 0f)),
    ("shoulders", new(0f, 2.3f, -2.5f), new(0f, 1.2f, 0f))
};

foreach (var view in views)
{
    var request = new FrameRequest(
        Width: 512,
        Height: 512,
        Vertices: geometry.Vertices.AsSpan(0, geometry.VertexFloats).ToArray(),
        Indices: geometry.Indices.AsSpan(0, geometry.IndexCount).ToArray(),
        ClearColour: new System.Numerics.Vector4(0.55f, 0.72f, 0.95f, 1f),
        CameraPosition: view.Eye,
        CameraTarget: view.Target,
        FieldOfViewDegrees: 70f)
    {
        Atlas = skin,
        SkipGradientSky = true
    };
    var frame = OffscreenRenderer.Render(request);
    var output = Path.Combine(Path.GetTempPath(), $"entity-model-{view.Name}.png");
    File.WriteAllBytes(output, PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba));
    Console.WriteLine($"sandbox {view.Name} written to {output} ({frame.AdapterName})");
}
