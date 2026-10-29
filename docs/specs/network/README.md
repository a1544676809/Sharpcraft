<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/network/`
最后更新：2026-10-28

# 网络协议（network）

本目录是**对外契约规格**：客户端必须与 26.3 原版服务端逐字节互通。规格来自对 jar 的核对，不从"经验"推断。

## 文档清单

<!-- BEGIN GENERATED: docs/specs/network -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`encryption.md`](encryption.md) | 服务端哈希、RSA 密钥交换、AES/CFB8 流 | 草稿 | 2026-10-28 |
| [`play-movement.md`](play-movement.md) | 移动包的字段、id 来源与"吸附/插值"的决策规则 | 已评审 | - |
| [`primitives-strings.md`](primitives-strings.md) | UTF-8 字符串、长度前缀与标识符的字节编码 | 草稿 | 2026-10-28 |
| [`primitives-varint.md`](primitives-varint.md) | VarInt / VarLong 编码与三字节帧长度前缀 | 草稿 | 2026-10-28 |
| [`protocol-phases.md`](protocol-phases.md) | 握手 / 状态 / 登录 / 配置 / 游戏五阶段、包 id 表与压缩规则 | 草稿 | 2026-10-28 |

<!-- END GENERATED: docs/specs/network -->

## 已核对的关键常量

| 项 | 值 | 来源 |
| --- | --- | --- |
| 协议版本 | **777** | jar 内 `version.json` |
| VarInt 上限 | 5 字节 | `VarInt.MAX_VARINT_SIZE` |
| VarLong 上限 | 10 字节 | `VarLong.MAX_VARLONG_SIZE` |
| 帧长度前缀上限 | **3 字节**（body < 2^21） | `Varint21LengthFieldPrepender.MAX_VARINT21_BYTES` |
| 默认字符串上限 | 32767 字符 | `FriendlyByteBuf.MAX_STRING_LENGTH` |

## 实现顺序

1. 编码原语（VarInt / VarLong / 字符串 / 帧）——本轮。
2. 阶段状态机与最小握手/状态查询（能拿到 MOTD 即算打通链路）。
3. 登录流程与加密。
4. 配置与游戏阶段的数据包骨架。

## 约束

- 规格与实现冲突时，先改规格并评审，再改实现（原则 12）。
- 每个编码原语必须有"往返 + 边界 + 畸形输入"三类测试。
