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

## 6. 实测记录（本地 offline-mode 服务端，Minecraft 26.3）

### 6.1 服务端权威 id 表（27–45，逐一探针确认）

服务端在解码失败时会**点名它读的是哪个包**，因此可以逐个 id 发一个超长包体，从服务端日志读回真实映射：

| id | 包 | id | 包 |
| --- | --- | --- | --- |
| 27 | `jigsaw_generate` | 37 | `pick_item_from_entity` |
| 28 | `keep_alive` | 38 | `ping_request` |
| 29 | `lock_difficulty` | 39 | `place_recipe` |
| 30 | （未命名，解码通过） | 40 | `player_abilities` |
| 31 | `move_player_pos_rot` | 41 | `player_action` |
| 32 | `move_player_rot` | 42 | `player_command` |
| 33 | `move_player_status_only` | 43 | `player_input` |
| 34 | `move_vehicle` | 44 | `player_loaded` |
| 35 | `paddle_boat` | 45 | `pong` |
| 36 | `pick_item_from_block` | | |

**结论**：我们此前的序号从 27 起整体少 1（分析用的客户端 jar 与服务端构建差一个注册项）。
本表以服务端为准，因为它才是判定方。

### 6.2 已验证通过

| 环节 | 证据 |
| --- | --- |
| 登录 → 配置 → play | `frame` 收到 9/9 区块 |
| 接 `PlayerPosition` | id **73**，位置与日志一致（1.596, 190.0, 11.498） |
| 回 `AcceptTeleportation` | 服务端不报错；**不发移动包时可稳定保持连接 4 秒以上** |
| `MovePlayerPosRot` 编码 | 探针：40 字节包体中服务端只多出 7 字节（=1 id + 3 double + 2 float + 1 标志）→ 布局正确 |

### 6.3 仍然失败：移动包被判非法

服务端日志：`lost connection: Invalid move player packet received`。

`handlePlayerPositionChange` 的判定：服务端按我们的增量自己 `move()` 一次，若最终位置与我们申报的相差超过
0.0625²（0.25 格）即判非法。

**原因**：`walk` 命令里的玩家踩在一个**虚构的平地**（`y < 0` 为实体）上，而服务端的世界在 y≈190 处有真实地形。
两边物理从第一帧就分叉，服务端每次重算都得不到我们的位置，于是判非法。

**修法（下一步）**：让 `walk` 用服务端下发的区块构建真实世界（`frame` 已经在做），并把玩家放到传送包给定的位置。

**这不是"坐标不对"，而是"世界不对"** —— 记下来，避免下次又去调 id 或发包频率。

## 7. 实现状态

- [x] 包 id 与字段布局（本节）
- [x] `PlayMovementPackets.BuildMovePlayer*` 与 `TryReadPlayerPosition`（12 项测试）
- [x] `PlayerSync`（发送决策、偏差分级、`AcceptTeleportation` 回应）
- [x] 接进 `PlaySession` 的收发循环（`player` + `orientation` 可选参数）
- [ ] 与真实服务器实测（本地 offline-mode 服务端已就绪）

### 6.4 改用真实世界之后

把 `walk` 换成"先收集区块、再在真实地形上走"之后：

| 观察 | 结果 |
| --- | --- |
| 玩家物理 | **正确**：`ground=True`，沿 −Z 平稳移动，速度 0.10–0.13 格/秒 |
| 只发 1 个位置包 | **连接存活** |
| 只发 `StatusOnly`（70 个） | **连接存活** |
| 持续发位置包 | 约 3 个包后连接结束；服务端日志 `lost connection: Disconnected`（**无踢人原因**），无 `moved wrongly` 警告，无异常栈 |

**发现的节奏 bug**：`lastFrame` 从计时器零点起算，进服阶段耗时被当成积压，第一帧就爆发几十个 tick。
已改为以开始行走的时刻为锚点，并按 50 ms 一包节流。

**仍未解决**：持续发位置包时连接仍会在约 3 个包后结束，服务端**不给出任何原因**（既非解码错误，
也非 `invalid_player_movement`）。已排除的假设：id 错、包体布局错、坐标含 NaN、同 tick 多发、
世界不一致。

**下一步需要权威对照**：抓官方客户端的 serverbound 序列（是否先发 `client_information`(14)、
`player_input`(43)、`player_loaded`(44)），或在服务端开启 `--debug` 观察 `handlePlayerPositionChange`
走的是哪条分支。
