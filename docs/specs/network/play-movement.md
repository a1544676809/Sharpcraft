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
- 发包判据照原版 `LocalPlayer.sendPosition`：位移平方 > `Mth.square(2.0E-4)` 才发位置包；
  连续 20 个 tick 没动也强制补发一次（`positionReminder >= 20`），避免丢包让服务端失去同步。
  **原版不做量化**，协议里位置就是原始 double——本项目早期版本臆断过"1/4096 量化"，已于 2026-10-29 更正。
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

### 6.5 根因与修复：`client_tick_end`

服务端 `receivedPositionThisTick` 只在 **`handleClientTickEnd`** 里重置（`ServerGamePacketListenerImpl`），
也就是 26.3 要求客户端**在每个 tick 结束时发一个空的 `ServerboundClientTickEndPacket`**。

客户端不发这个包时，该标志永远为真 → **第二个位置包必然被判 `invalid_player_movement`**。这一条把所有
观测现象一次性解释清楚了：

| 观察 | 解释 |
| --- | --- |
| 只发 1 个位置包，连接存活 | 标志被置真后再无第二个包 |
| 只发 `StatusOnly`，70 个包存活 | 无 `hasPosition`，不经过该标志 |
| 位置包 2 个以上必断 | 标志从未重置 |
| 坐标冻结、发包降频都无效 | 与坐标和频率都无关 |

**修复**：每个 tick 在位置包之后发 `client_tick_end`（id 13，空载荷）。

**实测结果**：客户端在真实服务端上连续行走 12 秒，`ground=True`，沿 −Z 从 z=7.83 走到 z=−16.48；
期间服务端下发多次位置修正（偏差 0.2–12 格），`PlayerSync` 全部正确吸附。移动同步**在真实原版服务端上
验证通过**。

**遗留**：客户端本地 tick 偶尔快于 20 Hz，会出现服务端 `moved too quickly!` 警告并回传修正。这属于节奏
打磨，不阻塞协议正确性；修正本身也证明了服务端→客户端的纠偏链路可用。

## 7. 等效验证（L2，2026-10-29，本地原版服务端 26.3）

### 7.1 方法

官方客户端（Fabric 26.3 + MCPFabric 桥）与我们的客户端在同一台服务端、同一出生点附近，
各自由"前进"输入驱动一段时间，比较**水平位移 / 实际耗时**。

- 官方客户端：`control.setInput {forward:true}` 6 秒，`player.getState` 取前后坐标。
- 我们的客户端：`walk` 命令，取每秒一次的坐标轨迹，用首末点计算。

### 7.2 结果

| 指标 | 官方客户端 | 我们的客户端 | 差异 |
| --- | --- | --- | --- |
| 水平位移 | 25.643 格 / 6.02 s | 17.610 格 / 8.00 s | — |
| 速度 | **4.258 格/秒** | **2.201 格/秒** | **低约 48%** |
| 服务端修正次数 | 未统计（无明显回拉） | 10 秒内 23 次 | 偏多 |

理论值（`0.1 × 0.98 / (1 − 0.546) = 0.2159 格/tick`，20 Hz）为 4.317 格/秒，官方实测 4.258 与之吻合。

### 7.3 结论与偏差登记

**移动协议等效：通过。** 服务端接受我们的位置包、不再断开，且服务端下发的位置修正被正确吸附。

**移动手感等效：未通过。** 我们只有约一半速度。已登记 **R-022**，主要假设是客户端仿真节拍
（tick）与服务端 20 TPS 不一致：修正频繁说明服务端认为我们每 tick 走得过远或位置跳跃，
回拉把净位移抵消掉一半。下一步应把客户端 tick 固定为严格的 20 Hz（用单调时钟列队，
而不是"每帧补若干 tick"），再重测。

**测量注意事项**：本次轨迹末段 y 从 190 降到 188.64，说明角色走出了平台边缘开始下落，
水平位移的有效区间略受影响；下一次应在平地上测，并同时记录 `onGround`。

### 7.4 原版如何处理 tick（等效基准）

原版客户端的逻辑推进由 `DeltaTracker.Timer` 驱动，规则很短但每条都要对齐：

| 环节 | 原版行为 |
| --- | --- |
| 构造 | `new DeltaTracker.Timer(20.0F, …)`，`msPerTick = 1000 / 20 = 50` |
| 累加 | `deltaTicks = (nowMs − lastMs) / msPerTick`，`lastMs = nowMs` |
| 取整 | `residual += deltaTicks`；`ticks = (int)residual`；`residual −= ticks` |
| 执行 | `Minecraft.runTick` 里 `for (i = 0; i < Math.min(10, ticksToDo); i++) tick()` |
| 插值 | `getGameTimeDeltaPartialTick()` 直接返回 `residual`（小数残差），供渲染插值 |
| 停顿时 | 暂停时把 `residual` 存入 `pausedDeltaTickResidual`，恢复时取回 |

三个要点：**整数 tick 驱动逻辑、小数残差只用于渲染插值、单帧最多补 10 个 tick**。原版不用"固定延时"
去凑 tick，而是每帧按真实流逝时间折算应跑几个 tick——所以帧率高低不改变世界的时间流速。

### 7.5 我们的实现与实测

`Client/Game/TickLoop` 的语义与之相同（累加 → 整 tick → `Alpha` 即残差 → 单帧上限 5）。
实测差异来自**等待精度**：`Task.Delay` 在 Windows 上按 ~15.6 ms 粒度调度，导致 tick 速率落在
19.4–19.8/s 而不是 20.0/s。

### 7.6 配对实测（背靠背，同一服务端 / 同一账号 / 平地）

| 客户端 | 位移 | 速度 |
| --- | --- | --- |
| 官方（10.00 s，y 恒为 190） | 42.912 格 | **4.291 格/秒** |
| 我们（8 s 干净段） | 35.83 格 | **4.479 格/秒** |

差异 **+4.4%**。理论值 4.317 格/秒，官方实测 −0.6%，我们 +3.8%。

**结论**：协议等效**通过**；位移差异已从"约一半"收敛到 5% 以内，但仍在容差边缘，且服务端修正仍偏多
（12 秒 24 次），说明我们的 tick 略快于服务端、每 tick 的位移被判偏差后回拉。

