<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/network/{Utf8String.java,FriendlyByteBuf.java}`
最后更新：2026-10-28

# UTF-8 字符串与标识符编码

## 1. 编码格式

`VarInt(字节长度)` + UTF-8 字节。注意长度前缀是**字节数**，不是字符数。

## 2. 写入校验（`Utf8String.write`）

1. 字符数 `value.length() > maxLength` → 抛错（默认 `maxLength = 32767`）。
2. 计算 UTF-8 字节数；若 `bytesWritten > utf8MaxBytes(maxLength)`（即 `maxLength * 3`）→ 抛错。
3. 写 `VarInt(bytesWritten)`，再写字节。

## 3. 读取校验（`Utf8String.read`，顺序很重要）

1. 读 `bufferLength`（VarInt 字节数）。
2. `bufferLength > maxLength * 3` → 抛错（注意：这是**按字节**的上限，先于任何解码）。
3. `bufferLength < 0` → 抛错（负数长度）。
4. `bufferLength > 可读字节数` → 抛错（数据不足）。
5. 按 UTF-8 解码 `bufferLength` 字节。
6. 解码后的**字符数** `> maxLength` → 抛错。

这套顺序的意义：先按字节上限拒绝，避免用恶意长度触发大分配；解码后再按字符数校验，两者不可合并。

**注意**：UTF-8 每个 UTF-16 码元最多 3 字节，所以写入侧的"字节 > maxLength×3"永远晚于"字符 > maxLength"触发，属于防御性检查而非可达分支（已由测试确认）。真正能拦住的是长度前缀本身被伪造过大——那只在读取侧。


## 4. 标识符

`Identifier` 的线上形式就是普通字符串（`writeIdentifier` → `writeUtf(32767)`），因此：
- 命名空间 + `:` + 路径，字符集校验在本地进行（见 `docs/analysis/common/identifier.md`）；
- 长度上限沿用 32767 字符（不是字节）。

## 5. 实现要求（C#）

1. 提供 `Write(Span<byte>, string, maxLength)`、`Read(ReadOnlySpan<byte>, maxLength, out string, out consumed)`。
2. 校验顺序必须与 §3 一致；错误类型区分"超长 / 负长度 / 数据不足 / 非法 UTF-8"。
3. 默认上限用常量 `32767`，并允许调用点显式覆盖（例如组件字符串上限 262144）。
4. 不得用 `Encoding.UTF8.GetByteCount` 之后又重复编码；实现应一次编码到目标缓冲。

## 6. 测试要求

- 往返：ASCII、中文、emoji（代理对）、空串。
- 上限：恰好 32767 字符通过；32768 被拒。
- 字节上限：32767 个 3 字节字符（98301 字节）在允许范围内；构造超字节上限的输入被拒。
- 畸形：负长度、长度大于剩余字节、解码后字符数超限。
- **非法 UTF-8 不报错**：原版用平台解码器，畸形序列会被替换成 U+FFFD，因此实现也必须宽松解码（报错反而是行为偏差）。
- 未配对代理项：Java 的 `writeUtf8` 写成 `?`，C# 默认写成 U+FFFD——这是已知偏差，需在接入真实连接前确认是否影响（`[待验证]`）。
- 与 `Identifier` 的互操作：写入 `minecraft:stone` 后读回相等。
