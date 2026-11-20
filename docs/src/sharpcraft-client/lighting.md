<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Sharpcraft.Client / Lighting

客户端光照计算。目录很小，职责单一：在服务端不发光照数据的前提下，本地把光照算出来。

## 职责

| 类型 | 作用 |
| --- | --- |
| `SkyLightEngine` | 天空光传播。`CheckBlock(x,y,z)` 重算受影响区域并把改动的 chunk 记入 `DirtyChunks` |
| `ChunkLight`（在 `Sharpcraft.Common`） | nibble 存储：按 section 的原始数组，加上按格读写的 `GetSkyLight/SetSkyLight(x,y,z)` |

## 入口与调用方

只有一个生产入口：`ClientWorld.SetBlockState`。写完方块、处理完方块实体后调用 `SkyLight.CheckBlock`，
再把 `DirtyChunks` 逐块 `MarkChunkDirty`。这与原版一致：`LevelChunk.setBlockState` 把变化交给光照引擎
（`ref://mc-26.3-analysis/src/net/minecraft/world/level/chunk/LevelChunk.java:292,301`），
`ClientLevel` 每 tick 跑一次队列（`ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientLevel.java:497`）。
本仓把那次队列直接内联执行。

## 与原版类对应

| 本仓 | 原版 26.3 |
| --- | --- |
| `SkyLightEngine` | `LevelLightEngine` / `SkyLightEngine`（**仅天空光**，且是一次性重算而非增量队列） |
| `ChunkLight` | `DataLayer` / `LayerLightSectionStorage` 的存储职责（按 section 的 nibble 数组） |
| `ClientWorld.SkyLight` + `SetBlockState` 接线 | `ClientChunkCache.lightEngine` + `LevelChunk.setBlockState` 的两处调用 |

未实现、合并或行为差异之处，逐条列在 `docs/modules/render.md` 的「5.5 客户端天空光传播」与
「已知刻意的简化」（`SIM-CLIENT-LIGHT-NO-BLOCK-LIGHT`、`SIM-CLIENT-LIGHT-FULL-RECOMPUTE`）。

## 测试

- `tests/Sharpcraft.Client.Tests/Repro/Repro_269_SkyLightTests.cs`：按原版规则断言数值（开天柱 15、侧向每格 −1、
  石头挡光、放置方块后光照回落）与"改动后 chunk 被标脏"。

<!-- BEGIN GENERATED: class-map -->

| Project | Sharpcraft type | Kind | Status | Reference class | Reference path | Note |
| --- | --- | --- | --- | --- | --- | --- |
| Sharpcraft.Client | Sharpcraft.Client.Lighting.SkyLightEngine | Mirror | Partial | net.minecraft.world.level.lighting.LevelLightEngine | ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/LevelLightEngine.java | Sky light only, naive full recompute instead of the incremental queue |

<!-- END GENERATED: class-map -->
