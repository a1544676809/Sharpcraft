<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/specs/network/play-movement.md`（区块与几何）、`docs/specs/network/entity-sync.md`（实体）
最后更新：2026-10-29

# Client/World —— 客户端世界与实体表

客户端持有的世界视图：区块、方块状态、光照，以及"服务器告诉我们存在的实体"。**它不是权威**，
权威在服务端；这里的一切都可以被一个更新包改写。

## 1. 职责范围

负责：区块存储与方块读写、脏区块标记、光照查询、实体表与实体插值、区块/实体的工作池调度。

不负责：物理（服务端权威，玩家自身的移动在 `Common/Entities`）、渲染、协议解析细节。

## 2. 关键类型

| 类型 | 职责 |
| --- | --- |
| `ClientWorld` | 区块表（并发字典 + 不可变快照数组）、`SetBlockState`、脏区块集合、光照查询 |
| `LevelChunk` / `ChunkSection` | 区段存储与调色板容器 |
| `ChunkWorkerPool` | 区块解析与网格化的自适应工作池；世界只由游戏线程写 |
| `ChunkMeshCache` | 每区块一份网格；本地编辑只重建受影响区块 |
| `EntityTracker` | 实体表：增量累加、绝对覆盖、移除、未知 id 计数、零分配插值 |
| `EntityRenderState` | 每帧交给渲染层的最小状态（id、类型、插值位置与朝向） |

## 3. 不变量

- **世界只由游戏线程写**；工作池只读，结果经队列交回游戏线程安装。
- **快照数组**：`GetChunks()` 返回不可变数组，写入时整体替换引用，因此后台遍历不加锁、也不装箱。
- **脏标记是增量的前提**：方块变化只让所在区块变脏，边界方块同时让相邻区块变脏（接缝的面剔除互相关联）。
- **实体表有上限**（`MaxTrackedEntities`）：异常服务端不能让它无限增长。
- **实体增量里 `0` 表示"该轴不变"**，不是"位移为零"（见 `docs/specs/network/entity-sync.md` §4.1）。
- **插值缓冲复用**：每帧插值不得分配（有分配测试守护）。

## 4. 与其他层的关系

- 向上：只消费 `Common/Network` 解析结果与 `Common/World` 的数据结构。
- 向下：渲染层只读实体表与区块网格，不反向依赖网络层。
- 依赖方向由 `Architecture.Tests` 守护。

## 5. 已知简化

- `VecDelta` 的分段形态按求和一次应用（终点一致、缺逐刻播放），见 T-091。
- 实体元数据（皮肤、装备、状态）尚未解析；M5 第一步只要求"看得到在动"。
