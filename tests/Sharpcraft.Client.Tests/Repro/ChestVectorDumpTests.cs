// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Dumps the chest geometry in the same block-local space the reference model works in, so the
/// two can be compared field by field. Set CHEST_VECTOR_OUT to a path to run it.
/// </summary>
/// <remarks>
/// Comparison contract: block-local space, one block is one unit, origin at the block's minimum
/// corner, Y up, facing transform included, no world position, no lighting. Positions are float32
/// on our side but the vertex writer stores UV as half floats, so UV needs a wider tolerance than
/// position.
/// </remarks>
public sealed class ChestVectorDumpTests
{
    [Fact]
    public void DumpChestVectors()
    {
        var path = Environment.GetEnvironmentVariable("CHEST_VECTOR_OUT");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var mesher = new ChestMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var lines = new List<string>();

        foreach (var (facingName, facing) in new[]
                 {
                     ("north", (byte)4), ("south", (byte)5), ("west", (byte)0), ("east", (byte)1)
                 })
        {
            foreach (var open in new[] { 0f, 0.5f, 1f })
            {
                foreach (var (halfName, half) in new[]
                         {
                             ("single", (byte)0), ("left", (byte)1), ("right", (byte)2)
                         })
                {
                    var state = new BlockEntityRenderState(
                        0f, 0f, 0f,
                        VanillaBlockEntityTypes.Chest,
                        Sky: 15,
                        Block: 0,
                        Open: open,
                        Facing: facing,
                        ChestType: half,
                        ChestMaterial: 0);

                    var geometry = mesher.Build(new[] { state }, sprite);
                    var prefix = $"chest|{facingName}|{open:F6}|{halfName}";

                    for (var i = 0; i < geometry.VertexFloats; i += ChunkMesher.FloatsPerVertex)
                    {
                        var x = geometry.Vertices[i];
                        var y = geometry.Vertices[i + 1];
                        var z = geometry.Vertices[i + 2];
                        var uvBits = BitConverter.SingleToUInt32Bits(geometry.Vertices[i + 4]);
                        var u = (float)BitConverter.UInt16BitsToHalf((ushort)(uvBits & 0xFFFF));
                        var v = (float)BitConverter.UInt16BitsToHalf((ushort)(uvBits >> 16));
                        lines.Add($"{prefix}|{x:F6}|{y:F6}|{z:F6}|{u:F6}|{v:F6}");
                    }
                }
            }
        }

        File.WriteAllLines(path, lines);
    }
}
