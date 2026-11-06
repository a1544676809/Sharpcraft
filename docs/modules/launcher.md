<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-11-06
授权：用户 2026-11-06 批准范围 A（启动器与游戏本体解耦，见 ADR-0026）；2026-10-28 授权设备码流程与最小 CLI 验收。

# 启动器模块（Sharpcraft.Launcher）

独立跨平台启动器：认证、版本/资产获取、参数装配、**以独立进程启动客户端**。**产品启动路径 = 装配参数 → spawn 客户端 → 启动器退出**（ADR-0026）。
游戏运行时（开窗、游戏循环、连接、渲染）已迁入 `Sharpcraft.Client/Game/`，不再住在本程序集。

## 1. 本阶段范围

负责：

- 命令行入口与子命令分发（`login` / `probe` / `help`）。
- `login`：驱动 `Sharpcraft.Auth` 完成设备码登录，输出账号档案名与 UUID。
- `probe`：对服务器做状态查询（复用 `Common` 的网络层），用于诊断。
- `assets`：校验 jar 并提取/复用资产 pack（原则 3）。
- `accounts`：多账号列表与离线闸门（原则 7.1）。
- `launch`：校验 → 打包 → 挂载 → 登录 → **装配 vanilla 参数并以独立进程启动客户端 → 启动器返回（不再等待子进程）**。
- 开发/诊断：`frame` / `window` / `walk` / `play`（见 §3.1），仅用于联调，不构成产品路径。

明确不负责：

- 游戏运行时（窗口、游戏循环、连接、渲染）：已迁入 `Sharpcraft.Client`（ADR-0026）。
- 版本清单解析、依赖/资产下载（原则 3、8，待资产管线评审）。

## 2. 侧别归属

- 外围宿主层。可引用 `Auth`、`Framework`、`Common`；`Common`/`Client`/`Server` MUST NOT 引用它（架构测试已强制，ADR-0026 增补）。
- 为使开发/诊断工具可复用客户端游戏库，启动器**仅对这些工具**引用 `Sharpcraft.Client`（`InternalsVisibleTo`）；产品启动路径只按可执行路径 `spawn`，不引用其类型。客户端本体不依赖启动器。

## 3. 命令契约

| 命令 | 行为 | 输出 |
| --- | --- | --- |
| `login` | 设备码流程（无缓存时）→ XBL → XSTS → Minecraft 登录 → 档案 | 指示 URL 与用户码；成功后打印档案名 + UUID；**不打印任何令牌** |
| `logout` | 删除本机令牌缓存 | 删除路径或"无缓存" |
| `play <address>` | 登录 → 连接服务器 → 登录阶段 → 配置阶段 → 进入 play 后断开 | 登录结果（加密/压缩/哈希前缀）与配置阶段的包序列 |
| `probe <address>` | 状态查询（支持 SRV） | 协议版本、版本名、在线人数、延迟、MOTD |
| `help` / 无参数 | 用法 | 用法文本 |
| `assets <jar> [<version-json>]` | 校验 jar 并打包/复用资产 pack | pack 路径、条目数、跳过的顶层目录 |
| `launch <jar> <version-json>` | 校验 → 打包 → 挂载 → 登录 → 装配 vanilla 参数 → **以独立进程启动客户端 → 返回（不等子进程）** | 计划（令牌脱敏）与已启动的客户端进程 id；启动器随后可退出 |
| `accounts` | 列出本机账号与离线可用性 | 类型、名字、UUID、最近校验时间 |
| `accounts add-offline <name>` | 新增离线账号（受闸门约束） | 成功或拒绝原因 |
| `accounts remove <name>` | 删除账号 | 成功或"不存在" |
| `launch ... --offline <name>` | 用离线账号启动（受闸门约束） | 同 `launch` |
| 未知命令 | 报错 | 用法 + 退出码 2 |

### 3.1 开发/诊断命令与游戏运行时

`frame` / `window` / `walk` / `play` 是联调诊断，**不是产品路径**。它们经客户端游戏库
（`Sharpcraft.Client.Game`）在开发工具进程内运行；宿主（启动器）把环境开关解释为显式 `GameOptions`，
再交给游戏库（原则 13）。客户端进程本身不读环境变量。

