// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Prints a SHA-256 of the colour target for each of the first N presented frames (T-837 3A).
/// </summary>
/// <remarks>
/// This is the multi-frame correctness criterion for the recording-thread change: threading may alter who calls the
/// frame executor, but it must not alter what the executor records, so the hash sequence for the same input must be
/// identical before and after. It is diagnostic only - it never changes a pass, a uniform or a resource - and can be
/// deleted as one file plus the three call sites in <c>WindowedRenderer</c>. The host decides how many frames to trace
/// through <see cref="FrameDiagnostics.FrameHashes"/>; zero disables it and costs nothing.
/// <para>
/// Like <see cref="PixelFrameCompare"/>, an enabled trace costs one readback and one extra submit per traced frame, so a
/// run that enables it must not also be used for frame timings. The trace stops after N frames, so the steady-state
/// frame pays nothing.
/// </para>
/// </remarks>
internal sealed class FrameHashTrace
{
    private readonly int _requested;
    private readonly List<string> _hashes = [];

    /// <summary>Captures the number of frames the host asked to trace.</summary>
    /// <param name="requested">Frames to hash; zero or less disables the trace.</param>
    public FrameHashTrace(int requested) => _requested = requested;

    /// <summary>Hashes and prints one presented frame, or does nothing once the trace is complete.</summary>
    /// <param name="backend">Backend that copies and maps the target.</param>
    /// <param name="instance">Instance the readback buffer is mapped against.</param>
    /// <param name="target">The frame target, created with <c>CopySrc</c> usage.</param>
    /// <param name="width">Target width in texels.</param>
    /// <param name="height">Target height in texels.</param>
    /// <param name="format">Colour format of the target.</param>
    public void Observe(
        WgpuRenderBackend backend,
        Instance instance,
        TextureHandle target,
        uint width,
        uint height,
        TextureFormat format)
    {
        if (_requested <= 0 || _hashes.Count >= _requested)
        {
            return;
        }

        if (FrameCapture.Capture(backend, instance, target, width, height, format) is not { } pixels)
        {
            _hashes.Add("<readback failed>");
            Console.WriteLine($"frame-hash: #{_hashes.Count - 1} <readback failed>");
            return;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(pixels));
        _hashes.Add(hash);
        Console.WriteLine($"frame-hash: #{_hashes.Count - 1} {hash}");
    }

    /// <summary>Prints one digest of the whole sequence, so two runs can be compared with a single string.</summary>
    public void WriteReport()
    {
        if (_requested <= 0 || _hashes.Count == 0)
        {
            return;
        }

        var joined = string.Join('|', _hashes);
        var digest = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined)));
        var distinct = _hashes.Distinct(StringComparer.Ordinal).Count();
        Console.WriteLine($"frame-hashes: count={_hashes.Count} distinct={distinct} digest={digest}");
    }
}
