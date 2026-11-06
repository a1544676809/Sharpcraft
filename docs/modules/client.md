<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-11-06
关联：ADR-0026（启动器与游戏本体解耦）、ADR-0007（资源归客户端）、原则 5 / 7 / 8 / 13

# 客户端模块（Sharpcraft.Client）

客户端是**独立的游戏可执行程序**：它自己开窗、连接服务器、运行游戏循环，不需要启动器常驻（ADR-0026）。
启动器（或脚本 / 任意第三方启动器）只负责装配命令行参数并 `spawn` 它。

## 1. 侧别归属与依赖

- 游戏逻辑 **Client** 侧；依赖 `Sharpcraft.Common`、`Sharpcraft.Assets`（底层资源包/索引 IO）与 `Sharpcraft.Framework`。
- **禁止**依赖 `Sharpcraft.Launcher`（外围宿主）与 `Sharpcraft.Auth`（认证库属宿主外围）。架构测试强制。
- 在线 join 的 HTTP 交互由客户端内的 `GameConnector` 直接完成（协议层），不引入 `Auth` 依赖。
- 资源栈分层（ADR-0027）：内置 jar 包（`--pack`）在下，外部索引只读叠加（`--assetsDir`+`--assetIndex`）在上，
  用户资源包（`--resourcePackDir`）最上；非 `en_us` 语言与音频由索引叠加提供。

## 2. 游戏入口（`Program.Main`）

处理顺序：

1. 空参数 / `--help` → 打印用法；空参数退出码 2，`--help` 退出码 0。
2. `ClientArguments.Parse` 解析（含 vanilla 参数与未知参数容忍）；不可用则打印全部错误 + 用法，退出码 2。
3. `GameOptions.Configure(...)`：把 `--debugOptionsFile`（宿主诊断导出）与 `--pack` / `--resourcePackDir` 显式注入游戏库；
   未传即"调试关闭"（原则 13，游戏不读环境变量）。
4. 构建会话身份 `GameIdentity`（会话文件优先；否则 username/uuid/accessToken；离线则空令牌 + no-op join）。
5. 按模式运行：
   - 默认（无诊断开关）：**开窗 → 连 `--quickPlayMultiplayer`（若给出）→ 游戏循环 → 关窗退出**；无地址则本地/演示世界。
   - `--frame` / `--window` / `--walk`：诊断模式（见 §5）。
6. 退出码：`0` 正常、`1` 运行期错误、`2` 参数非法、`3` 交接不可用（如 pack 缺失）。

在没有启动器的情况下从控制台启动是本模块的一等用例：`--offlineDeveloperMode` + `--quickPlayMultiplayer` 即可进离线服务器。

## 3. 命令行契约

完整的 vanilla 参数表与默认值见 `docs/decisions/0026-launcher-client-decoupling.md` §4。要点：

- `--version` 必填；`--accessToken` 原版必填但不校验；`--username` 缺省 `Player<ms%1000>`；
  `--uuid` 缺失/非法按 `MD5("OfflinePlayer:"+name)` 派生（`Common.Lifecycle.OfflineIdentity`）。
- `--quickPlayMultiplayer` 直接进服（26.3 已移除 `--server/--port`）。
- 本项目扩展：`--pack`（资产 pack）、`--sessionFile`（令牌文件）、`--debugOptionsFile`（宿主诊断导出）、
  `--frame` / `--window` / `--walk`（诊断模式）。
- 未知参数像原版 joptsimple 非选项一样被忽略，不崩溃。

## 4. 运行时组成（`Sharpcraft.Client/Game/`）

| 类型 | 职责 |
| --- | --- |
| `WindowCommand` | 开窗 + 游戏主循环；准备资源/图集/字体/UI；把窗口输入、实体、相机串起来 |
| `InteractiveSession` | 固定 20 Hz tick、相机跟随、聊天/标题/动作栏、实体表现、发送移动包 |
| `LivePlaySession` | 长连接收包（后台）与发包（调用线程）、注册表持有 |
| `ConfigurationProbe` | 走完登录/配置阶段（keepalive/ping/known-packs/资源包/行为准则）到达 play |
| `FrameCommand` | 离屏渲染与资源管线（图集、模型、字体、语言、天空/雾/云、方块实体等）；供窗口路径复用 |
| `WalkCommand` | 无渲染行走验证（对照真实服务器） |
| `GameConnector` / `GameIdentity` | 登录入口：在线 join 会话服务器 / 离线 no-op join |
| `GameOptions` | 宿主显式注入的只读调试选项快照（替代环境变量） |
| `SpeedOverlay` | 调试速度读数（受 `GameOptions` 控制，默认关） |

## 5. 诊断分离（原则 13）

游戏程序集**不读环境变量**。所有调试/诊断开关由宿主（启动器 `Diagnostics/HostDebugOptions`）解析后，
以 `GameOptions` 或 `--debugOptionsFile` 显式传入；默认等价于"调试关闭"。诊断模式（`--frame/--window/--walk`）
与 `SpeedOverlay` 属可整体移除的附属物：删除它们后正常运行路径不受影响。

## 6. 实现与原版类对应

| 本仓实现 | 26.3 原版类 | 说明 |
| --- | --- | --- |
| `Program` | `net.minecraft.client.main.Main` | 解析 vanilla 命令行参数、构建会话、启动游戏；参数表以其为准 |
| `ClientArguments` | `Main` 内的 `OptionParser` / `Options` | vanilla 参数与默认值、未知项容忍 |
| `GameIdentity` / `GameConnector` | `UserApiService` 选择 + `Minecraft` 构造 | `--offlineDeveloperMode` → `UserApiService.OFFLINE`（跳过在线校验）；在线走会话服务器 join |
| `WindowCommand` | `Minecraft`（主循环）+ `Minecraft` 的资源重载 | 开窗、主循环、资源/图集/字体/语言装配 |
| `InteractiveSession` | `Minecraft` 的 tick、`LocalPlayer`、`ChatComponent`、`Gui` | tick 循环、输入、相机、聊天/标题/动作栏、实体表现 |
| `LivePlaySession` | `ClientPacketListener`（连接与收发） | 连接保持、注册表、收发隔离 |
| `ConfigurationProbe` | `ClientPacketListener` 的配置阶段处理 | 配置阶段若干必须应答的包 |
| `FrameCommand` | `LevelRenderer` / `EntityRenderDispatcher` / `ItemRenderer`（离屏复用） | 静态网格化、实体/方块实体、天空/雾/云 |
| `WalkCommand` | 无直接对应（本地验证工具） | 复现 `LocalPlayer` 移动与 `ServerboundMovePlayerPacket` 序列 |

尚未实现/差异：客户端目前把协议与会话逻辑集中在上述类型，原版中分散在 `ClientPacketListener`、`LocalPlayer`、
`LevelRenderer` 等大量类型；本项目按行为对齐逐步拆分，未逐类一一映射。

## 7. 变更记录

- 2026-11-06：客户端从"交接桩"变为独立游戏可执行程序；游戏运行时从启动器迁入（ADR-0026）。
