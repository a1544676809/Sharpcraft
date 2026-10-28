<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[草稿]
基线：Minecraft 26.3（协议 777）
证据：`ref://mc-26.3-analysis/src/net/minecraft/{util/Crypt.java,network/protocol/login/*}`；向量由 `ref://mc-26.3-analysis/vectors/LoginCrypto.java` 产生
最后更新：2026-10-28

<!-- doc
title: 登录阶段的加密
summary: 服务端哈希、RSA 密钥交换、AES/CFB8 流
-->

# 登录阶段的加密

## 1. 报文序列

| 阶段 | 方向 | 内容 |
| --- | --- | --- |
| 1 | C→S | `LoginStart`：玩家名（≤16 字符）+ 玩家 UUID |
| 2 | S→C | `Hello`（Encryption Request）：`serverId`（≤20 字符）、RSA 公钥（DER）、challenge（随机字节）、`shouldAuthenticate` |
| 3 | C→S | `Key`（Encryption Response）：RSA 加密的共享密钥 + RSA 加密的 challenge |
| 4 | 双方 | **从此开始双向流量都走 AES/CFB8** |
| 5 | S→C | `Compression`（可选）→ `LoginFinished` |

## 2. 服务端哈希

```
digest = SHA-1( serverId 的 ISO-8859-1 字节 ‖ 共享密钥 ‖ 公钥 DER )
hash   = BigInteger(digest).toString(16)
```

三个容易错的点：

1. **编码是 ISO-8859-1**，不是 UTF-8。ASCII 服务端 id 下两者一致，非 ASCII 会不同。
2. **`BigInteger(byte[])` 是有符号的**：首字节最高位为 1 时结果为负数，`toString(16)` 会输出 `-` 加**补码绝对值**，而不是原始摘要。
3. 输出是**小写、无前导零**。

实测向量（含负数用例）：

| 输入 | 输出 |
| --- | --- |
| `00` | `5ba93c9db0cff93f52b521d7420e43f6eda2784f` |
| `80` | `-387142c37a5c65a69260a302d472dbf43e463edb` |
| `ff01` | `7dd9b9b6f1fe30c650ee7988643e98436c23da` |

第 2 行就是"负数哈希"：如果实现里直接 `Convert.ToHexString(digest)`，这里会输出完全不同的值，而且只有约一半的连接会触发。

## 3. 密钥交换

- 共享密钥：**16 字节随机**（AES-128）。
- RSA：**1024 位**，`Cipher.getInstance("RSA")` 即 **PKCS#1 v1.5** 填充。
- 客户端要加密两段：共享密钥本体、以及服务端给的 challenge（用于证明持有会话密钥）。

## 4. 流加密

| 项 | 值 |
| --- | --- |
| 算法 | **AES/CFB8/NoPadding** |
| IV | **共享密钥自身** |
| 启用时机 | 发出 `Key` 之后**立即**对双向生效 |
| 状态 | **有状态**，整条连接一条流 |

CFB8 是逐字节反馈：每字节用 16 字节寄存器加密，取首字节作密钥流，再把**密文**字节移入寄存器。因此：

- 不能按包重置，否则从第二个包起全部乱码；
- 加密与解密方向要各自维护寄存器状态（两侧的寄存器内容相同，但移入的字节来源不同）。

## 5. 实现约束

1. 流加密对象**生命周期 = 连接生命周期**，不得在包边界重建。
2. 必须在发出 `Key` 的同一时刻切换收发两条流，不能只切一边（否则服务端解不开后续包）。
3. 服务端哈希必须走"有符号十六进制"格式化，禁止直接 hex 转码。
4. 共享密钥用完即弃，不得写入日志；错误信息中一律脱敏。
5. 压缩在加密**之后**的层次上生效（先压缩后加密），顺序不可颠倒。

## 6. 测试要求

- 哈希：正数、负数、全零、多字节负数向量（已入库）。
- 流加密：与 JVM 参考实现逐字节一致；分片喂入与整体喂入结果相同；空输入不改变状态。
- RSA：公钥 DER 解析 + PKCS#1 填充往返。
- 端到端：登录成功后继续收发至少一个加密包（尚未覆盖，等登录实现完成）。

## 6.1 实测记录（真实服务器，无凭据）

| 日期 | 步骤 | 结果 |
| --- | --- | --- |
| 2026-10-28 | 握手(intention=2) → `LoginStart` → 解析 `Hello` → 发送 RSA 加密的 `Key` | 服务端接受 Key，**加密通道建立** |
| 2026-10-28 | 读取服务端的加密断开包 | 成功解密为 `{"translate":"multiplayer.disconnect.unverified_username"}` |

意义：这一步不需要账号就能验证"加密是否真的对得上"——如果 AES/CFB8 的寄存器或方向有问题，收到的会是一堆乱码而不是可解析的 JSON。

**同时抓到一个真实协议错误**：第一版把 UUID 按"字节数组"写（VarInt 长度前缀 + 16 字节），服务端直接回
`Packet login/serverbound/minecraft:hello ... was larger than I expected, found 1 bytes extra`。
`readUUID()` 是**裸 16 字节**（两个大端 long），没有长度前缀。已修正并加了断言（名字 "abc" 的包体必须是 21 字节）。

## 7. 待办

- 真实账号登录需要凭据：认证流程（设备码 / 授权码 + 回环）选型未定，见 `docs/STATE.md` 与 AGENTS.md 第 5 节。
- `shouldAuthenticate=false`（离线模式服务端）路径：不发送 `joinServer`，但加密流程相同。
