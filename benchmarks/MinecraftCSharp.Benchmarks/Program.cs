// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.Imaging;
using MinecraftCSharp.Common.Nbt;
using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Common.World;
using MinecraftCSharp.Framework.Rendering;

namespace MinecraftCSharp.Benchmarks;

/// <summary>
/// Reproducible performance baseline for the world data pipeline
/// (docs/specs/performance-baseline.md). Deliberately dependency-free: a stopwatch, a warm-up and
/// percentiles are enough to catch regressions, and they add no noise of their own.
/// </summary>
internal static class Program
{
    private static int _iterations = 20;
    private static int _warmup = 3;
    private static string? _realPacketPath;
    private static string? _outputPath;

    private static int Main(string[] args)
    {
        if (!ParseArguments(args))
        {
            return 2;
        }

        Console.WriteLine($"benchmarks: iterations={_iterations} warmup={_warmup} " +
                         $"{(_realPacketPath is null ? "synthetic chunk packet" : $"real packet {_realPacketPath}")}");
        Console.WriteLine();

        var packet = _realPacketPath is null ? SyntheticChunkPacket.Build() : File.ReadAllBytes(_realPacketPath);
        var world = SyntheticWorld.Build();
        var mesh = ChunkMesher.Mesh(world);
        var frame = new FrameRequest(
            Width: 960,
            Height: 540,
            Vertices: mesh.Vertices,
            Indices: mesh.Indices,
            ClearColour: Vector4.One,
            CameraPosition: new Vector3(24, 120, 24),
            CameraTarget: new Vector3(24, 60, 24));
        var nbt = SyntheticNbt.Build();

        Console.WriteLine($"fixtures: packet={packet.Length} bytes, world={world.LoadedChunkCount} chunks, " +
                          $"mesh={mesh.BlockCount} blocks / {mesh.TriangleCount} triangles, nbt={nbt.Length} bytes");
        Console.WriteLine();

        using var meshBuffer = new MeshBuffer(expectedBlocks: 400_000);
        var transformDestination = new float[ClipSpaceTransform.RequiredFloats(frame)];

        var results = new List<Measurement>
        {
            Measure("B-1 chunk packet parse", _iterations, _warmup, () =>
            {
                var status = ChunkPacketParser.TryRead(packet, sectionCount: 24, minSectionY: -64, airState: 0, out var parsed);
                if (status != DecodeStatus.Success || parsed is null)
                {
                    throw new InvalidOperationException($"chunk parse failed: {status}");
                }

                return parsed.Chunk.SectionCount;
            }),
            Measure("B-2 chunk meshing", Math.Max(10, _iterations / 2), 2, () =>
            {
                var data = ChunkMesher.Mesh(world);
                return data.BlockCount;
            }),
            Measure("B-2b chunk meshing (pooled)", Math.Max(10, _iterations / 2), 2, () =>
            {
                var blocks = ChunkMesher.Mesh(world, meshBuffer);
                return blocks;
            }),
            Measure("B-3 clip-space transform", _iterations, _warmup, () =>
            {
                var data = ClipSpaceTransform.Build(frame, Matrix4x4.Identity);
                return data.Length;
            }),
            Measure("B-3b clip-space (span dest)", _iterations, _warmup, () =>
            {
                ClipSpaceTransform.Build(frame, Matrix4x4.Identity, transformDestination);
                return transformDestination.Length;
            }),
            Measure("B-4 png encode 960x540", _iterations, _warmup, () =>
            {
                var png = PngEncoder.EncodeRgba(960, 540, SyntheticFrame.Rgba);
                return png.Length;
            }),
            Measure("B-5 nbt parse", _iterations, _warmup, () =>
            {
                var root = NbtReader.ReadNetworkRoot(nbt);
                return root.Count;
            })
        };

        PrintTable(results);

        if (_outputPath is not null)
        {
            File.WriteAllText(_outputPath, BuildMarkdown(results, packet, world, mesh, nbt));
            Console.WriteLine();
            Console.WriteLine($"results written to {_outputPath}");
        }

        return 0;
    }

