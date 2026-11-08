// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Text.Json;
using Sharpcraft.Assets;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>Display context names from <c>ItemDisplayContext</c>; order matches the reference ids.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.ItemDisplayContext", "ref://mc-26.3-analysis/src/net/minecraft/world/item/ItemDisplayContext.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public enum ItemDisplayContext : byte
{
    None = 0,
    ThirdPersonLeftHand = 1,
    ThirdPersonRightHand = 2,
    FirstPersonLeftHand = 3,
    FirstPersonRightHand = 4,
    Head = 5,
    Gui = 6,
    Ground = 7,
    Fixed = 8,
    OnShelf = 9
}

/// <summary>One resolved item display transform. Translation is already in model-space blocks.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.cuboid.ItemTransform", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/ItemTransform.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public readonly record struct ItemDisplayTransform(
    Vector3 RotationDegrees,
    Vector3 Translation,
    Vector3 Scale)
{
    /// <summary>The reference NO_TRANSFORM: zero rotation/translation and unit scale.</summary>
    public static readonly ItemDisplayTransform None = new(Vector3.Zero, Vector3.Zero, Vector3.One);

    /// <summary>Reference <c>NO_TRANSFORM</c> still centres the model by this much.</summary>
    public static readonly Vector3 CentreOffset = new(-0.5f, -0.5f, -0.5f);

    /// <summary>True when this transform is the reference <c>NO_TRANSFORM</c>.</summary>
    public bool IsNone => RotationDegrees == Vector3.Zero && Translation == Vector3.Zero && Scale == Vector3.One;

    /// <summary>
    /// Builds the row-vector matrix of the reference <c>ItemTransform.apply</c> sequence.
    /// </summary>
    /// <remarks>
    /// The reference calls <c>translate</c>, <c>rotate</c>, <c>scale</c>, then <c>translate(-0.5)</c>, and its matrices
    /// are column-vector, so a vertex sees the <b>last</b> call first: centre, then scale, then rotate, then translate.
    /// Row vectors evaluate left to right, so this reverses the call order — centring is innermost, not outermost.
    /// Getting that backwards makes an item orbit a corner instead of spinning about its own centre.
    /// <paramref name="applyLeftHandFix"/> negates the X translation and the Y/Z rotations exactly as the reference does
    /// for the left-hand contexts.
    /// </remarks>
    public Matrix4x4 ToMatrix(bool applyLeftHandFix = false)
    {
        var translation = applyLeftHandFix
            ? new Vector3(-Translation.X, Translation.Y, Translation.Z)
            : Translation;
        var rotation = applyLeftHandFix
            ? new Vector3(RotationDegrees.X, -RotationDegrees.Y, -RotationDegrees.Z)
            : RotationDegrees;

        if (IsNone)
        {
            return Matrix4x4.CreateTranslation(CentreOffset);
        }

        const float degToRad = MathF.PI / 180f;
        return Matrix4x4.CreateTranslation(CentreOffset)
            * Matrix4x4.CreateScale(Scale)
            * Matrix4x4.CreateRotationX(rotation.X * degToRad)
            * Matrix4x4.CreateRotationY(rotation.Y * degToRad)
            * Matrix4x4.CreateRotationZ(rotation.Z * degToRad)
            * Matrix4x4.CreateTranslation(translation);
    }
}

/// <summary>One item model's resolved parent chain, texture slots and display transforms.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.item.ItemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/ItemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed record ItemModelResolution(
    string ModelId,
    IReadOnlyDictionary<string, string> Textures,
    IReadOnlyDictionary<ItemDisplayContext, ItemDisplayTransform> Display,
    string? SpecialRendererType = null,
    IReadOnlyDictionary<string, string>? SpecialProperties = null,
    IReadOnlyList<ItemModelElement>? Elements = null)
{
    /// <summary>Resolved cuboids of the model, empty when the model has no <c>elements</c>.</summary>
    public IReadOnlyList<ItemModelElement> Cuboids => Elements ?? [];
}

