<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/input.md` §6（帧节奏）
最后更新：2026-10-29

# Client/Game —— 固定步长循环

`TickLoop`：把"帧"和"tick"分开。逻辑以固定 20 Hz 推进，渲染按到达速率出帧，两者用累加器衔接。

## 1. 职责范围

负责：累加时间、按整 tick 触发回调、给出插值因子、限制单帧补跑数量。

不负责：计时来源（调用方给 `TimeSpan`）、渲染、实体本身。

## 2. 对外契约

| 成员 | 说明 |
| --- | --- |
| `TickInterval` | 50 ms（20 Hz），与原版逻辑 tick 一致 |
| `Advance(TimeSpan, Action)` | 跑完整 tick，返回执行数量 |
| `Alpha` | 进入下一 tick 的比例，用于渲染插值 |
| `PendingTicks` | 累加器中未消费的 tick 数 |
| `MaxTicksPerAdvance` | 5，单帧上限 |

## 3. 为什么要上限

卡顿一次（拖动窗口、断点、GC）会积压大量时间。若一帧内全部补跑，这一帧就更慢，积压更多——典型的死亡螺旋。
超过上限的积压被**丢弃**而不是补跑：宁可让世界"跳一下"，也不能让帧时间失控。

## 4. 测试

60 帧 × 16 ms 得到 18–20 次 tick（固定速率）；10 秒的单帧只跑 5 次（上限生效）；25 ms 的 alpha ≈ 0.5。

## 5. 未完成

- 插值尚未用于渲染：相机直接取实体当前眼位，没有在两 tick 之间插值。实体移动较慢，1 tick 的抖动不明显，
  但高速移动（下落、冲刺）会看出来，届时按 `Alpha` 插值。
