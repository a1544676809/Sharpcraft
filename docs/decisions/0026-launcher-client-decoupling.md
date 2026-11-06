<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：已接受（2026-11-06，所有者批准范围 A）
基线：Minecraft 26.3
关联：ADR-0007（资源归客户端）、ADR-0009（访问控制只约束启动器）、原则 5 / 7 / 8 / 12 / 13

# 0026 启动器与游戏本体解耦：客户端成为独立可执行程序

## 1. 背景

在本次变更之前，**全部游戏运行时都住在 `Sharpcraft.Launcher`**：`WindowCommand`（开窗 + 游戏循环）、
`InteractiveSession`（输入、相机、UI、聊天、实体表现）、`LivePlaySession`（长连接收发包）、
`FrameCommand`（离屏渲染与资源管线）、`WalkCommand`（无渲染行走验证）、`ConfigurationProbe`（配置阶段）。
`Sharpcraft.Client` 只是一个"交接校验桩"（`Program.Main` 解析参数后打印日志并返回 0），不开窗、不连服、不运行游戏。

这违背原则 8：客户端必须是**独立可执行程序**，可由启动器、脚本或任意第三方启动器直接启动；启动器退出后游戏仍应运行。
原版形态正是如此：启动器（或任意入口）只负责装配参数并 `spawn` 一个**独立的客户端进程**，随后自身可退出。

## 2. 决策

1. **游戏运行时迁入 `Sharpcraft.Client/Game/`**，客户端成为真正的游戏可执行程序。
   - 迁入：`WindowCommand`、`InteractiveSession`、`LivePlaySession`、`ConfigurationProbe`、`FrameCommand`、`WalkCommand`、
     `SpeedOverlay`（宿主速度读数，改为客户端内的显式选项）。
   - 命名空间统一为 `Sharpcraft.Client.Game`。
2. **产品启动路径 = 装配参数 + 启动独立进程 + 启动器退出**。
   `launch` 不再 `WaitForExit` 阻塞；它 `Process.Start` 客户端后立即返回，启动器窗口可关闭，客户端继续运行。
3. **命令行契约对齐原版 `net.minecraft.client.main.Main`**（见 §4）。
4. **边界**：`Sharpcraft.Client` → 仅 `Common` / `Framework`；**MUST NOT** 引用 `Sharpcraft.Launcher` 或 `Sharpcraft.Auth`。
   启动器引用客户端**仅用于开发/诊断工具**（`frame`/`window`/`walk`/`play`），产品路径只按路径 `spawn`。
   客户端本体**完全独立**，即使删掉启动器亦能运行。
5. **调试开关去环境变量化**（原则 13）：游戏程序集不读环境变量。宿主（启动器）解析环境后，以显式 `GameOptions`
   传入；客户端进程则通过 `--debugOptionsFile` 显式接收。未传时默认等价于"调试关闭"。

### 2.1 为什么不是把启动器整个搬空

`assets`（资产提取/图集自检）、`accounts`（离线闸门）、`login`/`logout`、`probe`（状态查询）属于外围宿主层，**留在启动器**。
`probe` 走 `Common` 的状态查询，不触游戏运行时。`frame`/`window`/`walk`/`play` 属于诊断，仍由启动器**经客户端游戏库**运行；
它们不构成产品路径，也不要求启动器常驻。

## 3. 进程模型

```
launch:    [Launcher] --参数装配--> spawn([Client.exe]) --> Launcher 退出
                                     Client.exe: 解析参数 -> 建会话身份 -> 开窗 -> 连服 -> 游戏循环 -> 关窗退出

dev tool:  [Launcher frame/window/walk] （进程内）-> 调用 Sharpcraft.Client.Game.*（仅开发工具可见）
```

- 生产形态：启动器与客户端是两个进程，各自可独立更新/发布；客户端不依赖启动器的存在、GUI 或私有握手。
- 传输：令牌走**会话文件**（`--sessionFile`，仓库外），命令行只放非敏感项；`--accessToken` 亦被接受（原版兼容），
  但默认路径不使用它。
- 退出码：`2` 参数非法、`3` 交接不可用（pack 缺失等）、`0` 正常退出、`1` 运行期错误。

## 4. 命令行契约（`Sharpcraft.Client`）

对齐 `ref://mc-26.3-analysis/src/net/minecraft/client/main/Main.java` 的 vanilla 参数集合：