    private static bool ParseArguments(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--iterations" when i + 1 < args.Length && int.TryParse(args[i + 1], out var iterations):
                    _iterations = Math.Max(1, iterations);
                    i++;
                    break;

                case "--warmup" when i + 1 < args.Length && int.TryParse(args[i + 1], out var warmup):
                    _warmup = Math.Max(0, warmup);
                    i++;
                    break;

                case "--real" when i + 1 < args.Length:
                    _realPacketPath = args[i + 1];
                    i++;
                    break;

                case "--out" when i + 1 < args.Length:
                    _outputPath = args[i + 1];
                    i++;
                    break;

                case "--help":
                case "-h":
                    Console.WriteLine("usage: MinecraftCSharp.Benchmarks [--iterations N] [--warmup N] [--real <packet>] [--out <markdown>]");
                    return false;

                default:
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    return false;
            }
        }

        return true;
    }

    private static Measurement Measure(string name, int iterations, int warmup, Func<int> body)
    {
        for (var i = 0; i < warmup; i++)
        {
            _ = body();
        }

        var samples = new double[iterations];
        long allocated = 0;

        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var work = body();
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            samples[i] = elapsed.TotalMilliseconds;
            allocated += allocatedBytes;

            if (i == 0)
            {
                _ = work;
            }
        }

        Array.Sort(samples);
        return new Measurement(
            name,
            iterations,
            Percentile(samples, 0.50),
            Percentile(samples, 0.95),
            samples[0],
            samples[^1],
            allocated / iterations);
    }

    private static double Percentile(double[] sorted, double fraction)
    {
        var index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static void PrintTable(List<Measurement> results)
    {
        var header = string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} {1,10} {2,10} {3,10} {4,10} {5,14}",
            "measurement", "P50 ms", "P95 ms", "min ms", "max ms", "alloc B/op");
        Console.WriteLine(header);
        Console.WriteLine(new string('-', header.Length));

        foreach (var result in results)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} {1,10:F3} {2,10:F3} {3,10:F3} {4,10:F3} {5,14:N0}",
                result.Name,
                result.P50,
                result.P95,
                result.Min,
                result.Max,
                result.AllocatedBytesPerOperation));
        }
    }

    private static string BuildMarkdown(
        List<Measurement> results,
        byte[] packet,
        ClientWorld world,
        MeshData mesh,
        byte[] nbt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->");
        builder.AppendLine("<!-- Copyright (C) 2026 MinecraftCSharp contributors -->");
        builder.AppendLine();
        builder.AppendLine("# 性能基线结果");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"运行时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}　" +
            $"运行时：{Environment.Version}　CPU 核数：{Environment.ProcessorCount}");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"fixtures：packet={packet.Length} B，world={world.LoadedChunkCount} 区块，" +
            $"mesh={mesh.BlockCount} 方块 / {mesh.TriangleCount} 三角形，nbt={nbt.Length} B；" +
            $"iterations={_iterations}，warmup={_warmup}");
        builder.AppendLine();
        builder.AppendLine("| 项 | 采样 | P50 (ms) | P95 (ms) | min (ms) | max (ms) | 分配 (B/次) |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var result in results)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {result.Name} | {result.Iterations} | {result.P50:F3} | {result.P95:F3} | " +
                $"{result.Min:F3} | {result.Max:F3} | {result.AllocatedBytesPerOperation:N0} |");
        }

        builder.AppendLine();
        builder.AppendLine("> 读数说明见 `docs/specs/performance-baseline.md` §4（网络、JIT/GC、驱动缓存、降频）。同机前后对比才有意义。");
        return builder.ToString();
    }

    private sealed record Measurement(
        string Name,
        int Iterations,
        double P50,
        double P95,
        double Min,
        double Max,
        long AllocatedBytesPerOperation);
}

/// <summary>Deterministic chunk packet whose layout mirrors the real one.</summary>
internal static class SyntheticChunkPacket
{
    internal static byte[] Build(int sectionCount = 24, int paletteBits = 4)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteVarInt(stream, PacketIds.PlayClientbound.LevelChunkWithLight);
        WriteInt(stream, 7);
        WriteInt(stream, 7);

        // One heightmap entry with 37 longs, as observed on the wire.
        WriteVarInt(stream, 1);
        WriteVarInt(stream, 1);
        WriteVarInt(stream, 37);
        for (var i = 0; i < 37; i++)
        {
            stream.Write(BitConverter.GetBytes((long)i).AsSpan().ToArray().Reverse().ToArray());
        }

        // Section data is a length-prefixed byte array.
        var sections = BuildSections(sectionCount, paletteBits);
        WriteVarInt(stream, sections.Length);
        stream.Write(sections);

        WriteVarInt(stream, 0);   // block entities

        // Light: four byte-array masks then two lists of 2048-byte arrays.
        for (var i = 0; i < 4; i++)
        {
            WriteVarInt(stream, 0);
        }

