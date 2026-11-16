// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Reference;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;
using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws a vault's static display item. The item geometry and ground display transform come from
/// <see cref="BlockEntityItemMesher"/>; this renderer owns only the source block-relative pose.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/VaultRenderer.java</c>
/// (<c>extractRenderState</c> lines 35-50, <c>submit</c> lines 52-63). The source renders a cluster
/// with <c>ItemDisplayContext.GROUND</c> at <c>translate(0.5, 0.4, 0.5)</c> and a Y rotation from
/// <c>VaultClientData</c>. This first slice fixes the spin to zero and draws one item; the vault block
/// model, activation effects and multi-item cluster layout remain out of scope.
/// </remarks>
[ReferenceClass("net.minecraft.client.renderer.blockentity.VaultRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/VaultRenderer.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-VAULT-SPIN", "T-52", Note = "Vault display item Y spin is fixed at zero; VaultClientData.previousSpin/currentSpin is not wired.")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-VAULT-COUNT", "T-52", Note = "Only one display item is drawn; ItemClusterRenderState count layout and seed are not implemented.")]
public sealed class VaultRenderer : IBlockEntityRenderer
{
    private readonly BlockEntityItemMesher _items;
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Creates a vault renderer using the shared item mesher.</summary>
    /// <param name="items">Optional item mesher injection for tests; production uses the shared resolver.</param>
    public VaultRenderer(BlockEntityItemMesher? items = null)
    {
        _items = items ?? new BlockEntityItemMesher();
    }

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsVault(typeId);

    /// <inheritdoc />
    public int LastEntityCount { get; private set; }

    /// <inheritdoc />
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, atlas, itemAtlas: null);

    /// <inheritdoc />
    public DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        ItemAtlas atlas,
        ItemAtlas? itemAtlas)
    {
        _geometry.Clear();
        LastEntityCount = 0;
        if (itemAtlas is null)
        {
            return _geometry;
        }

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId))
            {
                continue;
            }

            var item = entity.Items.Slot0;
            if (item.IsEmpty)
            {
                continue;
            }

            // Source submit: translate(0.5, 0.4, 0.5), rotate Y by spin (fixed zero in this slice).
            var placement = Matrix4x4.CreateTranslation(entity.X + 0.5f, entity.Y + 0.4f, entity.Z + 0.5f);
            if (_items.AppendSlot(_geometry, item, itemAtlas, ItemDisplayContext.Ground, placement) > 0)
            {
                LastEntityCount++;
            }
        }

        return _geometry;
    }
}
