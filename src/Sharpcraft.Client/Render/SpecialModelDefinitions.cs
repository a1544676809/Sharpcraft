// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Special model definitions taken from the reference model classes.
/// </summary>
/// <remarks>
/// Geometry is authored in model pixels exactly as the reference <c>CubeListBuilder</c> calls read, so a definition can
/// be checked line by line against the reference model class it came from (docs/modules/item-rendering.md 3.5.18).
/// </remarks>
internal static class SpecialModelDefinitions
{
    /// <summary>Atlas key of the reference single-chest texture (<c>textures/entity/chest/normal.png</c>).</summary>
    public const string ChestTexture = "entity/chest/normal";

    /// <summary>Reference <c>ChestModel</c> texture size.</summary>
    public const int ChestTextureWidth = 64;

    /// <summary>Reference <c>ChestModel</c> texture height.</summary>
    public const int ChestTextureHeight = 64;

    /// <summary>
    /// Single-chest model: <c>bottom</c>, <c>lid</c> and <c>lock</c>, matching the reference
    /// <c>ChestModel.createSingleBodyLayer</c> boxes and poses.
    /// </summary>
    /// <param name="lidRotationDegrees">
    /// Lid rotation in degrees around X; the reference drives this from <c>openness</c> (zero for a dropped chest).
    /// </param>
    public static SpecialPart Chest(float lidRotationDegrees = 0f) => new(
        "chest",
        Vector3.Zero,
        Vector3.Zero,
        [],
        [
            new SpecialPart("bottom", Vector3.Zero, Vector3.Zero,
                [new SpecialCube(0, 19, new Vector3(1f, 0f, 1f), new Vector3(14f, 10f, 14f))], []),
            new SpecialPart("lid", new Vector3(0f, 9f, 1f), new Vector3(lidRotationDegrees, 0f, 0f),
                [new SpecialCube(0, 0, new Vector3(1f, 0f, 0f), new Vector3(14f, 5f, 14f))], []),
            new SpecialPart("lock", new Vector3(0f, 9f, 1f), new Vector3(lidRotationDegrees, 0f, 0f),
                [new SpecialCube(0, 0, new Vector3(7f, -2f, 14f), new Vector3(2f, 4f, 1f))], [])
        ]);
}