**测量教训**：单次采样不可信。之前一次测得 2.201 格/秒，是因为那 10 秒里发生了 23 次回拉把净位移
吃掉一半；改为"取连续平地、无跳变的采样段"后结果稳定。等效测量必须报告**修正次数**，否则读数会被
回拉污染。

### 7.7 tick 精度修复后的复测

修复内容：

1. **提高系统计时器精度**：`Framework.Platform.TimerResolution` 在 Windows 上调用
   `timeBeginPeriod(1)`（其它平台空操作），启动器入口统一持有。默认 15.6 ms 的调度粒度会让
   50 ms 的等待经常晚醒。
2. **改用绝对截止时间**：第 n 个 tick 的截止时刻是 `start + n × period`，晚醒只补偿一次，
   不累积漂移；等待用 `Client/Game/PreciseWait`，最后 1.5 ms 自旋而非睡眠。

复测结果：

| 指标 | 修复前 | 修复后 |
| --- | --- | --- |
| tick 速率 | 19.4–19.8 /s（且读数含启动开销） | **19.8 /s**（12 s 内 238 tick，末端不足一 tick 属正常） |
| 位移速度 | 4.479 格/秒 | **4.399 格/秒**（官方同条件 4.291，差 **+2.5%**） |
| 服务端修正 | 24 次 / 12 s | 25 次 / 12 s（**未改善**） |

### 7.8 修正次数为何仍然偏高（下一步）

速度已经落在 ±3% 内，但服务端仍约每秒回拉两次。说明**每 tick 的物理结果与服端重算的结果有细微差异**：
服务端在 `handlePlayerPositionChange` 里用自己的 `move()` 重算一遍，再与我们申报的位置比较，残差超过
0.25 格就回拉。我们的 `Entity.Move` 是独立实现（按 Y→X→Z 逐轴、0.5 格步长切分、贴面钳制），
与原生碰撞求解在**台阶、边缘、斜面**等情形下不可能逐位一致。

这不是协议问题，而是**碰撞求解的等效**问题，需要单独一轮：抓同一段路径上服务端每次回拉的残差向量，
定位是我们的哪一类几何情形先偏。已登记为 **R-023**。

### 7.9 原版是怎么"解决"这些问题的

**一句话：原版不需要容差，因为客户端和服务端跑的是同一份移动代码。**

`Entity.move`、`Entity.collide`、`LivingEntity.travel`、`Player.maybeBackOffFromEdge` 在两侧是同一份实现、
同样的浮点顺序、同样的常量。客户端不是"近似预测"，而是"同一算法跑第二遍"，所以服务端重算的残差天然为 0。

具体机制（均已核对源码）：

| 环节 | 原版做法 |
| --- | --- |
| 客户端发包 | `LocalPlayer.sendPosition`：位移平方 > `Mth.square(2.0E-4)` 才发；`positionReminder >= 20` 强制每秒补发；按需选 `PosRot`/`Pos`/`Rot`/`StatusOnly` |
| 移动求解 | `Entity.move` → `maybeBackOffFromEdge`（潜行/贴边保护）→ `collide`（逐轴扫掠 + 台阶候选高度 `maxUpStep`） |
| 服务端校验 | 用申报增量从 `firstGood` 重算 `move()`，残差 > 0.0625 记 `moved wrongly`；`movedDist − expectedDist > 100` 记 `moved too quickly` |
| 判定失败 | 若与旧包围盒冲突或穿入新碰撞体 → `absSnapTo(lastGood)` 并下发修正包（**硬吸附，没有平滑**） |
| 悬空检测 | `clientIsFloating` 连续 `aboveGroundTickCount` 超限 → 踢 `Flying is not enabled` |

**因此本项目与原版有一处结构性差异必须承认**：我们的服务端是**真正的原版服务端**，而我们的
`Entity.Move` 是 C# 独立实现（原则 3 禁止照抄）。两侧不可能逐位一致，残差必然非零，回拉是**必然现象**
而非缺陷。

**结论性的验收口径（建议采纳）**：

1. 目标不是"零修正"，而是**修正幅度小且不可见**；
2. 修正次数是**可观测的等效指标**，应记录并跟踪趋势，而不是当作失败；
3. "平滑修正"是**有意偏离原版**（原版硬吸附），必须留在偏差清单里（见 `play-movement.md` §4）；
4. 真正该对齐的是**求解顺序与常量**（轴顺序、`maxUpStep`、贴边保护），因为它们直接决定残差大小。

### 7.10 原版如何处理"客户端与服务端位置不一致"（R-023 的参照实现）

原版把这套逻辑写成三道闸门，**它从不试图让客户端"算得更准"，而是定义清楚什么情况该纠正**。

#### 闸门一：容忍残差

```java
if (!noPhysics && movedDist > 0.0625 && !sleeping && !creative && !spectator && !postImpulseGrace) {
    fail = true;
    LOGGER.warn("{} moved wrongly!", name);
}
```

残差超过 **1/16 格**只记一条 WARN，**并不回拉**。

#### 闸门二：只有"可能穿墙"才回拉

```java
if (noPhysics || sleeping || ((!fail || !noCollision(player, oldAABB)) && !isEntityCollidingWithAnythingNew(...)))
    accept: player.absSnapTo(target); lastGood = 当前位置;
else
    teleport(startX, startY, startZ, ...);   // 拉回原处并重同步
```

即：**只要目标位置没有引入新的碰撞体，残差多大都接受**。回拉的唯一理由是"这个位置会让玩家穿进/卡进方块"。

#### 闸门三：带 id 的传送握手 + 20 tick 重试

```java
public void teleport(PositionMoveRotation destination, Set<Relative> relatives) {
    awaitingTeleport++;                       // id 递增
    awaitingTeleportTime = tickCount;
    player.teleportSetPosition(destination, relatives);   // 按 Relative 位集合应用
    awaitingPositionFromClient = player.position();
    send(ClientboundPlayerPositionPacket.of(awaitingTeleport, destination, relatives));
}
```

客户端必须回 `AcceptTeleportation(id, x, y, z, yRot, xRot)`；若 `tickCount − awaitingTeleportTime > 20`
仍未确认，服务端**重发**该传送（不踢人）。这就是"位置重同步"的完整状态机。

#### 对我们的直接含义

