// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// No [ReferenceClass] annotation: Sharpcraft.Framework must not reference any Sharpcraft.* assembly
// (tests/Sharpcraft.Architecture.Tests/AssemblyBoundaryTests.cs), so the attribute in Sharpcraft.Common.Reference cannot
// resolve here, and tools/Sharpcraft.ClassMap does not scan this project.

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>An opaque texture owned by a render backend.</summary>
/// <remarks>
/// Handles carry no usable surface: a caller can pass one back to the backend that created it and nothing else. That is
/// what lets a second backend exist later without every call site changing.
/// </remarks>
public readonly struct TextureHandle
{
    internal TextureHandle(object value) => Value = value;

    internal object Value { get; }

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;
}

/// <summary>An opaque texture view owned by a render backend.</summary>
public readonly struct TextureViewHandle
{
    internal TextureViewHandle(object value) => Value = value;

    internal object Value { get; }

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;
}

/// <summary>An opaque buffer owned by a render backend.</summary>
public readonly struct BufferHandle
{
    internal BufferHandle(object value) => Value = value;

    internal object Value { get; }

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;
}

/// <summary>An opaque sampler owned by a render backend.</summary>
public readonly struct SamplerHandle
{
    internal SamplerHandle(object value) => Value = value;

    internal object Value { get; }

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;
}

/// <summary>An opaque render pipeline owned by a render backend.</summary>
public readonly struct PipelineHandle
{
    internal PipelineHandle(object value) => Value = value;

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}

/// <summary>How a texture is created.</summary>
/// <param name="Width">Width in texels.</param>
/// <param name="Height">Height in texels.</param>
/// <param name="Format">Colour or depth format.</param>
/// <param name="MipLevelCount">Levels including level zero.</param>
/// <param name="Usage">What the texture may be used for.</param>
public readonly record struct TextureDesc(uint Width, uint Height, TextureFormat Format, uint MipLevelCount, TextureUsages Usage);

/// <summary>How a buffer is created.</summary>
/// <param name="Size">Size in bytes.</param>
/// <param name="Usage">What the buffer may be used for.</param>
public readonly record struct BufferDesc(ulong Size, BufferUsages Usage);

/// <summary>What a buffer may be used for.</summary>
[Flags]
public enum BufferUsages
{
    /// <summary>Nothing yet; a buffer must declare at least one use.</summary>
    None = 0,

    /// <summary>Read as vertex data.</summary>
    Vertex = 1,

    /// <summary>Read as index data.</summary>
    Index = 2,

    /// <summary>Read as a uniform block.</summary>
    Uniform = 4,

    /// <summary>Read as storage.</summary>
    Storage = 8,

    /// <summary>Source of a copy.</summary>
    CopySrc = 16,

    /// <summary>Destination of a copy or upload.</summary>
    CopyDst = 32,

    /// <summary>Readable from the host.</summary>
    MapRead = 64,

    /// <summary>Source of indirect draw or dispatch arguments.</summary>
    Indirect = 128
}

/// <summary>What a texture may be used for.</summary>
[Flags]
public enum TextureUsages
{
    /// <summary>Nothing yet; a texture must declare at least one use.</summary>
    None = 0,

    /// <summary>Rendered into as a colour or depth attachment.</summary>
    RenderAttachment = 1,

    /// <summary>Sampled in a shader.</summary>
    TextureBinding = 2,

    /// <summary>Source of a copy.</summary>
    CopySrc = 4,

    /// <summary>Destination of a copy or upload.</summary>
    CopyDst = 8
}

