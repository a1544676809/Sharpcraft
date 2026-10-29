<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/specs/network/entity-sync.md`（实体包布局）；`ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/`
最后更新：2026-10-29

# Common/Network —— 协议编解码与 play 阶段数据

把线上的字节变成有类型的数据。这一层不认识渲染，也不认识世界，只负责"字段顺序与取值范围"。

## 1. 职责范围

负责：VarInt/VarLong 原语、字符串与 UUID、数据包帧与压缩、登录与会话、区块包解析、实体包解析、
调色板容器与光照段、NBT 的网络根格式。

不负责：连接状态机之外的重试策略、世界语义（方块是否可站立）、渲染、界面。

## 2. 目录内的关键文件

| 文件 | 职责 |
| --- | --- |
| `PayloadReader` / `PayloadWriter` | 定长与变长读取原语；越界一律 `NeedMoreData` |
| `ChunkPacketParser` | `level_chunk_with_light`：段容器、高度图、方块实体、光照 |
| `PalettedContainerCodec` | 调色板容器：`bits` + 调色板 + **无长度前缀**的定长 long 数组 |
| `LightDataCodec` | 四个掩码 + 两组光照数组（每段 2048 字节） |
| `EntityPackets` | `add_entity` / `remove_entities` / 三个移动变体 / `rotate_head` / `set_entity_motion` / `teleport_entity` |
| `PlaySession` / `PlayPackets` / `LivePlaySession`（启动侧） | play 阶段收发 |

## 3. 不变量

- **解码失败必须可区分**：`Success` / `NeedMoreData` / `Invalid`。把"数据不够"当成"格式非法"会让
  合法包被拒；反过来会把错位数据当成功。
- **长度字段先校验再分配**：任何由线上给出长度的数组都要有上限，避免一个坏包申请巨量内存。
- **局部不理解不毁整体**：区块的方块实体载荷走不下去时，保留已解析的地形，只丢该段与其后的光照
  （`docs/specs/network/play-movement.md` §7.28）。
- **角度字节**：一整圈 256 步，`度 = 字节 × 360/256`。
- **增量与定点的约定随版本变化**，不得按旧版习惯填写；26.3 的具体规则见
  `docs/specs/network/entity-sync.md` §4。

## 4. 包 id 的唯一来源

id 以 `GameProtocols` 的注册顺序为准，用已实测的包交叉核对（见 `docs/risks.md` R-021）。
serverbound 可用"发坏包看服务端报错"探针验证；**clientbound 不能**，只能靠顺序推导与行为核对。

## 5. 测试

每类包都有构造字节用例，重点覆盖"版本差异点"（三角度字节、压缩向量、打包属性、分段增量、
`Rot` 用布尔而非打包属性）。门禁与文档漂移检查见 `tools/Sharpcraft.Docs.Indexer`。
