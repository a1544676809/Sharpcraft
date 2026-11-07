<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体渲染层架构差距
summary: 合并 part 的简化实现与原版独立 RenderLayer 的差异、影响和修复契约
status: 已评审
updated: 2026-11-02
-->

# 实体渲染层架构差距

## 1. 结论

当前把 outer/clothing/overlay 合并进 `EntityModelMesher` 的同一个 `DynamicGeometry`，再由实体 pipeline 一次 draw，只能算**静态观感近似**，不能称为原版等效。

受影响的已实现：

- Skeleton `Stray/Bogged` 外层衣物；
- Player 的 hat/jacket/sleeves/pants；
- Drowned 的 `drowned_outer_layer`；
- Slime 的 outer cube；
- 任何后续需要独立材质/透明/发光的 layer。

这些必须按原版 `RenderLayer` 语义重构，否则违反原版等效性原则。

## 2. 原版模型

原版实体渲染是：

- 一个基础模型 `Model`；
- 零到多个 `RenderLayer`；
- 每个 layer 可以有：
  - 独立 `RenderType`（cutout / translucent / emissive / armor / eyes）；
  - 独立纹理；
  - 独立 model / model part；
  - 独立可见性条件；
  - 独立颜色/tint；
  - 独立提交顺序；
  - 独立深度/混合状态。

例如：

- `SkeletonClothingLayer`：单独 `SkeletonModel` + clothing texture + `coloredCutoutModelCopyLayerRender`；
- `DrownedOuterLayer`：单独 outer texture + 带 0.25 deformation 的 DrownedModel；
- `EyesLayer`：同模型、眼睛纹理、`RenderTypes.eyes`、depth `>=` 不写；
- `SlimeOuterLayer`：单独 outer texture + translucent。

## 3. 当前差距

| 能力 | 原版 | 当前 |
| --- | --- | --- |
| 每层独立 Texture | 是 | 仅通过 part TexturePath/图集，仍共享一次 draw |
| 每层独立 RenderType | 是 | 否，所有 part 使用同一 entity pipeline |
| 每层独立深度/混合 | 是 | 否 |
| layer 顺序 | 显式 submit 顺序 | 依赖 part 顺序 + 深度测试 |
| layer 可见性 | 每层可独立控制 | 只有少量全局字段 |
| layer tint | 每层可设置 | 无 |
| layer 独立模型/动画 | 是 | 否，只能拆成静态 parts |

这会在透明叠层、衣物/盔甲重叠、发光眼睛、拥有独立动画的 layer 上产生可观察偏差。

## 4. 修复契约

建议新增：

```csharp
public enum EntityLayerRenderKind
{
    Cutout,
    Translucent,
    Emissive
}

public sealed record EntityRenderLayerDefinition(
    string Name,
    EntityModelDefinition Model,
    string TexturePath,
    EntityLayerRenderKind Kind,
    int Order,
    Func<EntityRenderState, bool> Visible);

public sealed record EntityGeometryLayer(
    DynamicGeometry Geometry,
    string TexturePath,
    EntityLayerRenderKind Kind,
    int Order);
```

`EntityRenderer` 构造：

1. 基础模型 `EntityGeometryLayer`；
2. 按 `Order` 生成各 `RenderLayer` 的 geometry；
3. 每个 layer 使用自己的 texture/pipeline/depth state，按顺序提交。

`WindowedRenderer` 需要支持**多 entity geometry layer**，而不是只有一个 dynamic geometry + 一个 entity skin bind group。

## 5. 迁移顺序

1. 抽出 `EntityGeometryLayer` 和多 layer 提交；
2. 先迁移 Skeleton clothing（当前最明确的差异）；
3. 迁移 Player overlays；
4. 迁移 Drowned outer；
5. 迁移 Slime outer；
6. 再接需要 emissive 的 Enderman eyes。

在这些层完成前，相关模型状态应为“静态近似/未完成”，不能标记为等效完成。

## 6. 实施状态（2026-11-03，ADR-0021）

- **第一步（客户端分层）已完成**：`EntityModelMesher.BuildLayers` 按 `EntityGeometryLayerKind` 返回
  Cutout/Translucent 两份几何（`Build` 仍返回合并结果，供单 pass 调用）；基础部件与 Skeleton clothing 为 Cutout，
  Player overlays、Drowned outer、Slime outer 标为 Translucent（`EntityModelLayer.Kind` / `EntityModelPart.Kind`）。
- **第二步（框架分层绘制）已完成**：`WindowedRenderer` 接受 `entityLayers`，每层一份常驻缓冲；
  Cutout 走新 `fs_entity_cutout`（alpha 0.5、无混合、深度写），Translucent 走原 `fs_entity_translucent`（混合、深度写关）；
  绘制顺序按层 `Order`。
- **Emissive（eyes）已完成**：`EntityModelDefinition.EmissiveTexturePath` 声明眼睛纹理；mesher 用同一几何 + 眼睛纹理再生成一层 `Emissive`；`fs_entity_emissive`（无光照样本、alpha 0.1 测试）+ 混合（SrcAlpha/OneMinusSrcAlpha）+ 深度 `>=` 不写。Enderman/Spider 已接入并目视确认（品红/红眼发光）。
- 沙盒已统一走 `BuildLayers` + `entityLayers`（与窗口真实路径一致），Player/Drowned/Slime 的透明层已目视确认。\n- 验收：`EntityLayerTests` 钉住“无 overlay 只有 Cutout / Player 与 Drowned/Slime 产生 Translucent / 合并几何总量等于各层之和”；
  窗口启动无 pipeline validation error。

## 7. 实施状态（2026-11-07，T-733）

- `EntityModelDefinition` 增加 `RenderLayers`（`EntityRenderLayerDefinition { Name, Kind, Order }`），默认顺序为
  `cutout 0 / elytra 10 / translucent 20 / emissive 30`；
- `EntityModelMesher.BuildLayers` 从当前批次的模型定义取并集并按 `Order` 排序，不再使用固定
  `Cutout→Elytra→Translucent→Emissive` 数组；几何仍按 kind 聚合，独立的 per-layer model/texture/visible 尚未接线；
- `WindowedRenderer` 的 entity layer 缓冲由固定 4 组改为按层数增长，层数超过 4 不再截断；
- `EntityRenderLayerDefinition` 扩展为带 `EntityModelDefinition Model`、`TexturePath`、`TextureSelector`、
  `VisibleWhen` 的层模型；`EntityModelMesher.BuildModelBackedLayers` 对带 Model 的层按实体单独建几何并追加；
- humanoid 四槽护甲与 Elytra 已改为模型级独立层；基础模型/衣服/透明 overlay 仍按原 kind 层输出；
- T-739 后 Horse/Donkey/Mule/SkeletonHorse/ZombieHorse、Camel、Llama、Nautilus、HappyGhast、Wolf、Piglin 的装备树
  也改为模型级独立层；`AllCubes` 用于让装备树（cube Kind=Cutout）走 Elytra pipeline；
- 剩余：把 per-layer 几何构建从“逐实体临时几何”优化为按模型分组。


