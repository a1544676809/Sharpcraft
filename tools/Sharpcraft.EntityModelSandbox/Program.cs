// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

if (args.Contains("--window"))
{
    Sharpcraft.EntityModelSandbox.SandboxWindow.Run();
    return;
}

if (args.Contains("--serve"))
{
    Sharpcraft.EntityModelSandbox.SandboxServer.Run();
    return;
}

var customSkin = Environment.GetEnvironmentVariable("MC_SANDBOX_SKIN") ?? @"E:\\Users\\14947\\Downloads\\my_skin.png";
        byte[] imageBytes;
        if (File.Exists(customSkin))
        {
            imageBytes = File.ReadAllBytes(customSkin);
        }
        else
        {
            var packPath = Environment.GetEnvironmentVariable("MC_ASSETS_PACK")
                ?? @"C:\\Users\\14947\\AppData\\Local\\MinecraftCSharp\\packs\\minecraft-26.3-a51bffa6a64c.pack.zip";
            using var archive = ZipFile.OpenRead(packPath);
            var entry = archive.GetEntry("assets/minecraft/textures/entity/player/wide/steve.png")
                ?? throw new InvalidDataException("player skin entry missing from pack");
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            imageBytes = memory.ToArray();
        }

        var image = PngDecoder.Decode(imageBytes);
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
DumpFaceBasis(geometry);
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
        SkipGradientSky = true,
        NearestTextureFilter = true
    };
    var frame = OffscreenRenderer.Render(request);
    var output = Path.Combine(Path.GetTempPath(), $"entity-model-{view.Name}.png");
    File.WriteAllBytes(output, PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba));
    Console.WriteLine($"sandbox {view.Name} written to {output} ({frame.AdapterName})");
}

static void DumpFaceBasis(Sharpcraft.Framework.Rendering.DynamicGeometry geometry)
{
    var faceNames = new[] { "DOWN", "UP", "WEST", "NORTH", "EAST", "SOUTH" };
    var parts = Sharpcraft.Client.Render.PlayerModelDefinition.AdultParts;
    var vertex = 0;
    foreach (var part in parts)
    {
        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        var partCentre = new System.Numerics.Vector3(part.CentreX * px, (part.Bottom + (part.Height * 0.5f)) * px, 0f);
        for (var face = 0; face < 6; face++)
        {
            var p = new System.Numerics.Vector3[4];
            var uv = new System.Numerics.Vector2[4];
            for (var corner = 0; corner < 4; corner++, vertex++)
            {
                var o = vertex * 14;
                p[corner] = new System.Numerics.Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]);
                uv[corner] = new System.Numerics.Vector2(geometry.Vertices[o + 6], geometry.Vertices[o + 7]);
            }

            var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(p[1] - p[0], p[2] - p[0]));
            var centre = (p[0] + p[1] + p[2] + p[3]) * 0.25f;
            if (System.Numerics.Vector3.Dot(normal, centre - partCentre) < 0f)
            {
                normal = -normal;
            }

            var minU = uv[0].X; var maxU = uv[0].X; var minV = uv[0].Y; var maxV = uv[0].Y;
            foreach (var t in uv)
            {
                minU = MathF.Min(minU, t.X); maxU = MathF.Max(maxU, t.X);
                minV = MathF.Min(minV, t.Y); maxV = MathF.Max(maxV, t.Y);
            }

            var uVec = System.Numerics.Vector3.Zero;
            var vVec = System.Numerics.Vector3.Zero;
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++)
                {
                    if (uv[j].X > uv[i].X) uVec += p[j] - p[i];
                    if (uv[j].Y > uv[i].Y) vVec += p[j] - p[i];
                }
            }

            uVec = System.Numerics.Vector3.Normalize(uVec);
            vVec = System.Numerics.Vector3.Normalize(vVec);
            Console.WriteLine($"{part.Name,-9} {faceNames[face],-5} normal=({normal.X,4:0.0},{normal.Y,4:0.0},{normal.Z,4:0.0}) U->({uVec.X,4:0.0},{uVec.Y,4:0.0},{uVec.Z,4:0.0}) V->({vVec.X,4:0.0},{vVec.Y,4:0.0},{vVec.Z,4:0.0})");
        }
    }
}