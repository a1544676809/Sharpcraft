// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Which triangle faces are discarded.</summary>
public enum CullMode
{
    /// <summary>Draw both faces.</summary>
    None,

    /// <summary>Discard front faces.</summary>
    Front,

    /// <summary>Discard back faces.</summary>
    Back
}

/// <summary>Which winding counts as front facing.</summary>
public enum FrontFace
{
    /// <summary>Counter-clockwise in screen space.</summary>
    Ccw,

    /// <summary>Clockwise in screen space.</summary>
    Cw
}

/// <summary>How a fragment's value is compared with the existing one.</summary>
public enum CompareFunction
{
    /// <summary>Never passes.</summary>
    Never,

    /// <summary>Passes when less.</summary>
    Less,

    /// <summary>Passes when equal.</summary>
    Equal,

    /// <summary>Passes when less or equal.</summary>
    LessEqual,

    /// <summary>Passes when greater.</summary>
    Greater,

    /// <summary>Passes when different.</summary>
    NotEqual,

    /// <summary>Passes when greater or equal.</summary>
    GreaterEqual,

    /// <summary>Always passes.</summary>
    Always
}

/// <summary>How source and destination colours combine.</summary>
public enum BlendOperation
{
    /// <summary>Source plus destination.</summary>
    Add,

    /// <summary>Source minus destination.</summary>
    Subtract,

    /// <summary>Destination minus source.</summary>
    ReverseSubtract,

    /// <summary>Smaller of the two.</summary>
    Min,

    /// <summary>Larger of the two.</summary>
    Max
}

/// <summary>Weight applied to a blend operand.</summary>
public enum BlendFactor
{
    /// <summary>Zero.</summary>
    Zero,

    /// <summary>One.</summary>
    One,

    /// <summary>Source colour.</summary>
    Src,

    /// <summary>One minus source colour.</summary>
    OneMinusSrc,

    /// <summary>Source alpha.</summary>
    SrcAlpha,

    /// <summary>One minus source alpha.</summary>
    OneMinusSrcAlpha,

    /// <summary>Destination colour.</summary>
    Dst,

    /// <summary>One minus destination colour.</summary>
    OneMinusDst,

    /// <summary>Destination alpha.</summary>
    DstAlpha,

    /// <summary>One minus destination alpha.</summary>
    OneMinusDstAlpha
}

/// <summary>Per-channel component of a blend state.</summary>
/// <param name="Operation">How the operands combine.</param>
/// <param name="SrcFactor">Weight applied to the source.</param>
/// <param name="DstFactor">Weight applied to the destination.</param>
public readonly record struct BlendComponentDesc(BlendOperation Operation, BlendFactor SrcFactor, BlendFactor DstFactor);

/// <summary>Colour and alpha blend state.</summary>
/// <param name="Colour">Blend for the colour channels.</param>
/// <param name="Alpha">Blend for the alpha channel.</param>
public readonly record struct BlendStateDesc(BlendComponentDesc Colour, BlendComponentDesc Alpha);

/// <summary>One colour target of a pipeline.</summary>
/// <param name="Format">Format of the attachment.</param>
/// <param name="Blend">Blend state, or null to write the fragment value through.</param>
public readonly record struct ColorTargetDesc(TextureFormat Format, BlendStateDesc? Blend = null);

/// <summary>Depth attachment state of a pipeline.</summary>
/// <param name="Format">Format of the depth attachment.</param>
/// <param name="Compare">Comparison applied to the depth value.</param>
/// <param name="Write">Whether the depth value is written.</param>
public readonly record struct DepthStateDesc(TextureFormat Format, CompareFunction Compare, bool Write);

/// <summary>Format of one vertex attribute.</summary>
public enum VertexFormat
{
    /// <summary>Two 32-bit floats.</summary>
    Float32x2,

    /// <summary>Three 32-bit floats.</summary>
    Float32x3,

    /// <summary>Four 32-bit floats.</summary>
    Float32x4,

    /// <summary>Four unsigned 8-bit values normalised to [0, 1].</summary>
    Unorm8x4
}

/// <summary>One vertex attribute of a pipeline.</summary>
/// <param name="ShaderLocation">Location the shader reads.</param>
/// <param name="Offset">Byte offset inside the vertex.</param>
/// <param name="Format">Format of the attribute.</param>
public readonly record struct VertexAttributeDesc(uint ShaderLocation, uint Offset, VertexFormat Format);

/// <summary>Converts a declared vertex layout into the attributes a pipeline needs.</summary>
/// <remarks>
/// The conversion lives beside the descriptor so that the single-layout rule keeps holding: a renderer passes what
/// <c>VertexLayout</c> declares and never names the attribute type itself.
/// </remarks>
public static class VertexAttributes
{
    /// <summary>Converts the shared layout's attributes.</summary>
    /// <param name="attributes">Attributes as the shared layout declares them.</param>
    public static VertexAttributeDesc[] From(Brew.Wgpu.VertexAttribute[] attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var result = new VertexAttributeDesc[attributes.Length];
        for (var index = 0; index < attributes.Length; index++)
        {
            var attribute = attributes[index];
            result[index] = new VertexAttributeDesc(
                attribute.ShaderLocation,
                (uint)attribute.Offset,
                BackendConvert.FromWgpu(attribute.Format));
        }

        return result;
    }
}

/// <summary>How often a vertex buffer advances.</summary>
public enum VertexStepMode
{
    /// <summary>One element per vertex.</summary>
    Vertex,

    /// <summary>One element per instance.</summary>
    Instance
}

/// <summary>
/// Everything a render pipeline needs, in backend-neutral terms.
/// </summary>
/// <remarks>
/// The shader modules are passed as source bytes rather than as objects, because a pipeline is created from source in
/// this renderer and a handle to a module would add a lifetime the seam does not need yet.
/// </remarks>
/// <param name="VertexShader">Vertex shader source.</param>
/// <param name="VertexEntry">Vertex entry point name.</param>
/// <param name="FragmentShader">Fragment shader source.</param>
/// <param name="FragmentEntry">Fragment entry point name.</param>
/// <param name="Layout">Pipeline layout the draw must match, or null to let the backend derive one.</param>
/// <param name="ColourTargets">Colour targets, in attachment order.</param>
/// <param name="Depth">Depth state, or null when the pipeline has no depth attachment.</param>
/// <param name="Cull">Which faces to discard.</param>
/// <param name="Front">Which winding is front facing.</param>
/// <param name="VertexStride">Bytes per vertex.</param>
/// <param name="StepMode">How the vertex buffer advances.</param>
/// <param name="VertexAttributes">Attributes the vertex shader reads.</param>
public sealed record PipelineDesc(
    ReadOnlyMemory<byte> VertexShader,
    string VertexEntry,
    ReadOnlyMemory<byte> FragmentShader,
    string FragmentEntry,
    PipelineLayoutHandle? Layout,
    IReadOnlyList<ColorTargetDesc> ColourTargets,
    DepthStateDesc? Depth,
    CullMode Cull,
    FrontFace Front,
    ulong VertexStride,
    VertexStepMode StepMode,
    IReadOnlyList<VertexAttributeDesc> VertexAttributes);

/// <summary>An opaque pipeline layout owned by a render backend.</summary>
public readonly struct PipelineLayoutHandle
{
    internal PipelineLayoutHandle(object value) => Value = value;

    /// <summary>Whether this handle refers to a resource the backend created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}