/// <summary>
/// Resolves the item model selected by an item id or an <c>item_model</c> component. This is the data-only
/// T-631a layer: it deliberately returns no geometry or GPU state.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.item.ItemModelResolver", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/ItemModelResolver.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-MODEL-STATIC-FALLBACK", "T-631e", Note = "Vanilla selects renderer by ItemStackRenderState layers; Sharpcraft resolves one descriptor and takes the static fallback branch")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-RENDER-STATE", "T-631e", Note = "No ItemStackRenderState equivalent: no layer list, foil type, tint layers, particle material or model extents")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-DROP-DISPLAY-GUI", "T-761", Note = "Drops consume the ground ItemTransform, but item frames and GUI items are still unrendered")]
public sealed class ItemModelResolver
{
    private static readonly IReadOnlyDictionary<string, ItemDisplayContext> ContextNames =
        new Dictionary<string, ItemDisplayContext>(StringComparer.Ordinal)
        {
            ["none"] = ItemDisplayContext.None,
            ["thirdperson_lefthand"] = ItemDisplayContext.ThirdPersonLeftHand,
            ["thirdperson_righthand"] = ItemDisplayContext.ThirdPersonRightHand,
            ["firstperson_lefthand"] = ItemDisplayContext.FirstPersonLeftHand,
            ["firstperson_righthand"] = ItemDisplayContext.FirstPersonRightHand,
            ["head"] = ItemDisplayContext.Head,
            ["gui"] = ItemDisplayContext.Gui,
            ["ground"] = ItemDisplayContext.Ground,
            ["fixed"] = ItemDisplayContext.Fixed,
            ["on_shelf"] = ItemDisplayContext.OnShelf
        };

    private readonly ResourceManager _resources;
    private readonly Dictionary<string, ItemModelResolution?> _cache = new(StringComparer.Ordinal);

