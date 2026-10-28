<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/network/{ConnectionProtocol.java,protocol/ProtocolInfoBuilder.java,protocol/handshake/*,protocol/status/*,CompressionEncoder.java,CompressionDecoder.java}`
最后更新：2026-10-28

<!-- doc
title: 连接阶段、数据包表与压缩
summary: 握手 / 状态 / 登录 / 配置 / 游戏五阶段、包 id 表与压缩规则
-->

# 连接阶段、数据包表与压缩

## 1. 五个阶段

| 阶段 | 进入方式 | 退出方式 |
| --- | --- | --- |
| `Handshaking` | 客户端首包 `ClientIntention` | 按 intention 切到 `Status` 或 `Login` |
| `Status` | intention=1 | 收到 `PongResponse` 后断开（或直接断开） |
| `Login` | intention=2 | 服务端发 `LoginFinished` 后切到 `Configuration` |
| `Configuration` | 登录完成 | 服务端发 `FinishConfiguration` 后切到 `Play` |
| `Play` | 配置完成 | 断开 |

服务端可以在 `Configuration` 阶段多次往返（例如同步注册表与资源包），客户端必须按通用"收包—处理—回包"循环实现，不能假设一次往返就结束。

## 2. 帧与包体

- 帧 = `VarInt(bodyLength)` + body，前缀上限 3 字节（见 `primitives-varint.md`）。
- body = `VarInt(packetId)` + payload。**包 id 在 body 内，不在帧头**。
- 包 id 由协议注册顺序决定（`ProtocolInfoBuilder.listPackets` 用列表下标作 id），不是手写常量表；因此**改动注册顺序就会改协议**。

## 3. 包 id 表

包 id 由注册顺序决定，已从 jar 导出全表（纯标识符清单，入库为
`tests/Sharpcraft.Common.Tests/TestData/vanilla-packet-ids.txt`）。

| 阶段 | 方向 | 包数 |
| --- | --- | --- |
| Handshaking | C→S | 1 |
| Status | C→S / S→C | 2 / 2 |
| Login | C→S / S→C | 5 / 6 |
| Configuration | C→S / S→C | 10 / 21 |
| Play | C→S / S→C | 69 / 144 |

握手 / 状态 / 登录三个阶段的 id（我们已实现的部分）：

| 方向 | 包 | id |
| --- | --- | --- |
| C→S | `intention` | 0 |
| C→S | `status_request` / `ping_request` | 0 / 1 |
| S→C | `status_response` / `pong_response` | 0 / 1 |
| C→S | `hello`（LoginStart）/ `key` / `custom_query_answer` / `login_acknowledged` / `cookie_response` | 0–4 |
| S→C | `login_disconnect` / `hello`（加密请求）/ `login_finished` / `login_compression` / `custom_query` / `cookie_request` | 0–5 |

注意两处同名不同义：`minecraft:hello` 在 C→S 是 LoginStart、在 S→C 是加密请求；`minecraft:intention` 只存在于握手阶段。方向必须由状态机提供，包名不能自证。

`PacketIdTableTests` 会把我们硬编码的常量与这张表逐条比对，任何漂移都会让测试失败。

## 4. 握手包字段

| 顺序 | 字段 | 编码 |
| --- | --- | --- |
| 1 | protocolVersion | VarInt（26.3 = 777） |
| 2 | hostName | UTF-8 字符串，**上限 255 字符** |
| 3 | port | 无符号短整型（大端 2 字节） |
| 4 | intention | VarInt（1=状态，2=登录） |

注意 hostName 上限是 **255**，不是默认的 32767——许多客户端实现会退化为默认上限，服务端仍会接受，但一旦超过就是协议错误。

## 4.1 地址解析与 SRV

- 地址形如 `host[:port]`；**显式给了端口就不查 SRV**。
- 未给端口时查询 `_minecraft._tcp.<host>` 的 SRV 记录：取 **优先级最小**者，同优先级取**权重最大**者。
- TCP 连接使用 SRV 的 target / port；**握手包里的 host 字段仍发用户输入的地址**（转发型服务端按它匹配，改了就掉线）。
- SRV 不存在或查询失败时回退到 `host:25565`，不得让地址解析变成硬失败。
- 实现依赖 `DnsClient`（见 `docs/decisions/0002-dnsclient-dependency.md`）。

## 5. 压缩

登录阶段服务端可发 `LoginCompression(threshold)`，此后**双向**都进入压缩模式：

| 项 | 值 |
| --- | --- |
| 最大解压后包体 | 8388608（8 MiB） |
| 最大压缩后包体 | 2097152（2 MiB） |
| 发送：body < threshold | 写 `VarInt(0)` + 原始字节 |
| 发送：body ≥ threshold | 写 `VarInt(原始长度)` + zlib 数据 |
| 接收：`VarInt(0)` | 其后为原始字节 |
| 接收：`0 < size < threshold` | 报错（"Badly compressed packet"） |
| 接收：`size > 8 MiB` | 报错 |

`threshold = 0` 表示"全部压缩"（不存在原始的短包路径）。

## 5.1 配置阶段实测（真实服务器）

进入配置阶段后**必须立刻发送 `client_information`**（serverbound id 0），否则服务端会在 `ping` 之后直接断开，且断开原因是二进制组件（NBT），不解释原因。

字段顺序（对应 `ClientInformation`）：

```
VarInt id(0) | string language(≤16) | byte viewDistance | VarInt chatVisibility
| bool chatColors | byte modelCustomisation | VarInt mainHand
| bool textFilteringEnabled | bool allowsListing | VarInt particleStatus
```

默认值可用 `en_us / 2 / FULL(0) / true / 0 / RIGHT(1) / false / false / ALL(0)`。

其余要点：

- **`ping` / `pong` 用 int32**（4 字节大端），不是 VarInt；写错会被当作协议错误断开。
- `keep_alive` 用 long（8 字节大端）。
- 服务端会连续下发 **30+ 个 `registry_data`**，随后 `update_tags`、`finish_configuration`。
- `select_known_packs` 需要回包（可原样回显服务端列表）。
- 回 `finish_configuration` 后收到的第一个包就是 play 阶段的 `minecraft:login`（JoinGame，clientbound id 50）。

## 5.1.1 已知数据包声明（实测踩坑）

`select_known_packs` 的回复决定服务端是否**省略注册表 payload**：

- 客户端声明"我认识 `minecraft:core` 等包" → 服务端认为客户端本地已有同名数据文件，于是 `registry_data` 里的条目**只发 id、不发 NBT**。
- 本项目当前没有内置数据包，因此**必须回复空列表**。第一版代码把服务端的清单原样回显，结果 67 个生物群系、4 个维度类型全部没有 payload，维度高度无从得知（表现为 `dimension type 0 unknown`）。

结论：`select_known_packs` 不是"礼貌性握手"，它直接改变后续数据包的内容。空回复是当前阶段的正确选择。

## 5.2 play 阶段实测（真实服务器，45 秒）

| 观察项 | 值 |
| --- | --- |
| JoinGame（`login`，id 50）字段 | entity id 24617、hardcore=false、levels=`[overworld, the_end, the_nether]`、max=20、view=16、sim=10 |
| keep_alive 间隔 | 45 秒内 **2 次**（约 15–20 秒一次），回复后连接保持 |
| 45 秒内收到的包 | **8787 个**，最频繁的是 id 85（2729）、id 55（2450）、id 103（1844）、id 54（1111） |
| 稳定性 | 持续 45 秒无断开，随后由客户端主动断开 |

要点：

- JoinGame 头部字段顺序：`int playerId` → `bool hardcore` → `VarInt` 维度数 → 每项 Identifier → `VarInt maxPlayers` → `VarInt chunkRadius` → `VarInt simulationDistance` → 三个 bool。**其后的 spawn info 是复杂结构**，本阶段刻意跳过。
- `keep_alive` 是 **long**（8 字节大端），`ping` 是 **int32**；两者都只是回显标识，不要编码成 VarInt。
- play 阶段起始流量巨大（实体/方块更新），客户端必须能"边收边丢"未实现内容，否则会被推理速度拖死。

## 5.3 区块包布局（实测）

`level_chunk_with_light`（clientbound id 46）组成：

```
int chunkX | int chunkZ
map<heightmap type, long[]>        // VarInt 数量，每项 VarInt 类型 + VarInt long 数 + longs
byte[] chunkData                   // 段数据，见下
list<block entity>                 // VarInt 数量，每项 1 字节 XZ + 2 字节 Y + VarInt 类型 + NBT
light data                         // 尚未解析（本轮只保存原始字节的入口预留）
```

`chunkData` 中**每个段**的顺序（`LevelChunkSection.write`）：

```
ushort nonEmptyBlockCount | ushort fluidCount
PalettedContainer blockStates | PalettedContainer biomes
```

两个容器的线上格式：

```
byte bitsPerEntry
bits == 0        → VarInt 单值
bits <= 上限     → VarInt 调色板大小 + 每个 VarInt 条目   （方块状态上限 8，生物群系上限 3）
超过上限          → 直接模式，无调色板（值是全局 id）
long[ExpectedLongs(size,bits)]   // 固定长度，**没有长度前缀**
```

段数不是包里的字段，而由维度类型的 `height / 16` 决定，`min_y / 16` 给最小段索引——所以必须先从 `dimension_type` 注册表拿到高度。

**实测结果**：24 段（minY=-64, height=384）、**9 个区块全部解析成功**、275,141 个非空气方块、**97 个光照数组**（约 198 KB）。

### 5.3.1 三个只有实测才暴露的细节

1. **块实体标签是 optional**：`byte packedXZ | short y | VarInt type | bool hasTag | （可选）NBT`。直接把标志位当 NBT 类型字节会导致整包解析失败；这正是第一次 9 个区块中 1 个失败的根因。
2. **光照掩码不是 long 数组**：`ByteBufCodecs.BIT_SET` 走 `BitSet.toByteArray()`，线上是 **VarInt 字节长度 + 原始字节**，位序为小端（byte 0 的 bit 0 是索引 0）。按"VarInt 数量 + longs"解析会只吃掉 22 字节，剩余 22 KB 被静默丢弃。
3. **段数据与光照都必须"读满"**：一旦有字段错位，剩余字节不会报错，只会被忽略。实现里应当校验 `consumed == body.Length`。

## 6. 实现要求

1. 阶段是**显式状态**，不允许用布尔标志拼装；每次状态切换都要能说明触发包。
2. 包 id 集中在一处常量表中，并注明"来自注册顺序"，禁止散落在各 handler。
3. 压缩开关在收到 `LoginCompression` 后对**双向**生效，且必须在同一状态切换点启用，不能只改发送侧。
4. 解压前必须按上表校验长度，禁止"先解压再检查"（压缩炸弹）。

## 8. 实测记录

| 日期 | 目标 | 结果 |
| --- | --- | --- |
| 2026-10-28 | `mcf.ddger.top`（SRV → `home.loli.fun:60224`） | **protocol=777、version=26.3、players=0/20、RTT=21ms**，MOTD 正常返回 |

这条记录的意义：它证明 `VarInt`/`UTF-8`/帧前缀/握手/状态响应/JSON 解析这一整条链路与**真实原版 26.3 服务端**互通，而不是"自己编码自己解码"的自证。

复现方式：`MC_TEST_SERVER=mcf.ddger.top dotnet test --filter StatusPingIntegrationTests`（`StatusPingIntegrationTests` 在无环境变量时为空操作）。

## 7. 待办

- 按注册顺序导出 `Login` / `Configuration` / `Play` 三个方向的包 id 全表（工具待写，输出为文本表）。
- 加密（`encryption.md`）：登录阶段的 RSA 握手与 AES/CFB8 流加密。
- 状态查询响应 JSON 的字段子集（`version`、`players`、`description`，含 `favicon` 处理）。
