// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Reads a rendered colour target back into RGBA bytes for a screenshot.
/// </summary>
/// <remarks>
/// The reference (<c>Screenshot.takeScreenshot</c>) copies the main render target to a buffer, maps it, reads one pixel
/// per texel, writes the rows bottom-up and forces the alpha channel opaque; this does the same. The copy only happens
/// when a screenshot is requested, so a steady frame pays nothing (docs/specs/render/screenshot.md 2.2).
/// <para>
/// The whole path runs on the seam: the target and the readback buffer are handles, the copy is issued through
/// <see cref="IRenderBackend.CopyTextureToBuffer"/> and the mapping through <see cref="IRenderBackend.ReadBuffer"/>, so
/// the capture code never touches a native object.
/// </para>
/// </remarks>
internal static class FrameCapture
{
    /// <summary>Bytes per pixel in the returned buffer (RGBA8).</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Copies the frame target back, or returns null when the readback failed.</summary>
    public static byte[]? Capture(WgpuRenderBackend backend, Instance instance, TextureHandle target, uint width, uint height, TextureFormat format)
    {
        try
        {
            return Read(backend, instance, target, width, height, format, out _);
        }
        catch (Exception error)
        {
            Console.WriteLine($"screenshot: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }

    /// <summary>Copies <paramref name="target"/> back and returns top-down RGBA rows.</summary>
    /// <param name="backend">Backend that copies and maps.</param>
    /// <param name="instance">Instance the readback buffer is mapped against.</param>
    /// <param name="target">The frame target, created with <c>CopySrc</c> usage.</param>
    /// <param name="width">Target width in texels.</param>
    /// <param name="height">Target height in texels.</param>
    /// <param name="format">Target format, which decides whether red and blue are swapped.</param>
    /// <param name="bytesPerRow">Padded row pitch the copy uses; must be a multiple of 256.</param>
    public static byte[] Read(WgpuRenderBackend backend, Instance instance, TextureHandle target, uint width, uint height, TextureFormat format, out uint bytesPerRow)
    {
        bytesPerRow = AlignRowPitch(width * BytesPerPixel);
        var size = (ulong)bytesPerRow * height;

        var buffer = backend.CreateBuffer(new BufferDesc(size, BufferUsages.MapRead | BufferUsages.CopyDst));
        var encoder = backend.CreateCommandEncoder();
        backend.CopyTextureToBuffer(encoder, target, buffer, bytesPerRow, width, height);
        backend.Submit(encoder);

        try
        {
            // The seam maps, copies and unmaps the row block, so the caller only ever sees plain bytes.
            var mapped = backend.ReadBuffer(instance, buffer, 0, (nuint)size);
            var pixels = new byte[width * height * BytesPerPixel];

            // The reference flips because its GL readback is bottom-up; wgpu hands the rows back top-down, which is what
            // the PNG wants, so rows are copied in order (deviation registered in docs/specs/render/screenshot.md 2.3).
            for (var y = 0; y < height; y++)
            {
                var sourceRow = mapped.AsSpan((int)(y * bytesPerRow), (int)(width * BytesPerPixel));
                var destination = pixels.AsSpan((int)(y * width * BytesPerPixel));
                sourceRow.CopyTo(destination);

                // The reference forces an opaque alpha; the surface format's alpha is not meaningful here. A Bgra target
                // hands the channels back as B,G,R,A, so the red and blue bytes are swapped on the way out (the window's
                // swapchain format is Bgra8Unorm).
                var swapRedBlue = format == TextureFormat.Bgra8Unorm;
                for (var x = 0; x < width; x++)
                {
                    var offset = x * BytesPerPixel;
                    if (swapRedBlue)
                    {
                        (destination[offset], destination[offset + 2]) = (destination[offset + 2], destination[offset]);
                    }

                    destination[offset + 3] = 0xFF;
                }
            }

            return pixels;
        }
        finally
        {
            backend.Destroy(buffer);
        }
    }

    private static uint AlignRowPitch(uint rowBytes) => (rowBytes + 255u) / 256u * 256u;
}
