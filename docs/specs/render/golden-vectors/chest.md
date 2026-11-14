<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 箱子渲染真值向量（T-041 / `ChestRenderer`）

> 来源：`ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ChestRenderer.java`
> `ref://mc-26.3-analysis/src/net/minecraft/client/model/object/chest/ChestModel.java`
> 提取日期：2026-11-14

单位均为**像素（1/16 方块）**，与模型坐标系一致；`PartPose.offset` 为模型原点偏移，`addBox` 的前三项是盒最小角、后三项是尺寸。

## 1. 单箱几何（`createSingleBodyLayer`）

| 部件 | 纹理偏移 | 盒最小角 | 盒尺寸 | 部件位姿 |
| --- | --- | --- | --- | --- |
| `bottom` | `(0, 19)` | `(1, 0, 1)` | `(14, 10, 14)` | 零 |
| `lid` | `(0, 0)` | `(1, 0, 0)` | `(14, 5, 14)` | 偏移 `(0, 9, 1)` |
| `lock` | `(0, 0)` | `(7, -2, 14)` | `(2, 4, 1)` | 偏移 `(0, 9, 1)` |

纹理图集：**64 × 64**。

## 2. 双箱几何

| 变体 | 隐藏面 | `bottom` 最小角 / 尺寸 | `lid` 最小角 / 尺寸 | `lock` 最小角 / 尺寸 |
| --- | --- | --- | --- | --- |
| 左半 | 隐藏 `WEST` | `(0, 0, 1)` / `(15, 10, 14)` | `(0, 0, 0)` / `(15, 5, 14)` | `(0, -2, 14)` / `(1, 4, 1)` |
| 右半 | 隐藏 `EAST` | `(1, 0, 1)` / `(15, 10, 14)` | `(1, 0, 0)` / `(15, 5, 14)` | `(15, -2, 14)` / `(1, 4, 1)` |

两者的 `lid` 与 `lock` 部件位姿同为偏移 `(0, 9, 1)`。

## 3. 开合动画

`setupAnim(open)`：

- `lid.xRot = -(open × π/2)`
- `lock.xRot = lid.xRot`

而渲染器传入的 `open` **不是**方块实体的原始开度，而是先做三次方缓出：

```
open' = 1 - (1 - open)³
```

## 4. 朝向变换

`modelTransformation(facing)`：绕 **Y 轴**旋转 `-facing.toYRot()` 度。

`Direction.toYRot()` 的取值：`SOUTH = 0°`、`WEST = 90°`、`NORTH = 180°`、`EAST = 270°`。因此实际旋转角为 `-facing.toYRot()`。

## 5. 真值向量

| # | 输入 | 期望输出 |
| --- | --- | --- |
| 1 | 单箱、`facing = SOUTH`、`open = 0` | Y 旋转 `0°`；`lid.xRot = 0`，`lock.xRot = 0` |
| 2 | 单箱、`facing = SOUTH`、`open = 0.5` | `open' = 1 − 0.5³ = 0.875`；`lid.xRot = −0.875 × π/2 = −1.37445 rad ≈ −78.75°` |
| 3 | 单箱、`facing = SOUTH`、`open = 1` | `open' = 1`；`lid.xRot = −π/2 = −90°`（箱盖全开、竖直） |
| 4 | 单箱、`facing = WEST` | Y 旋转 `−90°`；盒与动画同 #1 |
| 5 | 单箱、`facing = NORTH` | Y 旋转 `−180°` |
| 6 | 单箱、`facing = EAST` | Y 旋转 `−270°`（等价 `+90°`） |
| 7 | 双箱左半、`open = 0` | 隐藏面 `WEST`；`bottom` 从 `x=0` 起、宽 15；`lock` 在 `x=0` |
| 8 | 双箱右半、`open = 0` | 隐藏面 `EAST`；`bottom` 从 `x=1` 起、宽 15；`lock` 在 `x=15` |

## 6. 用途

这些向量是 `ChestRenderer` 实现的**验收基准**：实现产出的盒定义、部件位姿、旋转角必须与上表逐值一致，再配合单方块沙盒像素对比。不得用实现反推期望值。
