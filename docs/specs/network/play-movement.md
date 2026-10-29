<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: Play 阶段的移动同步
summary: 移动包的字段、id 来源与"吸附/插值"的决策规则
status: 已评审
-->

# Play 阶段移动同步

客户端推进的实体位置必须与服务端一致。本文固定三件事：发什么、收到什么、冲突时怎么处理。
字段与 id 全部来自分析产物，不凭印象。

## 1. 包 id（26.3 / 协议 777）

| 方向 | 包 | id | 来源 |
| --- | --- | --- | --- |
| 客户端 → 服务端 | `AcceptTeleportation` | 0 | 链首 |
| 客户端 → 服务端 | `KeepAlive` 回应 | 27 | 全链序号 |
| 客户端 → 服务端 | `MovePlayerPos` | 29 | 全链序号 |
| 客户端 → 服务端 | `MovePlayerPosRot` | 30 | 全链序号 |
| 客户端 → 服务端 | `MovePlayerRot` | 31 | 全链序号 |
| 客户端 → 服务端 | `MovePlayerStatusOnly` | 32 | 全链序号 |
| 客户端 → 服务端 | `Pong` 回应 | 44 | 全链序号 |
| 服务端 → 客户端 | `PlayerPosition`（绝对传送/重置） | 73 | 以 `LevelChunkWithLight = 46` 为锚点相对计数 |

**校准方法**：id 等于该方向 builder 链内 `.addPacket(...)` 的出现序号。

**此处曾经出过错，必须记住**：通用包（`CommonPacketTypes`：keepalive、ping/pong、client information 等）
**也注册在同一条 play 链里**。只统计 `GamePacketTypes` 的计数会整体偏移，早期表中
`KeepAlive = 28`、`MovePlayerPos = 30`、`Pong = 45` 就是这么来的（真实值 27 / 29 / 44）。
统计时必须匹配任意 `*PacketTypes.` 前缀，而不是只匹配游戏包。

已知锚点：`AcceptTeleportation = 0`（实测可用）、`Login = 50`、`LevelChunkWithLight = 46`、
`PlayerPosition = 73`（实测收到并被接受）。

## 2. 字段布局

| 包 | 字段顺序 |
| --- | --- |
| `AcceptTeleportation` | `VarInt id`、`double x/y/z`、`float yRot`、`float xRot` |
| `MovePlayerPos` | `double x/y/z`、`bool onGround`、`bool horizontalCollision` |
| `MovePlayerPosRot` | 同上 + `float yRot`、`float xRot` |
| `MovePlayerRot` | `float yRot`、`float xRot`、`bool onGround`、`bool horizontalCollision` |
| `MovePlayerStatusOnly` | `bool onGround`、`bool horizontalCollision` |
| `PlayerPosition` | `VarInt id`、`PositionMoveRotation`（位置、速度、朝向）、`Relative` 位集合 |

`Relative` 位集合决定哪些分量是**相对**当前值而非绝对；实现时必须按位处理，不能一律当绝对坐标。

## 3. 发送策略

- 每个 tick 结束、位置或朝向变化时才发（`MovePlayerPosRot`）；只变了朝向发 `MovePlayerRot`；
  只变了落地状态发 `MovePlayerStatusOnly`；都没变则**不发**。
- 位置在服务端以 1/4096 格精度量化，发送前按该精度取整，避免"本地算出的微小抖动"变成持续发包。
- 收到 `PlayerPosition` 时：把实体位置与速度设为服务端值，回 `AcceptTeleportation`（带 id 与当前位置朝向），
  并**丢弃本地预测**（见 §4）。

## 4. 冲突处理：吸附还是平滑

| 情况 | 处理 | 理由 |
| --- | --- | --- |
| 传送包（`PlayerPosition`） | **吸附**，并让插值器 `Snap` | 传送不应被画成一次飞行 |
| 偏差 ≤ 2 格 | 保留本地预测，向服务端值插值修正 | 小偏差多为延迟抖动，硬吸附会顿挫 |
| 偏差 > 2 格 | **吸附** | 大偏差说明本地状态已失效，继续平滑只会越走越偏 |

阈值 2 格是**待实测确认**的经验值，不是原版常量；实测后写回本节。原版没有"平滑修正"机制，
它的做法是让服务端位置直接覆盖客户端——我们的插值属于**有意偏离**，目的是避免网络抖动导致的顿挫，
必须在 `docs/specs/quality-attributes.md` 的偏差清单里登记。

## 5. 与离线模式的关系

验证环境服务端不开 `online-mode`，因此进服不需要会话令牌；即便如此，**移动包的格式与 id 不变**，
同步逻辑全部可验证。离线服务器只影响认证链，不影响本规格。

## 6. 实测记录（本地 offline-mode 服务端，2026-10-29）

| 环节 | 结果 |
| --- | --- |
| 登录 / 配置 / 进 play | 通过（`frame` 收到 9/9 区块） |
| 收到 `PlayerPosition`（id 73） | 通过：偏差 190.32 格，吸附并回 `AcceptTeleportation` |
| 发送任意 `MovePlayer*` | **失败**：发出 2–3 个包后服务端直接关闭连接，无 disconnect 包 |
| 仅发 `MovePlayerStatusOnly`（3 字节） | **同样失败** |

**结论**：失败与坐标无关（纯标志包一样被关），问题在移动包本身的 id 或编码。
按 4.1 已停止试错，等待服务端日志给出确切原因（kick 原因会直接指明是解码错误还是移动校验）。

## 7. 实现状态

- [x] 包 id 与字段布局（本节）
- [x] `PlayMovementPackets.BuildMovePlayer*` 与 `TryReadPlayerPosition`（12 项测试）
- [x] `PlayerSync`（发送决策、偏差分级、`AcceptTeleportation` 回应）
- [x] 接进 `PlaySession` 的收发循环（`player` + `orientation` 可选参数）
- [ ] 与真实服务器实测（本地 offline-mode 服务端已就绪）
