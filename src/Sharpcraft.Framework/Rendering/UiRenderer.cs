// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The pipelines the interface layer draws with: orthographic, no depth test, alpha blended
/// (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// This first slice draws solid colours only, which is everything the layout kernel needs to put panels, bars and
/// placeholder text on screen. The textured variants, for real glyphs from a bitmap font and for GUI sprites, share the
/// same batching and range splitting.
/// <para>
/// The vertex is the interface one, twenty bytes, with the colour carried as four normalised bytes. Positions
/// arrive in virtual pixels and the projection is a uniform, so resizing the window rewrites sixty four bytes
/// rather than the whole batch.
/// </para>
/// <para>
/// Every pipeline is described to the seam rather than built on the device, so the interface draws with handles like
/// the rest of the frame and a second backend only has to implement <see cref="IRenderBackend"/>.
/// </para>
/// </remarks>
public static class UiRenderer
{
    /// <summary>The shader, kept here so the pipeline and its expectations cannot drift apart.</summary>
    public static readonly string ShaderSourceText = Shaders.ShaderLibrary.Load("ui_panel");

    /// <summary>Shader for GUI sprites: samples the sprite's own colours and tints by the vertex colour.</summary>
    public static readonly string SpriteShaderSourceText = Shaders.ShaderLibrary.Load("ui_sprite");

    /// <summary>Shader for text: samples the glyph sheet and tints by the vertex colour.</summary>
    public static readonly string TextShaderSourceText = Shaders.ShaderLibrary.Load("ui_text");

    /// <summary>The interface blend state: straight alpha over the frame, alpha accumulated the same way.</summary>
    private static BlendStateDesc OverlayBlend => new(
        new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
        new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha));

    /// <summary>Creates the textured interface pipeline used for text.</summary>
    /// <param name="backend">Backend that creates and owns the pipeline.</param>
    /// <param name="format">Colour target format.</param>
    /// <param name="layout">Pipeline layout; expects the glyph sheet, a sampler and the transform.</param>
    public static PipelineHandle CreateTextPipeline(IRenderBackend backend, TextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(TextShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_main",
            source,
            "fs_main",
            layout,
            [new ColorTargetDesc(format, OverlayBlend)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.UiStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.UiTextAttributes())));
    }

    /// <summary>
    /// Creates the interface pipeline used for GUI sprites, which samples the sprite's own colours.
    /// </summary>
    /// <param name="backend">Backend that creates and owns the pipeline.</param>
    /// <param name="format">Colour target format.</param>
    /// <param name="layout">Pipeline layout; expects a sprite texture, a sampler and the transform.</param>
    public static PipelineHandle CreateSpritePipeline(IRenderBackend backend, TextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(SpriteShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_main",
            source,
            "fs_main",
            layout,
            [new ColorTargetDesc(format, OverlayBlend)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.UiStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.UiTextAttributes())));
    }

    /// <summary>Creates the interface pipeline.</summary>
    /// <param name="backend">Backend that creates and owns the pipeline.</param>
    /// <param name="format">Colour target format, which must match the surface.</param>
    /// <param name="layout">Pipeline layout; only the transform uniform is read.</param>
    public static PipelineHandle CreatePipeline(IRenderBackend backend, TextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(ShaderSourceText);

        // Depth still has to be declared because the pass carries a depth attachment, but this layer never tests
        // or writes it: always passing and never writing is the cheapest way to say "no depth".
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_main",
            source,
            "fs_main",
            layout,
            [new ColorTargetDesc(format, OverlayBlend)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.UiStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.UiAttributes())));
    }
}
