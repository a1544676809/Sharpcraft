<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 方块事件与方块实体动画

> 状态：`规格已就绪`
> 更新：2026-11-14
> 关联：`docs/specs/render/special-block-rendering.md`、`docs/specs/render/block-entity-submission.md`

## 1. 要解决什么

箱子开合、门开关、音符盒发声这类行为在原版由**方块事件**驱动。本仓的网络层**已经解析** `block_event` 包，但**从不应用**它——事件只被记录在 `InteractiveSession._lastBlockEvent*` 里供诊断。

因此箱子永远画成关闭状态，其它依赖事件的方块（门、活板门、栅栏门）也无法表现。

## 2. 原版模型（已核实）

### 2.1 包

`ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundBlockEventPacket.java`

| 字段 | 类型 |
| --- | --- |
| `pos` | `BlockPos` |
| `b0` | 无符号字节（事件 id） |
| `b1` | 无符号字节（事件参数） |
| `block` | 方块注册表 id（VarInt） |

### 2.2 分发

`Level.blockEvent(pos, block, b0, b1)` → 该位置的 `BlockEntity.triggerEvent(b0, b1)`。

### 2.3 箱子

`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/ChestBlockEntity.java`

- 事件 id **1** = 开合：`triggerEvent(1, b1)` → `chestLidController.shouldBeOpen(b1 > 0)`
- `b1` 是**开箱人数**，只取 `> 0` 判断

### 2.4 开合动画

`ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/ChestLidController.java`

```
每 tick：
  oOpenness = openness
  目标为关且 openness > 0 → openness = max(openness - 0.1, 0)
  目标为开且 openness < 1 → openness = min(openness + 0.1, 1)

取值：
  getOpenness(partialTick) = lerp(partialTick, oOpenness, openness)
```

即**每 tick 变化 0.1，10 tick 走完全程**，渲染时按子 tick 插值。

## 3. 本仓现状

| 层 | 状态 |
| --- | --- |
| 协议解析 | ✅ `Sharpcraft.Common.Network.PlayPackets.TryReadBlockEvent`（pos 打包 long、u8 b0、u8 b1、VarInt blockId） |
| 会话接收 | ✅ `InteractiveSession` 每帧记录 `_lastBlockEvent*`（仅诊断） |
| **状态应用** | ❌ **缺失**——事件被丢弃 |
| **动画** | ❌ 缺失 |

## 4. 设计

### 4.1 `BlockEntityAnimations`

一个客户端侧的状态容器，按方块位置索引：

| 成员 | 职责 |
| --- | --- |
| `Apply(x, y, z, b0, b1)` | 收到方块事件：`b0 == 1` 时设置该位置箱子的目标开合 |
| `Tick()` | 每 tick 调用一次，按 0.1 步进 |
| `GetOpenness(x, y, z, partialTicks)` | 渲染时取值，未记录的位置返回 0 |
| `Prune(...)` | 方块实体被卸载后清理（可选，先不做，登记简化） |

**位置键**：方块坐标打包成 `long`，与区块键同构。

### 4.2 接入点

| 位置 | 动作 |
| --- | --- |
| `InteractiveSession` 收到 `block_event` | 调 `BlockEntityAnimations.Apply(x, y, z, b0, b1)` |
| 客户端 tick | 调 `Tick()` |
| `FrameCommand` 构造 `BlockEntityRenderState` | `Open: animations.GetOpenness(x, y, z, partialTicks)` |

`BlockEntityRenderState.Open` 字段**已存在**，`ChestMesher.OpenAngle` 也已按三次方缓出实现。

### 4.3 简化（先行登记）

| 简化 | 影响 | 计划 |
| --- | --- | --- |
| 只处理箱子的 `b0 == 1` | 门/活板门/音符盒仍无表现 | 后续按事件 id 扩展 |
| 位置记录不清理 | 长时间运行会累积 | 方块实体卸载时清理 |
| `partialTicks` 先取 0 或 1 | 动画可能有一帧的台阶 | 接入 tick 内插值 |

## 5. 验证

1. **单位测试** `ChestLidController` 等价实现：目标开/关各 10 tick 到 1/0；中途 `getOpenness` 插值正确；重复事件不抖动
2. **端到端**：需要真实开箱——用 RCON 无法触发 `block_event`（它由玩家开箱产生），因此端到端验证需要**脚本化玩家交互**；先以单位测试为准，像素验证留待有交互脚本时补
3. **不得**用"能编译"代替上述验证

## 6. 不做什么

- 不做服务端 `blockEvent` 的产生
- 不做音效
- 不实现方块事件之外的状态同步

## 7. 实现结果（2026-11-14）

| 项 | 内容 |
| --- | --- |
| `BlockEntityAnimations` | 按方块位置索引的开合状态；`Apply(eventId, parameter)`、`Tick()`、`GetOpenness(x,y,z)`（当前值）、`GetOpenness(x,y,z,partialTicks)`（插值） |
| 接入 | `InteractiveSession` 收到 `block_event` 时 `Apply`；tick 时 `Tick()`；`FrameCommand` 构造渲染状态时读 `GetOpenness` 填入 `Open` |
| 归属 | 状态挂在 `ClientWorld.BlockEntityAnimations`，因为它是世界状态而非渲染状态 |
| 测试 | 8 条：未跟踪位置为 0、10 tick 开满/关满、1 tick 只走 0.1、partialTicks 插值、重复事件不跳变、非箱子事件忽略、负坐标独立 |

**默认沙盒帧 `9CA9E034…` / 34897 B 逐字节不变**（无事件时开合恒为 0）。

### 仍需补的（已登记）

| 简化 | 影响 |
| --- | --- |
| 只处理 `b0 == 1`（箱子） | 门/活板门/音符盒仍无表现 |
| 位置记录不清理 | 长期运行会累积（每个开过的箱子一条记录） |
| 渲染传 `partialTicks = 0` | 动画按整 tick 台阶推进，无子 tick 插值 |
