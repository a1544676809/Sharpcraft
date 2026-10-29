<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]（**迁移中**：本目录的物理将并入 `Common/Entities`，见 `docs/modules/entities.md`）
基线：Minecraft 26.3
证据：`docs/modules/player.md`（设计规格）；AGENTS 原则 9（热路径）
最后更新：2026-10-29

# Client/Player —— 玩家物理实现

`PlayerBody`：一个没有渲染、没有网络依赖的纯物理体，输入是"这一 tick 想怎么走"，输出是位置、速度与是否落地。

## 0. 迁移说明（2026-10-29）

所有者指出：原版中玩家是实体系统的一部分。因此本目录的 `PlayerBody` 是**原型**，其算法将迁入
`Common/Entities`（`Entity` + `LivingEntity` + `PlayerEntity`），客户端只保留输入与相机。
迁移时必须以 `docs/modules/entities.md` §3 的公式为准——原版的移动是"输入累加 + 摩擦阻尼"，
不是本原型里的"地面直接赋值速度"。

## 1. 职责范围

负责：AABB（0.6×1.8，眼高 1.62）、重力与竖直阻力、地面摩擦、跳跃、逐轴碰撞与贴面钳制、高速位移分步。

明确不负责：视角（相机在 `Client` 侧组合）、液体与梯子等特殊移动、与服务端的权威同步（见 `docs/modules/player.md` §7）。

## 2. 对外契约

| 成员 | 说明 |
| --- | --- |
| `Tick(MoveIntent, float yaw)` | 推进一 tick；`yaw` 为弧度，0 指向 −Z |
| `Feet` / `Eye` | 脚位与眼位（相机锚点） |
| `Velocity` | blocks/tick |
| `OnGround` | 落地判定，跳跃与摩擦的前提 |
| `MoveIntent.Normalized()` | 对角输入归一到单位长度 |

依赖注入的 `isSolid(x, y, z)` 让物理体不感知方块注册表，测试可用一个 lambda 描述地形。

## 3. 顺序必须是这个顺序

1. 由输入算期望水平速度（地面直接赋值，空中只补 2% 差值）
2. 跳跃：落地时把竖直速度设为 0.42
3. **先移动**（逐轴：Y → X → Z）
4. 再积分：`vy = (vy − 0.08) × 0.98`，并夹到终端速度
5. 落地后对水平速度乘地面摩擦

第 3 步与第 4 步的顺序有实测依据：反过来做跳跃峰值只有 0.91 格，正确顺序约 1.25 格。

## 4. 碰撞与贴面

- 逐轴处理，某轴被挡只清该轴速度，另外两轴照常。
- 被挡时**钳制到接触面**（向下落到 `floor(y)+1`），而不是丢弃整步——丢弃会让身体悬在 0.05 格上方。
- 单步位移超过 0.5 格时拆分，避免高速穿模；`AHighSpeedFallDoesNotTunnelThroughTheFloor` 守护。

## 5. 已修的缺陷（保留为回归）

| 现象 | 根因 | 回归 |
| --- | --- | --- |
| 落地悬空约 0.055 格 | 碰撞时拒绝整步 | `PlayerBodyTests.TheBodyFallsAndStopsOnTheFloor` |
| 跳跃只有 0.91 格 | 先施加重力再移动 | `PlayerBodyTests.AJumpRaisesTheBodyAboutOneAndAQuarterBlocks` |

## 6. 未完成

- 物理常量仍是 `[待实测确认]`（见 `docs/modules/player.md` §3），测试只断言量级与关系。
- 潜行改变碰撞盒、液体浮力、实体挤压、飞行与摔落伤害。
- 与集成服务端的预测/回滚。
