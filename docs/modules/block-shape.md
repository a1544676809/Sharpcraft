<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：ref://mc-26.3-analysis/src/net/minecraft/world/phys/shapes/Shapes.java, ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/FluidRenderer.java
最后更新：2026-11-06

# 方块形状与遮挡模块（Block Shape / Occlusion）

提供方块状态的体素形状（`OcclusionShape`）及逐面遮挡判定（`BlockOccludes` / `IsFaceOccludedByState`），支撑流体面剔除与非完整方块（半砖、台阶、透明/树叶）面剔除。

## 1. 职责范围

负责：

- 表达 `[0,1]³` 单位方块内的体素遮挡形状（空形状 `Empty`、完整方块 `FullBlock`、轴对齐盒 `Box(minX,minY,minZ,maxX,maxY,maxZ)`）；
- 精确实现原版 `Shapes.blockOccludes(shape, occluder, direction)` 与 `FluidRenderer.isFaceOccludedByState / isFaceOccludedByNeighbor / isFaceOccludedBySelf / shouldRenderFace`；
- 提供按方块名/状态属性推导常见非完整方块（如顶/底/双半砖 `slab`、树叶 `leaves`、玻璃 `glass`、冰 `ice`、雪层 `snow`、地毯 `carpet`）遮挡形状的纯函数查询。

明确不负责：

- 区块网格顶点发射（归 `Client/Render/ChunkMesher`）；
- 实体移动物理积分（归 `Common/Entities`）。

## 2. 侧别归属

- 归属：`Common`（`Sharpcraft.Common.World`），跨侧共享、纯值类型与纯函数。
- 判定依据：`net.minecraft.world.phys.shapes.Shapes` 位于 `world/phys/shapes`，客户端流体/方块剔除与服务端共用。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | `Sharpcraft.Common.Registries` | 读取 `BlockStateLayout` 中的方块名与状态属性 |
| 被依赖 | `Sharpcraft.Client.Render` | `ChunkMesher` 流体与方块面遮挡剔除 |

依赖方向合法性自检：`Client` → `Common`，单向无环，不接触宿主层。

## 4. 对外契约

- 枚举：`BlockFace`（`Down = 0, Up = 1, North = 2, South = 3, West = 4, East = 5`，带 `Opposite()` 方法）。
- 结构：`readonly record struct OcclusionShape`（`Empty`、`FullBlock`、`Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)`）。
- 静态方法：
  - `OcclusionShape.BlockOccludes(OcclusionShape shape, OcclusionShape occluder, BlockFace direction)`
  - `OcclusionShape.IsFaceOccludedByState(BlockFace direction, float height, OcclusionShape stateFaceShape)`
  - `OcclusionShape.IsFaceOccludedBySelf(OcclusionShape selfOppositeFaceShape, BlockFace direction)`

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `OcclusionShape` | 单位体素盒 `[minX..maxX, minY..maxY, minZ..maxZ]` 或空/满标志 | 栈上值类型 | 任意线程（不可变） |
| `BlockFace` | 六方向枚举及轴号/正负向/反方向辅助函数 | 常量 | 任意线程 |

## 6. 原版源码映射表与真值向量（必填，原则 14）

### 6.1 原版 26.3 源码映射表

| 本模块概念 / 方法 | 原版类与方法路径（`ref://mc-26.3-analysis/...`） | 精确常量 / 公式 / 树结构 | 位序 / 正负号 / 色彩空间约定 | 缺省 / 越界回退值 |
| --- | --- | --- | --- | --- |
| `MaxFluidHeight` | `client/renderer/block/FluidRenderer.java#MAX_FLUID_HEIGHT` | `0.8888889F`（即 `8f / 9f`） | 正浮点，高度范围 `[0, 1]` | `0f` |
| `Epsilon` | `world/phys/shapes/Shapes.java#EPSILON` | `1.0E-7` | 双精度比较容差 `\|a - b\| <= 1.0E-7` | — |
| `BlockOccludes` | `world/phys/shapes/Shapes.java#blockOccludes` | 正向（+X/+Y/+Z）：`shape.max(axis) ≈ 1.0 && occluder.min(axis) ≈ 0.0` 且 `shape` 在该轴的 2D 截面被 `occluder` 截面完全包含；负向（-X/-Y/-Z）：`occluder.max(axis) ≈ 1.0 && shape.min(axis) ≈ 0.0` 且 `shape` 截面被 `occluder` 截面完全包含 | `Up=+Y, Down=-Y, South=+Z, North=-Z, East=+X, West=-X` | `occluder.IsEmpty` 返回 `false`；双 `FullBlock` 返回 `true` |
| `IsFaceOccludedByState` | `client/renderer/block/FluidRenderer.java#isFaceOccludedByState` | 取 `occluder = state.getFaceOcclusionShape(direction.getOpposite())`；空→`false`；`FullBlock`→`direction != Up \|\| height == 1.0f`；其余→`BlockOccludes(Box(0,0,0,1,height,1), occluder, direction)` | `height` 为单精度浮点 | 空形状返回 `false` |
| `IsFaceOccludedBySelf` | `client/renderer/block/FluidRenderer.java#isFaceOccludedBySelf` | `IsFaceOccludedByState(direction.Opposite(), 1.0f, state)` | — | `false` |