/// <summary>An opaque command encoder owned by a render backend.</summary>
/// <remarks>
/// Recording is a sequence, not a resource: an encoder is created, filled with passes, submitted once, and then dead. It
/// has no destroy call for that reason, which is the first place this seam departs from the resource handles.
/// </remarks>
public readonly struct CommandEncoderHandle
{
    internal CommandEncoderHandle(object value) => Value = value;

    /// <summary>Whether this handle refers to an encoder the backend created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}

/// <summary>An opaque compute pipeline owned by a render backend.</summary>
public readonly struct ComputePipelineHandle
{
    internal ComputePipelineHandle(object value) => Value = value;

    /// <summary>Whether this handle wraps a resource that was actually created.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}

/// <summary>An opaque render pass owned by a render backend.</summary>
/// <remarks>
/// A pass is borrowed from its encoder and must end before the encoder is submitted; there is no destroy call, only
/// <c>EndRenderPass</c>, and reusing one after it ends is an error rather than a no-op.
/// </remarks>
public readonly struct RenderPassHandle
{
    internal RenderPassHandle(object value) => Value = value;

    /// <summary>Whether this handle refers to a pass the backend began.</summary>
    public bool IsValid => Value is not null;

    internal object Value { get; }
}

/// <summary>
/// The seam a rendering backend implements./// </summary>
/// <remarks>
/// Principle 6 asks for every graphics call to converge in one abstraction layer so the backend can be replaced; this is
/// that layer's contract (docs/analysis/architecture/render-backend-seam.md). The interface deliberately covers only what
/// this renderer actually uses: copying wgpu's whole model would make a second backend write a wrapper rather than an
/// implementation. Nothing here may name a backend type.
/// </remarks>
public interface IRenderBackend
{
    /// <summary>Backend identifier, used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Creates a texture.</summary>
    TextureHandle CreateTexture(in TextureDesc desc);

    /// <summary>Uploads pixels into one mip level.</summary>
    void WriteTexture(TextureHandle texture, ReadOnlySpan<byte> pixels, uint bytesPerRow, uint width, uint height, uint mipLevel);

    /// <summary>Creates a view over the whole texture.</summary>
    TextureViewHandle CreateTextureView(TextureHandle texture);

    /// <summary>Creates a sampler from the state the caller asked for.</summary>
    SamplerHandle CreateSampler(AddressMode addressMode, FilterMode minFilter, FilterMode magFilter, bool useMipmaps);

    /// <summary>Begins a render pass on the encoder.</summary>
    RenderPassHandle BeginRenderPass(CommandEncoderHandle encoder, ReadOnlySpan<ColorAttachmentDesc> colours, in DepthAttachmentDesc? depth);

    /// <summary>Ends a render pass. The handle is dead afterwards.</summary>
    void EndRenderPass(RenderPassHandle pass);

    /// <summary>Restricts subsequent draws to a rectangle.</summary>
    void SetScissorRect(RenderPassHandle pass, uint x, uint y, uint width, uint height);

    /// <summary>Selects the pipeline subsequent draws use.</summary>
    void SetPipeline(RenderPassHandle pass, PipelineHandle pipeline);

    /// <summary>Binds a bind group at a slot.</summary>
    void SetBindGroup(RenderPassHandle pass, uint index, BindGroupHandle group);

    /// <summary>Binds a vertex buffer at a slot.</summary>
    void SetVertexBuffer(RenderPassHandle pass, uint slot, BufferHandle buffer);

    /// <summary>Binds a byte range of a vertex buffer at a slot.</summary>
    void SetVertexBuffer(RenderPassHandle pass, uint slot, BufferHandle buffer, ulong offset, ulong size);

    /// <summary>Binds the index buffer.</summary>
    void SetIndexBuffer(RenderPassHandle pass, BufferHandle buffer, IndexFormat format);

    /// <summary>Binds a byte range of the index buffer.</summary>
    void SetIndexBuffer(RenderPassHandle pass, BufferHandle buffer, IndexFormat format, ulong offset, ulong size);

    /// <summary>Draws vertices without an index buffer.</summary>
    void Draw(RenderPassHandle pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    /// <summary>Draws indexed vertices.</summary>
    void DrawIndexed(RenderPassHandle pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance);

    /// <summary>Compiles a compute pipeline from its description.</summary>
    ComputePipelineHandle CreateComputePipeline(in ComputePipelineDesc desc);

    /// <summary>Releases a compute pipeline.</summary>
    void Destroy(ComputePipelineHandle pipeline);

    /// <summary>Creates a command encoder for one frame's recording.</summary>
    CommandEncoderHandle CreateCommandEncoder();

    /// <summary>Submits the encoder's recorded commands and releases it.</summary>
    void Submit(CommandEncoderHandle encoder);

    /// <summary>Creates a pipeline layout from the bind group layouts a draw will set.</summary>
    PipelineLayoutHandle CreatePipelineLayout(ReadOnlySpan<BindGroupLayoutHandle> layouts);

    /// <summary>Creates a render pipeline from a neutral description.</summary>
    PipelineHandle CreateRenderPipeline(in PipelineDesc desc);

    /// <summary>Destroys a pipeline.</summary>
    void Destroy(PipelineHandle pipeline);

    /// <summary>Destroys a pipeline layout.</summary>
    void Destroy(PipelineLayoutHandle layout);

    /// <summary>Creates a bind group layout.</summary>
    BindGroupLayoutHandle CreateBindGroupLayout(ReadOnlySpan<BindGroupLayoutEntryDesc> entries);

    /// <summary>Creates a bind group from a layout and its entries.</summary>
    BindGroupHandle CreateBindGroup(BindGroupLayoutHandle layout, ReadOnlySpan<BindGroupEntryDesc> entries);

    /// <summary>Destroys a bind group.</summary>
    void Destroy(BindGroupHandle group);

    /// <summary>Destroys a bind group layout.</summary>
    void Destroy(BindGroupLayoutHandle layout);

    /// <summary>Creates a buffer and fills it in one step.</summary>
    BufferHandle CreateBufferInit(in BufferDesc desc, ReadOnlySpan<byte> data);

    /// <summary>Creates a buffer.</summary>
    BufferHandle CreateBuffer(in BufferDesc desc);

    /// <summary>Writes bytes into a buffer at an offset.</summary>
    void WriteBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, ulong offset);

    /// <summary>Destroys a buffer.</summary>
    void Destroy(BufferHandle buffer);

    /// <summary>Destroys a texture.</summary>

    void Destroy(TextureHandle texture);

    /// <summary>Destroys a texture view.</summary>
    void Destroy(TextureViewHandle view);

    /// <summary>Destroys a sampler.</summary>
    void Destroy(SamplerHandle sampler);
}

/// <summary>
/// The wgpu implementation of <see cref="IRenderBackend"/>.
/// </summary>
/// <remarks>
/// This type is the only place where the seam meets the wgpu bindings, so a second backend would sit beside it rather
/// than replacing it.
/// </remarks>
public sealed class WgpuRenderBackend : IRenderBackend
{
    private readonly Device _device;

    /// <summary>Wraps an existing device.</summary>
    /// <param name="device">Device that owns every resource this backend creates.</param>
    public WgpuRenderBackend(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    /// <inheritdoc/>
    public string Name => "wgpu";

    /// <summary>The wrapped device, for the code that has not moved behind the seam yet.</summary>
    public Device Device => _device;

    /// <summary>Unwraps a handle, rejecting one that was never created (for example <c>default</c>).</summary>
    private static T RequireValid<T>(bool isValid, object? value) =>
        isValid && value is not null
            ? (T)value
            : throw new InvalidOperationException("the handle was never created");

    /// <inheritdoc/>
    public TextureHandle CreateTexture(in TextureDesc desc) =>
        new(_device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = desc.Width, height = desc.Height, depthOrArrayLayers = 1 },
            MipLevelCount = desc.MipLevelCount,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = BackendConvert.ToWgpu(desc.Format),
            Usage = ToWgpu(desc.Usage)
        }));

    /// <inheritdoc/>
    public void WriteTexture(TextureHandle texture, ReadOnlySpan<byte> pixels, uint bytesPerRow, uint width, uint height, uint mipLevel)
    {
        var target = RequireValid<Texture>(texture.IsValid, texture.Value);

        // The extent is stated by the caller rather than derived from the row pitch: four bytes per texel only holds for
        // the 8-bit RGBA formats, and deriving it would write a quarter of a half-float texture.
        _device.Queue.WriteTexture(
            target,
            pixels.ToArray(),
            bytesPerRow,
            height,
            new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            mipLevel,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });
    }

    /// <summary>Unwraps a view handle for code that has not moved behind the seam yet.</summary>
    /// <remarks>
    /// The seam forbids backend types in its own signatures, so this lives on the implementation and disappears with the
    /// device escape hatch once every consumer is wired.
    /// </remarks>
    /// <param name="view">Handle from this backend.</param>
    internal TextureView AsTextureView(TextureViewHandle view) => RequireValid<TextureView>(view.IsValid, view.Value);

    /// <summary>Unwraps a texture handle for code that has not moved behind the seam yet.</summary>
    /// <param name="texture">Handle from this backend.</param>
    internal Texture AsTexture(TextureHandle texture) => RequireValid<Texture>(texture.IsValid, texture.Value);

    /// <summary>Unwraps a buffer handle for code that has not moved behind the seam yet.</summary>
    /// <param name="buffer">Handle from this backend.</param>
    internal Brew.Wgpu.Buffer AsBuffer(BufferHandle buffer) => RequireValid<Brew.Wgpu.Buffer>(buffer.IsValid, buffer.Value);

    /// <summary>Unwraps a sampler handle for code that has not moved behind the seam yet.</summary>
    /// <param name="sampler">Handle from this backend.</param>
    internal Sampler AsSampler(SamplerHandle sampler) => RequireValid<Sampler>(sampler.IsValid, sampler.Value);

    /// <summary>Wraps a native object that the caller already owns, so it can be passed as a handle.</summary>
    /// <remarks>
    /// Adopting transfers no ownership: whoever created the object still destroys it, and the adopter must never hand the
    /// handle to <c>Destroy</c>. This exists so a call site can keep its current lifetime while its callee speaks handles,
    /// which is what keeps a migration pixel-identical; the adoption points disappear as creation moves into the seam.
    /// </remarks>
    /// <param name="buffer">Buffer the caller owns.</param>
    internal BufferHandle AdoptBuffer(Brew.Wgpu.Buffer buffer) => new(buffer);

    /// <summary>Wraps a sampler the caller owns.</summary>
    /// <param name="sampler">Sampler the caller owns.</param>
    internal SamplerHandle AdoptSampler(Sampler sampler) => new(sampler);

    /// <summary>Wraps a bind group layout the caller owns.</summary>
    /// <param name="layout">Layout the caller owns.</param>
    internal BindGroupLayoutHandle AdoptBindGroupLayout(BindGroupLayout layout) => new(layout);

    /// <summary>Unwraps a bind group handle for code that has not moved behind the seam yet.</summary>
    /// <param name="group">Handle from this backend.</param>
    internal BindGroup AsBindGroup(BindGroupHandle group) => RequireValid<BindGroup>(group.Value is not null, group.Value);

    /// <summary>Unwraps a bind group layout handle for code that has not moved behind the seam yet.</summary>
    /// <param name="layout">Handle from this backend.</param>
    internal BindGroupLayout AsBindGroupLayout(BindGroupLayoutHandle layout) => RequireValid<BindGroupLayout>(layout.Value is not null, layout.Value);

    /// <summary>Unwraps a pipeline handle for code that has not moved behind the seam yet.</summary>
    /// <param name="pipeline">Handle from this backend.</param>
    internal RenderPipeline AsPipeline(PipelineHandle pipeline) =>
        RequireValid<RenderPipeline>(pipeline.Value is not null, pipeline.Value);

    /// <summary>Unwraps a pipeline layout handle for code that has not moved behind the seam yet.</summary>
    /// <param name="layout">Handle from this backend.</param>
    internal PipelineLayout AsPipelineLayout(PipelineLayoutHandle layout) =>
        RequireValid<PipelineLayout>(layout.Value is not null, layout.Value);

    /// <summary>Unwraps a compute pipeline handle for code that has not moved behind the seam yet.</summary>
    /// <param name="pipeline">Handle from this backend.</param>
    internal ComputePipeline AsComputePipeline(ComputePipelineHandle pipeline) =>
        RequireValid<ComputePipeline>(pipeline.Value is not null, pipeline.Value);

    /// <summary>Wraps a command encoder the caller already owns.</summary>
    /// <param name="encoder">Encoder the caller owns.</param>
    internal CommandEncoderHandle AdoptCommandEncoder(CommandEncoder encoder) => new(encoder);

    /// <summary>Unwraps a command encoder for code that has not moved behind the seam yet.</summary>
    /// <param name="encoder">Handle from this backend.</param>
    internal CommandEncoder AsCommandEncoder(CommandEncoderHandle encoder) =>
        RequireValid<CommandEncoder>(encoder.Value is not null, encoder.Value);

    /// <summary>Unwraps a render pass for code that has not moved behind the seam yet.</summary>
    /// <param name="pass">Handle from this backend.</param>
    internal RenderPassEncoder AsRenderPass(RenderPassHandle pass) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value);

    /// <summary>Wraps a texture view the caller already owns.</summary>
    /// <param name="view">View the caller owns.</param>
    internal TextureViewHandle AdoptTextureView(TextureView view) => new(view);

    /// <summary>Wraps a texture the caller already owns.</summary>
    /// <param name="texture">Texture the caller owns.</param>
    internal TextureHandle AdoptTexture(Texture texture) => new(texture);

    /// <summary>Wraps a bind group the caller already owns.</summary>
    /// <param name="group">Group the caller owns.</param>
    internal BindGroupHandle AdoptBindGroup(BindGroup group) => new(group);

    /// <summary>Wraps a pipeline the caller already owns.</summary>
    /// <param name="pipeline">Pipeline the caller owns.</param>
    internal PipelineHandle AdoptPipeline(RenderPipeline pipeline) => new(pipeline);

    /// <summary>Wraps a pipeline layout the caller already owns.</summary>
    /// <param name="layout">Layout the caller owns.</param>
    internal PipelineLayoutHandle AdoptPipelineLayout(PipelineLayout layout) => new(layout);

    /// <inheritdoc/>
    public TextureViewHandle CreateTextureView(TextureHandle texture) =>
        new(RequireValid<Texture>(texture.IsValid, texture.Value).CreateView());

    /// <inheritdoc/>
    public SamplerHandle CreateSampler(AddressMode addressMode, FilterMode minFilter, FilterMode magFilter, bool useMipmaps) =>
        new(SamplerCache.Create(_device, addressMode, minFilter, magFilter, useMipmaps));

    /// <inheritdoc/>
    public RenderPassHandle BeginRenderPass(
        CommandEncoderHandle encoder,
        ReadOnlySpan<ColorAttachmentDesc> colours,
        in DepthAttachmentDesc? depth)
    {
        var native = RequireValid<CommandEncoder>(encoder.Value is not null, encoder.Value);

        var attachments = new ColorAttachment[colours.Length];
        for (var index = 0; index < colours.Length; index++)
        {
            var colour = colours[index];
            attachments[index] = new ColorAttachment
            {
                View = RequireValid<TextureView>(colour.View.Value is not null, colour.View.Value),
                LoadOp = colour.Load == LoadOp.Clear ? WGPULoadOp.Clear : WGPULoadOp.Load,
                StoreOp = colour.Store == StoreOp.Store ? WGPUStoreOp.Store : WGPUStoreOp.Discard,
                ClearValue = new WGPUColor
                {
                    r = colour.ClearColour.X,
                    g = colour.ClearColour.Y,
                    b = colour.ClearColour.Z,
                    a = colour.ClearColour.W
                }
            };
        }

        var depthView = depth is { } state
            ? RequireValid<TextureView>(state.View.Value is not null, state.View.Value)
            : default;

        var pass = depth is { } descriptor
            ? native.BeginRenderPass(attachments, new DepthStencilAttachment
            {
                View = depthView,
                DepthLoadOp = descriptor.Load == LoadOp.Clear ? WGPULoadOp.Clear : WGPULoadOp.Load,
                DepthStoreOp = descriptor.Store == StoreOp.Store ? WGPUStoreOp.Store : WGPUStoreOp.Discard,
                DepthClearValue = descriptor.ClearDepth
            })
            : native.BeginRenderPass(attachments, default);

        return new RenderPassHandle(pass);
    }

    /// <inheritdoc/>
    public void EndRenderPass(RenderPassHandle pass) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value).End();

    /// <inheritdoc/>
    public void SetScissorRect(RenderPassHandle pass, uint x, uint y, uint width, uint height) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value).SetScissorRect(x, y, width, height);

    /// <inheritdoc/>
    public void SetPipeline(RenderPassHandle pass, PipelineHandle pipeline) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetPipeline(RequireValid<RenderPipeline>(pipeline.Value is not null, pipeline.Value));

    /// <inheritdoc/>
    public void SetBindGroup(RenderPassHandle pass, uint index, BindGroupHandle group) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetBindGroup(index, RequireValid<BindGroup>(group.Value is not null, group.Value));

    /// <inheritdoc/>
    public void SetVertexBuffer(RenderPassHandle pass, uint slot, BufferHandle buffer, ulong offset, ulong size) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetVertexBuffer(slot, RequireValid<Brew.Wgpu.Buffer>(buffer.Value is not null, buffer.Value), offset, size);

    /// <inheritdoc/>
    public void SetIndexBuffer(RenderPassHandle pass, BufferHandle buffer, IndexFormat format, ulong offset, ulong size) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetIndexBuffer(
                RequireValid<Brew.Wgpu.Buffer>(buffer.Value is not null, buffer.Value),
                format == IndexFormat.Uint16 ? WGPUIndexFormat.Uint16 : WGPUIndexFormat.Uint32,
                offset,
                size);

    /// <inheritdoc/>
    public void SetVertexBuffer(RenderPassHandle pass, uint slot, BufferHandle buffer) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetVertexBuffer(slot, RequireValid<Brew.Wgpu.Buffer>(buffer.Value is not null, buffer.Value));

    /// <inheritdoc/>
    public void SetIndexBuffer(RenderPassHandle pass, BufferHandle buffer, IndexFormat format) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .SetIndexBuffer(
                RequireValid<Brew.Wgpu.Buffer>(buffer.Value is not null, buffer.Value),
                format == IndexFormat.Uint16 ? WGPUIndexFormat.Uint16 : WGPUIndexFormat.Uint32);

    /// <inheritdoc/>
    public void Draw(RenderPassHandle pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .Draw(vertexCount, instanceCount, firstVertex, firstInstance);

    /// <inheritdoc/>
    public void DrawIndexed(RenderPassHandle pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance) =>
        RequireValid<RenderPassEncoder>(pass.Value is not null, pass.Value)
            .DrawIndexed(indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    /// <inheritdoc/>
    public ComputePipelineHandle CreateComputePipeline(in ComputePipelineDesc desc)
    {
        var layout = RequireValid<PipelineLayout>(desc.Layout.Value is not null, desc.Layout.Value);

        // The native call wants a compiled module, so the seam is where that module is made and released; a caller
        // states its shader as source, exactly as it does for a render pipeline.
        using var module = _device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(desc.Shader.ToArray())
        });

        return new ComputePipelineHandle(_device.CreateComputePipeline(
            module,
            System.Text.Encoding.UTF8.GetBytes(desc.EntryPoint),
            layout));
    }

    /// <inheritdoc/>
    public void Destroy(ComputePipelineHandle pipeline) =>
        RequireValid<ComputePipeline>(pipeline.Value is not null, pipeline.Value).Dispose();

    /// <inheritdoc/>
    public CommandEncoderHandle CreateCommandEncoder() => new(_device.CreateCommandEncoder());

    /// <inheritdoc/>
    public void Submit(CommandEncoderHandle encoder)
    {
        var native = RequireValid<CommandEncoder>(encoder.Value is not null, encoder.Value);
        using (native)
        {
            _device.Queue.Submit([native.Finish()]);
        }
    }

    /// <inheritdoc/>
    public PipelineLayoutHandle CreatePipelineLayout(ReadOnlySpan<BindGroupLayoutHandle> layouts)
    {
        var native = new BindGroupLayout[layouts.Length];
        for (var index = 0; index < layouts.Length; index++)
        {
            native[index] = RequireValid<BindGroupLayout>(layouts[index].Value is not null, layouts[index].Value);
        }

        return new PipelineLayoutHandle(_device.CreatePipelineLayout(native));
    }

    /// <inheritdoc/>
    public PipelineHandle CreateRenderPipeline(in PipelineDesc desc)
    {
        using var vertexShader = _device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(desc.VertexShader.ToArray())
        });
        using var fragmentShader = _device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(desc.FragmentShader.ToArray())
        });

        var targets = new ColorTargetState[desc.ColourTargets.Count];
        for (var index = 0; index < targets.Length; index++)
        {
            var target = desc.ColourTargets[index];
            targets[index] = target.Blend is { } blend
                ? new ColorTargetState
                {
                    Format = BackendConvert.ToWgpu(target.Format),
                    WriteMask = ColorWriteMask.All,
                    Blend = new WGPUBlendState
                    {
                        color = ToWgpu(blend.Colour),
                        alpha = ToWgpu(blend.Alpha)
                    }
                }
                : new ColorTargetState(BackendConvert.ToWgpu(target.Format));
        }

        var attributes = new VertexAttribute[desc.Attributes.Count];
        for (var index = 0; index < attributes.Length; index++)
        {
            var attribute = desc.Attributes[index];
            attributes[index] = new VertexAttribute
            {
                ShaderLocation = attribute.ShaderLocation,
                Offset = (ulong)attribute.Offset,
                Format = ToWgpu(attribute.Format)
            };
        }

        var depth = desc.Depth is { } state
            ? DepthStencilState.Depth(
                BackendConvert.ToWgpu(state.Format),
                ToWgpu(state.Compare),
                write: state.Write)
            : default;

        var vertexEntry = System.Text.Encoding.UTF8.GetBytes(desc.VertexEntry);
        var fragmentEntry = System.Text.Encoding.UTF8.GetBytes(desc.FragmentEntry);
        var primitive = PrimitiveState.Default with { CullMode = ToWgpu(desc.Cull), FrontFace = ToWgpu(desc.Front) };
        var stepMode = desc.StepMode == VertexStepMode.Instance ? WGPUVertexStepMode.Instance : WGPUVertexStepMode.Vertex;

        // A pipeline without a layout lets the backend derive one from the shaders, which is what the reference does for
        // its sky passes; building an empty explicit layout instead would change what the pipeline accepts.
        return desc.Layout is { } layout
            ? new PipelineHandle(_device.CreateRenderPipeline(
                vertexShader,
                vertexEntry,
                fragmentShader,
                fragmentEntry,
                targets,
                layout: RequireValid<PipelineLayout>(layout.Value is not null, layout.Value),
                primitive: primitive,
                multisample: MultisampleState.Default,
                depthStencil: depth,
                vertexStride: desc.VertexStride,
                vertexStepMode: stepMode,
                vertexAttributes: attributes))
            : new PipelineHandle(_device.CreateRenderPipeline(
                vertexShader,
                vertexEntry,
                fragmentShader,
                fragmentEntry,
                targets,
                primitive: primitive,
                multisample: MultisampleState.Default,
                depthStencil: depth,
                vertexStride: desc.VertexStride,
                vertexStepMode: stepMode,
                vertexAttributes: attributes));
    }

    /// <inheritdoc/>
    public void Destroy(PipelineHandle pipeline) => RequireValid<RenderPipeline>(pipeline.Value is not null, pipeline.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(PipelineLayoutHandle layout) => RequireValid<PipelineLayout>(layout.Value is not null, layout.Value).Dispose();

    /// <inheritdoc/>
    public BindGroupLayoutHandle CreateBindGroupLayout(ReadOnlySpan<BindGroupLayoutEntryDesc> entries)
    {
        var native = new BindGroupLayoutEntry[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var stage = ToWgpu(entry.Visibility);
            native[index] = entry.Kind switch
            {
                BindingKind.UniformBuffer => BindGroupLayoutEntry.Buffer(
                    entry.Binding,
                    stage,
                    ToWgpu(entry.BufferType),
                    false,
                    entry.MinBindingSize),
                BindingKind.Texture => BindGroupLayoutEntry.Texture(
                    entry.Binding,
                    stage,
                    ToWgpu(entry.SampleType),
                    ToWgpu(entry.ViewDimension),
                    entry.Multisampled),
                BindingKind.Sampler => BindGroupLayoutEntry.Sampler(entry.Binding, stage, ToWgpu(entry.SamplerType)),
                _ => throw new ArgumentOutOfRangeException(nameof(entries), entry.Kind, "no layout counterpart")
            };
        }

        return new BindGroupLayoutHandle(_device.CreateBindGroupLayout(native));
    }

    /// <inheritdoc/>
    public BindGroupHandle CreateBindGroup(BindGroupLayoutHandle layout, ReadOnlySpan<BindGroupEntryDesc> entries)
    {
        var nativeLayout = RequireValid<BindGroupLayout>(layout.Value is not null, layout.Value);
        var native = new BindGroupEntry[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            native[index] = entry.Kind switch
            {
                BindingKind.UniformBuffer => BindGroupEntry.Buffer(entry.Binding, RequireValid<Brew.Wgpu.Buffer>(entry.BoundBuffer.IsValid, entry.BoundBuffer.Value), entry.Offset, entry.Size),
                BindingKind.Texture => BindGroupEntry.TextureView(entry.Binding, RequireValid<TextureView>(entry.BoundView.IsValid, entry.BoundView.Value)),
                BindingKind.Sampler => BindGroupEntry.Sampler(entry.Binding, RequireValid<Sampler>(entry.BoundSampler.IsValid, entry.BoundSampler.Value)),
                _ => throw new ArgumentOutOfRangeException(nameof(entries), entry.Kind, "no entry counterpart")
            };
        }

        return new BindGroupHandle(_device.CreateBindGroup(nativeLayout, native));
    }

    /// <inheritdoc/>
    public void Destroy(BindGroupHandle group) => RequireValid<BindGroup>(group.Value is not null, group.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(BindGroupLayoutHandle layout) => RequireValid<BindGroupLayout>(layout.Value is not null, layout.Value).Dispose();

    /// <inheritdoc/>
    public BufferHandle CreateBufferInit(in BufferDesc desc, ReadOnlySpan<byte> data) =>
        new(_device.CreateBufferInit(
            new BufferInitDescriptor { Usage = ToWgpu(desc.Usage) },
            data.ToArray()));

    /// <inheritdoc/>
    public BufferHandle CreateBuffer(in BufferDesc desc) =>
        new(_device.CreateBuffer(new BufferDescriptor
        {
            Size = desc.Size,
            Usage = ToWgpu(desc.Usage)
        }));

    /// <inheritdoc/>
    public void WriteBuffer(BufferHandle buffer, ReadOnlySpan<byte> data, ulong offset) =>
        _device.Queue.WriteBuffer<byte>(RequireValid<Brew.Wgpu.Buffer>(buffer.IsValid, buffer.Value), offset, data);

    /// <inheritdoc/>
    public void Destroy(BufferHandle buffer) => RequireValid<Brew.Wgpu.Buffer>(buffer.IsValid, buffer.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(TextureHandle texture) => RequireValid<Texture>(texture.IsValid, texture.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(TextureViewHandle view) => RequireValid<TextureView>(view.IsValid, view.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(SamplerHandle sampler) => RequireValid<Sampler>(sampler.IsValid, sampler.Value).Dispose();

    private static WGPUBufferBindingType ToWgpu(BufferBindingType type) => type switch
    {
        BufferBindingType.Uniform => WGPUBufferBindingType.Uniform,
        BufferBindingType.ReadOnlyStorage => WGPUBufferBindingType.ReadOnlyStorage,
        BufferBindingType.Storage => WGPUBufferBindingType.Storage,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no binding counterpart")
    };

    private static WGPUTextureSampleType ToWgpu(TextureSampleType type) => type switch
    {
        TextureSampleType.Float => WGPUTextureSampleType.Float,
        TextureSampleType.Depth => WGPUTextureSampleType.Depth,
        TextureSampleType.Sint => WGPUTextureSampleType.Sint,
        TextureSampleType.Uint => WGPUTextureSampleType.Uint,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no sample counterpart")
    };

    private static WGPUTextureViewDimension ToWgpu(TextureViewDimension dimension) => dimension switch
    {
        TextureViewDimension.D2 => WGPUTextureViewDimension._2D,
        TextureViewDimension.D2Array => WGPUTextureViewDimension._2DArray,
        TextureViewDimension.Cube => WGPUTextureViewDimension.Cube,
        TextureViewDimension.CubeArray => WGPUTextureViewDimension.CubeArray,
        TextureViewDimension.D3 => WGPUTextureViewDimension._3D,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "no dimension counterpart")
    };

    private static WGPUSamplerBindingType ToWgpu(SamplerBindingType type) => type switch
    {
        SamplerBindingType.Filtering => WGPUSamplerBindingType.Filtering,
        SamplerBindingType.NonFiltering => WGPUSamplerBindingType.NonFiltering,
        SamplerBindingType.Comparison => WGPUSamplerBindingType.Comparison,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no sampler counterpart")
    };

    private static WGPUBlendComponent ToWgpu(BlendComponentDesc component) => new()
    {
        operation = ToWgpu(component.Operation),
        srcFactor = ToWgpu(component.SrcFactor),
        dstFactor = ToWgpu(component.DstFactor)
    };

    private static WGPUBlendOperation ToWgpu(BlendOperation operation) => operation switch
    {
        BlendOperation.Add => WGPUBlendOperation.Add,
        BlendOperation.Subtract => WGPUBlendOperation.Subtract,
        BlendOperation.ReverseSubtract => WGPUBlendOperation.ReverseSubtract,
        BlendOperation.Min => WGPUBlendOperation.Min,
        BlendOperation.Max => WGPUBlendOperation.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "no blend operation counterpart")
    };

    private static WGPUBlendFactor ToWgpu(BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => WGPUBlendFactor.Zero,
        BlendFactor.One => WGPUBlendFactor.One,
        BlendFactor.Src => WGPUBlendFactor.Src,
        BlendFactor.OneMinusSrc => WGPUBlendFactor.OneMinusSrc,
        BlendFactor.SrcAlpha => WGPUBlendFactor.SrcAlpha,
        BlendFactor.OneMinusSrcAlpha => WGPUBlendFactor.OneMinusSrcAlpha,
        BlendFactor.Dst => WGPUBlendFactor.Dst,
        BlendFactor.OneMinusDst => WGPUBlendFactor.OneMinusDst,
        BlendFactor.DstAlpha => WGPUBlendFactor.DstAlpha,
        BlendFactor.OneMinusDstAlpha => WGPUBlendFactor.OneMinusDstAlpha,
        _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, "no blend factor counterpart")
    };

    private static WGPUVertexFormat ToWgpu(VertexFormat format) => format switch
    {
        VertexFormat.Float32x2 => WGPUVertexFormat.Float32x2,
        VertexFormat.Float32x3 => WGPUVertexFormat.Float32x3,
        VertexFormat.Float32x4 => WGPUVertexFormat.Float32x4,
        VertexFormat.Unorm8x4 => WGPUVertexFormat.Unorm8x4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "no vertex format counterpart")
    };

    private static WGPUCompareFunction ToWgpu(CompareFunction compare) => compare switch
    {
        CompareFunction.Never => WGPUCompareFunction.Never,
        CompareFunction.Less => WGPUCompareFunction.Less,
        CompareFunction.Equal => WGPUCompareFunction.Equal,
        CompareFunction.LessEqual => WGPUCompareFunction.LessEqual,
        CompareFunction.Greater => WGPUCompareFunction.Greater,
        CompareFunction.NotEqual => WGPUCompareFunction.NotEqual,
        CompareFunction.GreaterEqual => WGPUCompareFunction.GreaterEqual,
        CompareFunction.Always => WGPUCompareFunction.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(compare), compare, "no compare counterpart")
    };

    private static WGPUCullMode ToWgpu(CullMode mode) => mode switch
    {
        CullMode.None => WGPUCullMode.None,
        CullMode.Front => WGPUCullMode.Front,
        CullMode.Back => WGPUCullMode.Back,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "no cull counterpart")
    };

    private static WGPUFrontFace ToWgpu(FrontFace face) => face switch
    {
        FrontFace.Ccw => WGPUFrontFace.CCW,
        FrontFace.Cw => WGPUFrontFace.CW,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, "no front face counterpart")
    };

    private static ShaderStage ToWgpu(ShaderStages stages)
    {
        var result = (ShaderStage)0;
        if (stages.HasFlag(ShaderStages.Vertex))
        {
            result |= ShaderStage.Vertex;
        }

        if (stages.HasFlag(ShaderStages.Fragment))
        {
            result |= ShaderStage.Fragment;
        }

        if (stages.HasFlag(ShaderStages.Compute))
        {
            result |= ShaderStage.Compute;
        }

        return result;
    }

    private static Brew.Wgpu.BufferUsage ToWgpu(BufferUsages usage)
    {
        var result = (Brew.Wgpu.BufferUsage)0;
        if (usage.HasFlag(BufferUsages.Vertex))
        {
            result |= Brew.Wgpu.BufferUsage.Vertex;
        }

        if (usage.HasFlag(BufferUsages.Index))
        {
            result |= Brew.Wgpu.BufferUsage.Index;
        }

        if (usage.HasFlag(BufferUsages.Uniform))
        {
            result |= Brew.Wgpu.BufferUsage.Uniform;
        }

        if (usage.HasFlag(BufferUsages.Storage))
        {
            result |= Brew.Wgpu.BufferUsage.Storage;
        }

        if (usage.HasFlag(BufferUsages.CopySrc))
        {
            result |= Brew.Wgpu.BufferUsage.CopySrc;
        }

        if (usage.HasFlag(BufferUsages.CopyDst))
        {
            result |= Brew.Wgpu.BufferUsage.CopyDst;
        }

        if (usage.HasFlag(BufferUsages.MapRead))
        {
            result |= Brew.Wgpu.BufferUsage.MapRead;
        }

        if (usage.HasFlag(BufferUsages.Indirect))
        {
            result |= Brew.Wgpu.BufferUsage.Indirect;
        }

        return result;
    }

    private static Brew.Wgpu.TextureUsage ToWgpu(TextureUsages usage)
    {
        var result = (Brew.Wgpu.TextureUsage)0;
        if (usage.HasFlag(TextureUsages.RenderAttachment))
        {
            result |= Brew.Wgpu.TextureUsage.RenderAttachment;
        }

        if (usage.HasFlag(TextureUsages.TextureBinding))
        {
            result |= Brew.Wgpu.TextureUsage.TextureBinding;
        }

        if (usage.HasFlag(TextureUsages.CopySrc))
        {
            result |= Brew.Wgpu.TextureUsage.CopySrc;
        }

        if (usage.HasFlag(TextureUsages.CopyDst))
        {
            result |= Brew.Wgpu.TextureUsage.CopyDst;
        }

        return result;
    }
}

/// <summary>
/// Reaches the native objects a handle wraps, for code that has not moved behind the seam yet.
/// </summary>
/// <remarks>
/// The seam forbids backend types in its own signatures, so the unwrapping lives here rather than on the interface. One
/// helper instead of a type test at every call site means a second backend fails in one place with one message, and the
/// whole file disappears when pass recording moves behind the seam.
/// </remarks>
internal static class RenderBackendInterop
{
    /// <summary>Returns the wgpu backend behind a seam reference.</summary>
    /// <param name="backend">Backend handed to the resource.</param>
    internal static WgpuRenderBackend Unwrap(IRenderBackend backend) =>
        backend as WgpuRenderBackend
        ?? throw new InvalidOperationException("the seam implementation must unwrap its own handles");
}