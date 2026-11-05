<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/client/model/`、`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/`
最后更新：2026-11-05

# Client/Render —— 实体/模型渲染

实体模型定义、网格构建（mesher）、实体级变换、装备层与动画 provider 的应用。本模块把模型部件几何与皮肤图集拼成动态顶点，交给 WGPU 管线绘制。

## 1. 职责范围
负责：模型部件几何与体素、`EntityModelDefinition`、网格构建、实体级变换（缩放/朝向/脚底对齐）、装备层与纹理选择。

不负责：区块/方块渲染、UI、动画数据本身（见 `Client/World` 与动画 provider）、WGPU 绑定细节。

## 2. 实现与原版类对应
见文末自动生成的 class-map 表（`<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Render.EntityModelMesher | Adapter | Partial | net.minecraft.client.model.geom.ModelPart | ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java | Builds vertex geometry equivalent to ModelPart cuboids |
| Sharpcraft.Client | Sharpcraft.Client.Render.EntityModelPart | Mirror | Partial | net.minecraft.client.model.geom.ModelPart | ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java | One model cuboid/part: pivot, box, UV, overlays |
| Sharpcraft.Client | Sharpcraft.Client.Render.EntityModelTransform | Merged | Partial | net.minecraft.client.renderer.entity.LivingEntityRenderer | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/LivingEntityRenderer.java | Entity-level transform chain (scale/rotate/feet offset, docs/specs/render/entity-transform.md) |
| Sharpcraft.Client | Sharpcraft.Client.Render.EntitySkinAtlas | New | Full | sharpcraft.EntitySkinAtlas | ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureAtlas.java | Our 2D-grid skin atlas builder; no direct reference counterpart |
| Sharpcraft.Client | Sharpcraft.Client.Render.PlayerModelDefinition | Merged | Partial | net.minecraft.client.model.PlayerModel | ref://mc-26.3-analysis/src/net/minecraft/client/model/PlayerModel.java | Helper to build humanoid/player part geometry |

<!-- END GENERATED: class-map -->
