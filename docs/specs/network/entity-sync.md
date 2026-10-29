<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 实体同步
summary: 实体类客户端包 id、26.3 的编码变化、客户端跟踪模型与 M5 第一步范围
status: 草稿
-->

# 实体同步（Entity Sync）

本文件描述 **M5 第一步**：让客户端"知道并且看得到"其他实体。聊天与界面属于 M5 第二步
（`docs/modules/ui.md` 已评审，排在后面）。

结论一律以 26.3 的服务端实现为准；本文只写可观测行为与字节布局，不复制实现。

## 1. 范围

负责：

- 订阅并解析实体类客户端包，维护"实体表"（id → 类型、位置、朝向、速度）
- 客户端插值（实体在网络更新之间平滑移动）
- 把实体表交给渲染层（先占位几何，后玩家/生物模型）

不负责：

- 实体物理（移动由服务端权威，客户端只呈现）；玩家自身的移动已由 `docs/modules/entities.md` 覆盖
- 元数据（皮肤、装备、状态效果）与玩家列表条目——属 M5 后续切片
- 界面与聊天——属 M5 第二步

## 2. 客户端包 id

**推导方法**（与 R-021 一致）：以 `GameProtocols` 的注册顺序为唯一事实源，以已实测的
`LevelChunkWithLight = 46` 为锚点，向上/向下计数。

**局限**：clientbound 包无法用"发一个坏包看服务端报错"的探针验证，那只适用于 serverbound。
因此本表由注册顺序推导，并用四个**已实测验证**的邻位 id 交叉核对
（`LevelChunkWithLight = 46`、`PlayerPosition = 73`、`BlockUpdate = 8`、`SectionBlocksUpdate = 86`）。
一旦某个 id 的行为与预期不符，应优先怀疑本表。

| id | 包 | M5 第一步是否必需 |
| --- | --- | --- |
| 1 | `AddEntity` | 是 |
| 78 | `RemoveEntities` | 是 |
| 54 | `MoveEntityPos` | 是 |
| 55 | `MoveEntityPosRot` | 是 |
| 57 | `MoveEntityRot` | 是 |
| 85 | `RotateHead` | 是 |
| 128 | `TeleportEntity` | 是 |
| 103 | `SetEntityMotion` | 是（占位渲染也需要速度） |
| 101 | `SetEntityData` | 后续（元数据） |
| 71 | `PlayerInfoUpdate` | 后续（皮肤与列表） |
| 70 | `PlayerInfoRemove` | 后续 |
| 34 | `EntityEvent` | 后续（动画、死亡） |
| 109 | `SetPassengers` | 后续（骑乘） |
| 104 | `SetEquipment` | 后续 |

## 3. 26.3 的编码变化（与旧版不同，容易踩）

| 变化 | 说明 |
| --- | --- |
| `AddEntity` 朝向是 **三个字节** | `xRot`、`yRot`、`yHeadRot`（旧版两个；漏读一个会让后续字段整体错位） |
| `AddEntity` 速度改为 **压缩向量** | `Vec3.LP_STREAM_CODEC`（`LpVec3`），不再是三个 short |
| `MoveEntity` 用 **打包属性 + 增量** | `VarInt entityId`、`VarInt packProperties(onGround, stepCount)`、然后 `VecDelta` |
| `VecDelta` 有 **两种形态** | `stepCount == 0` 时是三个 short；`> 0` 时每一步是 `VarInt ticks` + 三个 short |
| `MoveEntityRot` **不用**打包属性 | 它是 `VarInt id`、`Boolean onGround`、`byte yRot`、`byte xRot`（与其他三个子类不同） |
| `TeleportEntity` 携带完整状态 | `VarInt id`、`PositionMoveRotation`（位置 + 速度 + 朝向）、`Relative` 集合、`Boolean onGround` |

### 3.1 逐包布局（已确认部分）

```
AddEntity        VarInt id | UUID(16) | VarInt type | double x,y,z | LpVec3 movement
                 | byte xRot | byte yRot | byte yHeadRot | VarInt data
RemoveEntities   VarInt count | count × VarInt id
MoveEntityPos    VarInt id | VarInt properties | VecDelta
MoveEntityPosRot VarInt id | VarInt properties | VecDelta | byte yRot | byte xRot
MoveEntityRot    VarInt id | Boolean onGround | byte yRot | byte xRot
RotateHead       VarInt id | byte headYaw
SetEntityMotion  VarInt id | LpVec3 movement
TeleportEntity   VarInt id | PositionMoveRotation | Relative bitset | Boolean onGround
```

