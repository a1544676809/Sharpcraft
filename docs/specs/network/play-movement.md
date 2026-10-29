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
