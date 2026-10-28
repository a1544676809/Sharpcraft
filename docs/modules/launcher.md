<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-10-28
授权：用户 2026-10-28 要求"优先走设备码流程，可以继续做"，并同意用最小 CLI 做人工验收。

# 启动器模块（MinecraftCSharp.Launcher）

独立跨平台启动器：认证、版本/资产获取、参数装配、进程启动。**本阶段只实现"认证验收切片"**，其余待决策项就绪后再做。

## 1. 本阶段范围

负责：

- 命令行入口与子命令分发（`login` / `probe` / `help`）。
- `login`：驱动 `MinecraftCSharp.Auth` 完成设备码登录，输出账号档案名与 UUID。
- `probe`：对服务器做状态查询（复用 `Common` 的网络层），用于诊断。

明确不负责（本阶段）：

- 版本清单解析、依赖/资产下载与提取（原则 3、8，待资产管线评审）。
- 游戏进程启动与参数装配。
- 多账号管理与 UI（原则 7.1）。
- 离线登录解锁策略（原则 7.1，属于产品策略，须用户书面确认后实现）。

## 2. 侧别归属

- 外围宿主层。可引用 `Auth`、`Framework`、`Common`；`Common`/`Client`/`Server` MUST NOT 引用它（架构测试已强制）。

## 3. 命令契约

| 命令 | 行为 | 输出 |
| --- | --- | --- |
| `login` | 设备码流程（无缓存时）→ XBL → XSTS → Minecraft 登录 → 档案 | 指示 URL 与用户码；成功后打印档案名 + UUID；**不打印任何令牌** |
| `logout` | 删除本机令牌缓存 | 删除路径或"无缓存" |
| `play <address>` | 登录 → 连接服务器 → 登录阶段 → 配置阶段 → 进入 play 后断开 | 登录结果（加密/压缩/哈希前缀）与配置阶段的包序列 |
| `probe <address>` | 状态查询（支持 SRV） | 协议版本、版本名、在线人数、延迟、MOTD |
| `help` / 无参数 | 用法 | 用法文本 |
| 未知命令 | 报错 | 用法 + 退出码 2 |

## 4. 令牌处理

- 开发阶段**允许本机缓存**（用户 2026-10-28 授权）：默认 `%LOCALAPPDATA%/MinecraftCSharp/tokens.json`，仓库外、`TokenStore` 拒绝工作树内路径、`.gitignore` 二次屏蔽。
- 令牌不出现在日志与异常中；`play`/`login` 输出只含档案名与 UUID。
- 错误输出只包含分类原因与服务码，不含令牌片段。
- 持久化与安全存储属于后续工作（原则 7），需先有平台实现方案。

## 5. 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 认证或网络失败（已打印分类原因） |
| 2 | 用法错误 |

## 6. 测试要求

- 命令行解析必须单测（命令、参数缺失、未知命令）。
- `login` 真实流程不进入自动化测试（需要人工输码），属于手工验收项。
- `probe` 的网络调用依赖外部服务器，自动化测试用环境变量门控（与 `StatusPingIntegrationTests` 一致）。

## 7. 待办

- 版本/资产管线与启动编排（需要先评审资产提取规格）。
- 多账号与离线解锁策略（产品决策，需书面确认）。
- 令牌安全存储的平台实现。

## 8. 进服验收记录

| 日期 | 结果 |
| --- | --- |
| 2026-10-28 | **成功**：缓存令牌 → 登录（`encrypted=true`、`compression=256`）→ joinServer → 配置阶段 32 个 `registry_data` → `update_tags` → `finish_configuration` → 进入 play，首个 play 包为 `minecraft:login`（JoinGame），随后按计划断开 |

同一个服务器上的两次失败也值得记录：漏发 `client_information` 会在 `ping` 之后被断开；`ping`/`pong` 用 **int32** 而非 VarInt，写错同样被断开（服务端只给一个二进制断开原因，不会解释）。