    public ItemModelResolver(ResourceManager resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    /// <summary>Resolves one item id, or null when the item/model has no drawable model.</summary>
    public ItemModelResolution? Resolve(string itemId)
    {
        if (_cache.TryGetValue(itemId, out var cached))
        {
            return cached;
        }

        var resolved = ResolveCore(itemId);
        _cache[itemId] = resolved;
        return resolved;
    }

    /// <summary>Resolves the item selected by an <c>item_model</c> component, falling back to the item id.</summary>
    public ItemModelResolution? Resolve(string itemId, string? itemModelId) =>
        string.IsNullOrEmpty(itemModelId) ? Resolve(itemId) : ResolveModel(itemModelId);

    private ItemModelResolution? ResolveCore(string itemId)
    {
        if (!Identifier.TryParse(itemId, out var id))
        {
            return null;
        }

        var bytes = _resources.Read(ResourceKind.Item, id);
        if (bytes is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(bytes);
        if (!document.RootElement.TryGetProperty("model", out var modelElement) ||
            modelElement.ValueKind != JsonValueKind.Object ||
            !modelElement.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return ResolveElement(modelElement, depth: 0);
    }

    private ItemModelResolution? ResolveElement(JsonElement model, int depth)
    {
        if (depth > 8 ||
            !model.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var type = typeElement.GetString();
        switch (type)
        {
            case "minecraft:empty":
                return null;
            case "minecraft:model":
                return model.TryGetProperty("model", out var modelIdElement) &&
                       modelIdElement.ValueKind == JsonValueKind.String &&
                       Identifier.TryParse(modelIdElement.GetString()!, out var modelId)
                    ? ResolveModel(modelId.ToString())
                    : null;
            case "minecraft:select":
                return TryResolveNested(model, "fallback", depth) ??
                       (model.TryGetProperty("cases", out var cases) && cases.ValueKind == JsonValueKind.Array
                           ? ResolveFirstCase(cases, depth)
                           : null);
            case "minecraft:condition":
                return TryResolveNested(model, "on_false", depth) ??
                       TryResolveNested(model, "on_true", depth);
            case "minecraft:range_dispatch":
                return TryResolveNested(model, "fallback", depth) ??
                       (model.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array
                           ? ResolveFirstCase(entries, depth)
                           : null);
            case "minecraft:composite":
                return model.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array
                    ? ResolveFirstCase(models, depth)
                    : null;
            case "minecraft:special":
                if (!model.TryGetProperty("base", out var baseElement) ||
                    baseElement.ValueKind != JsonValueKind.String ||
                    !Identifier.TryParse(baseElement.GetString()!, out var baseId) ||
                    ResolveModel(baseId.ToString()) is not { } baseModel)
                {
                    return null;
                }

                var rendererType = model.TryGetProperty("model", out var specialModel) &&
                    specialModel.ValueKind == JsonValueKind.Object &&
                    specialModel.TryGetProperty("type", out var specialType) &&
                    specialType.ValueKind == JsonValueKind.String
                    ? specialType.GetString()
                    : null;
                Dictionary<string, string>? properties = null;
                if (specialModel.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in specialModel.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                            properties[property.Name] = property.Value.GetString()!;
                        }
                    }
                }

                return baseModel with { SpecialRendererType = rendererType, SpecialProperties = properties };
            default:
                // special/selectors with unsupported properties deliberately fall back to no drawable model.
                return null;
        }
    }

    private ItemModelResolution? TryResolveNested(JsonElement model, string property, int depth) =>
        model.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ResolveElement(nested, depth + 1)
            : null;

    private ItemModelResolution? ResolveFirstCase(JsonElement array, int depth)
    {
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (entry.TryGetProperty("model", out var nested) && nested.ValueKind == JsonValueKind.Object &&
                ResolveElement(nested, depth + 1) is { } resolved)
            {
                return resolved;
            }
        }

        return null;
    }

    private ItemModelResolution? ResolveModel(string modelId)
    {
        if (!Identifier.TryParse(modelId, out var id))
        {
            return null;
        }

        var merged = new ModelParts();
        if (!LoadModel(id, merged, depth: 0))
        {
            return null;
        }

        // The reference ItemTransforms deserializer copies a missing left-hand transform from the right hand after
        // the whole parent chain has been merged.
        if (!merged.Display.ContainsKey(ItemDisplayContext.ThirdPersonLeftHand) &&
            merged.Display.TryGetValue(ItemDisplayContext.ThirdPersonRightHand, out var thirdRight))
        {
            merged.Display[ItemDisplayContext.ThirdPersonLeftHand] = thirdRight;
        }

        if (!merged.Display.ContainsKey(ItemDisplayContext.FirstPersonLeftHand) &&
            merged.Display.TryGetValue(ItemDisplayContext.FirstPersonRightHand, out var firstRight))
        {
            merged.Display[ItemDisplayContext.FirstPersonLeftHand] = firstRight;
        }

        return new ItemModelResolution(
            modelId,
            new Dictionary<string, string>(merged.Textures, StringComparer.Ordinal),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>(merged.Display),
            Elements: ResolveElementTextures(merged.Elements, merged.Textures));
    }

    /// <summary>
    /// Resolves every face's <c>#variable</c> reference through the fully merged texture map. The reference does this
    /// after merging the parent chain, which is why the raw names are kept while loading.
    /// </summary>
    private static IReadOnlyList<ItemModelElement> ResolveElementTextures(
        List<ItemModelElement> elements,
        IReadOnlyDictionary<string, string> textures)
    {
        if (elements.Count == 0)
        {
            return elements;
        }

        var resolved = new List<ItemModelElement>(elements.Count);
        foreach (var element in elements)
        {
            var faces = new Dictionary<ModelFaceDirection, ItemModelFace>(element.Faces.Count);
            foreach (var (direction, face) in element.Faces)
            {
                faces[direction] = face with
                {
                    Texture = face.Texture is { } raw ? ResolveTextureReference(raw, textures) : null
                };
            }

            resolved.Add(new ItemModelElement(element.From, element.To, faces));
        }

        return resolved;
    }

    private bool LoadModel(Identifier modelId, ModelParts target, int depth)
    {
        if (depth > 16)
        {
            return false;
        }

        var bytes = _resources.Read(ResourceKind.Model, modelId);
        if (bytes is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.TryGetProperty("parent", out var parentElement) &&
            parentElement.ValueKind == JsonValueKind.String &&
            Identifier.TryParse(parentElement.GetString()!, out var parent) &&
            parent.Path != "builtin/generated")
        {
            LoadModel(parent, target, depth + 1);
        }

        if (root.TryGetProperty("textures", out var textures) && textures.ValueKind == JsonValueKind.Object)
        {
            foreach (var texture in textures.EnumerateObject())
            {
                if (texture.Value.ValueKind == JsonValueKind.String)
                {
                    target.Textures[texture.Name] = texture.Value.GetString()!;
                }
            }
        }

        if (root.TryGetProperty("display", out var display) && display.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in display.EnumerateObject())
            {
                if (ContextNames.TryGetValue(entry.Name, out var context))
                {
                    target.Display[context] = ParseTransform(entry.Value);
                }
            }
        }

            // A model that declares elements replaces whatever the parent chain contributed, matching the reference
            // loader. Texture references are resolved later, once the whole chain has been merged.
            if (root.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                target.Elements = ParseElements(elements);
            }

        return true;
    }

    private static List<ItemModelElement> ParseElements(JsonElement elements)
    {
        var parsed = new List<ItemModelElement>(elements.GetArrayLength());
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !TryReadVector(element, "from", out var from) ||
                !TryReadVector(element, "to", out var to))
            {
                continue;
            }