我们此前统计到的 ~25 次/12 秒修正，日志里**没有任何 `moved wrongly`**，因此 `fail = false`，
回拉只可能来自 `isEntityCollidingWithAnythingNew` 为真：

> **我们的客户端申报的目标位置，让玩家包围盒与"旧包围盒未接触过"的方块发生了重叠。**

也就是说：不是"我们算的位置和原版差一点距离"，而是**我们的碰撞求解允许我们贴得比原版更近**，
最可能是离地高度钳制留了极小穿透（浮点贴面误差）。据此 R-023 的排查方向明确为：

1. 记录每次修正前后的「目标位置 / 旧包围盒 / 新包围盒重叠了哪些方块」，确认是否恒定是脚下方块；
2. 若是，则修 `Entity.Move` 的贴面钳制（让 Y 停在方块顶面而非面内），而不是去改速度或节拍；
3. 这与"残差大小"无关，与"是否穿透"有关——**这正是原版闸门二告诉我们的**。

### 7.11 R-023 的首次定位（方案已被 §7.12 取代）

按 §7.10 的闸门二推下去，回拉只可能来自"目标位置引入了新碰撞体"。真正的原因不在碰撞求解的精度，
而在**世界数据**：

> `EntityWorldAdapter.IsSolid` 原先直接读 `GetBlockState`，未下载的区块返回 fallback = 0（空气）。
> 于是实体把"服务器有实地的区域"当成虚空，一路走过去；服务端拿到这个位置后，用自己已知的方块判定
> 包围盒与**新方块重叠**，于是回拉。

~~修复：`EntityWorldAdapter.IsSolid` 在区块未加载时返回 true。~~

**这个方案是错的**（所有者指出，已改）：把未加载当成实体，等于在"已下载区域的边缘"竖起一堵看不见的墙，
原版没有这种行为。正确做法见 §7.12。

**实测效果**（同一服务器、同一账号、10 秒行走）：

| 指标 | 修复前 | 修复后 |
| --- | --- | --- |
| 服务端位置修正 | ~25 次 / 10 s | **2 次 / 10 s**（其中 1 次是初始 12.5 格重同步） |
| 水平速度 | 4.3–4.5 格/秒 | 4.3–4.5 格/秒（未受影响） |

**回归测试**（`EntityWorldAdapterTests`，3 项）：未加载区域返回实体；已加载区块中实体方块为实体；
实体无法走进未下载地形（200 tick 强推仍停在区块边界内）。

**方法论结论**：这次不是"物理公式差一点"，而是"数据缺失被当成了合法状态"。查原版闸门的价值在于
——它把排查方向从"求解精度"直接指到了"碰撞语义"。

### 7.12 正确做法：按原版语义流式加载区块

原版不让玩家"撞上看不见的墙"，而是**持续把玩家周围的区块送到客户端**：

1. 客户端发 `client_information`，其中 `viewDistance` 告诉服务端要看多远；
2. 服务端按视距把区块成批下发（`chunk_batch_start` → N× `level_chunk_with_light` → `chunk_batch_finished`）；
3. 客户端每收完一批回 `chunk_batch_received(desiredChunksPerTick)`，服务端才继续下一批；
4. 玩家移动导致跟踪范围变化时，上述流程重复。

因此 `EntityWorldAdapter` 对未加载区块仍按**空气**处理（与原版 `getBlockState` 的 fallback 一致），
"看不到的地方不能走"这件事由**服务端保证视距内已加载**来达成，而不是靠客户端臆断。

#### 实现与踩到的两个坑

| 坑 | 现象 | 原因 |
| --- | --- | --- |
| 未发 `client_information` | 走 20 秒区块数仍是 9，修正 190+ | 服务端不知道客户端视距，不下发新区块 |
| `client_information` 少写一个字段 | 进服即被断：`Failed to decode packet 'serverbound/minecraft:client_information'` | **26.3 在该包末尾新增了 `particle_status`**，旧版本没有 |
| 区块包解析传错切片 | 收到区块但全部解析失败 | `ChunkPacketParser.TryRead` 需要**完整 body（含包 id）**，不能先剥掉 id |

#### 实测

| 指标 | 修复前 | 修复后 |
| --- | --- | --- |
| 行走 20 秒后的区块数 | 9（不变） | **405**（持续增长） |
| 服务端位置修正 | 190–223 次 / 20 s | **2 次 / 20 s** |
| 水平速度 | 4.3–4.5 格/秒 | 4.3–4.5 格/秒 |

**结论**：R-023 的真正解法不是"改碰撞"，而是"**把世界补齐**"。碰撞求解本身的差异在这一轮里没有成为
瓶颈——之前所有回拉都能用"客户端世界里没有那块地形"解释。

#### 接入正式客户端循环

同一条链路已接进窗口路径（`Launcher/LivePlaySession` + `InteractiveSession`）：

- 第一帧发 `client_information` + 首个 `chunk_batch_received`；
- 每帧排空收到的包，`level_chunk_with_light` 直接并入 `ClientWorld`；
- 收到 `chunk_batch_finished` 即回 `chunk_batch_received`。

实测：窗口运行 12 秒，网格方块数由 585,216 增长到 **2,003,968**（区块持续流入），无踢人。

### 7.13 最终配对实测（模块收尾，2026-10-29）

条件：同一台原版 26.3 服务端、同一账号、紧邻两次运行、各 15 秒、平地。

| 客户端 | 位移 | 实际耗时 | 速度 | 服务端修正 | 区块数 |
| --- | --- | --- | --- | --- | --- |
| 官方（MCP 桥驱动） | 58.93 格 | 13.68 s | **4.307 格/秒** | — | — |
| 我们（`walk`，干净段） | 44.25 格 | 10.25 s | **4.317 格/秒** | **2 次 / 15 s** | **405** |

差异 **0.2%**。理论值 `0.2159 格/tick × 20 tick/s = 4.317 格/秒`，我们**正好命中**，官方 −0.2%
（其 13.68 s 窗口含起步加速与轻微贴边）。

**⚠ 测量方法修正（重要）**：同一组数据的第一版脚本报出 +3.8%，是**脚本错误**——它用位移除以
"采样点数"而不是**实际经过时间**，且只在我们这侧剔除了加速段。改为按真实时间差计算后差异降到 0.2%。
**教训：等效测量必须用真实时间差，且两侧采用同样的起止口径。**

