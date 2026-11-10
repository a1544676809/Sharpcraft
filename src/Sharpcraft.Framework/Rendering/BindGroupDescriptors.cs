// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Stages a binding is visible to.</summary>
/// <remarks>
/// A mask rather than the reference's single stage value, because a layout entry is often shared by the vertex and the
/// fragment stage and describing that as two entries would change the layout.
/// </remarks>
[Flags]
public enum ShaderStages
{
    /// <summary>No stage; a binding must declare at least one.</summary>
    None = 0,

    /// <summary>Vertex stage.</summary>
    Vertex = 1,

    /// <summary>Fragment stage.</summary>
    Fragment = 2,

    /// <summary>Compute stage.</summary>
    Compute = 4
}

/// <summary>What kind of resource a binding expects.</summary>
public enum BindingKind
{
    /// <summary>A uniform block.</summary>
    UniformBuffer,

    /// <summary>A sampled texture.</summary>
    Texture,

    /// <summary>A sampler.</summary>
    Sampler,

    /// <summary>A writable storage buffer, as compute workgroups read and write.</summary>
    StorageBuffer,

    /// <summary>A storage buffer that is only read.</summary>
    ReadOnlyStorageBuffer
}

/// <summary>What kind of value a sampled texture returns.</summary>
public enum TextureSampleType
{
    /// <summary>Filtered floating point.</summary>
    Float,

    /// <summary>Floating point that cannot be filtered, as a high precision target returns.</summary>
    UnfilterableFloat,

    /// <summary>Depth comparison.</summary>
    Depth,

    /// <summary>Signed integer, unfilterable.</summary>
    Sint,

    /// <summary>Unsigned integer, unfilterable.</summary>
    Uint
}

/// <summary>Shape of the view a texture binding expects.</summary>
public enum TextureViewDimension
{
    /// <summary>A 2D texture.</summary>
    D2,

    /// <summary>An array of 2D textures.</summary>
    D2Array,

    /// <summary>A cube map.</summary>
    Cube,

    /// <summary>An array of cube maps.</summary>
    CubeArray,

    /// <summary>A 3D texture.</summary>
    D3
}

/// <summary>How a sampler binding is used.</summary>
public enum SamplerBindingType
{
    /// <summary>Filtering sampler.</summary>
    Filtering,

    /// <summary>Non-filtering sampler.</summary>
    NonFiltering,

    /// <summary>Comparison sampler, used for shadow lookups.</summary>
    Comparison
}

/// <summary>How a buffer binding is used.</summary>
public enum BufferBindingType
{
    /// <summary>Uniform block.</summary>
    Uniform,

    /// <summary>Read-only storage.</summary>
    ReadOnlyStorage,

    /// <summary>Read-write storage.</summary>
    Storage
}

/// <summary>One entry of a bind group layout.</summary>
/// <param name="Binding">Binding slot the shader declares.</param>
/// <param name="Visibility">Stages that may read the binding.</param>
/// <param name="Kind">Kind of resource the slot expects.</param>
public readonly record struct BindGroupLayoutEntryDesc(uint Binding, ShaderStages Visibility, BindingKind Kind)
{
    /// <summary>Smallest binding size the shader requires, or zero for no requirement.</summary>
    public ulong MinBindingSize { get; init; }

    /// <summary>Value a sampled texture returns; ignored for other kinds.</summary>
    public TextureSampleType SampleType { get; init; }

    /// <summary>Shape of the view a texture binding expects; ignored for other kinds.</summary>
    public TextureViewDimension ViewDimension { get; init; }

    /// <summary>Whether the texture binding is multisampled; ignored for other kinds.</summary>
    public bool Multisampled { get; init; }

    /// <summary>How a sampler binding is used; ignored for other kinds.</summary>
    public SamplerBindingType SamplerType { get; init; }

    /// <summary>How a buffer binding is used; ignored for other kinds.</summary>
    public BufferBindingType BufferType { get; init; }
    /// <summary>A uniform block visible to the given stages.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="visibility">Stages that may read it.</param>
    public static BindGroupLayoutEntryDesc UniformBuffer(uint binding, ShaderStages visibility) =>
        new(binding, visibility, BindingKind.UniformBuffer);

    /// <summary>A sampled texture visible to the given stages.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="visibility">Stages that may read it.</param>
    public static BindGroupLayoutEntryDesc Texture(uint binding, ShaderStages visibility) =>
        new(binding, visibility, BindingKind.Texture);

    /// <summary>A sampler visible to the given stages.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="visibility">Stages that may read it.</param>
    public static BindGroupLayoutEntryDesc Sampler(uint binding, ShaderStages visibility) =>
        new(binding, visibility, BindingKind.Sampler);
}

/// <summary>One entry of a bind group.</summary>
/// <remarks>
/// The factory methods are the only way to build one, so a slot cannot be filled with a resource of the wrong kind: the
/// mismatch that would otherwise surface as a validation error at draw time becomes a compile-time choice.
/// </remarks>
public readonly record struct BindGroupEntryDesc
{
    private BindGroupEntryDesc(uint binding, BindingKind kind, BufferHandle buffer, TextureViewHandle view, SamplerHandle sampler, ulong offset, ulong size)
    {
        Binding = binding;
        Kind = kind;
        BoundBuffer = buffer;
        BoundView = view;
        BoundSampler = sampler;
        Offset = offset;
        Size = size;
    }

    /// <summary>Binding slot this entry fills.</summary>
    public uint Binding { get; }

    /// <summary>Kind of resource in this entry.</summary>
    public BindingKind Kind { get; }

    /// <summary>Buffer for a uniform entry; empty otherwise.</summary>
    public BufferHandle BoundBuffer { get; }

    /// <summary>View for a texture entry; empty otherwise.</summary>
    public TextureViewHandle BoundView { get; }

    /// <summary>Sampler for a sampler entry; empty otherwise.</summary>
    public SamplerHandle BoundSampler { get; }

    /// <summary>Byte offset into the buffer, for a uniform entry.</summary>
    public ulong Offset { get; }

    /// <summary>Byte length bound, or zero for the whole buffer.</summary>
    public ulong Size { get; }

    /// <summary>A buffer range bound to a slot.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="buffer">Buffer to bind.</param>
    /// <param name="offset">Byte offset into the buffer.</param>
    /// <param name="size">Byte length, or zero for the rest of the buffer.</param>
    public static BindGroupEntryDesc Uniform(uint binding, BufferHandle buffer, ulong offset, ulong size) =>
        new(binding, BindingKind.UniformBuffer, buffer, default, default, offset, size);

    /// <summary>A texture view bound to a slot.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="view">View to bind.</param>
    public static BindGroupEntryDesc Texture(uint binding, TextureViewHandle view) =>
        new(binding, BindingKind.Texture, default, view, default, 0, 0);

    /// <summary>A sampler bound to a slot.</summary>
    /// <param name="binding">Binding slot.</param>
    /// <param name="sampler">Sampler to bind.</param>
    public static BindGroupEntryDesc Sampler(uint binding, SamplerHandle sampler) =>
        new(binding, BindingKind.Sampler, default, default, sampler, 0, 0);
}

/// <summary>An opaque bind group layout owned by a render backend.</summary>
public readonly struct BindGroupLayoutHandle
{
    internal BindGroupLayoutHandle(object value) => Value = value;

    /// <summary>Whether this handle refers to a resource the backend created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}

/// <summary>An opaque bind group owned by a render backend.</summary>
public readonly struct BindGroupHandle
{
    internal BindGroupHandle(object value) => Value = value;

    /// <summary>Whether this handle refers to a resource the backend created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}