### 6.2 真值向量（Golden Vectors）来源与测试落点

- 采样来源：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/FluidRenderer.java` 与 `Shapes.java` 精确数学推导：
  1. **Vector 1（空遮挡体）**：`IsFaceOccludedByState(North, 0.8888889f, Empty) == false`（如空气、树叶、玻璃无遮挡形状）。
  2. **Vector 2（整块遮挡侧面与底面）**：`IsFaceOccludedByState(North, 0.8888889f, FullBlock) == true`，`IsFaceOccludedByState(Down, 0.8888889f, FullBlock) == true`。
  3. **Vector 3（整块不遮挡未满格流体顶面）**：`IsFaceOccludedByState(Up, 0.8888889f, FullBlock) == false`，但满格流体 `IsFaceOccludedByState(Up, 1.0f, FullBlock) == true`。
  4. **Vector 4（下半砖 `Box(0,0,0,1,0.5,1)` 遮挡判定）**：
     - 对高度 `0.4f` 的流体侧面：`IsFaceOccludedByState(North, 0.4f, BottomSlab) == true`（流体高度 `0.4 <= 0.5` 被下半砖完全挡住）；
     - 对源水高度 `0.8888889f` 的流体侧面：`IsFaceOccludedByState(North, 0.8888889f, BottomSlab) == false`（流体高于下半砖，侧面必须绘制）；
     - 对上方流体底面（`Down`）：下半砖顶在 `y=0.5 != 1.0`，不接触上方方块底面，`IsFaceOccludedByState(Down, 0.8888889f, BottomSlab) == false`；
     - 对下方满高流体顶面（`Up, height=1.0f`）：下半砖底在 `y=0.0` 且 2D 截面 `[0,1]×[0,1]` 铺满，`IsFaceOccludedByState(Up, 1.0f, BottomSlab) == true`。
  5. **Vector 5（上半砖 `Box(0,0.5,0,1,1,1)` 遮挡判定）**：
     - 对上方流体底面（`Down`）：上半砖顶在 `y=1.0` 且 2D 截面铺满，`IsFaceOccludedByState(Down, 0.8888889f, TopSlab) == true`；
     - 对侧面流体（`North, height=0.8888889f`）：上半砖底在 `y=0.5 > 0.0`，未遮挡下半部，`IsFaceOccludedByState(North, 0.8888889f, TopSlab) == false`。
- 向量文件 / 测试类：`tests/Sharpcraft.Common.Tests/World/OcclusionShapeTests.cs`

## 7. 行为对齐与受控偏离

对齐点：

- `BlockOccludes` 与 `IsFaceOccludedByState` 1:1 复刻原版 `Shapes.blockOccludes` 与 `FluidRenderer.isFaceOccludedByState` 边界条件。

有意偏离：

- 首版 `OcclusionShape` 聚焦单 AABB 盒（覆盖原版空、整块、下半砖、上半砖、雪层、地毯等 95% 以上面投影切片），楼梯双盒组合留作后续扩展。

## 8. 未解决问题与风险

- 含水方块（waterlogged）的自身遮挡（`isFaceOccludedBySelf`）待方块状态属性表接入后启用。

## 9. 参考资料

- `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/FluidRenderer.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/phys/shapes/Shapes.java`
