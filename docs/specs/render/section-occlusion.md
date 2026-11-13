<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Section 遮挡剔除：可见性集合

**状态**：`[进行中]`　**任务**：T-833

## 1. 要解决的问题

地下场景实测（视距 12）：每帧绘制 **1,512,472 个三角形**，而其中大片被岩石挡住。视距从 4 到 12，帧时间从 1.68 ms 涨到 **3.44 ms**，说明几何与片元开销是真实瓶颈。

视锥剔除已剔除约一半区块（`chunks=637 culled=339`），但视锥内的地下空间仍被整片提交。

## 2. 原版源码映射表

| 本仓实现 | 原版 26.3 | 证据路径 |
| --- | --- | --- |
| `SectionVisibility` | `net.minecraft.client.renderer.chunk.VisibilitySet` | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/VisibilitySet.java` |
| `SectionVisibilityCompute` | `net.minecraft.client.renderer.chunk.VisGraph` | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/chunk/VisGraph.java` |
| 逐帧遍历（待做） | `net.minecraft.client.renderer.SectionOcclusionGraph` | `ref://mc-26.3-analysis/src/net/minecraft/client/renderer/SectionOcclusionGraph.java` |

## 3. 行为规格（本片范围：可见性集合计算）

输入为一个 16×16×16 section 的 4096 格"完全遮挡"位图，输出 36 位的**面对可达矩阵**。

### 3.1 索引约定

- 格索引：`x | (z << 4) | (y << 8)`，即 X 占低位（与原版 `getIndex` 一致）。
- 面编号（与原版 `Direction` 顺序等价）：`0=West, 1=East, 2=Down, 3=Up, 4=North, 5=South`。
- 矩阵位：`bit(from * 6 + to)`，且**对称**——`(a,b)` 与 `(b,a)` 同时置位。

### 3.2 算法

1. 统计遮挡格数 `n`。
2. `n < 256` → **全部可达**。理由：填满一个 16×16 截面需要 256 格，少于该数不可能完全阻断任何一对面。
3. `n == 4096` → **全不可达**。
4. 否则：从**每一个未被访问的边界格**（六面之一）出发做 6 邻域漫水填充，只经过**非遮挡**格；一个连通空腔若触碰了面集合 `S`，则把 `S` 内所有面对两两置为可达。

"已访问"与"遮挡"共用同一份标记是被允许的：漫水填充把走过的空格标记为已访问，效果等同视为不可再通过。

### 3.3 与原版的差异

无行为差异。实现独立编写，未使用原版代码；差异仅在于本仓把临时缓冲由调用方传入（`Span<ulong>`），以满足热路径零分配要求，而原版每次新建 `BitSet` 与队列。

## 4. 验收

- 全遮挡 → 全不可达；全空 → 全部可达；少于 256 遮挡 → 全部可达
- 单层实心地板 → Up 与 Down 互不可达，但 Up 与 West/East/North/South 可达
- 单格柱子 → 六面两两可达（空腔绕得过去）
- 稳态零分配
