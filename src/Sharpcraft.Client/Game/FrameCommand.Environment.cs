// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Game;

internal static partial class FrameCommand
{
    // Decoded celestial sprites are reused across frames: the window rebuilds the sun/moon/star geometry per frame but
    // must not re-read and re-decode the PNGs each time.
    private static readonly Dictionary<Sharpcraft.Common.Identifiers.Identifier, TextureData> CelestialTextureCache = [];

    // Host diagnostics: print the rebuilt celestial state only when MC_DIAG is on (rebuilds now run every frame).
    private static bool LogCelestial => GameOptions.Flag("MC_DIAG");


    /// <summary>
    /// Renders a sky-only frame with a body (sun, or the moon under MC_MOON_TEST) at angle 0 and a camera aimed straight
    /// at it, proving the celestial pipeline draws at the position the CPU projection predicts. Returns the PNG path.
    /// </summary>
    internal static string RenderSunTest(int width, int height)
    {
        // MC_MOON_TEST switches to the moon (size 20) so the moon sprite can be verified the same way as the sun.
        var isMoon = GameOptions.Get("MC_MOON_TEST") is { Length: > 0 };
        var bodySize = isMoon ? 20f : 30f;
        var label = isMoon ? "moon" : "sun";

        var (vertices, indices) = CelestialMeshBuilder.CelestialQuad(
            0f, height: 100f, size: bodySize, uv: (0f, 0f, 1f, 1f));

        // A 2x2 pure-white texture by default so a lit pixel is unambiguous against the blue sky and no pack is needed.
        TextureData bodyTexture = new(2, 2,
        [
            255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255
        ]);

        // With MC_SUN_TEST_PACK set, draw the real sprite from the pack instead, to check its orientation and UVs.
        if (GameOptions.Get("MC_SUN_TEST_PACK") is { Length: > 0 })
        {
            using var resources = CreateResourceManager();
            var identifier = isMoon ? CelestialSprites.MoonPhase(0) : CelestialSprites.Sun;
            var bytes = resources.Read(ResourceKind.Texture, identifier);
            if (bytes is not null)
            {
                var image = PngDecoder.Decode(bytes);
                bodyTexture = new TextureData(image.Width, image.Height, image.Pixels);
                Console.WriteLine($"{label} test: real sprite {identifier.Path} {image.Width}x{image.Height}");
            }
        }

        var draw = new CelestialQuadDraw(0, vertices, indices, Matrix4x4.Identity, Vector4.One);

        // Celestial geometry is built on a dome around the origin and drawn with a translation free camera matrix, so the
        // eye sits at the origin (docs: render-backend-seam.md 9.39). The target is the body's centre with a token tilt,
        // because a look-at straight up the Y axis is degenerate; the centre pixel stays on the body either way.
        var request = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            Vector3.Zero,
            new Vector3(0f, 100f, 0.5f),
            60f)
        {
            CelestialTextures = [bodyTexture],
            CelestialDraws = [draw]
        };

        var frame = OffscreenRenderer.Render(request);

