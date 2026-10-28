<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`E:\mcanalysis\26.3\reports\{edges.csv, summary.json, client_coupled.csv}`，工具 `tools/analysis/package_deps.py`
最后更新：2026-10-28

# 三侧边界划分（Client / Server / Common / 外围宿主层）

本文是原则 5 的落地依据：每个子系统归属哪一侧、证据是什么、哪些必须拆分。**代码归属以本文为准**，归属不清的项标 `[待分析]`，不得就地实现。

## 1. 判定规则

| 编号 | 规则 | 依据 |
| --- | --- | --- |
| R1 | 引用 `net.minecraft.client.*` 的代码**不可能是 Common 或 Server**，必须归 Client 或拆分 | 原则 5 单向依赖；原版自身遵守（见 R2） |
| R2 | 原版事实：`net.minecraft.*` 中除 `client` 自身外，**只有 `net.minecraft.realms`（4 类 / 10 次引用）** 触碰客户端类型 | 全量扫描 565 个包、8,692 条包级依赖边 |
| R3 | 引擎与平台设施（`com.mojang.renderpearl`、`com.mojang.blaze3d`、`com.mojang.math`）不属于三侧，归**外围宿主层** | 它们被客户端与整合服务端共用，但语义上不是游戏逻辑 |
| R4 | 工具链（`net.minecraft.data`、`gametest`、`client.data`、`server.dedicated`）归外围宿主层，不进入运行时客户端 | 数据生成器与测试框架不是运行时路径 |
| R5 | 依赖方向只允许 `Client` / `Server` → `Common`；`Common` 内不得出现反向引用 | 原则 5 |
| R6 | 同一包内既有共享数据又有权威逻辑时，**必须拆分**而不是整包归类 | 原版单 jar 结构的历史耦合，不能继承到 C# |

## 2. 证据方法与局限

- 方法：对 7,301 个反编译源文件提取 `package` 声明、`import` 与全限定名引用，构建包级依赖图（8,692 条边），按前缀统计"依赖 Client / 依赖 Server / 被 Client 引用 / 被 Server 引用"。
- 局限 1：只看 import 与全限定名，**接口回调式的运行期耦合不可见**。
- 局限 2：反编译器可能内联全限定名，个别边权偏高；因此本文只用"是否为 0"这类方向性判据，不用绝对数值下强结论。
- 结论强度：R2 属"零 vs 非零"级证据，可直接作为规则；其余归属判断为方向性证据 + 结构判断，标注置信度。

## 3. 深度聚合证据

列含义：类数 = 该子树 java 文件数；出/入 = 引用方向计数（仅用于方向判断，非精确度量）。