            var faces = new Dictionary<ModelFaceDirection, ItemModelFace>();
            if (element.TryGetProperty("faces", out var faceObject) && faceObject.ValueKind == JsonValueKind.Object)
            {
                foreach (var face in faceObject.EnumerateObject())
                {
                    if (!FaceNames.TryGetValue(face.Name, out var direction) || face.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var faceElement = face.Value;
                    var texture = faceElement.TryGetProperty("texture", out var textureElement) &&
                        textureElement.ValueKind == JsonValueKind.String
                        ? textureElement.GetString()!
                        : null;
                    // Reference FaceBakery.defaultFaceUV is used whenever the JSON omits "uv".
                    var uv = faceElement.TryGetProperty("uv", out var uvElement) &&
                        uvElement.ValueKind == JsonValueKind.Array &&
                        uvElement.GetArrayLength() == 4
                        ? new Vector4(
                            uvElement[0].GetSingle(),
                            uvElement[1].GetSingle(),
                            uvElement[2].GetSingle(),
                            uvElement[3].GetSingle())
                        : ItemModelElement.AutoUv(direction, from, to);
                    var rotation = faceElement.TryGetProperty("rotation", out var rotationElement) &&
                        rotationElement.ValueKind == JsonValueKind.Number
                        ? rotationElement.GetInt32()
                        : 0;
                    var tintIndex = faceElement.TryGetProperty("tintindex", out var tintElement) &&
                        tintElement.ValueKind == JsonValueKind.Number
                        ? tintElement.GetInt32()
                        : -1;
                    ModelFaceDirection? cullFace = faceElement.TryGetProperty("cullface", out var cullElement) &&
                        cullElement.ValueKind == JsonValueKind.String &&
                        FaceNames.TryGetValue(cullElement.GetString()!, out var cullDirection)
                        ? cullDirection
                        : null;
                    faces[direction] = new ItemModelFace(texture, uv, rotation, tintIndex, cullFace);
                }
            }

            parsed.Add(new ItemModelElement(from, to, faces));
        }

        return parsed;
    }

    /// <summary>Follows <c>#variable</c> indirection through the merged texture map, as the reference loader does.</summary>
    private static string? ResolveTextureReference(string value, IReadOnlyDictionary<string, string> textures)
    {
        var current = value;
        for (var hop = 0; hop < 8; hop++)
        {
            if (current.StartsWith('#'))
            {
                if (!textures.TryGetValue(current[1..], out var next))
                {
                    return null;
                }

                current = next;
                continue;
            }

            return Identifier.TryParse(current, out var id) ? id.Path : current;
        }

        return null;
    }

    private static bool TryReadVector(JsonElement element, string name, out Vector3 value)
    {
        value = default;
        if (!element.TryGetProperty(name, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() != 3)
        {
            return false;
        }

        value = new Vector3(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
        return true;
    }

    private static ItemDisplayTransform ParseTransform(JsonElement element)
    {
        // Reference ItemTransform.Deserializer: rotation is taken raw, translation is scaled by 0.0625 *first* and then
        // clamped to +/-5 blocks, and scale is clamped to +/-4 in model units.
        var rotation = ReadVector(element, "rotation", Vector3.Zero, clamp: 0f);
        var translation = Clamp(ReadVector(element, "translation", Vector3.Zero, clamp: 0f) * 0.0625f, 5f);
        var scale = Clamp(ReadVector(element, "scale", Vector3.One, clamp: 0f), 4f);
        return new ItemDisplayTransform(rotation, translation, scale);
    }

    private static Vector3 Clamp(Vector3 vector, float limit) =>
        Vector3.Clamp(vector, new Vector3(-limit), new Vector3(limit));

    private static Vector3 ReadVector(JsonElement element, string name, Vector3 fallback, float clamp)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 3)
        {
            return fallback;
        }

        var values = new float[3];
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            values[index++] = item.GetSingle();
        }

        var vector = new Vector3(values[0], values[1], values[2]);
        return clamp > 0f ? Clamp(vector, clamp) : vector;
    }

    private static readonly IReadOnlyDictionary<string, ModelFaceDirection> FaceNames =
        new Dictionary<string, ModelFaceDirection>(StringComparer.Ordinal)
        {
            ["down"] = ModelFaceDirection.Down,
            ["up"] = ModelFaceDirection.Up,
            ["north"] = ModelFaceDirection.North,
            ["south"] = ModelFaceDirection.South,
            ["west"] = ModelFaceDirection.West,
            ["east"] = ModelFaceDirection.East
        };

    private sealed class ModelParts
    {
        public Dictionary<string, string> Textures { get; } = new(StringComparer.Ordinal);
        public Dictionary<ItemDisplayContext, ItemDisplayTransform> Display { get; } = [];
        public List<ItemModelElement> Elements { get; set; } = [];
    }
}
