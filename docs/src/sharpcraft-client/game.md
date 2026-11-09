<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/input.md` §6（帧节奏）、`docs/modules/client.md`、ADR-0026
最后更新：2026-11-06

# Client/Game —— 固定步长循环

`TickLoop`：把"帧"和"tick"分开。逻辑以固定 20 Hz 推进，渲染按到达速率出帧，两者用累加器衔接。

## 1. 职责范围

负责：累加时间、按整 tick 触发回调、给出插值因子、限制单帧补跑数量。

不负责：计时来源（调用方给 `TimeSpan`）、渲染、实体本身。

## 2. 对外契约

| 成员 | 说明 |
| --- | --- |
| `TickInterval` | 50 ms（20 Hz），与原版逻辑 tick 一致 |
| `Advance(TimeSpan, Action)` | 跑完整 tick，返回执行数量 |
| `Alpha` | 进入下一 tick 的比例，用于渲染插值 |
| `PendingTicks` | 累加器中未消费的 tick 数 |
| `MaxTicksPerAdvance` | 5，单帧上限 |

## 3. 为什么要上限

卡顿一次（拖动窗口、断点、GC）会积压大量时间。若一帧内全部补跑，这一帧就更慢，积压更多——典型的死亡螺旋。
超过上限的积压被**丢弃**而不是补跑：宁可让世界"跳一下"，也不能让帧时间失控。

## 4. 测试

60 帧 × 16 ms 得到 18–20 次 tick（固定速率）；10 秒的单帧只跑 5 次（上限生效）；25 ms 的 alpha ≈ 0.5。

## 5. 未完成

- 插值尚未用于渲染：相机直接取实体当前眼位，没有在两 tick 之间插值。实体移动较慢，1 tick 的抖动不明显，
  但高速移动（下落、冲刺）会看出来，届时按 `Alpha` 插值。

## 6. 游戏运行时入口（ADR-0026）

本目录在本次解耦后承载客户端的游戏运行时（从启动器迁入）：

| 类型 | 说明 |
| --- | --- |
| `WindowCommand` | 开窗 + 游戏主循环；装配资源/UI/会话 |
| `InteractiveSession` | 固定 20 Hz tick、输入、相机、聊天/标题/动作栏、实体表现 |
| `LivePlaySession` | 长连接收发（收在后台任务、发在调用线程） |
| `ConfigurationProbe` | 登录后配置阶段（keepalive/ping/known-packs/资源包/准则）到 play |
| `FrameCommand` | 离屏渲染与资源管线（供窗口路径复用） |
| `WalkCommand` | 无渲染行走验证 |
| `GameConnector` / `GameIdentity` | 登录入口与身份（在线 join / 离线 no-op join） |
| `GameOptions` | 宿主显式注入的只读调试选项快照（替代环境变量，原则 13） |
| `GamePaths` | 客户端自负的默认目录（不依赖启动器） |

详见 `docs/modules/client.md`。`TickLoop`（上文）是被 `InteractiveSession` 使用的固定步长内核。

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Game.ScreenshotPaths | Mirror | Partial | net.minecraft.client.Screenshot | ref://mc-26.3-analysis/src/net/minecraft/client/Screenshot.java | screenshots directory, timestamped file name and the _N collision suffix |
| Sharpcraft.Client | Sharpcraft.Client.Game.SpeedOverlay | New | Full | sharpcraft.SpeedOverlay | ref://mc-26.3-analysis/src | Sharpcraft host/infra; no reference counterpart |
| Sharpcraft.Client | Sharpcraft.Client.Game.TickLoop | Mirror | Full | net.minecraft.client.Timer | ref://mc-26.3-analysis/src/net/minecraft/client/Timer.java | Fixed-timestep accumulator + partialTick (alpha) |

<!-- END GENERATED: class-map -->
