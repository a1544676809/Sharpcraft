// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Owns the size-dependent order independent transparency targets, their bind groups and the cloud depth blit bind
/// group for one frame executor.
/// </summary>
/// <remarks>
/// Step 2A of docs/specs/render/single-track-executor.md moved this state out of <c>WindowedRenderer.Run</c>. The
/// executor no longer reads mutable window locals through delegates: it creates, resizes and reads its own handles, and
/// the only target-dependent value the caller still supplies is the depth view in <see cref="FrameTarget.Depth"/>,
/// which keys the blit bind group cache. This is what lets a second, offscreen caller use the same recorder in step 2B.
/// <para>
/// The <see cref="ItemOitSet"/> is shared with the caller (the recorder binds it directly), so this class only
/// recreates and releases it as part of target (re)creation and does not dispose it at shutdown.
/// </para>
/// </remarks>
internal sealed class FrameOitTargets
{
    private readonly IRenderBackend _backend;
    private readonly TextureFormat _oitTargetFormat;
    private readonly TextureFormat _oitColorFormat;
    private readonly BindGroupLayoutHandle _transmittanceLayout;
    private readonly BindGroupLayoutHandle _accumulateLayout;
    private readonly BindGroupLayoutHandle _cullLayout;
    private readonly BindGroupLayoutHandle _compositeLayout;
    private readonly BindGroupLayoutHandle _blitDepthLayout;
    private readonly SamplerHandle _blitDepthSampler;
    private readonly ItemOitSet _itemOit;
    private readonly CloudOitResources? _cloudOit;

    private TextureHandle _depthBoundsTexture;
    private TextureHandle _depthBoundsCulledTexture;
    private TextureHandle _transmittance0Texture;
    private TextureHandle _transmittance1Texture;
    private TextureHandle _accumulateTexture;
    private TextureHandle _cloudDepthTexture;
    private BindGroupHandle _blitDepthBindGroup;
    private TextureViewHandle _blitDepthBoundFor;