| 包 | 类数 | 依赖 Client | 依赖 Server | 依赖引擎 | 被 Client 用 | 被 Server 用 | 判定 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `net.minecraft.client` | 1,891 | 4,340 | 188 | 743 | — | 0 | Client |
| `net.minecraft.world.level` | 1,418 | 0 | 287 | 5 | 499 | 242 | Common（偏客户端消费） |
| `net.minecraft.util` | 735 | 0 | 15 | 0 | 723 | 149 | Common |
| `net.minecraft.world.entity` | 727 | 0 | 310 | 1 | 466 | 101 | Common（数据在此，行为需拆） |
| `net.minecraft.server` | 432 | 0 | 612 | 0 | 188 | — | Server |
| `net.minecraft.network` | 416 | 0 | 18 | 0 | 392 | 245 | Common |
| `net.minecraft.world.item` | 323 | 0 | 78 | 0 | 291 | 53 | Common |
| `net.minecraft.data` | 174 | 0 | 10 | 0 | 14 | 3 | 外围（数据生成） |
| `com.mojang.renderpearl` | 146 | 2 | 0 | 249 | 193 | 0 | 外围（渲染引擎） |
| `com.mojang.realmsclient` | 137 | 184 | 0 | 218 | 13 | 0 | 外围（在线服务，客户端侧） |
| `net.minecraft.commands` | 124 | 0 | 34 | 0 | 11 | 250 | Server |
| `net.minecraft.core` | 123 | 0 | 27 | 1 | 376 | 135 | Common |
| `net.minecraft.advancements` | 115 | 0 | 83 | 0 | 8 | 12 | Server |
| `com.mojang.blaze3d` | 86 | 26 | 5 | 69 | 433 | 0 | 外围（渲染引擎） |
| `net.minecraft.world.inventory` | 64 | 0 | 16 | 0 | 47 | 2 | Common（菜单/容器定义） |
| `net.minecraft.gametest` | 48 | 0 | 26 | 0 | 1 | 2 | 外围（测试框架） |
| `net.minecraft.nbt` | 43 | 0 | 0 | 0 | 17 | 20 | Common |
| `net.minecraft.world.attribute` | 30 | 0 | 0 | 0 | 10 | 3 | Common |
| `net.minecraft.tags` | 29 | 0 | 3 | 0 | 26 | 11 | Common |
| `net.minecraft.world.phys` | 28 | 0 | 0 | 2 | 151 | 31 | Common |
| `net.minecraft.world.effect` | 20 | 0 | 16 | 0 | 14 | 6 | Common |
| `net.minecraft.world.scores` | 16 | 0 | 0 | 0 | 10 | 14 | Common（Server 权威、Client 读取） |
| `net.minecraft.gizmos` | 15 | 0 | 0 | 0 | 34 | 0 | Common（仅客户端消费，26.3 新增） |
| `net.minecraft.resources` | 12 | 0 | 7 | 0 | 466 | 68 | Common |
| `net.minecraft.stats` | 10 | 0 | 3 | 0 | 8 | 2 | Common |
| `net.minecraft.world.clock` | 11 | 0 | 2 | 0 | 2 | 3 | Common（26.3 新增） |
| `net.minecraft.world.waypoints` | 9 | 0 | 1 | 0 | 6 | 3 | Common（26.3 新增） |
| `com.mojang.math` | 10 | 0 | 0 | 0 | 104 | 0 | 外围（数学库） |
| `net.minecraft.world.food` | 6 | 0 | 1 | 0 | 1 | 0 | Common |
| `net.minecraft.sounds` | 6 | 0 | 0 | 0 | 49 | 7 | Common |
| `net.minecraft.references` | 5 | 0 | 0 | 0 | 0 | 0 | `[待分析]`（无引用，可能为注册表常量） |
| `net.minecraft.world.timeline` | 5 | 0 | 0 | 0 | 1 | 1 | `[待分析]` |
| `net.minecraft.realms` | 4 | 10 | 1 | 1 | 0 | 0 | **Client**（唯一违反 R1 的非 client 包） |

## 4. 关键结论

1. **原版本身就严守 R1**：除 `net.minecraft.realms` 外，所有共享与服务端包对客户端类型的引用次数为 **0**。这意味着"以是否引用客户端类型划界"不是我们发明的规则，而是原版结构的直接反映，可以直接作为 C# 工程的强制检查项。
2. **`world.*` 不是 Common 的同义词**：它们零客户端依赖（可以进 Common），但同时大量引用 Server（`world.entity.ai` 100、`world.level.block` 201），说明**权威逻辑混在共享命名空间里**。这是本项目要拆分的主体。
3. **客户端消费面很宽**：`world.entity.animal` 被客户端引用 271 次、`resources` 466 次、`world.item` 291 次。实体/物品的具体类型必须放在 Common，客户端只做表现层引用——若把这些类型放 Client，Common 与 Server 立刻不可用。
4. **`commands` / `advancements` 应归 Server**：被 Server 引用 250 / 12，被 Client 引用仅 11 / 8，且它们引用 Server 34 / 83。客户端侧只需要"命令补全建议"这类窄接口，不引入命令实现。
5. **`gizmos` 是纯客户端消费**（34 次引用全部来自客户端）：可作为 Client 侧调试绘制模块，不需要进 Common。

## 5. 必须拆分的区域（按工作量排序）