产品路径 `launch` 只做参数装配与进程启动：组装 `--version / --accessToken / --username / --uuid /
--offlineDeveloperMode / --gameDir / --assetsDir / --assetIndex / --versionType / --pack / --sessionFile`
并 `spawn` 客户端，随后**立即返回，不 `WaitForExit`**。启动器退出后客户端继续运行（ADR-0026）。

## 4. 令牌处理

- 开发阶段**允许本机缓存**（用户 2026-10-28 授权）：默认 `%LOCALAPPDATA%/Sharpcraft/tokens.json`，仓库外、`TokenStore` 拒绝工作树内路径、`.gitignore` 二次屏蔽。
- 令牌不出现在日志与异常中；`play`/`login` 输出只含档案名与 UUID。
- 错误输出只包含分类原因与服务码，不含令牌片段。
- 持久化与安全存储属于后续工作（原则 7），需先有平台实现方案。

## 5. 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功（`launch` 成功启动客户端进程即返回 0） |
| 1 | 认证或网络失败（已打印分类原因），或客户端进程启动失败 |
| 2 | 用法错误 |
| 4 | jar 校验失败 / 离线闸门拒绝 |

> `launch` **不再**回传客户端退出码：两者是独立进程，客户端在启动器退出后自行运行。客户端自身的退出码见
> `docs/modules/client.md`。

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

### 8.1 `launch` 验收记录（2026-10-28）

| 阶段 | 结果 |
| --- | --- |
| 版本清单 | `26.3`，assetIndex `34`，client sha1 `e877b6a0…` |
| jar 校验 | 通过（41,483,720 字节，与清单一致） |
| 资产 pack | 复用已有 pack（11,533 条目 / 5.6 MB / 163 ms） |
| 官方索引挂载 | `official index 34: 5147 objects (read-only mount)` |
| 认证 | 代码侧完成（设备码 / 刷新 / 分类报错）；在线登录能否跑通取决于运行环境的网络可达性，见 R-017 |
| 交接（手工验证） | 用启动器同形参数直接运行客户端：会话文件解析成功、退出码 0；缺 pack 退出码 3；缺参数退出码 2 |

- 客户端退出码与 §5 一致：`2` 参数不合法、`3` 交接不可用、`0` 成功。
- `launch` 的完整链路（含真实认证与进程启动）待网络恢复后再跑一次；**不提供跳过认证的开关**（原则 7.1：不得存在未登录即可启动游戏的路径）。

### 8.2 开发用直接启动客户端（不经过启动器界面）

调试/联调时常需要在**不打开启动器界面**的情况下反复以不同身份进本地 offline-mode 服务器。现在客户端是独立可执行程序，
可直接用 vanilla 形态参数启动，**无需启动器进程**（ADR-0026）：

```powershell
dotnet run --project src/Sharpcraft.Client --no-build -- `
  --version 26.3 --accessToken 0 --username OfflineTester1 `
  --offlineDeveloperMode --quickPlayMultiplayer 127.0.0.1:25565 `
  --gameDir "$env:LOCALAPPDATA\MinecraftCSharp\instances\26.3" `
  --pack "$env:LOCALAPPDATA\MinecraftCSharp\packs\minecraft-26.3-<hash>.pack.zip"
```

- 机制：`--offlineDeveloperMode` 选择离线身份并用 no-op join；`--quickPlayMultiplayer` 直接进服（原版 26.3 已移除 `--server`/`--port`）。
- **online-mode 服务器会拒绝与正版会话不匹配的身份**；offline-mode 服务器按 `OfflinePlayer:<name>` 派生离线 UUID。
- 无服务器时可用本地场景开关（宿主诊断，经 `--debugOptionsFile` 显式传入，例如 `MC_FRAME_LOCAL=1`）。
- 边界（原则 7.1 不变）：直接启动客户端是**开发/第三方启动器形态**，本项目分发的门户仍是启动器；离线身份只产生本地会话。

