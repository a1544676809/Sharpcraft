// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Common.Reference;

/// <summary>
/// The inactive conduit shell: a single six-pixel cube from
/// <c>ConduitRenderer.createShellLayer()</c>, textured from the 32 by 16
/// <c>entity/conduit/base</c> sheet.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ConduitRenderer.java:65-71</c>
/// (createShellLayer) and the inactive submit branch at lines 105-119.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.ConduitRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ConduitRenderer.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
public static class ConduitModelDefinition
{
    public const string TexturePath = "entity/conduit/base";
    public const int TextureWidth = 32;
    public const int TextureHeight = 16;

    public static readonly EntityModelDefinition Shell = CreateShell();

    private static EntityModelDefinition CreateShell()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "shell",
            VanillaPartPose.Zero,
            new VanillaCubeDefinition("shell", 0, 0, -3f, -3f, -3f, 6f, 6f, 6f));
        return EntityModelDefinition.FromTree(
            "conduit_shell",
            TextureWidth,
            TextureHeight,
            new VanillaMeshDefinition(root),
            PlayerModelDefinition.DefaultFaceTransforms,
            TexturePath,
            baselinePx: 0f,
            rawSourceFrame: true);
    }
}