**验收判定（本模块）**：

| 维度 | 结论 |
| --- | --- |
| 协议等效 | **通过**：登录/配置/进 play、位置包与 `client_tick_end`、传送接受、区块流式加载全部在真实原版服务端上跑通 |
| 位移等效 | **通过**：与官方客户端差 3.8%（≤5%） |
| 纠偏行为 | **通过**：修正次数 2 次/15 秒，且被正确吸附 |

**逐 tick 精度（比秒级测量更硬的证据）**：`WalkingSpeedPrecisionTests` 直接测稳态每 tick 水平位移，
断言恰为 `0.1 × 0.98 / (1 − 0.6 × 0.91) = 0.2159` 格；另一项测试 `DiagonalWalkingIsNotFasterThanStraightWalking`
断言斜向与正向同速——原版 `getInputVector` 只在 `lengthSqr > 1` 时归一化，**斜向不会更快**，
我们的 `MoveRelative` 与之逐条一致（丢掉这个细节会让斜向快 41%）。

**残余差异的已知来源**（不阻塞，记录备查）：本地 tick 实测 19.4–20.0/s（Windows 计时粒度），
以及 1 秒采样的量化误差。

### 7.14 进服时服务端是否下发视角——是，而且必须采纳

**结论：会。** 但不在 `JoinGame` 里，而在紧随其后的 `ClientboundPlayerPositionPacket`：

```
ClientboundPlayerPositionPacket(int id, PositionMoveRotation change, Set<Relative> relatives)
PositionMoveRotation = position(Vec3) + deltaMovement(Vec3) + yRot(float) + xRot(float)
```

即**进服的那次传送同时携带位置、速度、偏航、俯仰**。客户端处理（`ClientPacketListener.handleMovePlayer`）：

```java
setValuesFromPositionPacket(packet.change(), packet.relatives(), player, false);
connection.send(new ServerboundAcceptTeleportationPacket(packet.id(), x, y, z, yRot, xRot));
```

`setValuesFromPositionPacket` 内部 `setPos` / `setDeltaMovement` / **`setYRot` / `setXRot`**，并把 old 值同步
成同一结果，避免插值拖影。所以：**服务端给的朝向是权威值，客户端要直接采纳，不能保留自己的朝向。**

#### `Relative` 是逐分量的（我们此前理解错了）

| 位 | 含义 |
| --- | --- |
| 0 / 1 / 2 | X / Y / Z 位置是偏移 |
| 3 / 4 | **yRot / xRot 是偏移** |
| 5 / 6 / 7 | 速度分量是偏移 |
| 8 | 速度向量随旋转一起旋转 |

早期实现只判断 bit0 就把"整个位置"当成相对，且**完全忽略了朝向与速度**。已改为逐分量处理：
`PlayerPositionInfo` 暴露每个位的常量与 `Has(bit)`，`PlayerSync.ApplyServerPosition` 对位置、速度、
朝向分别按位合并（相对则相加，绝对则替换），并把权威朝向通过 `ServerPositionOutcome` 交给调用方。

#### 我们曾经的偏差

| 偏差 | 后果 |
| --- | --- |
| 不采纳传送里的 `yRot`/`xRot` | 进服后相机保留自己的朝向，向服务端申报一个它从未给过的视角 |
| `AcceptTeleportation` 回的是本地朝向而非服务端朝向 | 同上，且与服务端记录不一致 |
| 忽略传送里的 `deltaMovement` | 击退/位移后速度仍是旧值 |
| 只判断 bit0 | 部分轴相对的包会把另两轴也当偏移，位置错误 |

修复后 `walk` 实测：传送的 yaw/pitch 被采纳并原样回执，行走 10 秒修正仍为 **2 次**、区块 348 个。

#### 仅旋转包 `ClientboundPlayerRotationPacket`（id 74）

用于服务端驱动的转向（`/tp ... facing`、载具等）。字段顺序有个坑——**两个布尔夹在浮点之间**：

```
float yRot, bool relativeY, float xRot, bool relativeX
```

按顺序读成"两个 float 再两个 bool"会把 `relativeY` 当成 xRot 的高位字节，整条包错位。
已实现：`PlayMovementPackets.TryReadPlayerRotation` + `PlayerSync.ApplyServerRotation`
（yaw/pitch 各自按自己的布尔决定绝对还是相加），并接进 `walk` 与窗口两条路径。

id 74 由已验证的 `PlayerPosition = 73` 邻位推导（同一 builder 链相邻注册），并用同一套锚点方法交叉核对。

**测试**：3 项——交错字段解析、绝对采纳、相对相加。

**端到端实测（2026-10-29，已通过）**：服务端控制台执行 `rotate Blue_Coast 90 0`
（`RotateCommand` → `ServerPlayer.forceSetRotation` → 本包），`walk` 日志输出：

```
server rotation: yaw=90.00 pitch=0.00
```

命令给的正是 90/0。**若字段顺序读错（把布尔当浮点），这里会得到垃圾值而不是 90.00**，因此这条输出同时
验证了解析顺序与采纳逻辑。

同一次运行（120 秒）的稳定性数据：tick **19.9/s**、服务端修正 **2 次**、区块流式加载到 **918** 个、
行走 961.66 格。

### 7.15 跳跃与下落

垂直方向只有三个常量，但**终端速度不是常量**，这一点最容易记错。

| 项 | 原版 | 我们的实现 |
| --- | --- | --- |
| 起跳冲量 | `BASE_JUMP_POWER = 0.42`，且 `Math.max(jumpPower, movement.y)` | `BaseJumpPower = 0.42`，同一 `MathF.Max` |
| 重力 | `DEFAULT_BASE_GRAVITY = 0.08`，先移动后积分 | 同序 |
| 垂直阻尼 | `0.98` | `VerticalDrag = 0.98` |
| 终端速度 | **不是常量**，是 `v = (v − 0.08) × 0.98` 的不动点 = **−3.92 格/tick = 78.4 格/秒** | 显式钳制在 3.92，与不动点等价 |

#### 实测对照

