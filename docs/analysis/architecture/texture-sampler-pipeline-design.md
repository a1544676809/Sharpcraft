<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 纹理对象三段式 + 采样器表 + 管线缓存（P1/P2 落地设计）
summary: 按原版 AbstractTexture / ReloadableTexture / SamplerCache / PipelineCache 的结构与命名落地，解决 UV 越界、图集渗色与管线重复编译
status: 已评审
updated: 2026-11-09
-->

# P1/P2 落地设计：纹理对象三段式 + 采样器表 + 管线缓存

> 依据：`docs/analysis/architecture/vanilla-patterns-gap.md` P1、P2。
> 关联任务：T-784（本设计）、T-783（被本设计解除阻塞）。

## 1. 命名对照（**尽量与原版一致**）

| 原版 | 本仓目标类型 | 说明 |
| --- | --- | --- |
| `AbstractTexture` | `AbstractTexture`（抽象类） | 持有 `Texture` + `TextureView` + `Sampler`，提供 fail-fast getter 与 `Release()` |
| `ReloadableTexture` | `ReloadableTexture`（抽象类，继承前者） | 追加 `ResourceId`、`Apply(TextureContents)`、`LoadContents(ResourceManager)` |
| `TextureContents` | `TextureContents`（记录） | `TextureData Image`、`bool Clamp`、`bool Blur` —— 对应原版 image/clamp/blur 三字段 |
| `SamplerCache` | `SamplerCache` | 键 = (addressModeU, addressModeV, minFilter, magFilter, useMipmaps)，枚举全组合预建 |
| `AddressMode` / `FilterMode` | `AddressMode` / `FilterMode` | 各仅两个值（原版 `SamplerCache` 硬要求 2×2，见 §4 注） |
| `PipelineCache` | `PipelineCache` | 键 = 不可变管线描述符，值 = 编译后的 `Brew.Wgpu.RenderPipeline`；未命中即创建并缓存 |
| `RenderPipeline`（原版是不可变描述符） | `RenderPipelineDescriptor`（记录） | 与 Brew 已编译的 `RenderPipeline` 区分；**不可变、可作字典键** |
| `Dumpable` / `TickableTexture` | 暂不落地 | 正交能力，等有实际需求再加 |

## 2. 为什么这样能解除 T-783

原版 `ReloadableTexture.setSampler`（L31-35）：`contents.clamp() ? CLAMP_TO_EDGE : REPEAT`，`blur() ? LINEAR : NEAREST`。

采样器**属于纹理**，而不是属于"全图集共用的一次性 sampler"。落地后：

- 每张纹理（含将来"独立成纹理"的 special 模型贴图）自带 address mode
- UV 越界由采样器原生 REPEAT 环绕，**几何层不需要任何补偿**（`SIM-SPECIAL-UV-REPEAT` 的复查条件由此满足）
- 图集内部的 sprite 之间仍靠 padding + 单纹理 CLAMP，但"整图集一个 sampler"的硬约束解除

## 3. 最小落地顺序（切片）

| 切片 | 内容 | 验收 |
| --- | --- | --- |
| S1 | `AddressMode`/`FilterMode`/`SamplerCache`：全组合预建、按 `Clamp`/`Blur` 取；替换现有各处散装的 `CreateSampler` | `SamplerCacheTests`：同键返回同一实例；`Clamp` 决定 address mode |
| S2 | `TextureContents` + `AbstractTexture` + `ReloadableTexture`：迁移 `EntitySkinTextureGpu` 为 `ReloadableTexture` 的一个实现，保留其 bind group 责任在调用侧 | `AbstractTextureTests`：未初始化 getter 抛异常；`Release()` 后句柄为空；采样器随 `Clamp` 变化 |
| S3 | `RenderPipelineDescriptor` + `PipelineCache`：先在 1–2 条管线（如 `blit_color`、`present`）试点键化缓存 | `PipelineCacheTests`：同描述符只编译一次；`Clear()` 后重建 |
| S4 | 逐条把剩余的 `CreateRenderPipeline` 调用点迁移到描述符 + 缓存（不改 pass 划分） | 帧时间不退化；架构测试禁止新增裸 `CreateRenderPipeline` |

## 4. 已知刻意的简化与风险

| 编号 | 内容 | 状态 |
| --- | --- | --- |
| SIM-PIPELINE-ASYNC | 原版 `PipelineCache.get` 在后台执行器上编译并 `join()`；本仓 S3 先同步编译 | 保持（复查条件：出现首帧或切包卡顿） |
| — | 原版 `SamplerCache` 要求 `AddressMode`/`FilterMode` 各恰 2 值并在构造时校验；本仓照做，若将来需要 `MIRRORED_REPEAT` 需同步扩展编码 | 记录 |
| — | 原版 `Textures`/`TextureManager` 负责按 `Identifier` 缓存纹理实例；本设计**不含**纹理管理器，仅提供纹理对象与采样器表 | 留待后续任务 |

## 5. 原版源码映射表

| 行为 | 原版位置 | 关键事实 |
| --- | --- | --- |
| 纹理基类状态与生命周期 | `AbstractTexture.java:11-53` | `texture`/`textureView` 可空；默认 sampler = `getSampler(REPEAT, REPEAT, NEAREST, LINEAR, false)`；`close()`→`releaseTextures()`；未初始化 getter 抛 `IllegalStateException` |
| 可重载纹理 | `ReloadableTexture.java:13-47` | `apply(TextureContents)`→`setSampler` + `doLoad`；`setSampler` 由 `clamp()`/`blur()` 决定 address/filter mode；`doLoad` 先 `close()` 再建纹理与视图并上传 |
| 采样器表 | `SamplerCache.java:10-76` | 32 项数组，5 维编码（address×2、filter×2、mipmaps）；默认 REPEAT；`getClampToEdge`/`getRepeat` 便捷方法 |
| 管线缓存 | `PipelineCache.java:13-52` | 以不可变 `RenderPipeline` 为键；未命中则编译并写入；`clear()` 关闭全部；`close()` 同时关闭 shader source |
