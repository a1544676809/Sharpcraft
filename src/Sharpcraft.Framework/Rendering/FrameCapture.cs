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
/// </remarks>
internal static class FrameCapture
{
    /// <summary>Bytes per pixel in the returned buffer (RGBA8).</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Copies the frame target back, or returns null when the readback failed.</summary>
    public static byte[]? Capture(Instance instance, Device device, Texture target, uint width, uint height, TextureFormat format)
    {
        try
        {
            return Read(instance, device, device.Queue, target, width, height, format, out _);
        }
        catch (Exception error)
        {
            Console.WriteLine($"screenshot: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }

    /// <summary>Copies <paramref name="target"/> back and returns top-down RGBA rows.</summary>
    /// <param name="device">Device that owns the target.</param>
    /// <param name="queue">Queue the copy is submitted on.</param>
    /// <param name="target">The frame target, created with <c>CopySrc</c> usage.</param>
    /// <param name="width">Target width in texels.</param>
    /// <param name="height">Target height in texels.</param>
    /// <param name="bytesPerRow">Padded row pitch the copy uses; must be a multiple of 256.</param>
    public static byte[] Read(Instance instance, Device device, Queue queue, Texture target, uint width, uint height, TextureFormat format, out uint bytesPerRow)
    {
        bytesPerRow = AlignRowPitch(width * BytesPerPixel);
        var size = (ulong)bytesPerRow * height;

        using var buffer = device.CreateBuffer(new BufferDescriptor
        {
            Size = size,
            Usage = BufferUsage.MapRead | BufferUsage.CopyDst
        });

        using (var encoder = device.CreateCommandEncoder())
        {
            encoder.CopyTextureToBuffer(
                target,
                buffer,
                bytesPerRow,
                height,
                new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 });
            queue.Submit([encoder.Finish()]);
        }

        if (buffer.MapBlocking(instance, MapMode.Read, 0, (nuint)size) != WGPUMapAsyncStatus.Success)
        {
            throw new InvalidOperationException("the screenshot readback buffer could not be mapped");
        }

        try
        {
            var mapped = buffer.GetMappedRange<byte>(0, (nuint)size);
            var pixels = new byte[width * height * BytesPerPixel];

            // The reference flips because its GL readback is bottom-up; wgpu hands the rows back top-down, which is what
            // the PNG wants, so rows are copied in order (deviation registered in docs/specs/render/screenshot.md 2.3).
            for (var y = 0; y < height; y++)
            {
                var sourceRow = mapped.Slice((int)(y * bytesPerRow), (int)(width * BytesPerPixel));
                var destination = pixels.AsSpan((int)(y * width * BytesPerPixel));
                sourceRow.CopyTo(destination);

                // The reference forces an opaque alpha; the surface format's alpha is not meaningful here. A Bgra target
                // hands the channels back as B,G,R,A, so the red and blue bytes are swapped on the way out (the window's
                // swapchain format is Bgra8Unorm).
                var swapRedBlue = BackendConvert.ToWgpu(format) == WGPUTextureFormat.BGRA8Unorm;
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
            buffer.Unmap();
        }
    }

    private static uint AlignRowPitch(uint rowBytes) => (rowBytes + 255u) / 256u * 256u;
}