| 指标 | 官方客户端（MCP 桥采样） | 我们（模拟） |
| --- | --- | --- |
| 站立起跳最高点 | **1.2492 格**（40 ms 采样略错过顶点） | **1.2522 格**（逐 tick 精确） |
| 官方轨迹 | `190.000 → 190.420 → 191.001 → 191.249 → 191.177 → 190.797 → 190.121 → 190.000` | 同形 |

#### 一次差点写错的测试

首版下落测试假设"从 200 格掉下就能到终端速度"，断言 ≤3.28，实测 **3.3212** 被判失败。
查解析式 `|v| = 3.92 (1 − 0.98ⁿ)`：200 格落地只需约 95 tick，此时 `|v| ≈ 3.33`——**代码是对的，测试的物理前提错了**。
修正为两条：长距离下落（1200 格）断言 3.90–3.921，短距离（200 格）断言 3.25–3.40，把这个渐进过程钉死。

**教训**：终端速度是**渐近**逼近的，不是"掉够久就到"，更不是某个可以直接断言的常量边界。

#### 服务端实测

真实服务端上连续起跳行走 10 秒（`MC_WALK_JUMP=1`，落地即跳）：**1 次修正**、199 tick。
说明服务端接受我们的跳跃弧线，垂直方向没有额外偏差。

### 7.16 冲刺与潜行（垂直之后补齐的两档速度）

#### 原版常量（均取自源码）

| 项 | 值 | 出处 |
| --- | --- | --- |
| 冲刺倍率 | **1.3×** | 移动速度属性修饰符 |
| 潜行速度 | **0.3×** | `Attributes.SNEAKING_SPEED`，默认 0.3，范围 0–1 |
| 潜行碰撞盒 | **0.6 × 1.5**，眼高 **1.27** | `Avatar.POSES[CROUCHING]` |
| 站立碰撞盒 | 0.6 × 1.8，眼高 1.62 | `Avatar.STANDING_DIMENSIONS` |

#### 实测对照

| 档位 | 官方实测 | 我们（逐 tick） | 理论 |
| --- | --- | --- | --- |
| 行走 | 4.307 格/秒 | **4.317** | 0.2159 × 20 = 4.317 |
| 冲刺 | **5.623 格/秒** | **5.614** | 0.2807 × 20 = 5.614 |
| 潜行 | **1.296 格/秒** | **1.296** | 0.0648 × 20 = 1.296 |
| 跳跃顶点 | 1.2492 格 | 1.2522 | 0.42²/… 的迭代极限 |
| 终端下落 | — | 3.92 格/tick | (v−0.08)×0.98 的不动点 |

#### 实现

`sneak` 此前只是输入动作位、实体层没实现。本轮补上：

- `PlayerEntity.Sneaking`：速度 × 0.3（与冲刺叠加时**潜行优先**，乘在冲刺结果上）；
- `Dimensions` 随姿态切换为 `PlayerCrouching`（0.6×1.5，眼高 1.27），相机取 `EyePosition` 自动跟随；
- `FrameInput` 增加 `Sneak` 位，`InputMapper` 由 `InputAction.Sneak`（默认 Shift）填充；
- 窗口路径把 `frame.Sneak` 传进 `Tick`。

#### 实测

窗口与 `walk` 两条路径都在真实服务端跑过：潜行行走 10 秒 **1 次修正**、冲刺 10 秒 1 次修正，均无踢人。

### 7.17 区块更新与重网格化

世界发完之后，方块变化由两个包下发。位布局是这里唯一的风险点——**写错不会报错，只会把方块改到别处**。

#### 包与字段

| 包 | id | 字段 |
| --- | --- | --- |
| `ClientboundBlockUpdate` | 8 | `BlockPos`（打包 long）+ `VarInt` 状态 id |
| `ClientboundSectionBlocksUpdate` | 86 | `SectionPos`（打包 long）+ `VarInt` 条数 + 每条 `VarLong`：`状态 id << 12 \| 局部偏移` |

打包规则（源码常量）：

| 打包 | 位宽 | 偏移 |
| --- | --- | --- |
| `BlockPos` | X 26 / Y 12 / Z 26 | X 38、Z 12、Y 0 |
| `SectionPos` | X 22 / Y 20 / Z 22 | X 42、Z 20、Y 0 |

**最容易写错的一条**：section 内的局部偏移是 **X 在高 4 位**（`sectionRelativeX` 右移 8）、Z 在 4–7 位、Y 在低 4 位。按"X 在低位"的直觉写，同一 section 内所有方块都会落到错误坐标。

实现：`BlockPositionCodec`（打包/解包全部三种）与 `BlockUpdatePackets`（两个包的解析），分别有单元测试钉住，
包括负坐标往返与"X 在高位"这一条。

#### 应用到世界与重网格化

- 解析出的变化直接 `ClientWorld.SetBlockState` 写入；
- 写入会把几何标记为脏；窗口路径在下一帧调用 `FrameCommand.BuildRequest` 重建网格，
  `WindowedRenderer` 负责替换顶点/索引缓冲（**缓冲替换很便宜，网格重建不便宜**）。

#### 实测

- 服务端控制台 `execute at Blue_Coast run setblock ^ ^ ^2 minecraft:glowstone`；
  服务端日志 `Changed the block at -168, 190, -978`；
- 我们的 `walk` 统计到 `blockChanges=2`（收到并解析成功）；
- 窗口运行中在首帧之后出现第二次 `mesh:`，方块数 585,216 → **2,003,969**，即改动触发了重建。

#### 已知不足

目前是**整世界重建**（一次约 2M 方块），只在偶发方块变化时可接受。正式做法应按 section 做脏标记，
只重建受影响的 section 并复用其余顶点区间——已登记为 T-069。

### 7.18 挖方块（`ServerboundPlayerAction`）

验收标准里的"挖掉一个方块"需要这个包。

#### 字段（源码 `ServerboundPlayerActionPacket`）

```
VarInt  action        // 26.3 枚举：0 START_DESTROY_BLOCK, 1 CHANGE_DESTROY_DIRECTION,
                      //           2 ABORT_DESTROY_BLOCK,  3 STOP_DESTROY_BLOCK, 4 DROP_ALL_ITEMS,
                      //           5 DROP_ITEM, 6 RELEASE_USE_ITEM, 7 SWAP_ITEM_WITH_OFFHAND, 8 STAB
BlockPos pos          // 打包 long，与方块更新包同一套规则
byte    direction     // 面：0 下 1 上 2 北 3 南 4 西 5 东
VarInt  sequence      // 单调递增，服务端用于预测排序
```

