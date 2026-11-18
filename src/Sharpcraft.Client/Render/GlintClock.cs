// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Host glint clock. The reference computes millis = (long)(Util.getMillis() * glintSpeed * 8); the default
/// provider is the wall clock, while tests install a fixed source-millis provider. No environment variables and
/// no per-vertex allocation are involved.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.rendertype.TextureTransform", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/TextureTransform.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Host wall clock injects the source Glint millis; no environment variables")]
public static class GlintClock
{
    /// <summary>Options.glintSpeed default.</summary>
    public const float DefaultGlintSpeed = 0.5f;

    private static Func<long>? _fixedWallMillis;

    /// <summary>Source <c>millis</c> after the glint speed multiplier.</summary>
    public static long Millis
    {
        get
        {
            var wallMillis = _fixedWallMillis is { } fixedMillis
                ? fixedMillis()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return (long)(wallMillis * DefaultGlintSpeed * 8f);
        }
    }

    /// <summary>Installs a fixed wall-clock-millis provider, or null to restore the UTC epoch clock.</summary>
    public static void SetProvider(Func<long>? provider) => _fixedWallMillis = provider;

    /// <summary>Test helper: installs a fixed source millis (pre glint-speed multiplier).</summary>
    public static void SetFixedSourceMillis(long millis) =>
        _fixedWallMillis = () => (long)(millis / (DefaultGlintSpeed * 8f));
}