| 区域 | 类数 | 现状问题 | 拆分方案（草案） |
| --- | --- | --- | --- |
| `world.level.block` | 474 | 零客户端依赖但引用 Server 201 次：方块**定义**与方块**行为**（刻、更新、红石、掉落）混在一起 | 定义（状态、属性、形状、渲染挂钩）→ Common；刻/更新/掉落/交互结算 → Server；模型与外观 → Client |
| `world.entity` | 727 | 实体类自带 AI、寻路、生成规则与客户端渲染挂钩 | 实体类型与同步数据（`SynchedEntityData` 等）→ Common；AI/寻路/生成 → Server；模型/动画/渲染 → Client |
| `world.level`（其余） | ~700 | `storage`（被 Server 引用 41 > Client 17）、`gamerules`（26 vs 11）、`redstone`（仅 Server）、`pathfinder`（仅 Server）明显偏权威侧 | 存档读写 / 刻调度 / 红石 / 路径：→ Server；区块与光照的**数据结构**：→ Common，光照**重算**需按侧各一份 |
| `world.item` | 323 | 引用 Server 78 次：物品的掉落实体、使用结算属权威逻辑 | 物品/组件/附魔数据 → Common；使用结算与掉落 → Server |
| `world.level.levelgen` | 458 | 生成算法属权威侧，但客户端需"同种子同结果"以生成远景/结构预览 | 生成算法 → Server 侧实现，算法规格进 `docs/specs/`，客户端只消费结果（不重复实现，除非有确定性需求）`[待分析]` |

拆分判据（可操作）：**凡读写世界状态、推进时间、结算结果者 → Server；凡只读世界状态或持有纯数据者 → Common；凡产出像素/声音/输入者 → Client。**

## 6. 外围宿主层清单

不属于三侧，单独成层，且不得被游戏逻辑反向引用（原则 5）：

| 内容 | 归属层 | 说明 |
| --- | --- | --- |
| `com.mojang.renderpearl` / `blaze3d` / `math` | 渲染与数学设施 | 对应我们 `/src/Framework`，WGPU 抽象在此 |
| `com.mojang.realmsclient` | 在线服务客户端 | 与认证/启动器同层，不属游戏逻辑 |
| `net.minecraft.data` / `gametest` / `client.data` | 工具链 | 数据生成与测试，不随客户端发布 |
| `server.dedicated` / `server.jsonrpc` | 服务端宿主与管理面 | 我们不自研服务端（原则 4），仅作协议对齐参考 |
| 资产管线 / 日志 / 更新 | 宿主层 | 由启动器承担（原则 8） |

## 7. 到 C# 程序集的映射（草案，待评审）

| C# 目标 | 承接内容 |
| --- | --- |
| `MinecraftCSharp.Common` | `core`（registry/组件）、`util`、`nbt`、`tags`、`resources`、`network`（协议与编解码）、`world.phys`、`world.item` 数据部分、`world.entity` 类型与同步数据、`world.level` 数据结构 |
| `MinecraftCSharp.Client` | `client.*` 全部、`gizmos`、`realms`、实体/方块/物品的模型与渲染、UI、输入、音频表现 |
| `MinecraftCSharp.Server` | `server.*`、`commands`、`advancements`、`world.level.block` 行为部分、`world.entity.ai`、区块刻与光照重算、存档 |
| `/src/Framework` | WGPU 渲染抽象、窗口、输入、资源加载、音频后端、性能基础设施 |
| `/src/Modding` | 模组 API 契约与热加载宿主 |
| `MinecraftCSharp.Auth` / `Launcher` | 认证链路、版本与资产获取、启动编排 |

此表与 AGENTS.md 3.3 的目录草案一致；程序集是否进一步细拆（如 `Common.World` / `Common.Network`）留待模块文档评审时决定。

## 8. 待验证项

- `net.minecraft.references`（5 类，无入无出）与 `world.timeline`（5 类）的实际角色未知，暂标 `[待分析]`。
- `world.entity.variant`（11 类）完全孤立（零入零出），可能是数据驱动的新实体变体系统，需读代码确认。
- `world.attribute`（30 类）：零 Server 依赖、被 Client/Server 少量引用，疑为"实体/世界属性"重构产物。
- 光照在客户端与服务端的职责划分（原版两处都有 15 类左右），需要单独分析 `world.level.lighting` 与 `client.renderer` 的交互。
- 数据包（`data/`）驱动的注册表如何在 Common 侧加载，需在 `docs/analysis/registry/` 展开。

## 9. 证据指针

- 包级依赖明细：`E:\mcanalysis\26.3\reports\edges.csv`
- 聚合表：`summary.json`、`summary.md`
- 客户端耦合清单：`client_coupled.csv`（仅 1 条：`net.minecraft.realms`）
- 复现：`python tools/analysis/package_deps.py --src <反编译目录> --out <报告目录>`