## 4. 编码细节（已确认）

以下规则取自 26.3 的读/写两端，**旧版习惯在这里会直接算错**。

### 4.1 `VecDelta`：1/4096 定点，0 表示该轴不变

- 单位：`short / 4096` 个方块（`VecDeltaCodec` 的定点常量是 4096）。
- **`0` 不是"位移为零"，而是"该轴沿用基准值"**：解码是
  `absolute = decode(encode(base) + short)`，且当 `short == 0` 时直接取 `base`。
  把 0 当成"未移动"会在"只在一个轴上移动"的包上把另外两轴归零。
- 两种形态由 `stepCount` 决定：
  - `stepCount == 0` → 三个 short（线性增量）；
  - `stepCount > 0` → 每个 step 是 `VarInt ticks` + 三个 short，按刻分段播放。

### 4.2 `packProperties`：低 1 位是落地标志，其余是步数

```
properties = (onGround ? 1 : 0) | (stepCount << 1)
```

读出时 `onGround = (properties & 1) != 0`、`stepCount = properties >>> 1`。

### 4.3 `LpVec3`：15 位分量 + 二次幂缩放，续位在第 2 位

读取：

```
lowest  = u8                       // 0 表示零向量，直接返回
middle  = u8
highest = u32
buffer  = (highest << 16) | (middle << 8) | lowest
scale   = lowest & 3
if ((lowest & 4) != 0)             // ← 续位是 bit 2，不是最高位
    scale |= varint << 2
x = unpack(buffer >> 3)  * scale
y = unpack(buffer >> 18) * scale
z = unpack(buffer >> 33) * scale
unpack(v) = min(v & 32767, 32766) * 2.0 / 32766.0 - 1.0
```

即分量是 `[-1, 1]` 的归一化值，真实值 = 归一化值 × `scale`。

### 4.4 `TeleportEntity`：位置 + 速度 + 朝向 + 相对位集

已确认的顺序：

```
VarInt id
double x, y, z              // 绝对位置
double vx, vy, vz           // 传送后的速度
float  yaw                  // 偏航（度）
float  pitch                // 俯仰（度）
int    relatives            // 4 字节位集：bit0 x, bit1 y, bit2 z, bit3 yaw, bit4 pitch
bool   onGround
```

相对位的含义：置位的分量是**相对当前值**（相加），未置位是**绝对覆盖**。

`MoveEntityPosRot` 的尾部位序也已确认：`id`、`properties`、`VecDelta`、`yRot`、`xRot`。

### 4.5 已知简化

`VecDelta` 的分段形态（`stepCount > 0`）在服务端表示"接下来若干刻逐段位移"。当前实现把各段**求和后一次
应用**，终点位置一致，但缺少逐刻的播放动画。对快速实体（箭、掉落物）会有观感差异，已登记为后续项。

## 5. 客户端跟踪模型

| 结构 | 内容 |
| --- | --- |
| `EntityTracker` | `Dictionary<int, TrackedEntity>`；实体数量上限保护 |
| `TrackedEntity` | 类型 id、位置（当前/上一刻）、朝向、头朝向、速度、`OnGround` |
| 插值 | 与玩家一致：上一刻 → 当前刻按帧 `alpha` 线性插值，朝向走最短弧 |
| 增量应用 | `MoveEntity*` 的增量**累加**到当前位置；`TeleportEntity` 是绝对覆盖 |
| 移除 | `RemoveEntities` 立即移除；区块卸载导致的实体消失由服务端显式下发 |

约束：

- 实体表的写入只在游戏线程；网络解析可以在工作池（与区块走同一条后台管线）
- 插值必须零分配（热路径，原则 3.6）
- 未知实体 id 的增量包直接丢弃并计数（乱序/重连时的正常现象），不得抛异常

## 6. 与渲染的分工

- 第一步只要求"看得到在动"：占位几何（带朝向的盒体或简单人形），颜色区分玩家与生物
- 玩家皮肤与模型属后续切片；渲染层只消费实体表，不反向依赖网络层
- 视锥与距离剔除在渲染侧完成，实体表不参与

## 7. 测试与出口证据

| 层 | 测试 |
| --- | --- |
| 解析 | 每个包的构造字节用例（含"三个字节朝向""VecDelta 两种形态""Rot 用 boolean 不走打包属性"） |
| 跟踪 | 增量累加、绝对覆盖、未知 id 丢弃、移除、上限保护 |
| 插值 | 短弧朝向、跨刻位置连续性、零分配断言 |
| 端到端 | 官方客户端在服务端走动，我们的客户端看到该玩家实体的位置误差在 float 级 |

