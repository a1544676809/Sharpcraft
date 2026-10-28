<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: jar 条目地图
summary: jar 条目分类与资产清单（顶层分布、assets 分类、纹理格式）
status: 草稿
-->

# jar 条目地图（Asset Inventory Source）

本文件回答"26.3 客户端 jar 里有什么、哪些属于资产、提取时该取什么"，用于支撑资产提取器与资源加载层的设计（原则 3、原则 3.4 第 3 项）。

**证据**：`ref://mc-26.3-analysis/entries.csv`（jar 全部条目：路径 / 解压后大小 / 压缩后大小，共 34,227 行）。基线标识与 jar SHA-1 见 `docs/analysis/architecture/overview.md`。

## 1. 顶层分布

| 顶层目录 | 文件数 | 解压大小 | 用途 | 是否提取 |
| --- | --- | --- | --- | --- |
| `assets/` | 11,813 | 5.9 MB | 客户端资源（模型、纹理、语言、着色器、字体） | **是**（按类别筛选） |
| `net/` | 11,267 | 54.6 MB | Java 字节码 | 否（原则 3：不得入库、不得反编译进仓库） |
| `data/` | 10,449 | 9.4 MB | 数据包（战利品表、配方、维度定义等，服务端语义为主） | 否（当前阶段不消费） |
| `com/` | 690 | 2.6 MB | 第三方库（如 Mojang 打包的依赖） | 否 |
| `META-INF/` | 5 | 9.7 MB | 签名与清单（`MOJANGCS.SF` 等） | 否（仅用于校验来源） |
| `version.json` | 1 | <1 KB | 版本元数据 | 可选（用于确认基线） |
| `pack.png` / `flightrecorder-config.jfc` | 2 | <1 KB | 资源包图标 / JFR 配置 | 否 |

> 注意 `net/` 一项占解压总量的大头（54.6 MB），资产只有约 5.9 MB——提取器应当**只解压需要的条目**，不要整包展开。

## 2. `assets/minecraft` 分类（全部 11,811 条命名空间均为 `minecraft`）

| 类别 | 文件数 | 解压大小 | 说明 |
| --- | --- | --- | --- |
| `models/block` + `models/item` | 4,195（models 合计） | 1.1 MB | 方块/物品模型 JSON |
| `items/` | 1,659 | 0.3 MB | 物品定义（1.21.2+ 的 item 定义目录） |
| `textures/block/` | 1,406 | 0.3 MB | 方块纹理 |
| `blockstates/` | 1,289 | 1.4 MB | 方块状态 → 模型映射 |
| `textures/entity/` | 869 | 0.4 MB | 实体纹理 |
| `textures/item/` | 832 | 0.2 MB | 物品纹理 |
| `textures/gui/` | 673 | 0.9 MB | 界面纹理 |
| `textures/particle/` | 302 | <0.1 MB | 粒子纹理 |
| `particles/` | 116 | <0.1 MB | 粒子定义 JSON |
| `shaders/core/` 等 | 93 | 0.1 MB | 客户端着色器（`.vsh` / `.fsh`） |
| `textures/trims/` | 81 | <0.1 MB | 盔甲纹饰纹理 |
| `textures/painting/` | 53 | 0.2 MB | 画纹理 |
| `font/` | 15 | — | 字体定义与位图字体 |
| `lang/` | 3（含目录条目） | — | `en_us.json`、`deprecated.json` |

纹理格式统计（`assets/minecraft/textures/` 下）：**3,964 个 `.png`**，另有 **218 个 `.mcmeta`**（动画与纹理元数据）。`.mcmeta` 必须与同名 `.png` 一起提取，否则动画纹理（水、岩浆、传送门等）会静默退化为静态帧。

## 3. 与渲染的关系

- **可直接用于纹理化的最小集合**：`textures/block/`（1,406）+ 对应 `.mcmeta` + `blockstates/` + `models/block/`。理论最大收益：把当前"状态 id 哈希配色"换成真实方块纹理。
- **着色器来源**：`shaders/` 下的 93 个文件属于原版资产着色器（原则 11 的第 ① 类）。本项目**不直接复用**，只在行为规格层面参考；自研着色器写在代码里（第 ② 类）。
- **`data/` 与 `net/` 一律不提取**：前者是服务端语义数据，后者是字节码，两者都受原则 3 与原则 5 约束。
- **两个来源**（ADR-0005）：jar 内资产打成单文件 pack；音频等走官方资产索引的类别直接只读挂载官方 `objects/`，不复制。

## 4. 索引资产（实测）

用户本机 `E:/games/minecraft/.minecraft/assets` 提供了一手证据（2026-10-28）：

| 项 | 实测值 |
| --- | --- |
| 本机 26.2 版本的 `assetIndex.id` | **32** |
| 索引 SHA-1 | `d6540f46a8fec49e45fb7312cdcf401a8e4e15ff` |
| 索引 `totalSize` | 480,496,538 字节（约 458 MB） |
| 索引条目数 | 5,057 |
| 本机 `objects/` 文件数 | 14,705（多版本共享累积） |

索引里有什么（决定"哪些类别该走索引"）：

| 类别 | 在索引中 | 结论 |
| --- | --- | --- |
| `minecraft/sounds/…`（`.ogg`） | 有 | 音频走索引 |
| `minecraft/sounds.json` | 有 | 音效事件表走索引 |
| `minecraft/lang/<locale>.json` | 有（多语言齐全） | 语言走索引；jar 内只有 `en_us`/`deprecated` 可作兜底 |
| `minecraft/textures/block/…` | 无（0 条） | 纹理只在 jar 内 |

**结论**：jar 内资产（模型、纹理、blockstates、items、font、particles、shaders）打进 pack；音频与语言从官方索引只读挂载。

## 5. 仍未确认的点

1. **基线版本的索引 id**：本机装的是 26.2（索引 32），26.3 的 id 需在拿到 26.3 版本清单后确认；索引 id 与 `version.json` 的 `assetIndex.id` 必须一致，否则拒绝挂载。
2. **`skins/`、`log_configs/`、`virtual/` 与根下的 zip/hex**：属于官方或第三方启动器（PCL）产物，与客户端运行时无关，不读。
3. **签名验证**：jar 带 `MOJANGCS.SF`，当前结论是不必要（SHA-1 已足够判定版本）。
