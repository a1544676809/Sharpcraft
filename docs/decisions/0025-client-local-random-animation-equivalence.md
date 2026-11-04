<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR 0025：客户端本地随机动画的等价口径
summary: 明确客户端私有 RNG 驱动的装饰动画以算法/分布/触发条件为等价标准
status: 已采纳
-->

# 0025：客户端本地随机动画的等价口径

- 状态：已采纳
- 日期：2026-11-04
- 决策者：项目所有者（本会话确认）
- 关联：ADR-0022、ADR-0024、`docs/modules/entity-ai-presentation.md`

## 背景

Rabbit 的 idle-head-tilt 计时由客户端实体私有的 `Entity.random` 决定：

- `Entity.random = RandomSource.create()`，种子来自进程级 `RandomSupport.generateUniqueSeed()`
  （`SEED_UNIQUIFIER` 乘常数再 XOR `System.nanoTime()`），**不同步**；
- Rabbit 构造时 `idleAnimationTimeout = random.nextInt(40) + 180`，之后每次触发再抽样；
- 该 `random` 是实体级共享的，音效、粒子、其它分支也会消耗同一序列。

因此同一世界的两只客户端 Rabbit（甚至同一客户端重启）的 idle 时刻天然不同。原版并没有把"某个具体随机值/时刻"作为可观察契约。

## 决策

对**纯客户端、仅影响装饰性表现**的随机动画，等价口径定义为：

1. **同算法**：与参考使用同一 RNG 算法（`LegacyRandomSource`/`java.util.Random` 48-bit LCG，含 `nextInt(bound)` 拒绝采样）；
2. **同分布**：相同上界/调用点/触发条件，统计分布一致；
3. **同触发条件**：相同的同步/事件输入（如 `isNoAi`、leash 状态、jump 状态）；
4. **不要求**同一个随机种子或逐实例相同的随机值。

实现时每个实体使用自己的 `LegacyRandomSource`（进程唯一种子即可），不为此新增原版没有的同步字段。

## 影响面

- Rabbit idle-head-tilt 由"阻塞"转为可实现；不登记为受控偏差（实例随机值差异在原版中本来存在）。
- 需同时补：Mob flags bit0 → `isNoAi`、`set_entity_link`(102) → leash 状态。
- 其它纯装饰随机动画遵循同一口径；涉及权威逻辑（伤害、掉落、AI 决策）的随机**不适用**本口径，仍须服务端权威。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 同步种子/idle timeout | 原版无此字段，属于协议扩展。 |
| 固定计时 | 与原版分布不符，属近似。 |
| 继续阻塞 | 原版本身实例间就不同，阻塞没有行为依据。 |

## 复查条件

- 若后续确认该随机影响了服务端可观察行为（不应发生），回退为阻塞并评审。
- 若引入原版之外的同步字段，需另立 ADR。