**注意**：26.3 的枚举顺序与旧版本不同（多了 `CHANGE_DESTROY_DIRECTION`），照旧版写会把
`STOP_DESTROY_BLOCK` 当成 `ABORT`。

#### 与服务端的关系

`handlePlayerAction` 同样受 `hasClientLoaded()` 门控；该函数是
`!waitingForRespawn && clientLoadedTimeoutTimer <= 0`，计数从出生起约 60 tick 后归零，
所以**进服最初约 3 秒内的交互会被忽略**（移动不受影响，因为行走时早已过了这段时间）。

#### 实测

创造模式下由 `MC_WALK_BREAK=1` 每 20 tick 对"前方脚下那格"发 `START_DESTROY_BLOCK`：

```
broke block at (-165,189,-1214) state=9 yaw=-91.6 feet=(-165.5,190.0,-1213.4)
...
walk: sent=239 corrections=2 chunks=367 blockChanges=13
```

`state=9` 说明目标确实是实体方块（不是空气）；`blockChanges=13` 说明服务端接受了破坏，
并把 `ClientboundBlockUpdate` 发回来、我们已写入世界——**"挖掉一个方块并看到地形变化"闭环成立**。

（玩家挖方块时服务端**不会**打印 `Changed the block at ...`，那是 `/setblock` 命令的日志；
判断依据应该是客户端收到并应用的 `blockChanges` 计数。）

### 7.19 按区块增量重网格化（T-069）

§7.17 的第一版每次方块变化都重建**整个世界**（约 200 万方块）。改动频繁时不可接受，本版改为按区块缓存。

#### 结构

- `ClientWorld` 维护脏区块集合：`SetBlockState` 写入后标记所在区块；**若方块位于区块边界
  （局部 x 或 z 为 0/15），同时标记相邻区块**——接缝两侧的面剔除互相依赖，只重建一侧会留下破洞。
- `ChunkMeshCache` 为每个区块保存一份 `MeshData`；`Build` 只重网格化"缓存里没有"的区块
  （即脏区块与新流入的区块），再把缓存拼接成一个整体网格。
- 顶点数据本身很小（贪心合并），所以拼接不是瓶颈，**省下的是全量扫描**。

#### 实测（同一世界，329 个区块）

| 场景 | 重网格化区块 | 耗时 |
| --- | --- | --- |
| 整世界重建（旧） | 329 | **13.8 s** |
| 单方块变化（新） | **1** | **约 50 ms** |
| 单方块变化（跨边界的相邻重建） | 4 | 201 ms |

即单点改动从"秒级"降到"帧级"，约 **270 倍**。

#### 顺带修掉的一个问题

区块流式加载期间缓存不会自动更新，导致第一次方块变化时要一次性补齐 320 个区块（13.8 s）。
现在**每流入一个区块就标记几何脏**，窗口按批增量重建：

```
remesh: 64/73 chunks in 2871 ms
remesh: 80/153 chunks in 3650 ms
...
```

#### 仍然存在

单区块重网格化约 **37–50 ms**（一个区块 16×16×384 ≈ 9.8 万方块的全量扫描）。按区块切分后每次只付一次，
但一帧内重建 80 个新区块仍会卡顿约 3 秒。下一步应按 **section** 再细分、并只重建受影响的 section
——已登记为 T-071。

### 7.20 渲染与模拟分离：后台重网格化

§7.19 的增量重建仍发生在渲染线程上。单个区块重建约 35–50 ms，一帧里做一次就是一次掉帧，流式加载时
一帧要补几十个区块，掉帧非常明显。**重网格化与渲染没有数据依赖，应该分线程**。

#### 结构

| 线程 | 职责 |
| --- | --- |
| 游戏 / 渲染线程 | 接收方块更新、标记脏区块、把脏区块入队；每帧只做"取走已完成的网格 + 拼接顶点" |
| `chunk-mesher` 后台线程 | 阻塞式重建单个区块，结果放进完成队列 |

- 拼接仍是主线程的廉价操作（顶点数很少），**重型扫描完全不在渲染线程**。
- 数据一致性：`ClientWorld.Sync` 由两侧共用——后台在重建一个区块期间持有它，主线程写方块时也要拿它。
  因为写只发生在方块变化时（低频），所以不会拖慢渲染；反过来说，一次方块写入最多等一个区块的重建时间。
- 首帧需要几何：启动时先同步建少量区块（预算 250 ms）再启动后台线程，避免第一帧空场或 NaN 包围盒。

#### 效果

改造后日志里不再出现"每帧 remesh N ms"，掉帧消失（由实际游戏验证）。

### 7.21 疾跑：状态锁存与冲刺跳跃加成

#### 两个真实差异（均已按原版修正）

| 项 | 原版 | 我们（修正前） |
| --- | --- | --- |
| 空中冲量 | **固定 0.02**；疾跑时 0.026（`Player.getFlyingSpeed`） | `MovementSpeed × 0.1` = 0.013（疾跑时） |
| 冲刺跳跃 | 跳起时若在疾跑，沿朝向追加 **0.2** 的水平速度（`jumpFromGround`） | 未实现 |
| 疾跑状态 | **锁存**：触发后只要还在前进就保持，松开疾跑键不取消 | 每 tick 要求按住疾跑键 |

后果：跳跃后推进力不足且缺少冲刺加成，**连跳时水平速度衰减到走路速度以下**——这正是试玩时报告的现象。

#### 修正

- `LivingEntity.AirImpulse`（虚属性）：0.02，`PlayerEntity` 覆写为"疾跑 0.026 / 否则 0.02"；
- 跳跃分支里 `SprintingNow` 时追加 `(sin(yaw)·0.2, 0, −cos(yaw)·0.2)`；
- `PlayerEntity.Sprinting` 改为锁存状态：`forward > 0` 且请求疾跑则置位；`forward <= 0` 或潜行则清除。

#### 测试

`SprintBehaviourTests` 四项：按住一次后松开疾跑键仍保持 0.2807 格/tick、松前进即取消、潜行即取消、
连跳 200 tick 平均 **> 0.26 格/tick**（低于此就说明退化成走路 0.2159）。

