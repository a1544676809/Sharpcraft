// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>One box of a block model, in sixteenths of a block.</summary>
/// <param name="FromX">Minimum corner X.</param>
/// <param name="FromY">Minimum corner Y.</param>
/// <param name="FromZ">Minimum corner Z.</param>
/// <param name="ToX">Maximum corner X.</param>
/// <param name="ToY">Maximum corner Y.</param>
/// <param name="ToZ">Maximum corner Z.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public readonly record struct ModelBox(float FromX, float FromY, float FromZ, float ToX, float ToY, float ToZ);

/// <summary>One face of a box.</summary>
/// <param name="Texture">Texture variable as written, such as <c>#side</c>.</param>
/// <param name="Uv">Explicit UV rectangle, or null when the model leaves it to the projection.</param>
/// <param name="CullFace">Direction whose neighbour hides this face, or null.</param>
/// <param name="TintIndex">Tint slot, or -1 when the face carries none.</param>
/// <param name="Rotation">Texture rotation in degrees, one of 0, 90, 180 or 270.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public readonly record struct ModelFace(string Texture, float[]? Uv, string? CullFace, int TintIndex, int Rotation = 0);

/// <summary>Optional rotation applied to a box.</summary>
/// <param name="Angle">Angle in degrees, a multiple of twenty two and a half.</param>
/// <param name="Axis">Axis to turn about, one of x, y, z.</param>
/// <param name="OriginX">Rotation origin X.</param>
/// <param name="OriginY">Rotation origin Y.</param>
/// <param name="OriginZ">Rotation origin Z.</param>
/// <param name="Rescale">Whether the model asks for its vertices to be rescaled after turning.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public readonly record struct ModelRotation(float Angle, string Axis, float OriginX, float OriginY, float OriginZ, bool Rescale);

/// <summary>One element of a block model.</summary>
/// <param name="Box">The box.</param>
/// <param name="Faces">Faces by direction name, north/south/east/west/up/down.</param>
/// <param name="Rotation">Optional rotation, or null.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public sealed record ModelElement(ModelBox Box, IReadOnlyDictionary<string, ModelFace> Faces, ModelRotation? Rotation);

/// <summary>
/// Reads a block model's <c>elements</c> into plain data. Geometry is what the mesher needs and what
/// <see cref="BlockModelResolver"/> does not provide; that resolver only reads face textures
/// (docs/specs/render/block-model-geometry.md).
/// </summary>
/// <remarks>
/// Elements are not merged down a parent chain: the first model from the child upwards that declares
/// <c>elements</c> wins, exactly as the reference model does. A model that resolves but declares no
/// elements and no parent is empty: it returns an empty list, not null. Null is reserved for a model
/// that cannot be read at all.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.block.model.BlockModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/BlockModel.java")]
public sealed class BlockModelElements
{
    private const int MaxDepth = 16;

    private readonly Func<Identifier, byte[]?> _read;

    /// <summary>Creates a reader over an arbitrary model source, which is what tests supply.</summary>
    /// <param name="read">Reads a model's bytes, or null when it is absent.</param>
    public BlockModelElements(Func<Identifier, byte[]?> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
    }

    /// <summary>Creates a reader over a resource stack.</summary>
    /// <param name="stack">Stack holding the extracted models.</param>
    public static BlockModelElements FromStack(AssetStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return new BlockModelElements(id => stack.Read(ResourceKind.Model, id));
    }

