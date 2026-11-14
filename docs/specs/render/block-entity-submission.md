<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块实体提交路径（T-041）

> 状态：`规格已就绪`
> 更新：2026-11-14
> 关联：`docs/specs/render/special-block-rendering.md`（几何、图集、绘制段）

## 1. 原版模型

| 环节 | 原版做法 | 证据 |
| --- | --- | --- |
| 地形 | `SectionRenderDispatcher` → 每个 section 独立编译的网格 | — |
| 方块实体 | `submitBlockEntities` → `SubmitNodeCollector` | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/LevelRenderer.java` L326–330、L1008–1021 |
| 分批 | `SubmitNodeCollector` 按 **render type** 收集绘制提交，稍后分批 | `SubmitNodeCollector.java`、`SubmitNodeStorage` |
| 坐标 | `poseStack.translate(blockPos - cameraPos)`——**相机相对** | `LevelRenderer.java` L1017 |

**关键结论：方块实体从不进入地形几何。**

## 2. 本仓设计

### 2.1 数据流

```
BlockEntityRecord (Common, 协议) → BlockEntityRenderState (Client, 相机相对)
    → 按 TypeId 分派 Mesher（ChestMesher / 占位）
    → DynamicGeometry（CPU 顶点，每帧重建）
    → BlockEntitySubmission.Upload（GPU 缓冲，每帧一次）
    → 绘制段：切换绑定到该族的图集，画一次
```

### 2.2 `BlockEntitySubmission`

一个提交容器，对应原版"按 render type 分批"里的一族：

| 成员 | 职责 |
| --- | --- |
| `Upload(DynamicGeometry, BindGroupHandle)` | 把本帧顶点/索引上传到 GPU，记录绑定 |
| `Draw(backend, pass, pipeline)` | 切绑定 + 设缓冲 + 一次索引绘制 |
| `Dispose()` | 释放缓冲 |

- 缓冲**每帧重建内容**（`WriteBuffer` 到常驻缓冲，容量不足才重建），与地形 arena 的"一次上传常驻"不同。
- **注意**：与 T-076 的教训一致——写入必须保证上一帧不再读该缓冲；此处每帧只写一次且只被当帧读取，不存在跨帧复用风险。

### 2.3 与主渲染循环的接入

`FrameRequest` 增加可选的方块实体提交；`FrameExecutor` 在 opaque pass 末尾、地形绘制之后：

```
SetBindGroup(pass, 0, blockEntityBindGroup)
提交.Draw(pass, terrainOpaquePipeline)      // 与地形同一管线（布局相同）
SetBindGroup(pass, 0, worldBindGroup)       // 若其后仍有地形绘制则还原
```

## 3. 切片

| 切片 | 内容 | 文件 | 验收 |
| --- | --- | --- | --- |
| **1** | `BlockEntitySubmission` 容器（上传 + 绘制 + 释放） | 新增 1 文件 | 单测：上传零几何不创建缓冲；上传后有正确的索引计数 |
| **2** | `FrameRequest` / `FrameResources.Options` 携带提交与绑定；`FrameExecutor` 接入 | 3 文件 | 沙盒帧逐字节不变（默认无提交） |
| **3** | `ChestAtlas` + `ChestMesher` 接入 `FrameCommand` / `WindowCommand` | 3 文件 | 单方块沙盒像素对比 |

## 4. 相机相对坐标

`BlockEntityRenderState` 的 X/Y/Z 目前是世界绝对坐标。按原版应改为**相机相对**：

- 优点：远离原点时顶点精度不随距离恶化
- 改法：`FrameCommand` 构造状态时减去相机位置；mesher 不变（它只做相对原点的盒布局）

**切片 2 内完成**，因为它影响提交的顶点内容。

## 5. 不做什么

- 不引入第二个渲染循环（原则 14 单轨）
- 不为方块实体做剔除（原版按 section 剔除，我们当前规模不需要，登记为简化）
