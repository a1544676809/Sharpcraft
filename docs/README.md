<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# 文档体系

本目录是本项目的唯一知识库。**代码之前先有文档**：任何模块在写下第一行代码之前，其设计与分析文档必须已经落盘在这里，并且已经过评审。

本文件只讲"文档怎么组织"；项目级规则以 [AGENTS.md](../AGENTS.md) 为准。新会话的阅读顺序见 AGENTS.md 4.2：`AGENTS.md` → [`STATE.md`](STATE.md) → 目标模块文档，并核对 `git status` / `git log -1`。

## 目录结构

| 目录 | 内容 | 什么时候写 |
| --- | --- | --- |
| `analysis/` | Minecraft 26.3 jar 的分析结果，按子系统分子目录 | 分析阶段（先于一切代码） |
| `architecture/` | 总体架构、客户端/服务端/通用边界、依赖方向、分层规则 | 架构设计阶段 |
| `specs/` | 对外契约：网络协议、渲染管线、资产格式、启动与认证、命令行参数 | 规格梳理阶段 |
| `modules/` | 每个待实现模块的设计文档（一份一个模块） | **写该模块代码之前** |
| `decisions/` | 决策记录 ADR，一条决策一个文件，只追加不修改历史 | 做出决策的当下 |
| `repro/` | 最小复现用例库的约定（用例本体放在 `tests/<工程>/Repro/`） | 报缺陷与修缺陷时 |
| `milestones/` | 里程碑范围、验收标准、性能基线数据 | 规划与收尾时 |
| `_templates/` | 文档模板，新建文档时复制 | 新建文档时 |

守卫规则：

- 任何模块目录下没有对应文档的，**不得开始写该模块的代码**。
- `analysis/` 的结论是所有实现的依据；实现与文档冲突时，先改文档（并评审），再改代码。

## 命名与颗粒度

颗粒度的目标是**快速查阅**：读者应该能在 30 秒内定位到某一条结论。

- 文件名一律小写、连字符分词，只用 ASCII：`block-state-registry.md`；不用空格、中文、下划线、驼峰。
- **一个文件只讲一件事**。一个文件里出现两个以上"因此"级别的主题，就该拆开。
- 单文件目标 100–300 行；超过约 400 行必须拆分，并在原位置留一行指向新文件。
- 单章节目标 15–60 行；不要出现需要滚动很久才能读完的章节。
- 长表格、超大清单（如完整的包名列表、完整的包 ID 表）单独成文件，主文档只放摘要 + 链接。
- 分析类文档按"子系统 → 主题"两层次分文件：例如 `analysis/server/entity-tracking.md`，不要写出 `analysis/server.md` 这种一个文件装下整个服务端的东西。
- 每个子目录必须有 `README.md` 作为入口，列出该目录下的文档清单：文件名 / 一句话职责 / 状态 / 最后更新。
- 顶层总索引在本文档的"当前文档清单"一节维护。

## 文档状态

每份文档标题下方第一行标注状态与元信息：

```
状态：[规划] | [草稿] | [已评审] | [已实现] | [过时]
基线：Minecraft 26.3
证据：<分析运行编号 / 外部工作区路径标识>
最后更新：YYYY-MM-DD
```

- `[过时]` 文档不要删除，标注后保留，避免同一问题被反复重新推导。

## 写作规范

与 [AGENTS.md](../AGENTS.md) 原则 3 一致，违反即视为合规问题：

- 中文写作；代码标识符、类型名、路径、协议字段名保持英文原样。
- **只描述行为与架构**。不得粘贴反编译代码、字节码、原始实现片段或伪代码逐句对照。
- 结论必须可追溯：写到具体子系统 / 数据结构 / 数据文件，或指向仓库外的分析证据编号（如 `证据：run-2026-10-28 #server/entity-tracking`）。不写"一般认为""印象中是"。
- 区分事实与假设：未验证的推测一律标 `（假设，待验证）`，并登记进风险清单。
- 不用 emoji；不用装饰性分隔线；表格只在确有二维对照时使用。
- 术语统一：首次出现时给中英对照，之后只用一种写法。

## 模块文档必须回答的问题

写模块代码前，`modules/<模块>.md` 至少要回答：

