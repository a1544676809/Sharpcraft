<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 原版如何渲染"透明/半透明方块"（行为规格，不含实现代码）

本文件只描述可观测行为与判定规则，证据以 `ref://` 指针引用仓库外分析工作区。

## 1. 地形管线是**单面**的

- `RenderPipelines` 的地形管线由 `GENERIC_BLOCKS_SNIPPET` 派生，它只设 `DepthStencilState.DEFAULT`，
  **没有** `withCull(false)`；`RenderPipeline.Builder` 的 `cull` 默认是 `true`
  （`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/RenderPipeline.java:417`）。
- 因此 solid / cutout / translucent 三种地形管线**都启用背面剔除**。
- `withCull(false)` 只出现在线条、调试几何、粒子、云、实体（盔甲/告示牌等）等管线。

⇒ 结论：**原版玻璃不会同时画正反两面**。看到"透明"是因为**玻璃后面的方块被正常绘制**（玻璃不是遮挡体），
以及玻璃自身各朝向的可见面按视角各自绘制，而不是把同一面的背面也画一遍。

## 2. 相邻面何时被剔除：按方块族覆写 `skipRendering`

- 默认实现返回 `false`（不额外剔除）：`BlockBehaviour.skipRendering`
  （`ref://.../world/level/block/state/BlockBehaviour.java:151`）。
- 面是否可见的总判定在 `Block.shouldRenderFace`：先看 `state.skipRendering(neighborState, direction)`，
  再看向邻方块是否遮挡（`ref://.../world/level/block/Block.java:301`）。
- 各族的覆写：
  | 族 | 规则 | 证据 |
  | --- | --- | --- |
  | `HalfTransparentBlock`（冰等） | 邻方块**是同一个方块**时剔除，否则按默认 | `HalfTransparentBlock.java:13` |
  | `PowderSnowBlock` | 同上 | `PowderSnowBlock.java:51` |
  | `LeavesBlock` | 关闭"镂空树叶"时才剔除同类树叶 | `LeavesBlock.java:45` |
  | `LiquidBlock` | 邻方块是**同种流体**时剔除 | `LiquidBlock.java:112` |

## 3. **流体不走方块模型渲染**

- `LiquidBlock.getRenderShape` 返回 `RenderShape.INVISIBLE`（`LiquidBlock.java` 同处），
  水/岩浆由**独立的流体渲染器**绘制，而不是作为方块的六个面。
- 这一点我们目前**没有做**：我们把水当作一个方块丢进 translucent 层。

## 4. 分层仍由材质/贴图 alpha 决定

- 与 `block-layer-and-tint.md` 一致：`Transparency` → `ChunkSectionLayer.byTransparency`
  （不透明→SOLID，纯镂空→CUTOUT，含半透明→TRANSLUCENT）。

## 5. 对本项目的直接结论

1. 我们刚修的"只有真正遮挡才剔除邻居面"与原版语义一致（对应 `Block.shouldRenderFace` 的遮挡判定）。
2. `neighbour == state` 的剔除应细化为**按族**：冰/玻璃类"同方块剔除"，流体"同流体剔除"，树叶按 `cutoutLeaves` 开关。
3. **"能看到背面"若是期望，则属于有意的行为偏差**（原版单面）：需要先明确是哪一种情形
   （①从外面看玻璃想看到内壁；②潜入水中想看到水面背面），并登记为受控偏差。
4. 流体渲染应当独立成一条路径（第 3 点），否则水面在侧面/底部会出现单面剔除后的空洞。
