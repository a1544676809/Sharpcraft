<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体坐标系统调查
summary: 地形/相机/实体状态/模型空间的坐标约定、当前已统一与未统一的部分
status: 草稿
updated: 2026-11-02
-->

# 实体坐标系统调查

## 1. 结论先行

- **地形/方块/相机**：已经是统一的世界坐标，Y-up，单位为方块；不需要为实体渲染再做一次地图转换。
- **实体世界状态**：`EntityRenderState.X/Y/Z` 是脚底世界坐标，Y-up，与地形一致。
- **实体模型空间**：**没有统一转换**。当前 `EntityModelMesher` 把原版模型像素手工换算成世界坐标，并通过 `Bottom`、`CentreX`、`RotationX/Z` 的逐模型符号修正来“拟合”。Villager/Spider 的问题就是这里造成的。
- **实体朝向**：`EntityRenderState.Yaw/Pitch` 存在，但当前 `EntityModelMesher` 没有使用；这是另一个独立缺口。

## 2. 地形/相机层（已统一）

证据：

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/...`：原版世界方块坐标 Y-up；
- `src/Sharpcraft.Client/Render/ChunkMesher.cs`：
  - `baseX = chunk.ChunkX * 16`
  - `baseY = (chunk.MinSectionY + sectionIndex) * 16`
  - `baseZ = chunk.ChunkZ * 16`
  顶点直接写入绝对世界坐标，没有额外轴交换。
- `src/Sharpcraft.Framework/Rendering/FrameRequest.cs` 的 `CameraMath.BuildViewProjection`：
  - `Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY)`，明确 Y-up。

因此地图/地形已经是统一世界空间；如果再对地图套 `scale(-1,-1,1)` 反而会破坏它。

## 3. 实体状态层（已统一）

`EntityRenderState`：

- `X/Y/Z`：脚底世界坐标，Y-up；
- `Yaw/Pitch`：实体朝向；
- `TypeId`：模型解析键。

这部分与地形单位一致，不需要模型矩阵再处理。

## 4. 实体模型层（未统一）

当前 `EntityModelMesher.Build`：

```csharp
var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * model.Scale;
var min = new Vector3(feet.X + centreX - halfW, feet.Y + bottom, feet.Z + centreZ - halfD);
```

`EntityModelPart` 的 `Bottom` 已经是“从脚底向上”的自定义坐标，不是原版 `PartPose` 的 y-down 像素坐标。由此产生：

- Player 的 `Bottom = 24 - maxY` 手工换算；
- 某些模型手工翻转 `CentreX`；
- Spider 手工翻转 `RotationZ`；
- Villager 手工翻转 `RotationX`；
- `EntityModelMesher` 没有 `Yaw/Pitch` 的根变换。

这些不是模型差异，而是**模型空间到世界空间缺少一个统一矩阵**。

原版参考链路：

- `ModelPart` 的局部坐标是 y-down 模型像素；
- `LivingEntityRenderer.render` 在提交模型前做：
  - `poseStack.scale(-1, -1, 1)`
  - `poseStack.translate(0, -1.501, 0)`
- `EntityModel.MODEL_Y_OFFSET = -1.501F`。

我们当前把这些语义拆成了每个 part 的 `Bottom` 和符号翻转，导致每个新 mob 都要重新推导。

## 5. 建议的统一变换

新增 `EntityModelTransform`（名称待定），只在实体渲染层使用：

```text
world = entityTranslation
      * entityYaw
      * entityPitch
      * entityScale
      * modelPixelsToBlocks(1/16)
      * modelYFlip/scale(-1,-1,1)
      * translate(0,-1.501,0)
      * modelLocal
```

顺序需与 `LivingEntityRenderer` 的 pose 顺序逐项核对；建议先做矩阵测试，再迁移模型。

模型定义改为直接保存原版语义：

- `PartPose` 的 pivot/rotation；
- `Cube` 的 min/size；
- y-down 模型像素；
- 不再存 `Bottom = 24 - maxY` 这种世界化字段。

UV 不受顶点变换影响；但 `scale(-1,...)` 会反转三角形绕序，需要统一处理 winding（或继续用实体无剔除 pipeline，但必须明确等价性）。

## 6. 当前额外缺口

1. **实体 Yaw/Pitch 未应用到 `EntityModelMesher`**：目前模型只按世界位置摆放，朝向字段未消费；
2. **EntityPlaceholderMesher** 也直接使用脚底 + 模型像素，没有统一模型变换；
3. 名字牌高度依赖模型头部锚点，重构后应一起改为从模型定义取头部位置；
4. 现有 `Player/Zombie/Husk/Drowned/Skeleton/Enderman/Spider/Villager` 的 `Bottom/CentreX/Rotation` 补丁需要在统一变换后清理。

## 7. 推荐执行顺序

1. 先实现矩阵 + 单元测试（Player 包围盒、Y-up 方向、绕序）；
2. 迁移 Player/Humanoid 系列，删除逐模型符号补偿；
3. 迁移 Spider/Villager，验证旋转；
4. 再接 Pig/Cow/Sheep 等 Quadruped。

**不要先继续增加新 mob**：否则每个 quadruped 都会继续放大当前的坐标补丁问题。


## 8. 已实现的第一层（2026-11-02）

- 新增 `EntityModelTransform`：`Translation + Yaw/Pitch + Scale`；
- `EntityModelMesher` 不再把 feet 直接加到每个 part，而是用 `EntityModelTransform.Apply` 统一输出世界顶点；
- 新增 3 项变换单测（平移、缩放、Yaw）；Client 591 通过。

这一步先统一了**实体根变换**。后续仍需要把模型定义从当前 `Bottom/CentreX` 兼容坐标迁移到原版 y-down/pivot 坐标，并让 `EntityPlaceholderMesher` 也走同一变换。
