<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/input.md`（设计规格）
最后更新：2026-10-29

# Client/Input —— 动作映射与帧快照

把平台原始输入（键码、鼠标位移）变成客户端能消费的 `FrameInput`。纯逻辑，不需要 GPU 或窗口。

## 1. 职责范围

负责：动作枚举、键位表、把 `WindowInputState` 映射为 `FrameInput`、鼠标位移的"每帧只消费一次"。

不负责：采集（在 `Framework.Platform`）、实体推进（在 `Common.Entities`）、相机数学（下一任务）。

## 2. 对外契约

| 类型 | 说明 |
| --- | --- |
| `InputAction` | 前进/后退/左/右/跳跃/冲刺/潜行/释放鼠标 |
| `KeyBindings` | 动作 → `InputKey`；不可变，`With` 返回新表；`Default` 为原版布局（WASD / Space / Ctrl / Shift / Esc） |
| `FrameInput` | `readonly record struct`：`Forward`、`Strafe`、`Jump`、`Sprint`、`ReleaseCursor`、`LookDelta` |
| `InputMapper` | `Map(WindowInputState)`；`LookSensitivity`（默认 0.15°/px） |

## 3. 语义

- 相反方向互相抵消（W+S 同时按 → 0），归一化留给实体层的 `moveRelative`（原版就在那里做）。
- `LookDelta` 每帧**只取一次**：`TakeMouseDelta` 取完即清，避免一帧内被两个消费者重复使用。
- 键位表不可变：读取无锁，将来做绑定 UI 时整体替换，不会出现"半个表改了"的中间态。

## 4. 平台层的分工

- `WindowHost` 用 GLFW 回调采集（`Silk.NET.Windowing` 2.x 没有键鼠事件面，见 `docs/modules/input.md` §3），
  并把后端键码翻译成平台无关的 `InputKey`；游戏代码永远看不到 GLFW 常量。
- `Reset()`：焦点丢失或释放鼠标捕获时清空按键与累计位移，避免"切出去再回来一直往前走"。

## 5. 测试（7 项）

前进/横移、相反方向抵消、跳跃/冲刺/释放绑定、松键即停、鼠标位移只消费一次、失焦清空、重绑定生效。

## 6. 未完成

- `Shift` 潜行目前只是动作位，实体层尚未缩小碰撞盒。
- 手柄、触屏、可重绑定 UI、绑定持久化。
