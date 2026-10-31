<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-11-01
授权：用户 2026-10-31 评审通过并"开始实现"。

# 时间系统模块（Time / World Clock）

同步服务端的**世界时间/时钟**，导出日时间(dayTime)与派生的**环境属性**（太阳/月亮/星星角度、天空光、天空色、日出色、月相），供天空天体渲染与日夜光照使用。

## 1. 职责范围

负责：

- 解析 `set_time` 包，维护每 `WorldClock` 的 `ClockNetworkState`；
- 计算 `dayTime = overworld.totalTicks % 24000`（含 partialTick）与游戏时间；
- 由 `Timelines(OVERWORLD_DAY/MOON)` 的**关键帧**求环境属性（角度/颜色/亮度/月相）。

明确不负责：光照/渲染本身（消费本模块输出）、服务端时钟推进。

## 2. 侧别归属与依赖

- 归属：`Common`（时间数据 + 属性求值，纯逻辑、可测）。
- 依赖：`Common.Network`（包解析、`PacketIds`）、`Common.Registries`（WorldClock 标识）。
- 被依赖：`Client`（sky 渲染 / 光照）、未来的模组 API。

## 3. 协议（原版 26.3）

`ClientboundSetTimePacket`：`VarInt packetId` + `long gameTime` + `Map<Holder<WorldClock>, ClockNetworkState>`。

- `ClockNetworkState(long totalTicks /*VAR_LONG*/, float partialTick, float rate)`（`ref://.../world/clock/ClockNetworkState.java`）。
- 地图编码：`VarInt count` + 若干 `(WorldClock, ClockNetworkState)`；`WorldClock` 以注册表 holder 编码（`VarInt` id）。
- 需在 `PacketIds.PlayClientbound` 增加 `SetTime`（**id 待从原版 `GamePacketTypes` 顺序/协议导出核对后填入**，不得凭猜）。

## 4. 客户端时钟模型

- 每时钟存 `{ totalTicks(long), partialTick(float), rate(float) }`；每 tick `totalTicks` 前进（按 rate）。
- 主世界时钟 = `WorldClocks.OVERWORLD`；`dayTime = floor(totalTicks) % 24000`。
- `gameTime` 原样保存（供统计/调试）。

## 5. 环境属性求值（Timelines）

`Timelines` 以**关键帧 + easing** 定义属性随 `overworldClock` 时间变化（`ref://.../Timelines.java`）：

- `SUN_ANGLE`：keyframe 6000→360°、6000→0°（周期 24000，`symmetricCubicBezier(0.362,0.241)` 缓动）；
- `MOON_ANGLE`：6000→540°、6000→180°；`STAR_ANGLE` 同太阳；
- `SKY_LIGHT_FACTOR/COLOR`、`SKY_COLOR`、`SUNRISE_SUNSET_COLOR`、`MOON_PHASE`、`STAR_BRIGHTNESS` 各有自己的关键帧/修饰。
- 求值 = 按 `dayTime` 在关键帧间插值（含 easing）。**必须逐属性核对关键帧与缓动**，不得只取首尾近似当等效。

阶段策略（建议）：
- 一版：精确实现 `SUN/MOON/STAR_ANGLE`（天空天体必需）+ `SKY_LIGHT_FACTOR/COLOR`（日夜光照）；
- 其余（日出/月相/星亮度/天空色修饰）：先按关键帧实现或显式标"未实现"，不臆造。

## 6. 数据/参考

- `ref://mc-26.3-analysis/src/net/minecraft/network/protocol/game/ClientboundSetTimePacket.java`
- `ref://mc-26.3-analysis/src/net/minecraft/world/clock/{ClockNetworkState,WorldClock,WorldClocks}.java`
- `ref://mc-26.3-analysis/src/net/minecraft/Timelines.java`（关键帧/easing）
- `ref://mc-26.3-analysis/src/net/minecraft/util/EasingType.java`（缓动）
- `docs/specs/render/sky-celestial.md`、`docs/specs/render/lighting.md §6`

## 7. 测试与验收

- **包解析**：合成 `set_time` 字节（含多时钟、含/不含 overworld）→ 断言解析出的 `ClockNetworkState` 字段；
- **时钟推进**：给定 rate/partial，tick N 次后 `totalTicks/dayTime` 手算断言；
- **角度求值**：对若干 `dayTime`（0/6000/12000/18000/24000）断言 `sunAngle` 与按原版关键帧+easing 手算一致；
- **边界**：缺 overworld 时钟、负 totalTicks、rate=0（暂停）。

## 8. 风险 / 未解决

- **packet id**：`PlayClientbound.SetTime` 数值需从原版协议核对（错误会错解析后续包）。
- **Timelines easing/多 track 精确度**：`symmetricCubicBezier` 需按原版实现并逐值对照。
- **WorldClock 集合**：服务端可能定义多个时钟；只取 overworld，其余保存备用。
- **与光照/天空的接线顺序**：先本模块(数据) → 天空天体/日夜光照消费。
- 时间系统一旦接入，可解锁 `lighting.md §6` 日夜与 `sky-celestial` 全部天体。

## 9. 落地步骤（评审通过后）

1. `PacketIds.PlayClientbound.SetTime` + `SetTimePacket` 解析 + 单测。
2. `ClientWorld` 增加时钟字段与 `dayTime` 推进；单测。
3. `Timelines` 属性求值（角度优先）+ 单测（逐值对照）。
4. 供 sky/lighting 消费；再回到天空天体渲染与日夜光照。
