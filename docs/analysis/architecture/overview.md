<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`ref://mc-26.3-analysis`（entries.csv、src 反编译工作区、logs）
最后更新：2026-10-28

# 26.3 jar 架构总览（第一轮：结构摸底）

本轮目标只有一个：**搞清楚 Minecraft 到底是个什么架构**，不涉及任何实现决策。结论均为结构级（包、层、入口、数据布局），行为细节留待后续各子系统文档。

## 0. 基线标识（可追溯）

| 项 | 值 |
| --- | --- |
| 版本 | 26.3（release） |
| 客户端 jar SHA-1 | `e877b6a07acd633fb3bb475002175cec036e7b87` |
| jar 大小 | 41,483,720 字节 |
| 构建时间 | 2026-09-15T11:20:48Z |
| 协议版本 | 777 |
| 资源包 / 数据包版本 | 97.1 / 121.0 |
| world_version | 5023 |
| Java | 25（java-runtime-epsilon） |
| 主类 | `net.minecraft.client.main.Main` |
| 资产索引 | 34（piston-meta 分发） |
| 签名 | jar 内含 MOJANGCS.SF / MOJANGCS.RSA，为官方签名包 |

jar 内 `version.json` 与启动器元数据 `26.3.json` 的 SHA-1 一致，可作为"分析的就是这一份"的凭证。

## 1. 一句话结论

26.3 是**单一 Java 进程内的三层结构**：数据驱动的游戏内核（`net.minecraft.*`）+ 自研引擎与库（`com.mojang.*`）+ 完全外置的资源与数据包（`assets/`、`data/`）。客户端与服务端代码同处一个 jar，仅靠包名区分；26.3 最显著的变化是**独立的 GPU 抽象层**（`com.mojang.renderpearl`，原生 OpenGL + Vulkan 双后端），这直接决定我们 WGPU 层的移植策略。

## 2. 规模与分布

| 分区 | 条目数 | 说明 |
| --- | --- | --- |
| `net/` | 11,267 | 游戏逻辑，11,383 个 class 条目的绝大部分 |
| `assets/` | 11,813 | 客户端资源：纹理、模型、着色器、语言、字体 |
| `data/` | 10,449 | 服务端/数据包内容：战利品表、配方、世界生成、结构、标签 |
| `com/` | 690 | Mojang 引擎与库（渲染、数学、Realms 客户端） |
| `META-INF/` | 5 | 签名、清单、LICENSE |

class 条目 11,383 个；Vineflower 反编译得到 7,301 个 .java（差额来自内部类与合成类）。

## 3. 分层结构

```
net.minecraft.*          游戏内核（数据驱动 + 平台无关为主）
  ├─ client.*            客户端：渲染、GUI、输入、音频、多人会话
  ├─ server.*            服务端：刻循环、世界权威逻辑、命令、管理
  ├─ world.*             世界模型：区块、方块、实体、物品、光照、世界生成
  ├─ network.*           协议与编解码（登录、配置、游戏三阶段）
  ├─ commands.*          命令系统与参数类型
  ├─ core.*, util.*, nbt.*, tags.*, resources.*   共享基础
  └─ data.*, advancements.*, gametest.*           数据生成、进度、测试框架
com.mojang.*             引擎与库
  ├─ renderpearl.*       新 GPU 抽象（api / backend{opengl,vulkan} / frontend）
  ├─ blaze3d.*           既有渲染基础设施（framegraph、buffers、pipeline、vertex、font）
  ├─ realmsclient.*      Realms 客户端
  └─ math.*              数学与噪声
assets/minecraft/*       客户端资源（模型、纹理、shader、lang、字体）
data/minecraft/*         数据包内容（由 registry 挂载）
```

jar 根还有一个 `assets/.mcassetsroot` 与 `data/.mcassetsroot`（0 字节标记文件），说明资源已按"根"组织，允许被资源包整体替换/追加。这一点对启动器的资产提取设计有直接影响。

## 4. 客户端入口与启动序列

启动器元数据显示 `mainClass = net.minecraft.client.main.Main`，且 `arguments.game` 为**纯命令行契约**：

```
--username --version --gameDir --assetsDir --assetIndex --uuid
--accessToken --clientId --xuid --versionType
```

这印证了原则 8：原版客户端就是"接受命令行上下文的独立进程"，我们不引入任何私有握手即可保持同等解耦。

已识别的客户端骨架层次（按包）：

- `client.main`：进程入口、参数解析、会话装配。
- `client.ClientBootstrap`：静态注册引导（物品模型、特殊模型渲染器、染色源、对话框屏幕、Vulkan 特性集等）。
- `client.Minecraft`：客户端主对象，持有窗口、渲染器、资源管理器与主循环。
- `client.DeltaTracker`、`FramerateLimiter`：帧时间与限帧。
- `client.multiplayer`、`client.server`：连接管理与集成服务端装配（单人模式）。

