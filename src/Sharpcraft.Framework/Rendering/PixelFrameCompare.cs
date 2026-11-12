// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Compares consecutive rendered frames pixel by pixel so a change to what the renderer records can be shown equal
/// rather than argued equal.
/// </summary>
/// <remarks>
/// The offscreen paths build one merged mesh and never touch the paged arena, and two separate runs of the windowed
/// client differ in what the server has streamed, so neither can answer "did this change a pixel" (T-823 estimated
/// four to six hundred lines and a second rendering entry point, which the single track rule forbids). This instead
/// reads the target the screenshot path already reads: <see cref="FramePresentation.TargetHandle"/> carries
/// <c>RenderAttachment | TextureBinding | CopySrc</c> precisely because the swapchain image cannot be copied, and the
/// window loop captures it today. No pass, no buffer, no second path.
/// <para>
/// It reports two numbers. When the A/B group changed between the two frames the difference is the change under test;
/// when it did not, the same comparison measures the floor set by clouds, particles and anything else that keeps
/// moving. Both come from neighbouring frames of one run, so scene motion moves each by the same amount instead of
/// having to be argued about across runs.
/// </para>
/// <para>
/// Read the ratio, not the absolute: a change is only invisible when it does not exceed the floor, and a floor above
/// one percent of texels means this method cannot answer the question at all. A floor that large is not a pass.
/// </para>
/// <para>
/// Costs a full readback per frame while enabled, which lands in the frame preparation bucket, so a run that enables
/// it must not also be used for the frame timings.
/// </para>
/// </remarks>
internal sealed class PixelFrameCompare
{
    private long _abPixels;
    private long _abWorst;
    private long _abPairs;
    private long _floorPixels;
    private long _floorWorst;
    private long _floorPairs;
    private byte[]? _previous;
    private int _previousGroup = -1;
    private bool _enabled;

    /// <summary>Compares this frame against the previous one, or records it when there is nothing to compare yet.</summary>
    /// <param name="backend">Backend that copies and maps the target.</param>
    /// <param name="instance">Instance the readback buffer is mapped against.</param>
    /// <param name="target">The frame target, created with <c>CopySrc</c> usage.</param>
    /// <param name="width">Target width in texels.</param>
    /// <param name="height">Target height in texels.</param>
    /// <param name="format">Colour format the target was created with.</param>
    /// <param name="group">Which A/B group this frame was recorded under.</param>
    /// <param name="diagnostics">Host switches; nothing happens unless the compare is asked for.</param>
    public void Observe(
        WgpuRenderBackend backend,
        Instance instance,
        TextureHandle target,
        uint width,
        uint height,
        TextureFormat format,
        int group,
        FrameDiagnostics? diagnostics)
    {
        _enabled = diagnostics?.PixelCompare == true;
        if (!_enabled || width == 0 || height == 0)
        {
            return;
        }

        if (FrameCapture.Capture(backend, instance, target, width, height, format) is not { } pixels)
        {
            return;
        }

        if (_previous is { Length: > 0 } before && before.Length == pixels.Length)
        {
            Measure(before, pixels, _previousGroup != group);
        }

        _previous = pixels;
        _previousGroup = group;
    }

    /// <summary>Prints both numbers, or nothing when the compare never ran.</summary>
    /// <param name="width">Target width in texels.</param>
    /// <param name="height">Target height in texels.</param>
    public void WriteReport(uint width, uint height)
    {
        if (!_enabled)
        {
            return;
        }

        var texels = Math.Max(1L, (long)width * height);
        Report("A/B", _abPixels, _abWorst, _abPairs, texels);
        Report("floor", _floorPixels, _floorWorst, _floorPairs, texels);
    }

    private void Measure(byte[] before, byte[] after, bool groupChanged)
    {
        long differing = 0;
        var worst = 0;
        for (var offset = 0; offset + 3 < after.Length; offset += 4)
        {
            var delta = Math.Abs(before[offset] - after[offset])
                + Math.Abs(before[offset + 1] - after[offset + 1])
                + Math.Abs(before[offset + 2] - after[offset + 2]);
            if (delta == 0)
            {
                continue;
            }

            differing++;
            if (delta > worst)
            {
                worst = delta;
            }
        }

        if (groupChanged)
        {
            _abPixels += differing;
            _abWorst = Math.Max(_abWorst, worst);
            _abPairs++;
        }
        else
        {
            _floorPixels += differing;
            _floorWorst = Math.Max(_floorWorst, worst);
            _floorPairs++;
        }
    }

    private static void Report(string label, long differing, long worst, long pairs, long texels)
    {
        var compared = pairs * texels;
        var share = compared == 0 ? 0d : 100d * differing / compared;
        Console.WriteLine(
            $"window: pixel {label} differing {differing} of {compared} texels ({share:F4}%), worst channel sum {worst}, {pairs} pairs");
    }
}
