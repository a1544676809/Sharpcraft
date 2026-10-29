// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The pipeline the interface layer draws with: orthographic, no depth test, alpha blended
/// (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// This first slice draws solid colours only, which is everything the layout kernel needs to put panels, bars and
/// placeholder text on screen. The textured variant, for real glyphs from a bitmap font, arrives with the font
/// work and reuses the same batching and range splitting.
/// <para>
/// The vertex is the interface one, twenty bytes, with the colour carried as four normalised bytes. Positions
/// arrive in virtual pixels and the projection is a uniform, so resizing the window rewrites sixty four bytes
/// rather than the whole batch.
/// </para>
/// </remarks>
public static class UiRenderer
{
    /// <summary>The shader, kept here so the pipeline and its expectations cannot drift apart.</summary>
    public const string ShaderSourceText = """
struct Transform {
    matrix: mat4x4<f32>,
};

@group(0) @binding(2) var<uniform> transform: Transform;

struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) colour: vec4<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) colour: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    // Positions are already two dimensional; depth is fixed at zero because nothing in this layer occludes.
    output.position = transform.matrix * vec4<f32>(input.position, 0.0, 1.0);
    output.colour = input.colour;
    return output;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return input.colour;
}
""";

    /// <summary>Creates the interface pipeline.</summary>
    /// <param name="device">Device to create it on.</param>
    /// <param name="format">Colour target format, which must match the surface.</param>
    /// <param name="layout">Pipeline layout; only the transform uniform is read.</param>
    public static RenderPipeline CreatePipeline(Device device, WGPUTextureFormat format, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(ShaderSourceText))
        });

        var blend = new WGPUBlendState
        {
            color = new WGPUBlendComponent
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = WGPUBlendFactor.SrcAlpha,
                dstFactor = WGPUBlendFactor.OneMinusSrcAlpha
            },
            alpha = new WGPUBlendComponent
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = WGPUBlendFactor.One,
                dstFactor = WGPUBlendFactor.OneMinusSrcAlpha
            }
        };

        var target = new ColorTargetState(format) { Blend = blend };

        // Depth still has to be declared because the pass carries a depth attachment, but this layer never tests
        // or writes it: always passing and never writing is the cheapest way to say "no depth".
        var depth = DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false);

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [target],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: depth,
            vertexStride: (ulong)VertexLayout.UiStride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.UiAttributes());
    }
}
