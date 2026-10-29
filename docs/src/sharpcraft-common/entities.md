<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
证据：`docs/modules/entities.md`（设计）；`ref://mc-26.3-analysis/src/net/minecraft/world/entity/`
最后更新：2026-10-29

# Common/Entities —— 实体系统根基的实现

`Aabb` / `EntityDimensions` / `Entity` / `LivingEntity` / `PlayerEntity`。这个世界里没有"玩家物理"这种特例，
玩家只是尺寸与属性不同的一具实体。

## 1. 职责范围

负责：包围盒、尺寸与眼高、逐轴碰撞与碰撞标志、上台阶、`Travel` 的摩擦/重力/阻力、玩家的输入映射。

不负责：渲染、输入采集、网络同步、AI、液体与攀爬等特殊移动。

## 2. 对外契约

| 类型 | 关键成员 |
| --- | --- |
| `Aabb` | `Intersects` / `Offset` / `Union` / `Size` |
| `EntityDimensions` | `Player`、`WithDefaultEyeHeight`、`MakeBoundingBox(feet)` |
| `IEntityWorld` | `IsSolid(x,y,z)`、`Friction(x,y,z)`；实体层不认识方块注册表 |
| `Entity` | `Move(Vector3)`、`Position`、`DeltaMovement`、`OnGround` / `HorizontalCollision` / `VerticalCollision`、`StepHeight` |
| `LivingEntity` | `Travel(input, yaw, jumping)`、`ImpulseSpeed(friction)`、`MoveRelative` |
| `PlayerEntity` | `Tick(forward, strafe, jumping, sprinting, yaw)`、`Dimensions`、`MovementSpeed` |

## 3. 关键常量（全部来自分析产物）

| 常量 | 值 |
| --- | --- |
| `BaseGravity` | 0.08 |
| `InputFriction` | 0.98 |
| `BaseJumpPower` | 0.42 |
| `HorizontalDrag` | 0.91 |
| `VerticalDrag` | 0.98 |
| `TerminalVelocity` | 3.92 |
| `DefaultBlockFriction` | 0.6 |
| 玩家尺寸 / 眼高 | 0.6 × 1.8 / 1.62 |
| 玩家速度 / 冲刺 | 0.1 / ×1.3 |

## 4. 三组官方数值同时对上（等效性的证据）

`Input.tick` 里对输入脉冲乘 0.98，这一点是"文档速度"能对上的关键。代入公式：

| 量 | 推导 | 结果 | 官方值 |
| --- | --- | --- | --- |
| 行走 | `0.1 × 0.98 / (1 − 0.6 × 0.91)` | 0.2159 b/t = **4.317 b/s** | 4.317 b/s |
| 冲刺 | `0.1 × 1.3 × 0.98 / (1 − 0.546)` | 0.2806 b/t = **5.612 b/s** | 5.612 b/s |
| 跳跃峰值 | 0.42 起跳、每 tick 先移动再 `(v−0.08)×0.98` | **≈1.252 格** | ≈1.25 格 |

三组独立数值同时吻合，说明公式结构（先移动、后积分；先加输入、后乘阻尼）读对了，而不是凑出来的。

## 5. 冰面：加速慢、滑得远、稳态速度略低

用同一公式算出的结果，与"冰面更快"的直觉相反：

| 地面 | 首 tick 位移 | 松键后 20 tick 滑行 |
| --- | --- | --- |
| 石头（f=0.6） | 较大 | 较短 |
| 冰（f=0.98） | 很小（<50%） | 明显更远（>1.5×） |

原因是阻尼 `f × 0.91` 同时变大：速度系数虽然因 `0.216/f³` 变小，但阻尼更大，稳态位移略低于石头。
玩家感受到的"冰上刹不住"来自滑行距离，不是最高速度。测试按这个事实断言。

## 6. 已修缺陷（回归保留）

| 现象 | 根因 |
| --- | --- |
| 落地悬空 0.055 格 | 碰撞时拒绝整步而非钳制到接触面 |
| 跳跃只有 0.91 格 | 先施加重力再移动（正确顺序相反） |
| 地面速度等于输入（起步过快、松键即停） | 缺少"输入累加 + 阻尼"的加速模型与 0.98 输入摩擦 |

## 6.1 等效性尚未验证（所有者裁决）

所有者 2026-10-29 明确：**等效性要等系统成体系之后再统一验证**。当前状态必须如实理解为：

- 公式与常量来自分析产物（L1 结构等效），三组官方数值能推导出来（L1 数值自洽）；
- 但这 12 项测试是**用同一套公式写成的断言**——它们防回归，不证明与原版一致；
- 因此**不得**对外声称"移动已等效"。

验证协议（成体系后执行，登记为 R-020）：

| 层 | 判据 | 谁做 |
| --- | --- | --- |
| L1 结构 | 分析产物里的顺序与常量 | 已完成 |
| L2 数值 | 原版 F3 实测：行走 4.317 b/s、冲刺 5.612 b/s、跳跃 +1.2522、终端 78.4 b/s、冰面停止距离、起步加速曲线 | 需要一次原版实测记录（所有者） |
| L3 行为对拍 | 把本地 tick 位移发到真实服务器；是否被 `moved too quickly` 拒绝；回包位置与预测偏差 | 输入接线后由本项目执行 |

L3 比自测更硬：原版服务端的移动校验是外部判据，绕不过去。

## 7. 未完成

- 属性修饰符（`Attributes`）与实体类型注册表：目前速度/跳跃是常量。
- 潜行缩小碰撞盒、液体、攀爬、鞘翅、实体挤压。
- 与服务端的权威同步与预测回滚（M5）。
- 移动常量仍需一次**行为对照实验**（实测起跳高度与行走 10 格耗时）后再收紧测试区间。