出口证据（M5 第一步完成判据）：**同服两个客户端，我方能看到对方移动的占位实体，位置误差在 float 级，
且实体表随 `RemoveEntities` 正确清空。**

## 8. 风险

| 风险 | 缓解 |
| --- | --- |
| id 表推导错位 | 已用四个实测 id 交叉核对；行为不符时优先怀疑本表 |
| 编码细节按旧版填写 | 第 4 节列为编码前置条件，未确认不写解析代码 |
| 实体数量与插值开销 | 上限保护 + 零分配插值 + 距离剔除 |
| 元数据与会话状态耦合 | 第一步不碰元数据，`SetEntityData` 仅记录计数 |

## 9. 等效验证（2026-10-29，实测）

协议：一个客户端当**观察者**（记录它跟踪到的实体位置），另一个当**被观察者**。
移动包由原版服务端依据被观察者上报的位置计算后下发，因此这条链路检验的正是最容易算错的部分——
1/4096 定点增量、零轴语义、增量累加与绝对覆盖。

### 9.1 静止：逐位吻合（硬判据）

```
官方客户端自报: (4.5000, 117.0000, 2.5000)
我们跟踪到    : (4.500,  117.000,  2.500)
```

同一次会话中实体 id 不变（4821），说明重连后的 `add_entity` 与绝对定位都被正确应用。
另一组独立测量同样吻合：`(-1.50, 88.00, -1.70)` 对 `(-1.500, 88.000, -1.700)`。

### 9.2 运动：采样落在真实轨迹上

以被观察者 20–40 Hz 的自报位置为参考折线，我们每秒的跟踪采样到该折线的距离：

| 场景 | 偏差 |
| --- | --- |
| 平地行走（10 个采样中的 8 个） | ≤ **0.0005 格** |
| 平地行走（20 Hz 参考下的前 4 个采样） | **0.0001 格** |
| 坠落段个别采样 | 0.2–2.1 格 |

坠落段的偏差不是跟踪误差：坠落速度约 78 格/秒，20 Hz 参考点之间已隔约 4 格，折线把弧线切掉了。

### 9.3 按时间戳对齐时会出现约 0.1 秒的滞后——这是固有的

把两边按墙钟时间对齐后，观察者比被观察者的**自报位置**落后约 0.1 秒（0.86–3.6 格，取决于速度）。
原因不在我们的跟踪：被观察者自报的是它**本地的预测位置**，观察者只能拿到服务端广播的版本，
中间隔着一次往返。原版同样如此。

### 9.4 由此发现的一处行为差异（记入 T-090）

原版对**远端实体**不做"瞬时跳到权威位置"，而是：

1. 把位置编解码的基准推进到该次增量的**终点**（权威位置）；
2. 用 `lerpPositionAndRotationStep`，`stepsToTarget = 3`，即**用 3 刻插值**逼近该目标。

我们目前是瞬时应用。后果：移动中的远端实体我们比原版"更靠前"、不如原版平滑。
权威位置本身正确（§9.1、§9.2 的判据成立），差别在渲染表现，因此归入 T-090 一起做。

### 9.5 顺带确认的行为

- 两个客户端相距约 185 格时观察者收不到对方实体——超出服务端跟踪范围，与原版一致，不是缺陷。
- 生物实体（类型 11）被正常跟踪，位置随时间平滑变化。
- 经官方客户端发出 `/tp` 把被观察者移到观察者身边后，实体位置随之更新。

### 9.6 可重复的执行方式

1. 观察者：`MC_LOGIN_NAME=<离线名> MC_ENTITY_LOG=1` 起窗口客户端。离线服接受任意名字；
   在线服会拒绝与会话不符的名字，因此这个钩子无法用于冒充。
2. 被观察者：官方客户端（或第二个我们的客户端，换一个名字），可用 `/tp` 让它靠近观察者。
3. 取样：静止比对一次；运动时以高频自报位置为参考折线，比较跟踪采样到折线的距离。
   日志带 `at=<毫秒>` 前缀，可按时间戳对齐。

### 9.7 结论

M5 第一步的**跟踪正确性成立**：静止逐位一致，平地运动误差在 1/4096 量化级。
渲染侧还需补 3 刻插值（T-090）才算在观感上与原版一致。