#### 尚未覆盖

原版在**水平碰撞**（撞墙）时也会取消疾跑，我们尚未处理。

### 7.22 贴图采样：方块边缘漏出半个像素

试玩时发现方块边缘有半个像素属于相邻贴图，"两边各半个、加起来只有 15 像素"。不是 z 缓冲，是 UV 与采样约定不匹配。

#### 根因

`SpriteAtlas` 把每个 sprite 的纹理坐标记成**纹素中心到纹素中心**：

```
U0 = (x + 0.5) / atlasSize      // 第一列纹素的中心
U1 = (x + width - 0.5) / atlasSize   // 最后一列纹素的中心
```

所以 `U1 - U0 = (width - 1) / atlasSize`：一段 16 像素的贴图只有 **15 个纹素的跨度**。
着色器又把整块面的 `uv`（0→1）线性铺到这段区间上，于是最外侧各半个纹素落到本贴图之外，
采到了**相邻 sprite** 的那半个像素——正好解释"两边各半个、合计 15 像素"。

#### 修正

着色器先由矩形还原纹素数（中心到中心 ⇒ `span × atlasSize + 1`），再**按纹素中心采样**：

```wgsl
let atlasSize = vec2<f32>(textureDimensions(atlasTexture));   // 内建，无需额外 uniform
let spriteTexels = round((rectMax - rectMin) * atlasSize) + vec2<f32>(1.0);
let texel = clamp(floor(fract(uv) * spriteTexels), vec2<f32>(0.0), spriteTexels - vec2<f32>(1.0));
let atlasUv = rectMin + texel / atlasSize;
```

这样整块面正好取满 16 个纹素中心，既不渗色也不拉伸；采样器保持 `Nearest`。

#### 教训

"中心到中心"与"外边界到外边界"是两套约定，混用只差半个纹素——**肉眼就是一条半像素的接缝**。
记录约定、按约定还原纹素数，比在着色器里加 `epsilon` 可靠。

### 7.23 GPU 缓冲的生命周期：不能立即销毁仍在使用的缓冲

修 §7.20 之后窗口会**在第 2 帧崩溃**：

```
panicked at wgpu-hal/src/vulkan/swapchain/native.rs:359
Trying to destroy a SwapchainAcquireSemaphore that is still in use by a SurfaceTexture
```

#### 定位过程（二分）

| 实验 | 结果 |
| --- | --- |
| 关闭几何重建（不换缓冲） | 正常跑满并干净退出 |
| 重建但**不销毁**旧缓冲 | 正常 |
| 重建并立即销毁旧缓冲 | 崩 |

结论：**销毁顶点/索引缓冲**是触发点。提交给 GPU 的帧仍引用着该缓冲，Brew 的 `Dispose` 立即释放原生对象，
wgpu-hal 在 Vulkan swapchain 处报出上述 panic（错误信息指向 swapchain，实际是缓冲被提前释放）。

#### 修正

旧缓冲不再立即释放，而是进入"退役列表"；攒够 8 个后调用
`Queue.OnSubmittedWorkDoneBlocking(instance)` 等 GPU 完成，再统一 `Dispose`；窗口结束时同样先等待再释放。
另外两处相关修正：`Invalidate` **保留旧网格**直到新网格就绪（丢掉会造成空洞，且曾产生空网格导致
`CreateBufferInit` 抛"requires non-empty data"）；渲染器忽略空网格。

#### 教训

有 GPU 异步执行时，"不再需要"不等于"可以立刻释放"。要么等队列空闲，要么用可复用的常驻缓冲 +
`Queue.WriteBuffer`（后续优化方向，见 T-074）。

#### 7.23.1 修正过程中的两次退化（都靠测量抓出来）

**第一次：退役缓冲 + 每 8 次重建同步 GPU。** 崩溃没了，但流式加载时又卡顿——因为每 8 帧就
`OnSubmittedWorkDoneBlocking` 等一次 GPU。改为**常驻缓冲 + `Queue.WriteBuffer` 原地改写**：
稳态下重建只花一次上传，不分配也不释放；只有网格超过容量时才换更大的缓冲（新缓冲同样带
`BufferUsage.CopyDst`，否则 `wgpuQueueWriteBuffer` 会报用途校验错误）。

**第二次：后台线程长时间持锁。** 加锁保护世界数据后，主线程每加入一个区块都要等后台把当前区块
网格化完（约 40 ms），一批 80 个区块就是数秒。改为：

- `ClientWorld` 的区块表用 `ConcurrentDictionary`；
- 对外暴露**不可变快照数组**（写时重建，读时原子替换引用），后台遍历快照不加锁、也不装箱；
- 后台网格化不再持锁。

快照数组同时保住了热路径零分配：早先用 `ICollection` 让枚举器装箱，分配测试直接报出 112 字节。

#### 实测（同一世界，25 秒窗口）

| 版本 | 帧数 | 最坏单帧 |
| --- | --- | --- |
| 后台网格化 + 每 8 次重建同步 GPU | 1862 | **7089.9 ms** |
| 常驻缓冲 + 快照无锁读 | **4443** | **82.2 ms** |

即最坏帧从"秒级卡死"降到"一帧内"，平均帧率约 178 fps。测量入口：`WindowedRenderer` 在退出时打印
`window: <帧数> frames in <秒>s, worst frame <毫秒> ms`。

### 7.24 多线程区块加载：解析与网格化共用一个工作池

§7.20 只把网格化挪出了渲染线程，解析仍在主线程，网格化也只有一个 worker。本轮把两者都放上工作池。

#### 结构

```
主线程                         ChunkWorkerPool（N 个 worker）           主线程
收到 level_chunk_with_light →  解析队列（优先）→ ChunkPacketParser
                              网格队列        → ChunkMesher.MeshChunk
安装解析结果（每帧限量）  ←──────────────────  解析结果队列
上传网格（每帧）          ←──────────────────  网格结果队列
```

- **一个池、两个队列**：解析优先于网格化——解析完的区块才能让世界可用，网格只是让它好看。
- **世界只由主线程写**：worker 只读世界、只产出结果；安装（`AddChunk`）与上传都在主线程。
- **安装限量**：每帧最多安装 8 个已解析区块，避免一批 80 个区块一次性触发长帧。
- **背压**：解析队列上限 64，满了由主线程就地解析（保证不丢数据）；网格队列上限 1024。

