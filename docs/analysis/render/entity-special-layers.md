<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体特殊层与材质
summary: 不属于普通 base/overlay cube 的实体渲染层清单，以及后续补充顺序
status: 已评审
updated: 2026-11-02
-->

# 实体特殊层与材质

## 1. 为什么单独记录

当前 `EntityModelDefinition` 已经能表达：

- 基础部件；
- 同一纹理内的 overlay（`EntityModelLayer`）；
- 独立纹理的 overlay（如 Drowned outer layer）；
- 模型 scale。

但原版还有一类**不属于普通 cube overlay** 的渲染层：独立纹理、独立 pipeline、独立 depth/blend 或每帧重提交模型。先记录，模型移植完成后统一补。

## 2. Enderman 眼睛（已确认）

原版：

- `EndermanRenderer` 添加 `EnderEyesLayer`；
- 眼睛纹理：`textures/entity/enderman/enderman_eyes.png`；
- `EnderEyesLayer` 的 `renderType()` 返回 `RenderTypes.eyes(...)`；
- `EyesLayer.submit` 再次提交**同一个模型几何**，不是额外 cube。

`RenderPipelines.EYES` 状态：

- `EMISSIVE`
- `NO_OVERLAY`
- `NO_CARDINAL_LIGHTING`
- `ColorTargetState(BlendFunction.TRANSLUCENT)`
- `DepthStencilState(GREATER_THAN_OR_EQUAL, write=false)`

结论：

- 不能把眼睛作为普通 overlay 贴在同位置，否则 z-fighting；
- 需要单独 eyes geometry + 独立 emissive/no-lightmap pipeline，最后绘制，深度只比较不写入。

状态：**未实现，记录待补。**

## 3. Player 双层与透明

原版 `PlayerModel`：

- `PlayerModel` 使用 `RenderTypes::entityTranslucent`；
- `hat`、`jacket`、`left_sleeve`、`right_sleeve`、`left_pants`、`right_pants` 是独立 ModelPart 子层；
- `setupAnim` 有 showHat/showJacket/showLeftSleeve/showRightSleeve/showLeftPants/showRightPants 可见性。

当前状态：

- overlay 几何与 UV 已实现；
- alpha blending 已实现；
- show* 可见性字段已接入 `EntityRenderState` / mesher；
- 仍需游戏回归。

## 4. Drowned outer layer

原版：

- `DrownedRenderer.addLayer(new DrownedOuterLayer(...))`；
- outer 纹理：`textures/entity/zombie/drowned_outer_layer.png`；
- outer 模型是 `DrownedModel.createBodyLayer(new CubeDeformation(0.25F))`，即同一套 Humanoid 几何整体外扩。

当前状态：

- `EntityModelLayer` 支持独立 `TexturePath`；
- `EntitySkinLibrary` 会加载 base 与 outer 纹理；
- `Drowned` definition 已接入 base + outer；
- 沙盒已验证。

## 5. 已确认的模型/资源特例

| 实体 | 特例 | 状态 |
| --- | --- | --- |
| Enderman | 独立 emissive eyes 纹理与 `EyesLayer`；carried block 另层 | 眼睛未实现 |
| Drowned | 独立 `drowned_outer_layer` 纹理 | 已完成 |
| Player | `entityTranslucent` + hat/jacket/sleeves/pants + show* | 几何/混合已实现，待回归 |
| Skeleton | 2×12×2 四肢，64×32 纹理 | 已完成 |
| Husk | ZombieModel，模型 scale=1.0625 | 已完成 |
| Creeper | 可选 `CreeperArmorLayer`（充能/护甲表现） | 未开始 |
| Spider / Cave Spider | `SpiderEyesLayer` 独立眼睛层 | 未开始 |
| Sheep | 羊毛层 | 未开始 |
| Blaze | 旋转 rods / 发光层 | 未开始 |
| Ghast / Guardian | 触手 / beam 等特殊层 | 未开始 |

## 6. 后续补充顺序

1. 先完成普通模型清单（Humanoid → Quadruped → 多肢）；
2. 统一实现 `EyesLayer` 等价物：
   - 模型定义增加 `EyeTexturePath`；
   - 生成同模型 eyes geometry；
   - 独立 emissive pipeline，depth compare `>=` 且不写深度；
3. 再补 Drowned 之后的其它独立外层/特殊层；
4. 最后处理 held block、armor、beam、saddle 等交互性渲染层。