| 参数 | 语义 | 默认 / 说明 |
| --- | --- | --- |
| `--version <id>` | 版本 id | **必填** |
| `--accessToken <token>` | 访问令牌 | 原版必填但**不校验**；离线可任意/缺省 |
| `--username <name>` | 玩家名 | 缺省 `Player<ms%1000>` |
| `--uuid <uuid>` | 档案 UUID | 缺失/非法时按 `UUIDUtil.createOfflinePlayerUUID = MD5("OfflinePlayer:"+name)` 派生 |
| `--offlineDeveloperMode` | 离线服务、免在线校验 | 布尔开关；对应原版 `UserApiService.OFFLINE` |
| `--quickPlayMultiplayer <address>` | 直接进服务器 | 原版 26.3 已移除 `--server`/`--port` |
| `--gameDir <dir>` | 实例目录 | |
| `--assetsDir <dir>` / `--assetIndex <id>` | 官方资源根 / 索引 id | |
| `--width` / `--height` | 窗口尺寸 | 缺省 960×540 |
| `--versionType <type>` | 版本类型 | 仅记录 |
| `--resourcePackDir <dir>` | 资源包目录 | 映射到资源栈 |
| `--pack <path>` | **本项目扩展**：资产 pack zip | 缺失则交接不可用 |
| `--sessionFile <path>` | **本项目扩展**：会话文件（令牌） | 与 `--username` 二选一 |
| `--debugOptionsFile <path>` | **本项目扩展**：宿主导出的调试选项 JSON | 缺省即"调试关闭" |
| `--frame` / `--window` / `--walk` | **本项目扩展**：诊断模式 | 缺省为正常运行 |

- **未知参数必须像原版 joptsimple 的非选项一样被忽略**，不得崩溃。布尔开关不消费后续值。
- 身份来源：`--sessionFile` 优先；否则用 `--username` / `--uuid` / `--accessToken`。
- 离线（`--offlineDeveloperMode` 或空令牌）→ 离线身份 + `LoginAsync` 的 join 为 no-op；
  在线 → 用 `--accessToken`（或会话文件令牌）走会话服务器 join。

原版依据：
- `Main.java` 定义了上述 `--version/--accessToken/--username/--uuid/--quickPlayMultiplayer/--gameDir/--assetsDir/--assetIndex/--width/--height/--versionType` 解析；未知项被 joptsimple 当作非选项忽略。
- `Minecraft.java` 在构造时若 `--offlineDeveloperMode` 为真则选择 `UserApiService.OFFLINE`，完全跳过在线校验。

## 5. 边界与依赖

- `Sharpcraft.Client`：依赖 `Sharpcraft.Common`、`Sharpcraft.Framework`。**禁止**依赖 `Sharpcraft.Launcher`、`Sharpcraft.Auth`。
  - 在线 join 的 HTTP 交互由客户端内的 `GameConnector` 直接完成（协议层），不引入 `Auth` 依赖。
- `Sharpcraft.Launcher`：保留 `Auth`；**仅**为开发/诊断工具引用 `Sharpcraft.Client`（并通过 `InternalsVisibleTo` 访问游戏库）。
  产品路径只按可执行路径 `spawn`。
- 架构测试新增断言：`Client` 不得引用 `Launcher` / `Auth`；游戏程序集不得读环境变量（既有的原则 13 断言保留）。

## 6. 迁移清单

| 原位置（Launcher） | 新位置（Client） |
| --- | --- |
| `WindowCommand.cs` | `Client/Game/WindowCommand.cs` |
| `InteractiveSession.cs` | `Client/Game/InteractiveSession.cs` |
| `LivePlaySession.cs` | `Client/Game/LivePlaySession.cs` |
| `ConfigurationProbe.cs` | `Client/Game/ConfigurationProbe.cs` |
| `FrameCommand.cs` | `Client/Game/FrameCommand.cs` |
| `WalkCommand.cs` | `Client/Game/WalkCommand.cs` |
| `Diagnostics/SpeedOverlay.cs` | `Client/Game/SpeedOverlay.cs` |

- `AuthenticationFlow` 的调用点（`FrameCommand` / `WindowCommand` / `LivePlaySession`）改为 `GameConnector.LoginAsync(identity)`。
- 环境变量读取点改为 `GameOptions.Get/Flag`；`HostDebugOptions`（启动器）负责从环境收集并 `GameOptions.Configure`。
- 26.3 协议逻辑（登录、配置、play 报文）**保持不变**。

## 7. 风险

- **R-024**：跨程序集搬迁涉及约 5 千行与 130+ 处调试开关，回归面广。缓解：迁移后全量构建 + 全部测试 + 类图/文档门禁 + 直连客户端冒烟；行为不变的开关映射逐条保留。
- **R-025**：登录器与客户端共享 `Sharpcraft.Client` 游戏库（开发工具用），存在"启动器再长回游戏逻辑"的回退风险。缓解：产品路径只 `spawn`；架构测试禁止 `Client → Launcher/Auth`；评审时审查启动器是否新增游戏类型引用。
- **R-026**：离线令牌为空时若误连 online-mode 服务器会被拒。缓解：文档与错误信息明确 `--offlineDeveloperMode` 只用于离线/自备服务器（与原版一致）。
- **R-027**：`GameOptions` 是宿主显式注入、进程内一次配置的只读快照；若未来出现运行期热切换需求，需要改为显式参数而非全局快照（当前无此需求）。
