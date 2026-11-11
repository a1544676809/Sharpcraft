// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Turns the cloud quad list into the per-face words the procedural cloud shader reads (T-827).
/// </summary>
/// <remarks>
/// The reference sends no vertices for clouds. It sends three integers per face and lets the vertex shader turn a
/// vertex index into a corner of a cube, which is twelve bytes against the hundred and twelve this project uploads
/// for the same face (docs/specs/render/cloud-geometry.md).
/// <para>
/// Layout per face: the cell X and Z with their low bit moved into the flags word, then a direction and a set of
/// flags. Coordinates are stored halved so sixteen bits cover twice the range; the bit that was shifted out rides in
/// the flags and is put back by the shader.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.CloudRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/CloudRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CloudFaceTable
{
    /// <summary>Words written per face: cell X, cell Z, direction and flags.</summary>
    public const int WordsPerFace = 3;

    /// <summary>Mask for the direction, which occupies the low three bits of the flags word.</summary>
    public const int DirectionMask = 0b111;

    /// <summary>Set when the face's winding runs the other way, which happens on the inside of a bank.</summary>
    public const int FlagInsideFace = 1 << 4;

    /// <summary>Set when the face should take the top brightness instead of its own direction's.</summary>
    public const int FlagUseTopColour = 1 << 5;

    /// <summary>Holds the low bit of the cell Z coordinate, shifted out of the coordinate word.</summary>
    public const int FlagExtraZ = 1 << 6;

    /// <summary>Holds the low bit of the cell X coordinate, shifted out of the coordinate word.</summary>
    public const int FlagExtraX = 1 << 7;

    /// <summary>
    /// Packs the quads into the face table the shader reads.
    /// </summary>
    /// <param name="quads">Faces to emit, in the order they should be drawn.</param>
    /// <remarks>
    /// The top-colour flag is never set: this project's existing cloud geometry picks brightness by direction only,
    /// where the reference can override a face to the top brightness. Setting it would change the picture while this
    /// slice is meant to preserve it, so it is registered as a simplification in the spec and taken up in the slice
    /// that compares against the reference rather than this project's current output.
    /// </remarks>
    public static int[] Build(IReadOnlyList<CloudQuad> quads)
    {
        ArgumentNullException.ThrowIfNull(quads);

        var table = new int[quads.Count * WordsPerFace];
        for (var index = 0; index < quads.Count; index++)
        {
            var quad = quads[index];
            var flags = (int)quad.Direction & DirectionMask;
            if (quad.InsideFace)
            {
                flags |= FlagInsideFace;
            }

            if ((quad.CellZ & 1) != 0)
            {
                flags |= FlagExtraZ;
            }

            if ((quad.CellX & 1) != 0)
            {
                flags |= FlagExtraX;
            }

            var word = index * WordsPerFace;
            table[word] = quad.CellX >> 1;
            table[word + 1] = quad.CellZ >> 1;
            table[word + 2] = flags;
        }

        return table;
    }
}