        WriteVarInt(stream, 0);
        WriteVarInt(stream, 0);

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildSections(int sectionCount, int paletteBits)
    {
        using var stream = new MemoryStream();
        var palette = new int[1 << paletteBits];
        for (var i = 0; i < palette.Length; i++)
        {
            palette[i] = i == 0 ? 0 : 100 + i;
        }

        for (var section = 0; section < sectionCount; section++)
        {
            var nonEmpty = section < sectionCount / 2 ? 4096 : 0;
            WriteUShort(stream, nonEmpty);
            WriteUShort(stream, 0);

            // Block states: local palette with a fixed bit width.
            stream.WriteByte((byte)paletteBits);
            WriteVarInt(stream, palette.Length);
            foreach (var state in palette)
            {
                WriteVarInt(stream, state);
            }

            var longs = new long[PalettedContainer.ExpectedLongs(4096, paletteBits)];
            var perLong = 64 / paletteBits;
            for (var i = 0; i < longs.Length; i++)
            {
                long packed = 0;
                for (var slot = 0; slot < perLong; slot++)
                {
                    var entry = (i * perLong + slot) % palette.Length;
                    packed |= (long)entry << (slot * paletteBits);
                }

                longs[i] = packed;
            }

            foreach (var value in longs)
            {
                WriteLong(stream, value);
            }

            // Biomes: single value.
            stream.WriteByte(0);
            WriteVarInt(stream, 1);
        }

        return stream.ToArray();
    }

    private static void WriteUShort(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt(Stream stream, int value) =>
        stream.Write(BitConverter.GetBytes(value).AsSpan().ToArray().Reverse().ToArray());

    private static void WriteLong(Stream stream, long value) =>
        stream.Write(BitConverter.GetBytes(value).AsSpan().ToArray().Reverse().ToArray());

    private static void WriteVarInt(Stream stream, int value)
    {
        var remaining = (uint)value;
        while (remaining >= 0x80)
        {
            stream.WriteByte((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte)remaining);
    }
}

/// <summary>Terrain-shaped world of the same scale as the live capture (3x3 chunks, 24 sections).</summary>
internal static class SyntheticWorld
{
    internal static ClientWorld Build()
    {
        var world = new ClientWorld(minSectionY: -4, sectionCount: 24);

        for (var chunkX = 0; chunkX < 3; chunkX++)
        {
            for (var chunkZ = 0; chunkZ < 3; chunkZ++)
            {
                var chunk = world.AddChunk(chunkX, chunkZ);
                var sections = new ChunkSection[24];
                for (var i = 0; i < sections.Length; i++)
                {
                    sections[i] = new ChunkSection(0, -4 + i);
                }

                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        var worldX = chunkX * 16 + x;
                        var worldZ = chunkZ * 16 + z;
                        var height = 60 + (int)(8 * Math.Sin(worldX * 0.3) + 6 * Math.Cos(worldZ * 0.4));

                        for (var y = -64; y < height; y++)
                        {
                            var sectionIndex = (y + 64) / 16;
                            var state = y >= height - 1 ? 2 : 1;
                            sections[sectionIndex].Set(x, (y + 64) % 16, z, state);
                        }
                    }
                }

                foreach (var section in sections)
                {
                    chunk.SetSection(section);
                }
            }
        }

        return world;
    }
}

/// <summary>Compound shaped like a dimension type payload.</summary>
internal static class SyntheticNbt
{
    internal static byte[] Build()
    {
        var root = new NbtCompound()
            .Set("name", new NbtString("minecraft:overworld"))
            .Set("min_y", new NbtInt(-64))
            .Set("height", new NbtInt(384))
            .Set("logical_height", new NbtInt(384))
            .Set("coordinate_scale", new NbtDouble(1.0))
            .Set("has_skylight", new NbtByte(1))
            .Set("has_ceiling", new NbtByte(0))
            .Set("ambient_light", new NbtFloat(0f))
            .Set("monster_spawn_block_light_limit", new NbtInt(0))
            .Set("monster_spawn_light_level", new NbtInt(0))
            .Set("infiniburn", new NbtString("#minecraft:infiniburn_overworld"))
            .Set("effects", new NbtString("minecraft:overworld"))
            .Set("heightmap_longs", new NbtLongArray(Enumerable.Range(0, 37).Select(i => (long)i).ToArray()))
            .Set("biome_ids", new NbtIntArray(Enumerable.Range(0, 64).ToArray()));

        return NbtWriter.WriteNetworkRoot(root);
    }
}

/// <summary>Fixed RGBA frame so PNG encoding measures the encoder, not random data.</summary>
internal static class SyntheticFrame
{
    internal static byte[] Rgba { get; } = BuildFrame(960, 540);

    private static byte[] BuildFrame(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                pixels[offset] = (byte)(x % 256);
                pixels[offset + 1] = (byte)(y % 256);
                pixels[offset + 2] = (byte)((x ^ y) % 256);
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }
}
