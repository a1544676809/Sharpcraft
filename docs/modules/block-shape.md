<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[规划]（**暂缓**：依赖尚未开发的方块实体/碰撞行为）
基线：Minecraft 26.3
最后更新：2026-10-31
授权：用户 2026-10-31 指示——遮挡/形状模块需要方块实体（未开发），**先搁置实现、先形成文档**。

# 方块形状与遮挡模块（Block Shape / Occlusion）

提供"方块状态 → 每面遮挡形状"的等价查询，支撑流体面剔除（现）+ 未来的方块面剔除、含水方块（waterlogged）等。

## 1. 职责范围

负责：

- 维护每方块/状态的**面遮挡形状**（等价原版 `VoxelShape` 的碰撞/遮挡形状），以及"是否整块遮挡、是否与流体体积求交"的判定；
- 提供 `OccludesFace(stateId, face, fluidHeight)` 之类的纯函数查询（供流体与后续方块渲染）。

明确不负责：

- **方块实体（BlockEntity）本身**（箱子/告示牌等，另立模块）；
- 方块行为逻辑、渲染、模型贴图（资产/渲染模块）。

## 2. 侧别归属

- 归属：`Common`（状态数据与形状查询，跨侧共享、只读）。
- 依赖方向：`Client`/`Server` → 本模块 → `Common.Identifiers`/注册表。

## 3. 为什么暂缓（硬依赖）

忠实实现依赖**方块的面遮挡/碰撞形状**。该形状来自方块**碰撞行为**（`BlockBehaviour.getCollisionShape/getOcclusionShape`），不是 assets 的模型几何：

- 反例：**树叶**模型是整块立方，但**无碰撞**、不应遮挡；**冰/玻璃**是整块但透明；**台阶/半砖**是部分高度。
- 因此**不能用模型 `elements` 近似**，否则树叶/玻璃/台阶判错，且会污染将来的**含水方块**（水在半砖/楼梯内按形状裁面）。

取得该数据的可靠途径（离线生成，仓库外元数据）与**方块实体/碰撞行为的建模**强相关，而后者**当前尚未开发**，故按所有者指示**先搁置实现**。

## 4. 原版行为（待实现规格）

`FluidRenderer.isFaceOccludedByState(direction, height, state)`：

```
occluder = state.getFaceOcclusionShape(direction.getOpposite())   // 每方向的面遮挡形状
if occluder 为空                      -> false
if occluder == Shapes.block()（整块） -> 遮挡，除非 (direction == UP 且 height < 1.0)
else                                  -> Shapes.blockOccludes(盒(0..1,0..height), occluder, direction)
```

- `isFaceOccludedByNeighbor(dir,height,neighbor)` 同上；`isFaceOccludedBySelf(state,dir)` 用流体方块自身 `state`、height=1。
- 流体面剔除综合 `!isNeighborSameFluid && !isFaceOccludedBySelf`。

## 5. 数据模型（提案）

| 结构 | 用途 | 生命周期 |
| --- | --- | --- |
| `OcclusionShape`（每状态） | 一组 AABB（或"整块"标志）；`Occludes(face, height)` | 加载期构建，不可变 |
| `stateToShape`（扁平表） | `stateId → shapeIndex`，O(1) | 加载期构建，不可变 |

- 数据来源：离线从原版生成"每方块/状态遮挡形状"快照（元数据，非资产），格式同 `vanilla-block-states-26.3.tsv` 的做法。
- 热重载：只存原始数据（int/float/AABB），不持模组类型；快照原子替换（对齐 `block-state` 模块 §9）。

## 6. 当前中间态（临时偏差，须记录）

- 流体面剔除暂用 **`IsSolid` 近似**（"非空气且非水"即视为遮挡）。偏差：树叶/玻璃被过度剔除；台阶/半砖不能被正确处理。
- 该偏差在**含水方块**普及前影响有限；一旦开发含水方块/方块实体，MUST 以本模块替换。

## 7. 落地步骤（恢复开发后）

- A：离线生成遮挡形状快照 + `OcclusionShape` 解码 + 单测（整块/树叶空/半砖）。
- B：实现 `isFaceOccludedByState/Neighbor/Self` 纯函数并接入流体（替换 `IsSolid`）。
- C：接入方块面剔除与含水方块（与方块实体模块协同）。

## 8. 风险 / 未解决

- 遮挡形状数据生成方式（从 jar/数据报告/运行期 dump）需评估；树叶=模型整块但无碰撞，务必以碰撞/遮挡形状而非模型为准。
- 与方块实体模块的依赖顺序需排期（本模块依赖其碰撞/行为建模能力）。

## 9. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/FluidRenderer.java`（`isFaceOccludedByState/Neighbor/Self`）
- `ref://mc-26.3-analysis/src/net/minecraft/world/phys/shapes/Shapes.java`（`blockOccludes`）
- `ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/BlockBehaviour.java`（`getCollisionShape/getOcclusionShape`）
- `docs/modules/block-state.md`
