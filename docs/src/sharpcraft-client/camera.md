<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/input.md` §5（朝向约定）
最后更新：2026-10-29

# Client/Camera —— 第一人称相机

`CameraController`：维护 yaw/pitch，把鼠标位移变成旋转，并把实体眼位与朝向变成一条视线。

## 1. 对外契约

| 成员 | 说明 |
| --- | --- |
| `Yaw` / `Pitch` | 弧度；yaw 0 指向 −Z，正 yaw 向右转；pitch 正为抬头 |
| `ApplyLook(Vector2 pixels, float degreesPerPixel)` | 灵敏度默认 0.15°/px；pitch 夹在 ±89°；yaw 取模防漂移 |
| `Forward` / `Right` | 单位向量，供移动与射线使用 |
| `Target(eye, distance)` | 视线目标点，10 格前 |
| `SetLook(yaw, pitch)` | 直接设定朝向（会话开始时） |

## 2. 朝向上的一致性（关键）

实体层 `moveRelative` 用同一 yaw 旋转输入，因此"按 W 前进"与"相机看向哪里"必须同向。约定：
yaw 0 看 −Z，正 yaw 向右（+X）。已由测试 `TheCameraForwardMatchesTheDirectionThePlayerWalks` 守护——
这类"相机和移动不同向"的缺陷在游戏里表现为"按 W 却横着走"，靠肉眼很难第一时间定位。

## 3. 未完成

- 视角插值（见 `game.md` §5）。
- 第三人称、潜行降低视点、受伤视角晃动。
- 与实体朝向的双向同步：目前相机单向读实体位置，实体朝向由相机给；将来服务端同步玩家朝向时需反过来接收。