    /// <summary>Reads the elements of a model, following parents until one declares them.</summary>
    /// <param name="modelId">Model to start from.</param>
    /// <returns>
    /// The elements; an empty list when the model resolves but declares no elements anywhere; null
    /// only when a model in the chain cannot be read. An empty list is an empty model, which draws
    /// nothing, while null lets the caller fall back to the cube path.
    /// </returns>
    public IReadOnlyList<ModelElement>? Resolve(Identifier modelId)
    {
        Identifier? current = modelId;
        for (var depth = 0; depth < MaxDepth && current is { } model; depth++)
        {
            var bytes = _read(model);
            if (bytes is null)
            {
                return null;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(bytes);
            }
            catch (JsonException)
            {
                return null;
            }

            using (document)
            {
                var root = document.RootElement;

                if (root.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
                {
                    return ParseElements(elements);
                }

                if (root.TryGetProperty("parent", out var parent) &&
                    parent.ValueKind == JsonValueKind.String &&
                    parent.GetString() is { Length: > 0 } name)
                {
                    current = NormalizeModel(name);
                    continue;
                }

                // A real model file that declares neither elements nor a parent draws nothing. Chests
                // and shulker boxes are exactly this: their block models carry only a particle texture.
                return [];
            }
        }

        return null;
    }

    private static List<ModelElement> ParseElements(JsonElement elements)
    {
        var parsed = new List<ModelElement>(elements.GetArrayLength());

        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !TryReadVector(element, "from", out var from) ||
                !TryReadVector(element, "to", out var to))
            {
                continue;
            }

            var faces = new Dictionary<string, ModelFace>(StringComparer.Ordinal);
            if (element.TryGetProperty("faces", out var faceObject) && faceObject.ValueKind == JsonValueKind.Object)
            {
                foreach (var face in faceObject.EnumerateObject())
                {
                    if (face.Value.ValueKind != JsonValueKind.Object ||
                        !face.Value.TryGetProperty("texture", out var texture) ||
                        texture.ValueKind != JsonValueKind.String ||
                        texture.GetString() is not { Length: > 0 } textureName)
                    {
                        continue;
                    }

                    float[]? uv = null;
                    if (face.Value.TryGetProperty("uv", out var uvElement) && uvElement.ValueKind == JsonValueKind.Array && uvElement.GetArrayLength() == 4)
                    {
                        uv =
                        [
                            uvElement[0].GetSingle(),
                            uvElement[1].GetSingle(),
                            uvElement[2].GetSingle(),
                            uvElement[3].GetSingle()
                        ];
                    }

                    var cull = face.Value.TryGetProperty("cullface", out var cullElement) && cullElement.ValueKind == JsonValueKind.String
                        ? cullElement.GetString()
                        : null;

                    var tint = face.Value.TryGetProperty("tintindex", out var tintElement) && tintElement.ValueKind == JsonValueKind.Number
                        ? tintElement.GetInt32()
                        : -1;

                    var faceRotation = face.Value.TryGetProperty("rotation", out var faceRotationElement) &&
                        faceRotationElement.ValueKind == JsonValueKind.Number
                        ? faceRotationElement.GetInt32()
                        : 0;

                    faces[face.Name] = new ModelFace(textureName, uv, cull, tint, faceRotation);
                }
            }

            ModelRotation? rotation = null;
            if (element.TryGetProperty("rotation", out var rotationElement) && rotationElement.ValueKind == JsonValueKind.Object &&
                rotationElement.TryGetProperty("angle", out var angle) && angle.ValueKind == JsonValueKind.Number &&
                rotationElement.TryGetProperty("axis", out var axis) && axis.ValueKind == JsonValueKind.String &&
                TryReadVector(rotationElement, "origin", out var origin))
            {
                rotation = new ModelRotation(
                    angle.GetSingle(),
                    axis.GetString() ?? "y",
                    origin.X,
                    origin.Y,
                    origin.Z,
                    rotationElement.TryGetProperty("rescale", out var rescale) && rescale.ValueKind == JsonValueKind.True);
            }

            parsed.Add(new ModelElement(
                new ModelBox(from.X, from.Y, from.Z, to.X, to.Y, to.Z),
                faces,
                rotation));
        }

        return parsed;
    }

    private static bool TryReadVector(JsonElement parent, string name, out (float X, float Y, float Z) vector)
    {
        vector = default;

        if (!parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3)
        {
            return false;
        }

        vector = (element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle());
        return true;
    }

    private static Identifier NormalizeModel(string name) =>
        Identifier.TryParse(name.Contains(':') ? name : $"minecraft:{name}", out var id)
            ? id
            : throw new InvalidDataException($"model name is not an identifier: '{name}'");
}