    /// <summary>Captures the backend, layouts, formats and shared resources the targets are built from.</summary>
    /// <param name="backend">Backend that owns every handle.</param>
    /// <param name="oitTargetFormat">Format of the depth-bounds targets.</param>
    /// <param name="oitColorFormat">Format of the transmittance and accumulation targets.</param>
    /// <param name="transmittanceLayout">Layout of the terrain transmittance bind group.</param>
    /// <param name="accumulateLayout">Layout of the terrain accumulate bind group.</param>
    /// <param name="cullLayout">Layout of the depth-bounds cull bind group.</param>
    /// <param name="compositeLayout">Layout of the terrain composite bind group.</param>
    /// <param name="blitDepthLayout">Layout of the cloud depth blit bind group.</param>
    /// <param name="blitDepthSampler">Sampler the depth blit uses.</param>
    /// <param name="itemOit">Shared item transparency target set, recreated with the terrain targets.</param>
    /// <param name="cloudOit">Cloud OIT resources, or null when cloud OIT is off.</param>
    public FrameOitTargets(
        IRenderBackend backend,
        TextureFormat oitTargetFormat,
        TextureFormat oitColorFormat,
        BindGroupLayoutHandle transmittanceLayout,
        BindGroupLayoutHandle accumulateLayout,
        BindGroupLayoutHandle cullLayout,
        BindGroupLayoutHandle compositeLayout,
        BindGroupLayoutHandle blitDepthLayout,
        SamplerHandle blitDepthSampler,
        ItemOitSet itemOit,
        CloudOitResources? cloudOit)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _oitTargetFormat = oitTargetFormat;
        _oitColorFormat = oitColorFormat;
        _transmittanceLayout = transmittanceLayout;
        _accumulateLayout = accumulateLayout;
        _cullLayout = cullLayout;
        _compositeLayout = compositeLayout;
        _blitDepthLayout = blitDepthLayout;
        _blitDepthSampler = blitDepthSampler;
        _itemOit = itemOit ?? throw new ArgumentNullException(nameof(itemOit));
        _cloudOit = cloudOit;
    }

    /// <summary>True once the targets exist for the current framebuffer size.</summary>
    public bool Created { get; private set; }

    /// <summary>True when the independent cloud depth target exists.</summary>
    public bool CloudDepthTargetsCreated { get; private set; }

    /// <summary>How often the cloud depth blit bind group was rebuilt (once per depth target).</summary>
    public int BlitDepthRebuilds { get; private set; }

    /// <summary>Terrain depth bounds view.</summary>
    public TextureViewHandle DepthBounds { get; private set; }

    /// <summary>Culled terrain depth bounds view.</summary>
    public TextureViewHandle DepthBoundsCulled { get; private set; }

    /// <summary>First wavelet coefficient view.</summary>
    public TextureViewHandle Transmittance0 { get; private set; }

    /// <summary>Second wavelet coefficient view.</summary>
    public TextureViewHandle Transmittance1 { get; private set; }

    /// <summary>Weighted colour accumulation view.</summary>
    public TextureViewHandle Accumulate { get; private set; }

    /// <summary>Independent cloud depth view.</summary>
    public TextureViewHandle CloudDepth { get; private set; }

    /// <summary>Depth-bounds cull bind group.</summary>
    public BindGroupHandle Cull { get; private set; }

    /// <summary>Terrain transmittance bind group.</summary>
    public BindGroupHandle TransmittanceGroup { get; private set; }

    /// <summary>Terrain accumulate bind group.</summary>
    public BindGroupHandle AccumulateGroup { get; private set; }

    /// <summary>Terrain composite bind group.</summary>
    public BindGroupHandle CompositeGroup { get; private set; }

    /// <summary>(Re)creates every target and bind group for a framebuffer size, releasing the previous ones.</summary>
    /// <param name="width">Framebuffer width in pixels.</param>
    /// <param name="height">Framebuffer height in pixels.</param>
    public void Create(uint width, uint height)
    {
        if (Created)
        {
            _backend.Destroy(Cull);
            _backend.Destroy(TransmittanceGroup);
            _backend.Destroy(AccumulateGroup);
            _backend.Destroy(CompositeGroup);
            _backend.Destroy(DepthBounds);
            _backend.Destroy(_depthBoundsTexture);
            _backend.Destroy(DepthBoundsCulled);
            _backend.Destroy(_depthBoundsCulledTexture);
            _backend.Destroy(Transmittance0);
            _backend.Destroy(_transmittance0Texture);
            _backend.Destroy(Transmittance1);
            _backend.Destroy(_transmittance1Texture);
            _backend.Destroy(Accumulate);
            _backend.Destroy(_accumulateTexture);
            if (CloudDepthTargetsCreated)
            {
                _backend.Destroy(CloudDepth);
                _backend.Destroy(_cloudDepthTexture);
                CloudDepthTargetsCreated = false;
            }

            _itemOit.Dispose();
        }

        _depthBoundsTexture = CreateOitTarget(_backend, width, height, _oitTargetFormat);
        DepthBounds = _backend.CreateTextureView(_depthBoundsTexture);
        _depthBoundsCulledTexture = CreateOitTarget(_backend, width, height, _oitTargetFormat);
        DepthBoundsCulled = _backend.CreateTextureView(_depthBoundsCulledTexture);
        _transmittance0Texture = CreateOitTarget(_backend, width, height, _oitColorFormat);
        Transmittance0 = _backend.CreateTextureView(_transmittance0Texture);
        _transmittance1Texture = CreateOitTarget(_backend, width, height, _oitColorFormat);
        Transmittance1 = _backend.CreateTextureView(_transmittance1Texture);
        _accumulateTexture = CreateOitTarget(_backend, width, height, _oitColorFormat);
        Accumulate = _backend.CreateTextureView(_accumulateTexture);

        Cull = _backend.CreateBindGroup(_cullLayout, [BindGroupEntryDesc.Texture(0, DepthBounds)]);
        TransmittanceGroup = _backend.CreateBindGroup(
            _transmittanceLayout,
            [BindGroupEntryDesc.Texture(0, DepthBoundsCulled)]);
        AccumulateGroup = _backend.CreateBindGroup(
            _accumulateLayout,
            [
                BindGroupEntryDesc.Texture(0, DepthBoundsCulled),
                BindGroupEntryDesc.Texture(1, Transmittance0),
                BindGroupEntryDesc.Texture(2, Transmittance1)
            ]);
        CompositeGroup = _backend.CreateBindGroup(
            _compositeLayout,
            [
                BindGroupEntryDesc.Texture(0, Accumulate),
                BindGroupEntryDesc.Texture(1, Transmittance0),
                BindGroupEntryDesc.Texture(2, Transmittance1),
                // The composite writes the closest translucent device depth from the original terrain depth bounds,
                // matching the reference composite (which samples the un-culled target); clouds are excluded from
                // this depth write, exactly as the reference excludes them.
                BindGroupEntryDesc.Texture(3, DepthBounds)
            ]);
        _itemOit.Create(
            _backend,
            width,
            height,
            _oitTargetFormat,
            _transmittanceLayout,
            _accumulateLayout,
            _compositeLayout);
        // Clouds always get an independent depth target so the cloud depth-bounds write never pollutes the main
        // depth the terrain transparency passes test against (otherwise glass/water behind a cloud are culled).
        if (_cloudOit is not null)
        {
            _cloudDepthTexture = CreateDepthTexture(_backend, width, height);
            CloudDepth = _backend.CreateTextureView(_cloudDepthTexture);
            CloudDepthTargetsCreated = true;
        }

        Created = true;
    }

    /// <summary>Rebuilds the targets for a new framebuffer size and drops the cached blit bind group.</summary>
    /// <remarks>
    /// The blit group samples the caller's depth view, so it is released before that view is replaced; the next
    /// <see cref="BlitDepthGroup"/> call recreates it for the new view.
    /// </remarks>
    /// <param name="width">New framebuffer width in pixels.</param>
    /// <param name="height">New framebuffer height in pixels.</param>
    public void Resize(uint width, uint height)
    {
        if (_blitDepthBindGroup.IsValid)
        {
            _backend.Destroy(_blitDepthBindGroup);
            _blitDepthBindGroup = default;
            _blitDepthBoundFor = default;
        }

        if (Created)
        {
            Create(width, height);
        }
    }

    /// <summary>Resolves the cloud depth blit bind group for the given depth view.</summary>
    /// <param name="depthView">The frame's main depth view.</param>
    /// <returns>The bind group, reused until the depth view changes.</returns>
    public BindGroupHandle BlitDepthGroup(TextureViewHandle depthView)
    {
        if (_blitDepthBindGroup.IsValid && _blitDepthBoundFor.Equals(depthView))
        {
            return _blitDepthBindGroup;
        }

        if (_blitDepthBindGroup.IsValid)
        {
            _backend.Destroy(_blitDepthBindGroup);
        }

        _blitDepthBindGroup = _backend.CreateBindGroup(
            _blitDepthLayout,
            [BindGroupEntryDesc.Texture(0, depthView), BindGroupEntryDesc.Sampler(1, _blitDepthSampler)]);
        _blitDepthBoundFor = depthView;
        BlitDepthRebuilds++;
        return _blitDepthBindGroup;
    }

    /// <summary>Releases the blit bind group and all targets owned here.</summary>
    /// <remarks>
    /// The blit bind group samples the caller's depth view, so it is destroyed first. The shared item OIT set is not
    /// disposed here: the caller also binds it directly, and the windowed shell never released it at shutdown.
    /// </remarks>
    public void Dispose()
    {
        if (_blitDepthBindGroup.IsValid)
        {
            _backend.Destroy(_blitDepthBindGroup);
            _blitDepthBindGroup = default;
            _blitDepthBoundFor = default;
        }

        if (!Created)
        {
            return;
        }

        _backend.Destroy(Cull);
        _backend.Destroy(TransmittanceGroup);
        _backend.Destroy(AccumulateGroup);
        _backend.Destroy(CompositeGroup);
        _backend.Destroy(DepthBounds);
        _backend.Destroy(_depthBoundsTexture);
        _backend.Destroy(DepthBoundsCulled);
        _backend.Destroy(_depthBoundsCulledTexture);
        _backend.Destroy(Transmittance0);
        _backend.Destroy(_transmittance0Texture);
        _backend.Destroy(Transmittance1);
        _backend.Destroy(_transmittance1Texture);
        _backend.Destroy(Accumulate);
        _backend.Destroy(_accumulateTexture);
        if (CloudDepthTargetsCreated)
        {
            _backend.Destroy(CloudDepth);
            _backend.Destroy(_cloudDepthTexture);
            CloudDepthTargetsCreated = false;
        }

        Created = false;
    }

    /// <summary>Creates a depth texture usable as a render attachment, a copy source and a sampled texture.</summary>
    internal static TextureHandle CreateDepthTexture(IRenderBackend backend, uint width, uint height) =>
        backend.CreateTexture(new TextureDesc(
            width,
            height,
            TextureFormat.Depth32Float,
            1,
            TextureUsages.RenderAttachment | TextureUsages.CopySrc | TextureUsages.CopyDst | TextureUsages.TextureBinding));

    /// <summary>Creates one order independent transparency target: renderable and sampleable, at frame size.</summary>
    private static TextureHandle CreateOitTarget(IRenderBackend backend, uint width, uint height, TextureFormat format) =>
        backend.CreateTexture(new TextureDesc(
            width,
            height,
            format,
            1,
            TextureUsages.RenderAttachment | TextureUsages.TextureBinding));
}
