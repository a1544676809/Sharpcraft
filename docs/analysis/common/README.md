<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/...`
最后更新：2026-10-28

# 基础类型子系统（common）

本目录分析 `Common` 侧最底层的类型行为：标识符、随机数。它们被客户端与服务端同时消费（见 `docs/architecture/boundaries.md`），且**必须与原版逐位一致**——标识符决定注册表顺序与协议字符串，随机数决定同种子世界生成与战利品结果。

## 文档清单

| 文档 | 主题 | 状态 |
| --- | --- | --- |
| [`identifier.md`](identifier.md) | 标识符的解析、校验、等值、排序、序列化 | 草稿 |
| [`random.md`](random.md) | 随机源家族、算法参数、种子派生、位置化随机与战利品序列 | 草稿 |

## 为什么这两块必须先做

1. **排序语义不可改**：注册表遍历顺序影响协议包体与存档内容；标识符的 `compareTo` 顺序是registry 同步的基础。
2. **随机数不可"近似"**：LCG 与 xoroshiro128++ 的每个位都要一致，包括 Java 的 32 位溢出与符号扩展行为。用 `System.Random` 顶替会导致同种子世界不同。
3. **种子派生链有三层**：64 位世界种子 → 128 位升级混合 → 位置/标识符 XOR。任何一层错位都会让整条链失效。

## 未覆盖（后续）

- 噪声函数（`BlendedNoise` / `NormalNoise`）与 128 位种子的具体消费方式：属于世界生成，另立文档。
- 战利品表的结构与求值顺序：属于 `data/minecraft/loot_table` 的执行语义，另立文档。
