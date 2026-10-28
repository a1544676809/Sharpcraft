<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis/entries.csv`（class 条目统计）
最后更新：2026-10-28

# 包级地图（class 条目计数）

计数口径：jar 内 `*.class` 条目数（含内部类），按路径前缀归组。**这里只放聚合数字与职责判断，完整类名清单不进仓库**，需要时在外部工作区用 `entries.csv` 过滤。

总量：11,383 个 class 条目。以下各表之和略大于该值，因前缀归组有交叠。

## 1. `net.minecraft` 顶层分布

| 包 | 类数 | 职责 |
| --- | --- | --- |
| `net.minecraft.world` | 4,187 | 世界模型：区块、方块、实体、物品、光照、世界生成 |
| `net.minecraft.client` | 2,987 | 客户端：渲染、GUI、输入、音频、多人会话 |
| `net.minecraft.util` | 987 | 工具：datafix、profiling、解析器、线程、路径 |
| `net.minecraft.server` | 698 | 服务端：刻循环、玩家、命令、数据包管理、管理面 |
| `net.minecraft.network` | 630 | 协议、编解码、聊天组件、数据同步 |
| `net.minecraft.core` | 249 | 核心：registry、组件、方向、标识符、粒子 |
| `net.minecraft.commands` | 245 | 命令调度与参数类型 |
| `net.minecraft.data` | 222 | 数据生成器（服务端数据包内容的生产端） |
| `net.minecraft.advancements` | 201 | 进度系统（触发器、判据、展示） |
| `net.minecraft.nbt` | 109 | NBT 读写 |
| `net.minecraft.gametest` | 80 | 游戏内测试框架 |
| `net.minecraft.tags` | 36 | 标签系统 |
| `net.minecraft.resources` | 24 | 资源定位与元数据 |
| `net.minecraft.gizmos` | 20 | 调试绘制（26.3 新增，待验证） |
| `net.minecraft.stats` | 14 | 统计 |
| 其余小包 | ~20 | `realms`、`sounds`、`references`、`recipebook`、`locale` 等 |

## 2. `net.minecraft.client` 子树

| 子树 | 类数 | 备注 |
| --- | --- | --- |
| `client.renderer` | 1,006 | 全部渲染：区块、实体、物品、粒子、UI 批处理 |
| `client.gui` | 794 | 屏幕、控件、组件、对话框界面 |
| `client.model` | 282 | 模型定义（实体/方块等） |
| `client.particle` | 230 | 粒子类型 |
| `client.resources` | 205 | 资源管理器、资源包、重载 |
| `client.multiplayer` | 130 | 连接、会话、客户端世界、聊天 |
| `client`（自身） | 94 | `Minecraft`、`Options`、`Camera`、`DeltaTracker`、`User` 等核心 |
| `client.data` | 47 | 客户端侧数据生成（模型/状态生成） |
| `client.color` | 31 | 物品/方块染色源 |
| `client.sounds` | 30 | 音效实例与混音接入 |
| `client.animation` | 29 | 实体动画定义 |
| `client.telemetry` | 26 | 遥测 |
| `client.main` | 16 | 进程入口与参数装配 |
| `client.input` | 13 | 输入设备抽象 |
| `client.server` | 8 | 集成服务端装配点（很薄） |

结论：客户端体重集中在 **renderer + gui**（1,800 类，占客户端整体约 60%）。这是移植工作量的重心，也是性能目标的主战场。

## 3. `net.minecraft.world` 子树

| 子树 | 类数 | 归属倾向 |
| --- | --- | --- |
| `world.level` | 2,176 | 混合：数据结构共享，刻推进/光照/区块存储偏权威侧 |
| `world.entity` | 1,157 | 混合：实体数据结构 + 服务端 AI/寻路 + 客户端表现 |
| `world.item` | 460 | Common 为主（物品/附魔/组件） |
| `world.inventory` | 114 | 侧别不明（菜单/容器，两侧皆用） |
| `world.attribute` | 60 | 26.3 新增或重构，待验证 |
| `world.phys` | 39 | Common（AABB、向量、射线） |
| `world.scores` | 29 | 偏 Server（记分板）、客户端需要读取 |
| `world.effect` | 24 | Common |
| `world.waypoints` | 24 | 26.3 新增，待验证（定位/路标） |
| `world.ticks` | 20 | 偏 Server（刻调度） |
| `world.clock` | 13 | 26.3 新增，待验证（世界时钟） |
| `world.damagesource` | 13 | Common |
| `world.level.levelgen` | 747 | 偏 Server（生成），客户端需与种子一致 |
| `world.level.block` | 635 | 混合：方块行为大量含权威逻辑 |
| `world.level.storage` | 358 | Server（存档读写），客户端读取用于本地世界 |
| `world.level.chunk` | 93 | 混合 |
| `world.level.lighting` | 25 | 混合（客户端也要重算局部光照） |
| `world.level.redstone` | 16 | Server 权威（红石更新） |

`world.level.*` 与 `world.entity.*` 是边界判定的重点区域，逐项结论进 `docs/architecture/boundaries.md`。

## 4. `net.minecraft.network` / `server` / 其它

| 子树 | 类数 | 备注 |
| --- | --- | --- |
| `network.protocol` | 368 | 分阶段数据包（握手/状态/登录/配置/游戏） |
| `network.chat` | 123 | 文本组件、样式、序列化 |
| `network.codec` | 70 | StreamCodec 等编解码基础 |
| `network.syncher` | 13 | 实体数据同步器 |
| `server.commands` | 180 | 命令实现 |
| `server.packs` | 110 | 数据包/资源包加载与优先级 |
| `server.jsonrpc` | 95 | 26.3 管理面 RPC（与游戏协议分离） |
| `server.level` | 71 | 世界实例、区块调度 |
| `server.network` | 53 | 连接处理、握手、加密 |
| `server.dialog` | 41 | 26.3 对话框系统（服务端驱动 UI） |
| `server.players` | 31 | 玩家列表、权限、白名单 |
| `server.permissions` | 18 | 权限模型 |
| `server.dedicated` | 11 | 专用服务端入口 |

## 5. `com.mojang` 子树

| 包 | 类数 | 职责 |
| --- | --- | --- |
| `com.mojang.renderpearl` | 249 | 新 GPU 抽象层：`api`(73) / `backend`(151) / `frontend`(20) / `util`(4) |
| `com.mojang.realmsclient` | 232 | Realms 客户端（在线服务） |
| `com.mojang.blaze3d` | 147 | 既有渲染设施：`framegraph`、`pipeline`、`buffers`、`vertex`、`font`、`systems`、`platform`、`audio`、`resource` |
| `com.mojang.math` | 18 | 数学与噪声 |

`renderpearl.backend` 的分支为 `opengl` / `vulkan` / `common` / `api` / `util`——这是 26.3 最重要的架构事实之一：原版已是多图形后端设计，`PreferredGraphicsApi` 提供 `default / opengl / vulkan` 三选项。

## 6. 资源与数据布局

| 路径 | 条目 | 内容 |
| --- | --- | --- |
| `assets/minecraft/textures` | 4,371 | 方块/物品/实体/GUI/粒子/地图/装饰 |
| `assets/minecraft/models` | 4,195 | 方块模型 2,884 + 物品模型 1,310 |
| `assets/minecraft/items` | 1,659 | 物品定义（26.3 的物品定义与模型分离） |
| `assets/minecraft/blockstates` | 1,289 | 方块状态到模型映射 |
| `assets/minecraft/particles` | 116 | 粒子定义 |
| `assets/minecraft/shaders` | 93 | `core` 64 / `post` 10 / `include` 18 |
| `assets/minecraft/atlases` | 13 | 图集配置 |
| `assets/minecraft/font` | 9 | 字体定义 |
| `data/minecraft/advancement` | ~1,750+ | 进度（含 recipes 子类） |
| `data/minecraft/loot_table` | 1,202+ | 方块掉落 |
| `data/minecraft/structure` | 2,000+ | 结构模板（村庄、试炼厅、废弃营地等） |
| `data/minecraft/worldgen` | 1,000+ | 生物群系、特征、噪声、模板池 |
| `data/minecraft/recipe` | 280+ | 配方 |
| `data/minecraft/tags` | 700+ | 方块/物品/世界生成标签 |

`assets/.mcassetsroot` 与 `data/.mcassetsroot` 为 0 字节根标记，说明两套内容树都支持"根级"替换。

## 7. 使用方式

- 需要具体类名/文件路径时，在外部工作区执行：
  - 过滤包：`Import-Csv entries.csv | Where-Object Path -like 'net/minecraft/world/level/block/*'`
  - 统计：按 `Split('/')` 前缀分组计数。
- 反编译源码位于 `ref://mc-26.3-analysis/src`，**不得复制进仓库**（原则 3）。