## 5. 渲染栈（对我们的关键约束）

26.3 存在**两代渲染基础设施并存**：

| 层 | 包 | 角色 |
| --- | --- | --- |
| 新 GPU 抽象 | `com.mojang.renderpearl.api` | 设备、命令、管线、缓冲、纹理、顶点格式 |
| 后端实现 | `renderpearl.backend.{opengl,vulkan}` | 原生图形 API 实现 |
| 前端 | `renderpearl.frontend` | 面向引擎的封装 |
| 既有设施 | `com.mojang.blaze3d.{framegraph,pipeline,buffers,vertex,font,systems}` | 帧图、管线状态、顶点构建、字体 |

图形后端选择由 `client.PreferredGraphicsApi` 暴露：`default / opengl / vulkan`。

对我们的含义（仅记录，不在本文决策）：

- 原版已有"渲染 API 抽象 + 双后端"的成熟分层，我们的 WGPU 层可以按同样的抽象粒度设计，而不是直接照搬某个后端的调用习惯。
- 原版能力集合包含 Vulkan 特性探测（`VulkanFeatureSets`），说明其抽象层已考虑能力差异；WGPU 的能力模型与之不完全等价，需在渲染规格文档中逐项对照。
- `assets/minecraft/shaders` 分 `core / post / include` 三类（64/10/18），着色器属于资产，必须走提取路径（原则 3）。

## 6. 服务端与网络

- `server.*`（698 类）：`server.level`（世界实例）、`server.network`、`server.players`、`server.commands`、`server.packs`、`server.dedicated`、`server.permissions`、`server.dialog`。
- 26.3 新增 `server.jsonrpc`（95 类）：管理面 RPC，属于服务端管理能力，与游戏协议分离。
- `network.*`（630 类）：`network.protocol`（368，分阶段数据包定义）、`network.codec`（编解码器）、`network.chat`（聊天组件）、`network.syncher`（实体数据同步）。

单人模式的集成服务端在客户端进程内运行（`client.server` 仅 8 类，说明装配点很薄，真正的服务端逻辑在 `server.*`）——这与原则 5 的边界划分直接相关，需在边界文档中逐子系统判定。

## 7. 数据驱动的内容体系

`data/minecraft/` 一等内容类型（按条目量，前几名）：`advancement`、`loot_table`、`structure`、`worldgen`（template_pool / placed_feature / feature / biome / noise）、`tags`、`recipe`、`villager_trade`、`datapacks`。

这说明 26.3 的"内容"绝大部分由数据包驱动、由 registry 挂载，代码只提供类型与加载器。对我们最重要的推论：**Common 侧需要一套与数据包对齐的注册/加载机制**，而不是把内容硬编码进 C# 类里。

## 8. 三侧归属初判（待边界文档定稿）

| 包 | 初判 | 依据 |
| --- | --- | --- |
| `client.*` | Client | 直接依赖窗口、渲染、资源与输入 |
| `server.*` | Server | 世界权威逻辑、刻推进、玩家管理 |
| `world.*` | Common（部分偏 Server） | 数据结构与规则共享；但刻推进与光照更新属权威侧 |
| `network.protocol` / `codec` | Common | 协议字节与编解码两侧共用 |
| `core.*` / `util.*` / `nbt.*` / `tags.*` | Common | 无侧别依赖的基础设施 |
| `com.mojang.renderpearl` / `blaze3d` | 外围宿主层（渲染） | 引擎设施，不属于游戏逻辑三侧 |
| `data.*` / `gametest.*` | 外围宿主层（工具） | 数据生成与测试，不进入运行时客户端 |

细化与逐项证据见 `docs/architecture/boundaries.md`（下一轮产出）。

## 9. 风险与待验证

- `world.*` 的 4,187 类里混着大量"规则数据 + 权威逻辑"，Client/Server 切分将是本项目最大的边界风险。需要在边界文档中给出逐子树判定与理由。
- `renderpearl` 是新栈，26.3 内可能仍存在向 `blaze3d` 的过渡路径；哪些路径是主路径需要读代码确认（下一轮渲染子系统文档）。
- 未验证：`world.attribute`（60 类）、`gizmos`（20 类）、`waypoints`（24 类）、`util.filefix`（73 类）的确切职责，均为 26.3 新增或重构点，暂列假设。
- 重放/录制（`flightrecorder-config.jfc`）与 `special` 渲染路径未调查。

## 10. 证据指针

- 条目清单：`ref://mc-26.3-analysis/entries.csv`（34,227 条，含路径/大小/压缩后大小）。
- 反编译源码：`ref://mc-26.3-analysis/src`（7,301 个 .java，Vineflower 1.12.0）。
- 反编译日志：`ref://mc-26.3-analysis/logs/decompile-vineflower.log`。
- 启动器元数据：`E:\Games\minecraft\.minecraft\versions\26.3\26.3.json`。