1. 这个模块负责什么，明确不负责什么。
2. 属于哪一侧（Client / Server / Common / 外围宿主层），判定依据是什么。
3. 依赖谁、被谁依赖，依赖方向是否合法。
4. 对外暴露的契约（接口、数据、事件），以及不打算暴露的部分。
5. 关键数据结构与它们的生命周期、所属线程。
6. 与原版行为的对齐点，以及有意的偏离点（含性能驱动偏离）。
7. 未解决问题与风险。

模板见 [`_templates/module-doc.md`](_templates/module-doc.md)。

## 当前文档清单

| 文档 | 职责 | 状态 |
| --- | --- | --- |
| [`STATE.md`](STATE.md) | 跨会话进度锚点：里程碑、进行中、阻塞、下一步、未提交改动 | 活文档 |
| [`traceability.md`](traceability.md) | 追踪矩阵：规格章节 ↔ 测试 ↔ 代码目录 | 活文档 |
| [`risks.md`](risks.md) | 风险登记册 | 活文档 |
| [`analysis/README.md`](analysis/README.md) | jar 分析的组织方式与总进度 | 草稿 |
| [`analysis/architecture/README.md`](analysis/architecture/README.md) | 整体架构分析入口与第一轮关键结论 | 草稿 |
| [`analysis/architecture/overview.md`](analysis/architecture/overview.md) | 基线标识、分层、入口、渲染栈、三侧初判 | 草稿 |
| [`analysis/architecture/package-map.md`](analysis/architecture/package-map.md) | 包级计数地图与子树职责/归属倾向 | 草稿 |
| [`analysis/common/README.md`](analysis/common/README.md) | 基础类型子系统入口 | 草稿 |
| [`analysis/assets/README.md`](analysis/assets/README.md) | 资产子系统分析入口（jar 条目清单） | 草稿 |
| [`analysis/assets/jar-entry-map.md`](analysis/assets/jar-entry-map.md) | jar 条目分类与资产清单（顶层分布、assets 分类） | 草稿 |
| [`analysis/assets/resource-resolution.md`](analysis/assets/resource-resolution.md) | 资源解析语义：路径推导、包栈优先级、元数据兄弟、合并方向 | 草稿 |
| [`analysis/assets/block-models.md`](analysis/assets/block-models.md) | 方块状态与模型：变体、继承、`#变量`、面→纹理 | 草稿 |
| [`analysis/common/identifier.md`](analysis/common/identifier.md) | 标识符：解析、校验、等值、排序、序列化 | 草稿 |
| [`analysis/common/random.md`](analysis/common/random.md) | 随机源家族、算法参数、种子派生、战利品序列 | 草稿 |
| [`analysis/common/registry-order.md`](analysis/common/registry-order.md) | 注册表顺序与方块状态全局 id 的生成链 | 草稿 |
| [`analysis/common/coordinates.md`](analysis/common/coordinates.md) | 坐标换算与负数取整规则 | 草稿 |
| [`analysis/common/random-vectors.md`](analysis/common/random-vectors.md) | 随机数参考向量：取真值方法、覆盖范围、回放约定 | 草稿 |
| [`architecture/README.md`](architecture/README.md) | 架构目录入口与生效中的边界规则 | 草稿 |
| [`architecture/boundaries.md`](architecture/boundaries.md) | 三侧 + 外围宿主层的判定规则、证据、拆分清单、程序集映射 | 草稿 |
| [`modules/README.md`](modules/README.md) | 模块文档入口与进度 | 草稿 |
| [`modules/build-scaffolding.md`](modules/build-scaffolding.md) | 构建骨架：程序集划分、依赖规则、构建与测试配置 | 草稿（已实施） |
| [`modules/common-primitives.md`](modules/common-primitives.md) | Common 基础类型：标识符、注册表、几何基元 | 草稿 |
| [`modules/auth.md`](modules/auth.md) | 认证模块：设备码流程、Xbox Live、Minecraft 会话、令牌安全 | 已评审 |
| [`modules/launcher.md`](modules/launcher.md) | 启动器（本阶段：认证与探测 CLI 垂直切片） | 已评审 |
| [`modules/world.md`](modules/world.md) | 客户端世界数据：调色板容器、区块段与列、坐标查询 | 已评审 |
| [`modules/nbt.md`](modules/nbt.md) | NBT 读写（网络/存储两种根、读取上限） | 已评审 |
| [`modules/assets.md`](modules/assets.md) | 资产管线：jar 校验、白名单提取、增量幂等、产物清单 | 待评审 |
| [`modules/asset-resolution.md`](modules/asset-resolution.md) | 资产解析层：来源栈、优先级、元数据兄弟、语言合并 | 待评审 |
| [`modules/accounts.md`](modules/accounts.md) | 多账号与离线登录闸门：账号模型、实时闸门、离线身份派生、防篡改 | 待评审 |
| [`modules/render.md`](modules/render.md) | 渲染层：网格生成、相机数学、离屏 WGPU 渲染与 PNG | 已评审 |
| [`_templates/module-doc.md`](_templates/module-doc.md) | 模块设计文档模板 | 已评审 |
| [`_templates/state.md`](_templates/state.md) | STATE 模板 | 已评审 |
| [`traceability.md`](traceability.md) | 追踪矩阵：规格章节 ↔ 测试 ↔ 代码目录 | 活文档 |
| [`risks.md`](risks.md) | 风险登记册 | 活文档 |
| [`milestones/README.md`](milestones/README.md) | 里程碑文档入口与约定 | 已评审 |
| [`milestones/roadmap.md`](milestones/roadmap.md) | M1–M6 里程碑划分、出口条件与验收标准 | 已评审 |
| [`specs/quality-attributes.md`](specs/quality-attributes.md) | 可测质量属性：性能、协议、热加载、跨平台 | 草稿 |
| [`specs/performance-baseline.md`](specs/performance-baseline.md) | 性能基线与测量口径（基准套件、噪声源、阈值建议） | 待评审 |
| [`specs/network/README.md`](specs/network/README.md) | 网络协议规格入口（协议 777 契约） | 草稿 |
| [`specs/network/primitives-varint.md`](specs/network/primitives-varint.md) | VarInt / VarLong 与帧长度前缀 | 草稿 |
| [`specs/network/primitives-strings.md`](specs/network/primitives-strings.md) | UTF-8 字符串与标识符编码 | 草稿 |
| [`specs/network/protocol-phases.md`](specs/network/protocol-phases.md) | 五阶段、包 id 表、帧与压缩规则 | 草稿 |
| [`specs/network/encryption.md`](specs/network/encryption.md) | 登录加密：服务端哈希、RSA 密钥交换、AES/CFB8 流 | 草稿 |
| [`decisions/README.md`](decisions/README.md) | ADR 索引与写作规则 | 活文档 |
| [`decisions/0000-template.md`](decisions/0000-template.md) | ADR 模板 | 已评审 |
| [`decisions/0001-architecture-rules-as-tests.md`](decisions/0001-architecture-rules-as-tests.md) | ADR-0001：架构规则以测试门禁落地 | 已采纳 |
| [`decisions/0002-dnsclient-dependency.md`](decisions/0002-dnsclient-dependency.md) | ADR-0002：引入 DnsClient.NET 做 SRV 查询 | 已采纳 |
| [`decisions/0003-wgpu-binding.md`](decisions/0003-wgpu-binding.md) | ADR-0003：WGPU 绑定（Brew.Wgpu）与离屏首帧 | 已采纳 |
| [`decisions/0004-window-layer.md`](decisions/0004-window-layer.md) | ADR-0004：窗口层（Silk.NET.Windowing）与原生句柄抽象 | 已接受 |
| [`decisions/0005-asset-pack-and-official-index.md`](decisions/0005-asset-pack-and-official-index.md) | ADR-0005：资产打包为单文件 pack，音频沿用官方索引 | 待评审 |
| [`decisions/0006-offline-login-gating.md`](decisions/0006-offline-login-gating.md) | ADR-0006：离线登录闸门、身份派生与防篡改边界 | 待评审 |
| [`decisions/0007-resources-live-in-the-client.md`](decisions/0007-resources-live-in-the-client.md) | ADR-0007：资源加载与图集归客户端，启动器只做校验与打包 | 已接受 |
| [`repro/README.md`](repro/README.md) | 最小复现用例库约定 | 活文档 |

（`milestones/` 已创建：入口见 [`milestones/README.md`](milestones/README.md)，阶段划分见 [`milestones/roadmap.md`](milestones/roadmap.md)。）
