<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/{util/Mth.java,core/SectionPos.java,world/level/ChunkPos.java}`
最后更新：2026-10-28

# 坐标与取整规格

坐标换算看着简单，但**负数取整**是最经典的错误源：Java 的 `>>` 是算术右移（等价于向下取整除以 2^n），而 C# 的整数除法 `/` 是向零截断。两者在负数上不同。

## 1. 核心规则

| 换算 | 规则 | 依据 |
| --- | --- | --- |
| 方块坐标 → 区块坐标 | `blockCoord >> 4`（算术右移） | `SectionPos.blockToSectionCoord` |
| 浮点 → 方块坐标 | `(int)Math.floor(v)`（**向下**取整） | `Mth.floor` |
| 浮点 → 区块坐标 | `floor(v) >> 4` | `SectionPos.blockToSectionCoord(double)` |
| 区块内相对坐标 | `blockCoord & 15`（结果恒在 0..15） | `SectionPos.sectionRelative` |
| 区块坐标 → 最小方块坐标 | `chunkCoord << 4` | `ChunkPos.getMinBlockX` |
| 向下取整除法 | `Math.floorDiv(a, b)`（**不是**截断除法） | `Mth.floorDiv` |

## 2. 边界样例（必须进测试）

| 输入方块坐标 | 区块坐标 | 区块内相对 |
| --- | --- | --- |
| 0 | 0 | 0 |
| 15 | 0 | 15 |
| 16 | 1 | 0 |
| -1 | **-1** | **15** |
| -16 | **-1** | 0 |
| -17 | **-2** | 15 |

`-1 >> 4 == -1`、`-1 & 15 == 15` 这两条是负数正确性的分水岭。若用 C# 的 `/ 16`，`-1 / 16 == 0`，会把负坐标区块整体错一格。

## 3. 浮点取整

- `Mth.floor(double)` 用 `Math.Floor` 后转 `int`：`-0.5 → -1`、`0.5 → 0`、`-0.0 → 0`。
- 注意 `(int)` 转换在 C# 中是**向零截断**，直接 `(int)v` 是错的，必须先 `Math.Floor`。
- `[待验证]` 超出 `int` 范围的输入行为（原版依赖 Java 的饱和转换语义，C# 未定义/饱和，需在实现时显式加边界策略）。

## 4. 对 C# 实现的约束

1. 换算集中在一个静态类中提供，禁止各处手写 `/ 16` 或 `(int)`。
2. 提供 `FloorDiv` 工具，禁止用 `/` 代替向下取整除法。
3. 边界样例必须写成参数化测试（含负数六连）。
4. 坐标类型（`BlockPos` / `Vec3` / `Aabb`）的相等性、哈希与格式化在 `common-primitives.md` §4.3 中另行规定；本文只约束换算与取整。

## 5. 待验证

- `ChunkPos` 的主构造器是否直接使用 `>> 4`（本轮读到的是 `getMinBlockX` 的 `<< 4` 与区块判定；主构造器体未逐行核对）。
- 双精度坐标跨度极大时的饱和行为（见 §3）。