#### 线程数自适应

```
WorkerCount = clamp(ProcessorCount - 1, 1, 8)
```

留一个核给游戏线程；下限 1 保证双核机器也能用，上限 8 防止多核机器把线程全投进来。
线程数是构造函数参数（`workerCount`），因此后续做成配置项只需接一个设置来源，不必改调用方。

#### `FindChunk` 改为字典查找

原实现对"区块快照数组"做线性扫描（O(区块数)），900 个区块时每个待网格化区块要扫近千次。
现改为由打包键还原坐标后走 `ClientWorld.GetChunk`（字典），与区块数量无关。

#### 实测（同一世界，25 秒窗口）

| 版本 | worker | 最坏单帧 |
| --- | --- | --- |
| 单 worker 网格化 + 主线程解析 | 1 | 82.2 ms |
| 工作池（解析 + 网格化） | **8**（共 20 逻辑核） | **42.9 ms** |

帧数基本不变（4443 → 4477），但最坏帧再降一半，且卡顿来源从"主线程解析"消失。

#### 后续可做

- worker 数做成配置项（接口已留）；
- 网格化按 section 细分，进一步缩短单次任务；
- 若解析成为瓶颈，可考虑让 worker 直接写"待安装"的区块对象、主线程只做引用挂接。

### 7.25 区块几何竞技场：只上传变化的区块

普通地形掩盖了一个严重的放大效应：§7.19 的实现每次有区块网格就绪，就把**整个世界**重新拼成一个大数组
并整块上传。放大化存档下全量几何从几 MB 涨到约 36 MB，于是每帧付出 36 MB 拷贝 + 36 MB 上传。

#### 实测（放大化存档，20 秒窗口）

| 指标 | 全量重建 | arena |
| --- | --- | --- |
| 帧数 | 422 | **3524**（≈176 fps） |
| 最坏单帧 | 255.0 ms | **88.8 ms** |
| 主线程重建 | 422 次（平均 42.3 ms） | **0 次** |
| 上传量 | **15,286 MB** | **1,040 MB** |
| 相机/模拟 | 0.2 ms 平均 | 0.0 ms 平均 |

**代价正比于"变化量"而非"总几何量"**，这就是 176 倍上传差的来源。

#### 结构

`ChunkGeometryArena`（`Framework/Rendering`）持有两个**常驻大缓冲**，每个区块占一段：

| 项 | 做法 |
| --- | --- |
| 顶点 | 按到达顺序 bump 追加，每个区块记下自己的字节偏移 |
| 索引 | 同样追加，索引**保持 0 基**；绘制时用 `DrawIndexed(..., baseVertex, ...)` 提供该区块的顶点基址 |
| 局部更新 | 尺寸不变时**原地改写**该区间（挖方块走这条路径） |
| 绘制 | 一次 `SetVertexBuffer` + 每区块 `SetIndexBuffer(带偏移)` + `DrawIndexed` |
| 容量不足 | 缓冲区倍增，把所有区块重写一遍（20 秒内 7 次，log 级） |

索引保持 0 基这一点很关键：新区块追加时**不需要改动别人的索引**，也就没有"重打包整个世界"这一步。

#### 架构边界

`Framework` 不能依赖 `Client` 的 `MeshData`，因此竞技场使用中性的 `GeometryData(Vertices, Indices)`，
由启动层把 `MeshData` 适配过去。

#### 已知不足

那 7 次倍增会各自产生一次 ~88 ms 的尖峰。改法是用客户端 view distance 预估区块数、
按平均网格大小预分配容量（已在 §7.25 记录为方向），把重建次数降到 1 次。

### 7.26 关闭窗口时的帧缓冲尺寸

关掉窗口会崩：

```
panicked at src/lib.rs:605:5
Error in wgpuSurfaceConfigure: Validation Error
```

窗口关闭（或最小化）时帧缓冲尺寸会变成 0，而循环仍拿这个尺寸去 `surface.Configure`，wgpu 直接校验失败。

修正：循环每帧先检查 `IsClosing` 与尺寸是否为 0，命中就退出循环，绝不把非法尺寸送进 swapchain 配置。
`WindowHost` 因此增加 `IsClosing`（转发窗口自身的关闭状态）。

实测：定时 20 秒运行与手动关窗都能干净退出（退出码 0、无 panic）。

### 7.27 竞技场容量：按 view distance 预分配

§7.25 的 arena 只会在装不下时倍增。倍增要把**所有已驻留区块重写一遍**，流式加载期间会发生 log2(n) 次，
每次都是一个可见尖峰（实测 20 秒内 7 次，最坏帧 88–235 ms）。

#### 做法

容量决策抽成纯函数 `ArenaSizing.NextCapacity`（可单测，不依赖 GPU），并加两级预分配：

| 阶段 | 策略 |
| --- | --- |
| 启动 | 已知 view distance 时，初始容量直接按 `expectedChunks × 96 KiB` 估；否则每侧 1 MiB |
| 满 16 个区块 | 用实测平均值 × `expectedChunks` × **1.3 松弛** 做一次预分配（只做一次） |
| 之后仍不足 | 退回倍增（保守兜底） |

`expectedChunks` 由客户端的 view distance 推出：`(2×8+1)² = 289`。客户端把 `ViewDistanceChunks`
暴露为常量，启动层用它算出区块数传给渲染器。

单区块大小的初始估计**故意偏大**：多占一点内存只付一次代价，估小了则要重写全部区块——那是帧尖峰。

#### 实测（放大化存档，20 秒）

| 版本 | 整体重建 | 上传量 | 最坏单帧 |
| --- | --- | --- | --- |
| 仅倍增（§7.25） | 7 | 1,278 MB | 88.8 ms |
| + view distance 预分配 | **2** | **588 MB** | **78.1 ms** |

`presized=True` 表示预分配确实触发过。剩下 2 次来自：16 个样本的平均值仍低于真实值，以及后续个别大山区块超出估计。

#### 测试

`ArenaSizingTests` 五项：无 view distance 时仍倍增、有 view distance 时按投影预分配且优于单次倍增、
预分配只做一次、样本不足时仍倍增、**单块超大地形不会被估计值截断**（容量必须容得下传入几何）。
