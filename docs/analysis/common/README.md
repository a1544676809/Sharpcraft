<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/src/net/minecraft/...`
最后更新：2026-10-28

# 基础类型子系统（common）

本目录分析 `Common` 侧最底层的类型行为：标识符、随机数、注册表顺序。它们被客户端与服务端同时消费（见 `docs/architecture/boundaries.md`），且**必须与原版一致**——标识符排序与注册表顺序决定协议包体与方块状态 id，随机数决定同种子世界生成与战利品结果。

## 文档清单

| 文档 | 主题 | 状态 |
| --- | --- | --- |
| [`identifier.md`](identifier.md) | 标识符的解析、校验、等值、排序、序列化 | 草稿 |
| [`random.md`](random.md) | 随机源家族、算法参数、种子派生、位置化随机与战利品序列 | 草稿 |
| [`registry-order.md`](registry-order.md) | 注册表元素顺序、方块状态全局 id 的生成链 | 草稿 |
| [`coordinates.md`](coordinates.md) | 坐标换算与负数取整规则 | 草稿 |
| [`random-vectors.md`](random-vectors.md) | 参考向量的取真值方法、覆盖范围与回放约定 | 草稿 |

## 为什么这三块必须先做

1. **顺序不可改**：注册表遍历顺序影响网络同步包体与存档内容；标识符的 `compareTo` 顺序（先 path 后 namespace）是注册表有序性的基础。
2. **方块状态 id 是链式的**：方块注册顺序 → 属性名排序 → 属性取值顺序 → 笛卡尔积顺序，任一环错位都会让区块数据里的状态 id 整体偏移。
3. **随机数不可"近似"**：LCG 与 xoroshiro128++ 的每一位都要一致，包括 Java 的 32 位溢出与符号扩展；种子派生还有三层（64 位世界种子 → 128 位混合 → 位置/标识符 XOR）。

## 未覆盖（后续）

- 噪声函数（`BlendedNoise` / `NormalNoise`）与 128 位种子的具体消费方式：属于世界生成，另立文档。
- 战利品表的结构与求值顺序：属于 `data/minecraft/loot_table` 的执行语义，另立文档。
- 各注册表的完整注册清单（用于"顺序快照"测试）：属于数据清单，另立文件。
