<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[待评审]
基线：Minecraft 26.3
依据：AGENTS.md 原则 7.1；`docs/decisions/0006-offline-login-gating.md`
最后更新：2026-10-28

# 账号模块（MinecraftCSharp.Launcher.Accounts）

启动器的多账号与离线登录闸门。**这是产品策略模块，不是便利功能**：它决定"谁能启动游戏"，任何削弱都必须由项目所有者书面确认（AGENTS 原则 7.1）。

## 1. 职责范围

负责：

- 维护本机账号列表：类型（正版 / 离线）、档案名、UUID、来源、最近校验时间。
- 判定离线登录是否可用：**当前**列表里是否存在至少一个已通过官方认证的正版账号。
- 产出本地会话：离线账号生成只在本机生效的会话文件，**不产生任何在线令牌**。
- 记录与清理：正版账号登录成功时登记；删除时立即重新评估闸门。
- 持久化与基本防篡改（原则 7.1 的 SHOULD 项）。

明确不负责：

- 在线认证链路本身（`MinecraftCSharp.Auth`）。
- 游戏内玩家档案同步、皮肤（属于客户端/协议）。
- 服务端侧验证（服务器自己决定是否接受离线玩家，我们不干预）。

## 2. 侧别归属

- 外围宿主层（启动器）。MUST NOT 被 `Client`/`Server`/`Common` 引用。
- 客户端只通过会话文件接收结果，不参与闸门判定（原则 8：客户端不得自行降级为离线）。

## 3. 数据模型

```
AccountRecord
  Kind        : Premium | Offline      # 账号类型，不容混用
  Name        : string                 # 档案名（正版来自档案，离线由用户输入）
  Uuid        : string                 # 正版来自档案；离线由派生规则生成
  VerifiedAt  : DateTimeOffset?        # 正版：最近一次官方认证成功时间；离线：null
  CreatedAt   : DateTimeOffset
```

- `Kind = Premium` 只由**官方认证成功**写入，用户不能手工添加（没有"导入正版账号"的入口）。
- `Kind = Offline` 不能携带任何令牌字段；其会话令牌是一个固定哨兵值，客户端与服务端客户端代码 MUST 拒绝用它做在线鉴权。

## 4. 闸门判定（唯一入口）

```
OfflineLoginGate.Evaluate(IReadOnlyList<AccountRecord> accounts) -> GateResult
  Available = accounts.Any(a => a.Kind == Premium && a.VerifiedAt is not null)
  Reason    = Available ? "premium account present" : "no verified premium account"
```

- 判定 MUST 基于**当前**列表实时计算，MUST NOT 缓存一个"已解锁"布尔值（原则 7.1：删除全部正版账号后立即失效）。
- UI / CLI 在不可用时 MUST 隐藏或禁用离线入口，而不是"点了再报错"。
- 无正版账号时，任何形式的离线启动路径都 MUST 拒绝：CLI 子命令、`launch --offline`、环境变量，一律无例外。

## 5. 离线身份派生（与原版一致）

原版在没有 UUID 参数时用 `UUIDUtil.createOfflinePlayerUUID(name)`，其规则是
`nameUUIDFromBytes("OfflinePlayer:" + name, UTF-8)`（MD5 派生的 v3 UUID）。证据：`ref://mc-26.3-analysis/src/net/minecraft/core/UUIDUtil.java` 与 `.../client/main/Main.java`。

- MUST 复现同一规则，否则同一离线名在不同客户端/服务端会得到不同 UUID，玩家数据会对不上。
- 名字校验：长度与字符集按原版档案名规则（3–16，`[A-Za-z0-9_]`）；不合法直接拒绝并给出原因。
- 本地会话：`Username` = 输入名，`Uuid` = 派生值，`AccessToken` = 哨兵（见 §3），`VersionId` = 目标版本。

## 6. 持久化与防篡改（SHOULD 项）

- 位置：`<userData>/MinecraftCSharp/accounts.json`，仓库外，启动时校验路径不得落在仓库内（复用 `HostPaths`）。
- 完整性：整文件用 **HMAC-SHA256** 保护，密钥保存在平台安全存储（Windows DPAPI / 其它平台受限权限文件）；校验失败视为**列表不可信**，直接禁用离线登录并要求重新登录，不做"尽力修复"。
- 原子写：先写 `.tmp` 再重命名，避免半写坏文件。
- 该机制是**本地策略**，不承诺抵抗有内核权限的攻击者（原则 7.1 明确"非 DRM 承诺"）。

## 7. 关键行为

| 场景 | 期望 |
| --- | --- |
| 正版登录成功 | 写入/更新对应 `Premium` 记录，`VerifiedAt` 刷新为当前时间 |
| 用户删除了唯一正版账号 | 下一次判定立即返回不可用；已存在的离线会话文件**不被删除**但**不再生成新会话** |
| 所有正版账号被删除后立刻离线启动 | 拒绝，退出码 4，提示"需要先登录至少一个正版账号" |
| 离线名非法 | 拒绝并说明规则（长度 3–16、字符集），退出码 2 |
| 账号文件缺失 | 视为空列表（离线不可用），不当作错误 |
| 账号文件完整性校验失败 | 视为空列表 + 警告，要求重新登录正版账号 |
| 离线账号尝试在线鉴权 | 哨兵令牌被服务端客户端拒绝；作为缺陷处理，MUST 有测试守住 |
| 同时存在多个正版与离线账号 | 允许切换；闸门只看"是否存在已验证正版账号" |

## 8. 命令契约（与 §3 的命令表衔接）

| 命令 | 行为 | 失败码 |
| --- | --- | --- |
| `accounts` | 列出本机账号：类型、名字、UUID、最近校验时间；离线可用性一行 | 0 |
| `accounts add-offline <name>` | 新增离线账号（受闸门约束） | 4 无正版账号；2 名字非法 |
| `accounts remove <name>` | 删除账号；删完正版后离线入口立即失效 | 2 不存在 |
| `login` | 正版登录并登记账号 | 1 认证失败 |
| `launch ... --offline <name>` | 用离线账号启动（受闸门约束） | 4 / 2 |

## 9. 测试策略（不依赖真实账号）

1. 闸门真值表：空列表 / 只有离线 / 只有未验证正版 / 有已验证正版 / 混合 → 期望 `Available`。
2. 删除最后一个正版账号后，同一进程内再次判定必须为不可用（证明没有缓存布尔值）。
3. 离线 UUID 派生：对若干已知名字断言与 MD5-v3 规则一致，且大小写与首尾空格**不**被静默修正（原样参与派生）。
4. 名字校验边界：2/3/16/17 字符、含空格/中文/连字符。
5. 会话文件不含任何在线令牌字段或被写入哨兵值；断言离线会话无法通过"需要在线令牌"的校验路径。
6. 完整性：篡改 `accounts.json` 任一字节后判定为不可用；HMAC 密钥缺失时同样不可用。
7. 原子写：模拟中断（写临时文件后不重命名）不产生可用账号文件。

## 10. 验收标准

- 有正版账号时 `accounts add-offline` 与 `launch --offline` 可用；删掉全部正版账号后两者**立即**拒绝；
- 离线会话能启动客户端到"交接已验证"（与 T-035 同一条路径），且**没有**任何网络请求携带离线令牌；
- 上述 7 组测试全绿；`accounts.json` 不在仓库内、被 `.gitignore` 覆盖；
- 代码中不存在跳过闸门的开关、隐藏参数或"仅测试"分支（评审时逐条核对）。
