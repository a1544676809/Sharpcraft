<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/network/{ConnectionProtocol.java,protocol/ProtocolInfoBuilder.java,protocol/handshake/*,protocol/status/*,CompressionEncoder.java,CompressionDecoder.java}`
最后更新：2026-10-28

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

## 3. 已核对的包 id（握手 / 状态）

| 方向 | 包 | id |
| --- | --- | --- |
| C→S | `ClientIntention` | 0 |
| C→S | `StatusRequest` | 0 |
| C→S | `PingRequest` | 1 |
| S→C | `StatusResponse` | 0 |
| S→C | `PongResponse` | 1 |

`Login` / `Configuration` / `Play` 的表**尚未整理**（数量大，需按注册顺序导出，见 §7）。

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