        // The centre pixel is on the ray to the body's centroid, so it must be the disc, not the sky gradient.
        var centreX = frame.Width / 2;
        var centreY = frame.Height / 2;
        var offset = ((centreY * frame.Width) + centreX) * 4;
        Console.WriteLine($"{label} test: centre rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]},{frame.Rgba[offset + 3]})");

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), $"sharpcraft-{label}-test.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// Day time used for lighting and celestial angles. A host override (<c>MC_FORCE_DAYTIME</c>) renders a frame at a chosen
    /// tick for verification; otherwise it is the world clock.
    /// </summary>
    internal static int EffectiveDayTime(ClientWorld world) =>
        GameOptions.Get("MC_FORCE_DAYTIME") is { Length: > 0 } text && int.TryParse(text, out var forced)
            ? forced
            : world.DayTime;

    /// <summary>
    /// Sub-tick fraction for interpolating environment attributes. <c>MC_FORCE_PARTIAL</c> fixes it for verification;
    /// otherwise it is the overworld clock's partial tick (matching the reference EnvironmentAttributeProbe).
    /// </summary>
    internal static float EffectiveDayTimePartial(ClientWorld world) =>
        GameOptions.Get("MC_FORCE_PARTIAL") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f)
            : world.DayTimePartial;

    /// <summary>Rain level at a sub-tick fraction, with <c>MC_FORCE_RAIN</c> override for verification.</summary>
    internal static float EffectiveRainLevel(ClientWorld world, float partialTick) =>
        GameOptions.Get("MC_FORCE_RAIN") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f)
            : world.RainLevel(partialTick);

    /// <summary>Thunder level (already scaled by rain), with <c>MC_FORCE_THUNDER</c> override for verification.</summary>
    internal static float EffectiveThunderLevel(ClientWorld world, float partialTick)
    {
        var rain = EffectiveRainLevel(world, partialTick);
        return GameOptions.Get("MC_FORCE_THUNDER") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f) * rain
            : world.ThunderLevel(partialTick);
    }

    /// <summary>
    /// The lightmap parameters for the current time and weather: the day/night sky factor and colour from the timelines,
    /// then the RAIN/THUNDER layers applied (docs/modules/weather-and-sky-color.md 6.3).
    /// </summary>
    internal static LightmapParameters EffectiveLighting(ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var parameters = LightmapParameters.OverworldAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);

        return parameters with
        {
            SkyFactor = Sharpcraft.Common.World.WeatherBlend.SkyLightFactor(parameters.SkyFactor, rain, thunder),
            SkyLightColor = Sharpcraft.Common.World.WeatherBlend.ApplySkyLightColor(parameters.SkyLightColor, rain, thunder)
        };
    }

    /// <summary>Fog colour for the current day time and weather (base overworld fog × FOG_COLOR timeline × weather).</summary>
    internal static Vector4 EffectiveFogColour(ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var factor = Sharpcraft.Common.World.SkyAttributeTracks.FogColorFactorAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        var fog = Sharpcraft.Common.World.WeatherBlend.ApplyFog(new Vector3(0.75f, 0.85f, 1.0f) * factor, rain, thunder);
        return new Vector4(fog, 1f);
    }

    /// <summary>
    /// The SKY_COLOR at a world position: the biome's calculateSkyColor blended over neighbours, multiplied by the
    /// day/night SKY_COLOR timeline and darkened by weather (docs/modules/weather-and-sky-color.md 6.2).
    /// </summary>
    internal static Vector3 EffectiveSkyColour(ClientWorld world, Sharpcraft.Common.World.BiomeRegistry biomes, int x, int y, int z)
    {
        // The reference clamps the biome sample's quart Y into the world height (ChunkAccess.getNoiseBiome), so a camera
        // above/below the build range still gets the top/bottom section's biome instead of no weight and a black colour.
        var minBlockY = world.MinSectionY * 16;
        var maxBlockY = minBlockY + (world.SectionCount * 16) - 1;
        var sampleY = Math.Clamp(y, minBlockY, maxBlockY);
        var baseSky = Sharpcraft.Common.World.SkyColourSource.ColorAt(world.GetBiomeAt, biomes, x, sampleY, z);
        if (baseSky == Vector3.Zero)
        {
            // A column whose extreme sections are missing still falls back to a loaded section near sea level.
            baseSky = Sharpcraft.Common.World.SkyColourSource.ColorAt(world.GetBiomeAt, biomes, x, 64, z);
        }
        var partialTick = EffectiveDayTimePartial(world);
        var multiplier = Sharpcraft.Common.World.SkyAttributeTracks.SkyColorMultiplierAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        return Sharpcraft.Common.World.WeatherBlend.ApplySkyColour(baseSky * multiplier, rain, thunder);
    }

    /// <summary>
    /// Builds the per-frame cloud position-colour draw: visible faces via CloudMeshBuilder, geometry via
    /// CloudGeometryBuilder, tinted by the CLOUD_COLOR timeline and weather (docs/modules/clouds.md 2).
    /// </summary>
    internal static CelestialColorDraw? BuildCloudDraw(
        ClientWorld world,
        Sharpcraft.Client.Assets.CloudTexture texture,
        Vector3 eye,
        Sharpcraft.Client.Render.CloudDrawCache? cache = null,
        int rangeChunks = 128,
        bool fancy = true,
        float? partialTickOverride = null)
    {
        var partialTick = partialTickOverride ?? EffectiveDayTimePartial(world);
        var offset = (float)(world.GameTime % (texture.Width * 400L)) + partialTick;
        var cloudX = eye.X + (offset * 0.03f);
        var cloudZ = eye.Z + 3.96f;
        var widthBlocks = texture.Width * 12.0;
        var heightBlocks = texture.Height * 12.0;
        cloudX -= (float)(Math.Floor(cloudX / widthBlocks) * widthBlocks);
        cloudZ -= (float)(Math.Floor(cloudZ / heightBlocks) * heightBlocks);
        var cellX = (int)Math.Floor(cloudX / 12.0);
        var cellZ = (int)Math.Floor(cloudZ / 12.0);
        var xInCell = (float)(cloudX - (cellX * 12.0));
        var zInCell = (float)(cloudZ - (cellZ * 12.0));

        const float bottomY = 192.33f;
        var relativeBottom = bottomY - eye.Y;
        var relativeTop = relativeBottom + 4f;
        var side = relativeTop < 0f
            ? Sharpcraft.Client.Render.CloudCameraSide.AboveClouds
            : relativeBottom > 0f
                ? Sharpcraft.Client.Render.CloudCameraSide.BelowClouds
                : Sharpcraft.Client.Render.CloudCameraSide.InsideClouds;

        var radiusCells = (int)MathF.Ceiling(rangeChunks * 16f / 12f);
        float[] vertices;
        uint[] indices;
        int[] faces;
        Vector3 cloudOrigin;
        Matrix4x4 model;

        if (cache is null || !fancy)
        {
            var quads = fancy
                ? Sharpcraft.Client.Render.CloudMeshBuilder.Build(
                    texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells, side)
                : Sharpcraft.Client.Render.CloudMeshBuilder.BuildFlat(
                    texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells);
            if (quads.Count == 0)
            {
                return null;
            }

            // Absolute-vertex path (offscreen one-shot): origin is the eye shifted by the in-cell remainder.
            var origin = Sharpcraft.Client.Render.CloudGeometryBuilder.OriginFromEye(eye, xInCell, zInCell, bottomY);
            (vertices, indices) = Sharpcraft.Client.Render.CloudGeometryBuilder.Build(quads, eye, origin);
            faces = Sharpcraft.Client.Render.CloudFaceTable.Build(quads);
            cloudOrigin = origin;
            model = Matrix4x4.Identity;
        }
        else
        {
            // Cached camera-relative mesh: vertices already carry the vertical offset; move it with the eye each frame.
            (vertices, indices, faces) = cache.Get(
                texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells, side);
            if (vertices.Length == 0)
            {
                return null;
            }

            // Keep the cloud plane at the fixed absolute bottom height; only horizontal offset follows the eye, so a
            // jump adds no vertical snapping.
            var placed = new Vector3(eye.X - xInCell, bottomY, eye.Z - zInCell);
            model = Matrix4x4.CreateTranslation(placed.X, placed.Y, placed.Z);
            cloudOrigin = placed;
        }

        var factor = Sharpcraft.Common.World.SkyAttributeTracks.CloudColorFactorAt(EffectiveDayTime(world));
        var cloudAlpha = Sharpcraft.Common.World.SkyAttributeTracks.CloudColorAlphaAt(EffectiveDayTime(world));
        var rain = EffectiveRainLevel(world, partialTick);
        var tint = new Vector4(
            factor.X * (1f - (0.5f * rain)),
            factor.Y * (1f - (0.5f * rain)),
            factor.Z * (1f - (0.4f * rain)),
            cloudAlpha);

        return new CelestialColorDraw(vertices, indices, model, tint, CelestialColorBlend.Translucent)
        {
            // The transparency passes generate the geometry from this table instead of the vertex array (T-827);
            // the classic path still uses the vertices, which is why both travel here.
            CloudFaces = faces,
            CloudFaceCount = faces.Length / Sharpcraft.Client.Render.CloudFaceTable.WordsPerFace,
            CloudFogEnd = Sharpcraft.Client.Render.CloudGeometryBuilder.DefaultFogEnd,
            CloudOrigin = cloudOrigin,
            CloudEye = eye
        };
    }

    /// <summary>Maps the client lightmap parameters to the framework uniform the terrain shader reads.</summary>
    internal static LightmapUniform ToLightmapUniform(LightmapParameters parameters) => new(
        SkyFactor: parameters.SkyFactor,
        BlockFactor: parameters.BlockFactor,
        BrightnessFactor: parameters.BrightnessFactor,
        Padding: 0f,
        SkyLightColor: new Vector4(parameters.SkyLightColor, 1f),
        BlockLightTint: new Vector4(parameters.BlockLightTint, 1f),
        AmbientColor: new Vector4(parameters.AmbientColor, 0f));

    /// <summary>
    /// Adds the celestial bodies for the world's current day time. The sun (and later the moon, stars and sunrise band)
    /// are attached here so every offscreen path draws the same sky.
    /// </summary>
    internal static FrameRequest AttachCelestial(
        FrameRequest request,
        ClientWorld world,
        Sharpcraft.Assets.ResourceManager resources,
        Vector3? skyColour = null,
        Vector3? fogColour = null)
    {
        var textures = new List<TextureData>();
        var textureIndices = new Dictionary<Sharpcraft.Common.Identifiers.Identifier, int>();
        var draws = new List<CelestialQuadDraw>();
        var colorDraws = new List<CelestialColorDraw>();
        var skyDiscDraws = new List<CelestialColorDraw>();
        var dayTime = EffectiveDayTime(world);
        var partialTick = EffectiveDayTimePartial(world);

        // Rain/thunder levels (host override for verification); rain brightness dims the sun and moon, and rain/thunder
        // dims the stars (WeatherAttributes sets STAR_BRIGHTNESS to 0 under rain/thunder).
        var rainLevel = EffectiveRainLevel(world, partialTick);
        var thunderLevel = EffectiveThunderLevel(world, partialTick);
        var rainBrightness = Sharpcraft.Common.World.WeatherBlend.RainBrightness(rainLevel);

        // The moon phase follows total world ticks (one phase per 24000 ticks); angles follow the day clock.
        var totalTicks = world.TryGetClock(ClientWorld.OverworldClockId, out var clock) ? clock.TotalTicks : world.GameTime;
        var moonPhaseIndex = Sharpcraft.Common.World.SkyAttributeTracks.MoonPhaseIndex(totalTicks);
        var moonAngle = GameOptions.Get("MC_MOON_ANGLE") is { Length: > 0 } moonText &&
            float.TryParse(moonText, out var parsedMoonAngle)
            ? parsedMoonAngle
            : Sharpcraft.Common.World.SkyTimelines.MoonAngleDegrees(dayTime, partialTick);

        // A fixed angle is available for verification; otherwise the sun follows the overworld clock.
        var sunAngle = GameOptions.Get("MC_SUN_ANGLE") is { Length: > 0 } angleText &&
            float.TryParse(angleText, out var parsedAngle)
            ? parsedAngle
            : Sharpcraft.Common.World.SkyTimelines.SunAngleDegrees(dayTime, partialTick);

        // Preload the sun and every moon phase so the texture array has a stable index while the phase advances.
        AddTexture(CelestialSprites.Sun);
        for (var phase = 0; phase < CelestialSprites.MoonPhaseCount; phase++)
        {
            AddTexture(CelestialSprites.MoonPhase(phase));
        }

        AddBody(CelestialSprites.Sun, sunAngle, size: 30f);
        // The reference builds the moon quads with reversed UVs (buildMoonPhases) while the sun uses forward UVs, so the
        // raw phase images are rotated 180 degrees to match the reference's on-screen orientation.
        AddBody(CelestialSprites.MoonPhase(moonPhaseIndex), moonAngle, size: 20f, flip: true);

        // Sunrise/sunset band: a position-colour triangle fan, skipped below the reference alpha cutoff. Rain/thunder
        // multiply the colour (WeatherAttributes SUNRISE_SUNSET_COLOR MULTIPLY_ARGB).
        var sunrise = Sharpcraft.Common.World.WeatherBlend.ApplySunrise(
            Sharpcraft.Common.World.SkyAttributeTracks.SunriseSunsetColorAt(dayTime, partialTick), rainLevel, thunderLevel);
        if (sunrise.W > 0.001f)
        {
            var sinSun = MathF.Sin(sunAngle * (MathF.PI / 180f));
            var theta = ((sinSun < 0f ? 180f : 0f) + 90f) * (MathF.PI / 180f);
            var fan = CelestialGeometry.SunriseFanWorld(theta, sunrise.W);

            var colours = new Vector4[fan.Length];
            colours[0] = new Vector4(1f, 1f, 1f, 1f);
            for (var i = 1; i < colours.Length; i++)
            {
                colours[i] = new Vector4(1f, 1f, 1f, 0f);
            }

            var triangles = new List<uint>((fan.Length - 2) * 3);
            for (var i = 1; i + 1 < fan.Length; i++)
            {
                triangles.Add(0);
                triangles.Add((uint)i);
                triangles.Add((uint)(i + 1));
            }

            AddColor(fan, colours, triangles, sunrise, CelestialColorBlend.Translucent);
        }

        // Stars: the reference star field rotated by the star angle, brightened by the star-brightness attribute.
        var starBrightness = Sharpcraft.Common.World.SkyAttributeTracks.StarBrightnessAt(dayTime, partialTick)
            * (1f - MathF.Max(0f, rainLevel - thunderLevel)) * (1f - thunderLevel);
        if (starBrightness > 0f)
        {
            var starCorners = StarField.Build(out var keptStars);
            var starAngle = Sharpcraft.Common.World.SkyTimelines.StarAngleDegrees(dayTime, partialTick);
            var rotated = new Vector3[starCorners.Length];
            CelestialGeometry.SkyRotate(starCorners, starAngle * (MathF.PI / 180f), rotated);

            var colours = new Vector4[rotated.Length];
            Array.Fill(colours, Vector4.One);
            var triangles = new List<uint>(keptStars * 6);
            for (var quad = 0; quad < keptStars; quad++)
            {
                var b = (uint)(quad * 4);
                triangles.Add(b);
                triangles.Add(b + 1);
                triangles.Add(b + 2);
                triangles.Add(b);
                triangles.Add(b + 2);
                triangles.Add(b + 3);
            }

            AddColor(rotated, colours, triangles, new Vector4(starBrightness, starBrightness, starBrightness, starBrightness), CelestialColorBlend.Overlay);
        }

        if (LogCelestial && (draws.Count > 0 || colorDraws.Count > 0))
        {
            Console.WriteLine(
                $"celestial: {textures.Count} texture(s), {draws.Count} textured draw(s), {colorDraws.Count} colour draw(s), " +
                $"sun {sunAngle:F1} deg, moon {moonAngle:F1} deg (phase {moonPhaseIndex}), sunrise a={sunrise.W:F3}, stars {starBrightness:F3}, rain {rainLevel:F2} (sun/moon alpha {rainBrightness:F2})");
        }

        var effectiveSkyColour = skyColour ?? new Vector3(request.ClearColour.X, request.ClearColour.Y, request.ClearColour.Z);
        var effectiveFogColour = fogColour ?? effectiveSkyColour;
        var skyFogEnd = MathF.Min(InteractiveSession.ViewDistanceChunks * 16f, 512f);
        const int skyRings = 16;
        const int skySegments = 64;
        const float skyRadius = 512f;
        var skyVertexCount = 1 + (skyRings * skySegments);
        var skyVertices = new float[skyVertexCount * 7];

        void WriteSkyVertex(int index, float x, float y, float z, Vector3 colour)
        {
            var offset = index * 7;
            skyVertices[offset] = x;
            skyVertices[offset + 1] = y;
            skyVertices[offset + 2] = z;
            skyVertices[offset + 3] = colour.X;
            skyVertices[offset + 4] = colour.Y;
            skyVertices[offset + 5] = colour.Z;
            skyVertices[offset + 6] = 1f;
        }

        WriteSkyVertex(0, 0f, 16f, 0f, effectiveSkyColour);
        var skyIndex = 1;
        for (var ring = 1; ring <= skyRings; ring++)
        {
            var radius = skyRadius * ring / skyRings;
            var fogFactor = Math.Clamp(radius / MathF.Max(skyFogEnd, 0.001f), 0f, 1f);
            var colour = Vector3.Lerp(effectiveSkyColour, effectiveFogColour, fogFactor);
            for (var segment = 0; segment < skySegments; segment++)
            {
                var angle = segment * MathF.Tau / skySegments;
                WriteSkyVertex(skyIndex++, radius * MathF.Cos(angle), 16f, radius * MathF.Sin(angle), colour);
            }
        }

        var skyTriangles = new List<uint>((skySegments * 3) + ((skyRings - 1) * skySegments * 6));
        for (var segment = 0; segment < skySegments; segment++)
        {
            var next = (segment + 1) % skySegments;
            skyTriangles.Add(0);
            skyTriangles.Add((uint)(1 + segment));
            skyTriangles.Add((uint)(1 + next));
        }

        for (var ring = 1; ring < skyRings; ring++)
        {
            var inner = 1 + ((ring - 1) * skySegments);
            var outer = inner + skySegments;
            for (var segment = 0; segment < skySegments; segment++)
            {
                var next = (segment + 1) % skySegments;
                skyTriangles.Add((uint)(inner + segment));
                skyTriangles.Add((uint)(outer + segment));
                skyTriangles.Add((uint)(outer + next));
                skyTriangles.Add((uint)(inner + segment));
                skyTriangles.Add((uint)(outer + next));
                skyTriangles.Add((uint)(inner + next));
            }
        }

        skyDiscDraws.Add(new CelestialColorDraw(
            skyVertices,
            [.. skyTriangles],
            Matrix4x4.CreateTranslation(request.CameraPosition),
            new Vector4(1f, 1f, 1f, 1f),
            CelestialColorBlend.Opaque));

        return request with
        {
            CelestialTextures = textures.Count > 0 ? [.. textures] : null,
            CelestialDraws = draws.Count > 0 ? [.. draws] : null,
            CelestialColorDraws = colorDraws.Count > 0 ? [.. colorDraws] : null,
            SkyDiscDraws = skyDiscDraws.Count > 0 ? [.. skyDiscDraws] : null
        };

        int AddTexture(Sharpcraft.Common.Identifiers.Identifier identifier)
        {
            if (textureIndices.TryGetValue(identifier, out var existing))
            {
                return existing;
            }

            if (!CelestialTextureCache.TryGetValue(identifier, out var decoded))
            {
                var data = resources.Read(ResourceKind.Texture, identifier);
                if (data is null)
                {
                    return -1;
                }

                var decodedImage = PngDecoder.Decode(data);
                decoded = new TextureData(decodedImage.Width, decodedImage.Height, decodedImage.Pixels);
                CelestialTextureCache[identifier] = decoded;
            }

            var index = textures.Count;
            textures.Add(decoded);
            textureIndices[identifier] = index;
            return index;
        }

        void AddBody(Sharpcraft.Common.Identifiers.Identifier identifier, float angleDegrees, float size, bool flip = false)
        {
            var textureIndex = AddTexture(identifier);
            if (textureIndex < 0)
            {
                return;
            }

            var uv = flip ? (1f, 1f, 0f, 0f) : (0f, 0f, 1f, 1f);
            var (vertices, indices) = CelestialMeshBuilder.CelestialQuad(
                angleDegrees * (MathF.PI / 180f), height: 100f, size: size, uv: uv);

            // Translate to the eye so the body stays at the same place relative to the camera, i.e. infinitely far.
            // RAIN_BRIGHTNESS (= 1 - rainLevel) is the reference alpha on the sun/moon transform.
            draws.Add(new CelestialQuadDraw(textureIndex, vertices, indices, Matrix4x4.CreateTranslation(request.CameraPosition), new Vector4(1f, 1f, 1f, rainBrightness)));
        }

        void AddColor(Vector3[] positions, Vector4[] colours, List<uint> triangles, Vector4 tint, CelestialColorBlend blend)
        {
            var vertices = new float[positions.Length * 7];
            for (var i = 0; i < positions.Length; i++)
            {
                var offset = i * 7;
                vertices[offset] = positions[i].X;
                vertices[offset + 1] = positions[i].Y;
                vertices[offset + 2] = positions[i].Z;
                vertices[offset + 3] = colours[i].X;
                vertices[offset + 4] = colours[i].Y;
                vertices[offset + 5] = colours[i].Z;
                vertices[offset + 6] = colours[i].W;
            }

            colorDraws.Add(new CelestialColorDraw(vertices, [.. triangles], Matrix4x4.CreateTranslation(request.CameraPosition), tint, blend));
        }
    }

    /// <summary>
    /// Offscreen check of the position-colour celestial path: renders a sky-only frame with the sunrise fan (solid red) and
    /// the star field, then without them, and reports how many pixels the colour draws changed.
    /// </summary>
    internal static string RenderColorTest(int width, int height)
    {
        var fan = CelestialGeometry.SunriseFanWorld(MathF.PI / 2f, 1f);
        var fanColours = new Vector4[fan.Length];
        Array.Fill(fanColours, new Vector4(1f, 0f, 0f, 1f));
        var fanTriangles = new List<uint>((fan.Length - 2) * 3);
        for (var i = 1; i + 1 < fan.Length; i++)
        {
            fanTriangles.Add(0);
            fanTriangles.Add((uint)i);
            fanTriangles.Add((uint)(i + 1));
        }

        var starCorners = StarField.Build(out var keptStars);
        var starRotated = new Vector3[starCorners.Length];
        CelestialGeometry.SkyRotate(starCorners, 0f, starRotated);
        var starColours = new Vector4[starRotated.Length];
        Array.Fill(starColours, Vector4.One);
        var starTriangles = new List<uint>(keptStars * 6);
        for (var quad = 0; quad < keptStars; quad++)
        {
            var b = (uint)(quad * 4);
            starTriangles.Add(b);
            starTriangles.Add(b + 1);
            starTriangles.Add(b + 2);
            starTriangles.Add(b);
            starTriangles.Add(b + 2);
            starTriangles.Add(b + 3);
        }

        var (fanVertices, fanIndices) = PackColor(fan, fanColours, fanTriangles);
        var (starVertices, starIndices) = PackColor(starRotated, starColours, starTriangles);

        // Colour bodies are dome geometry too, drawn with a translation free camera, so the eye sits at the origin. The
        // token tilt keeps the look-at off the degenerate straight-up axis (T-810).
        var eye = Vector3.Zero;
        var withBodies = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            eye,
            new Vector3(0f, 100f, 0.5f))
        {
            CelestialColorDraws =
            [
                new CelestialColorDraw(fanVertices, fanIndices, Matrix4x4.Identity, new Vector4(1f, 0f, 0f, 1f), CelestialColorBlend.Translucent),
                new CelestialColorDraw(starVertices, starIndices, Matrix4x4.Identity, Vector4.One, CelestialColorBlend.Overlay)
            ]
        };

        var frame = OffscreenRenderer.Render(withBodies);
        var without = OffscreenRenderer.Render(withBodies with { CelestialColorDraws = null });
        var difference = PixelComparison.Compare(without.Rgba, frame.Rgba, frame.Width, frame.Height, 0);
        Console.WriteLine(
            $"color test: {keptStars} stars + sunrise changed {difference.Mismatches} pixels" +
            (difference.Mismatches > 0 ? $", first ({difference.FirstX},{difference.FirstY})" : string.Empty));

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-color-test.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// Diagnostic: draws the sky disc (<see cref="CelestialGeometry.SkyDisc"/> at y=16) with the calculateSkyColor(temperature)
    /// tint and probes the centre pixel, proving the disc geometry and colour pipeline. <c>MC_BIOME_TEMP</c> overrides the
    /// temperature (default 0.5).
    /// </summary>
    internal static string RenderSkyDiscTest(int width, int height)
    {
        var temperature = GameOptions.Get("MC_BIOME_TEMP") is { Length: > 0 } text && float.TryParse(text, out var parsed)
            ? parsed
            : 0.5f;
        var packed = Sharpcraft.Common.World.SkyColour.FromTemperature(temperature);
        var tint = new Vector4(
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f,
            1f);

        var disc = CelestialGeometry.SkyDisc(16f);
        var colours = new Vector4[disc.Length];
        Array.Fill(colours, Vector4.One);
        var triangles = new List<uint>();
        for (var i = 1; i + 1 < disc.Length; i++)
        {
            triangles.Add(0);
            triangles.Add((uint)i);
            triangles.Add((uint)(i + 1));
        }

        var (vertices, indices) = PackColor(disc, colours, triangles);
        var request = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            new Vector3(0f, -10f, 30f),
            new Vector3(0f, 16f, 0f),
            60f)
        {
            CelestialColorDraws = [new CelestialColorDraw(vertices, indices, Matrix4x4.Identity, tint, CelestialColorBlend.Translucent)]
        };

        var frame = OffscreenRenderer.Render(request);
        var without = OffscreenRenderer.Render(request with { CelestialColorDraws = null });
        var difference = PixelComparison.Compare(without.Rgba, frame.Rgba, frame.Width, frame.Height, 0);

        var centreX = frame.Width / 2;
        var centreY = frame.Height / 2;
        var offset = ((centreY * frame.Width) + centreX) * 4;
        Console.WriteLine(
            $"sky disc test: temp {temperature:F2} expect argb 0x{packed:X8}, " +
            $"centre rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]},{frame.Rgba[offset + 3]}), " +
            $"changed {difference.Mismatches} px");

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-sky-disc.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    private static (float[] Vertices, uint[] Indices) PackColor(Vector3[] positions, Vector4[] colours, List<uint> triangles)
    {
        var vertices = new float[positions.Length * 7];
        for (var i = 0; i < positions.Length; i++)
        {
            var offset = i * 7;
            vertices[offset] = positions[i].X;
            vertices[offset + 1] = positions[i].Y;
            vertices[offset + 2] = positions[i].Z;
            vertices[offset + 3] = colours[i].X;
            vertices[offset + 4] = colours[i].Y;
            vertices[offset + 5] = colours[i].Z;
            vertices[offset + 6] = colours[i].W;
        }

        return (vertices, [.. triangles]);
    }

    private static Vector3 SkyTintColour(float temperature)
    {
        var packed = Sharpcraft.Common.World.SkyColour.FromTemperature(temperature);
        return new Vector3(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f);
    }

    /// <summary>Applies the SKY_COLOR timeline multiplier and weather layers to a base sky RGB (docs/... 6.2/6.3).</summary>
    private static Vector3 ApplySkyWeather(Vector3 baseSky, ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var multiplier = Sharpcraft.Common.World.SkyAttributeTracks.SkyColorMultiplierAt(EffectiveDayTime(world), partialTick);
        var sky = baseSky * multiplier;
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        return Sharpcraft.Common.World.WeatherBlend.ApplySkyColour(sky, rain, thunder);
    }

    /// <summary>Replaces the gradient sky with the SKY_COLOR top disc (plus the dark bottom disc below the horizon).</summary>
    private static FrameRequest AppendSkyDisc(FrameRequest request, Vector3 skyColour)
    {
        var eye = request.CameraPosition;
        var draws = request.CelestialColorDraws is null
            ? new List<CelestialColorDraw>()
            : new List<CelestialColorDraw>(request.CelestialColorDraws);

        AddDisc(CelestialGeometry.SkyDisc(16f), Matrix4x4.CreateTranslation(eye), new Vector4(skyColour, 1f));

        var horizon = GameOptions.Get("MC_HORIZON") is { Length: > 0 } horizonText && float.TryParse(horizonText, out var parsedHorizon)
            ? parsedHorizon
            : 63f;
        var underwater = GameOptions.Get("MC_UNDERWATER") == "1";
        if (SkyVisibility.ShouldRenderDarkDisc(eye.Y, horizon, underwater))
        {
            AddDisc(CelestialGeometry.SkyDisc(-16f), Matrix4x4.CreateTranslation(eye + new Vector3(0f, 12f, 0f)), new Vector4(0f, 0f, 0f, 1f));
        }

        return request with { SkipGradientSky = true, CelestialColorDraws = [.. draws] };

        void AddDisc(Vector3[] positions, Matrix4x4 model, Vector4 tint)
        {
            var colours = new Vector4[positions.Length];
            Array.Fill(colours, Vector4.One);
            var triangles = new List<uint>((positions.Length - 2) * 3);
            for (var i = 1; i + 1 < positions.Length; i++)
            {
                triangles.Add(0);
                triangles.Add((uint)i);
                triangles.Add((uint)(i + 1));
            }

            var (vertices, indices) = PackColor(positions, colours, triangles);
            draws.Add(new CelestialColorDraw(vertices, indices, model, tint, CelestialColorBlend.Translucent));
        }
    }
}
