<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/network/{VarInt.java,VarLong.java,Varint21LengthFieldPrepender.java,Varint21FrameDecoder.java}`
最后更新：2026-10-28

# VarInt / VarLong 与帧长度前缀

## 1. VarInt（32 位）

- 每字节承载 **7 位数据**，最高位是**续位**（`0x80`）。
- 写入：`while ((value & ~0x7F) != 0) { byte((value & 0x7F) | 0x80); value >>>= 7; }` 最后写 `byte(value)`。
- 读取：逐字节 `out |= (byte & 0x7F) << (7 * i)`，遇到续位为 0 停止。
- 上限 **5 字节**；超过抛错（原版消息 `VarInt too big`）。
- **负数固定 5 字节**（因为 `>>>` 是无符号右移，负值高位全 1，需要 5 字节才收敛）。
- `getByteSize` 用于预计算长度（帧头需要）。

## 2. VarLong（64 位）

同上，但上限 **10 字节**；负数固定 10 字节。

## 3. 帧长度前缀

- 每帧 = **VarInt 长度前缀** + 该长度的 body。
- 长度前缀本身**不得超过 3 字节**（`MAX_VARINT21_BYTES = 3`），即 body 长度必须 < 2^21（2,097,152）。
- 超限抛错；原版异常文本写的是 "is over 8"（历史遗留，实际限制是 3 字节 / 2^21）。
- 解码侧（`Varint21FrameDecoder`）先读 VarInt 长度，再校验剩余可读字节是否足够，不足即断开。

## 4. 实现要求（C#）

1. 提供 `Write(Span<byte>, value) -> bytesWritten`、`Read(ReadOnlySpan<byte>, out value, out consumed)`、`TryRead(...)`、`GetByteSize(value)`。
2. 读取必须**拒绝超长编码**（第 6 个续位字节即失败），不能"读多少算多少"。
3. 解码必须支持从**不完整缓冲**中判定"还需要更多字节"，不得把截断当成错误。
4. 帧层单独一层：`PacketFramer` 负责长度前缀的读写与 3 字节上限校验，不混进包体编解码。

## 5. 测试要求

- 往返：`{0, 1, 127, 128, 255, 16383, 16384, 2097151, int.MaxValue, -1, int.MinValue}`。
- 边界：`GetByteSize` 与实际写出字节数一致。
- 畸形：6 字节续位序列必须报错；长度前缀超 3 字节必须报错。
- 截断：只给出前半段时必须返回"需要更多数据"而不是抛异常。
