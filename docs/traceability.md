<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 追踪矩阵
summary: 规格章节 ↔ 测试 ↔ 代码目录，一行一个任务
status: 活文档
-->

# 追踪矩阵（Traceability）

AGENTS.md 原则 12：**每个小任务在这里有且仅有一行**，串联"规格章节 ↔ 测试 ↔ 代码目录"。三列任一为空即视为未就绪或未完成。

- 规格列空 → 不得开始实现。
- 测试列空 → 不得声称完成。
- 代码列空 → 任务尚未落地。

## 当前矩阵

| ID | 任务 | 规格（文档章节） | 测试 | 代码目录 | 状态 |
| --- | --- | --- | --- | --- | --- |
| T-001 | 程序集划分与依赖方向 | `AGENTS.md` 原则 5；`docs/architecture/boundaries.md` R1–R6 | `tests/Sharpcraft.Architecture.Tests/AssemblyBoundaryTests.cs` | `src/*` | 已完成 |
| T-002 | 构建与警告门禁 | `docs/modules/build-scaffolding.md` §5 | CI `.github/workflows/ci.yml`（`-warnaserror` 由 props 保证） | `Directory.Build.props`、`global.json` | 已完成 |
| T-003 | 热路径零分配机制 | `AGENTS.md` 3.6；`docs/modules/build-scaffolding.md` §6.1 | `tests/Sharpcraft.Common.Tests/HotPathAllocationTests.cs` | `src/Sharpcraft.Common/Diagnostics/` | 已完成 |
| T-004 | 文档漂移检查 | `AGENTS.md` 原则 12 | `tests/Sharpcraft.Architecture.Tests/DocumentationDriftTests.cs` | `docs/` | 已完成 |
| T-005 | 标识符 `Identifier` | `docs/modules/common-primitives.md` §4.1；`docs/analysis/common/identifier.md` | `tests/Sharpcraft.Common.Tests/IdentifierTests.cs`（18 项） | `src/Sharpcraft.Common/Identifiers/` | 已完成 |
| T-006 | 随机数 `LegacyRandom` / `Xoroshiro128PlusPlus` | `docs/modules/common-primitives.md` §4.4；`docs/analysis/common/random.md`、`random-vectors.md` | `RandomVectorTests.cs`（回放 840 值）、`RandomAllocationTests.cs` | `src/Sharpcraft.Common/Random/` | 已完成 |
| T-009 | 位置化随机、世界生成混合器、战利品序列 | `docs/analysis/common/random.md` §5–§7；`random-vectors.md` §4.1 | `PositionalAndWorldgenVectorTests.cs`（105 值） | `src/Sharpcraft.Common/Random/`（PositionalSeed / JavaStringHash / *PositionalRandomFactory / WorldgenRandom / RandomSequence） | 已完成 |
| T-007 | 注册表顺序与 `Registry` | `docs/modules/common-primitives.md` §4.2；`docs/analysis/common/registry-order.md` | `RegistryOrderTests.cs`（含 1286 条原版 block 顺序快照） | `src/Sharpcraft.Common/Registries/` | 已完成 |
| T-010 | 方块状态全局 id 生成器 | `docs/analysis/common/registry-order.md` §5、§7.1 | `BlockStateIdTests.cs`（含 1442 个原版状态 id 与 35723 总数） | `src/Sharpcraft.Common/Registries/`（BlockStateDefinition / BlockStateLayout） | 已完成 |
| T-019 | 全阶段包 id 表导出与交叉校验 | `docs/specs/network/protocol-phases.md` §3 | `PacketIdTableTests.cs`（3 项：id 对照、连续性、唯一性） | `tests/.../TestData/vanilla-packet-ids.txt`（260 条） | 已完成 |
| T-018 | 启动器 CLI 垂直切片（login / probe / help） | `docs/modules/launcher.md` §3–§5 | `LauncherCommandLineTests.cs`（10 项）、手工验收：`probe mcf.ddger.top` 输出 protocol 777 | `src/Sharpcraft.Launcher/` | 已完成 |
| T-055 | 相机改为统一矩阵（世界空间顶点 + camera uniform） | `docs/modules/render.md` §6.1 | `CameraUniformTests`（1 项：uniform 与行向量变换逐分量一致）+ `VertexLayoutTests`（步长 48）| `Framework/Rendering/CameraUniform` | 已完成 |
| T-057 | 渲染插值（tick 之间平滑移动 + 传送吸附） | `docs/src/sharpcraft-client/game.md` §5 | `PlayerInterpolatorTests`（4 项：平滑插值、alpha 夹紧、yaw 短弧、Snap 丢弃旧样本） | `Client/Game/PlayerInterpolator`、`Launcher/InteractiveSession` | 已完成 |
| T-060 | 正式客户端循环接入移动同步（LivePlaySession） | `docs/specs/network/play-movement.md` §6.5 | `window` 实测：live 会话在服务端存活 14 秒、零踢人；`walk` 连续行走 12 秒 | `Launcher/LivePlaySession`、`Launcher/InteractiveSession` | 已完成 |
| T-062 | tick 精度（计时器精度 + 绝对截止时间） | `docs/specs/network/play-movement.md` §7.7 | `walk` 实测 tick 19.8/s、位移 4.399 格/秒（官方 4.291，+2.5%） | `Framework/Platform/TimerResolution`、`Client/Game/PreciseWait` | 已完成 |
| T-064 | 采纳服务端下发的朝向与速度（传送包逐分量合并） | `docs/specs/network/play-movement.md` §7.14 | `PlayMovementTests`（4 项：绝对朝向、相对朝向相加、仅标位轴为偏移、速度随传送下发）| `Common/Network/PlayerSync`、`PlayerPositionInfo` | 已完成 |
| T-068 | 区块更新包解析与应用 + 重网格化 | `docs/specs/network/play-movement.md` §7.17 | `BlockUpdateTests`（5 项：两种打包往返含负坐标、X 在高位、单方块包、多方块包）+ 实测 blockChanges=2 与窗口重建 mesh 585216→2003969 | `Common/Network/BlockPositionCodec`、`BlockUpdatePackets`、`Framework/Rendering/WindowedRenderer` | 已完成 |
| T-070 | 挖方块（`ServerboundPlayerAction`）与地形变化闭环 | `docs/specs/network/play-movement.md` §7.18 | 实测：创造模式每 20 tick 破坏目标格，目标 `state=9`（实体方块），`blockChanges=13` 回传并被应用 | `Common/Network/PlayPackets.BuildPlayerAction`、`Launcher/WalkCommand` | 已完成 |
| T-069 | 按区块增量重网格化（替代整世界重建） | `docs/specs/network/play-movement.md` §7.19 | `ChunkMeshCacheTests`（4 项：边界脏两列、内部脏一列、只重建脏区块、拼接索引不越界）+ 实测 1 个区块 50 ms 对 329 个 13.8 s | `Client/Render/ChunkMeshCache`、`Client/World/ClientWorld` | 已完成 |
| T-072 | 渲染与模拟分离：后台重网格化 | `docs/specs/network/play-movement.md` §7.20 | `ChunkMeshCacheTests`（4 项）+ 实测掉帧消失 | `Client/Render/ChunkMeshCache`、`Launcher/WindowCommand` | 已完成 |
| T-074 | 贴图纹素中心采样（修方块边缘半像素渗色） | `docs/specs/network/play-movement.md` §7.22 | 实测：方块边缘完整 16 纹素、无相邻贴图渗色 | `Framework/Rendering/OffscreenRenderer`（着色器）| 已完成 |
| T-075 | GPU 缓冲生命周期：退役列表 + 队列空闲后释放 | `docs/specs/network/play-movement.md` §7.23 | 实测：崩溃前第 2 帧 panic，修正后连跑 20 秒并干净退出 | `Framework/Rendering/WindowedRenderer` | 已完成 |
| T-077 | 多线程区块加载：解析与网格化共用工作池 | `docs/specs/network/play-movement.md` §7.24 | `ChunkWorkerPoolTests`（5 项：默认线程数随机器、显式线程数、网格在后台产出、缺失区块不产出、缓存接收）| `Client/World/ChunkWorkerPool`、`Launcher/InteractiveSession` | 已完成 |
| T-078 | worker 线程数自适应并可配置 | `docs/specs/network/play-movement.md` §7.24 | `ChunkWorkerPoolTests.TheDefaultWorkerCountFollowsTheMachine`、`AnExplicitWorkerCountIsHonoured` | `Client/World/ChunkWorkerPool` | 已完成 |
| T-080 | 区块几何竞技场：只上传变化的区块 | `docs/specs/network/play-movement.md` §7.25 | 实测（放大化）：主线程重建 422 次/42.3 ms → **0 次**，上传 15,286 MB → 1,040 MB，帧数 422 → 3524 | `Framework/Rendering/ChunkGeometryArena`、`WindowedRenderer` | 已完成 |
| T-083 | 区块解析失败不再丢弃整块地形 | `docs/specs/network/play-movement.md` §7.28 | `Repro_007_ChunkWithUnreadableBlockEntityTests`（2 项）+ 实测失败 25 → 1、空洞消失 | `Common/Network/ChunkPacketParser`、`Client/World/ChunkWorkerPool` | 已完成 |
| T-088 | 实体同步规格：id 表、26.3 编码变化、跟踪模型、M5 第一步范围 | `docs/specs/network/entity-sync.md` | 由 T-089 的解析测试共同验证 | — | 已完成 |
| T-089 | 实体包解析与 `EntityTracker`（增量/绝对/移除/未知 id） | `docs/specs/network/entity-sync.md` §3、§4.4、§5 | `EntityPacketsTests`（10 项：三角度字节、压缩向量、打包属性、定点增量、分段增量、Rot 用布尔、传送全字段）+ `EntityTrackerTests`（7 项：累加与零轴保持、未知 id、相对传送、移除、上限、短弧插值、零分配）| `Common/Network/EntityPackets`、`Client/World/EntityTracker` | 已完成 |
| T-090 | 实体占位渲染与插值（看得到别人在动） | `docs/specs/network/entity-sync.md` §5、§6 | 待补 | 未开始 | 规格已就绪 |
| T-091 | `VecDelta` 分段逐刻播放（替代求和后一次应用） | `docs/specs/network/entity-sync.md` §4.5 | 待补 | 未开始 | 规格已就绪 |
| T-085 | UI 模块规划：XAML 声明式结构 + 代码化行为 + 热加载边界 + 性能预算 | `docs/modules/ui.md` | 待补（P0 落地时补）| 未开始 | 已完成 |
| T-086 | UI P0 内核：2D 批次 + 位图字体 + 裁剪 + 最小声明式 + 文件热加载 | `docs/modules/ui.md` §7、§9、§10 | 待补 | 未开始 | 规格已就绪 |
| T-087 | M5 出口的一半：聊天浮层 + 玩家列表 | `docs/modules/ui.md` §13 | 待补 | 未开始 | 规格已就绪 |
| T-084 | 方块实体 1 字节错位（多读了一个不存在的布尔）| `docs/specs/network/play-movement.md` §7.28 | `Repro_007_ChunkWithUnreadableBlockEntityTests`（2 项）+ 实测失败 1 → **0**（328 个包全通过）| `Common/Nbt/NbtReader`、`Common/Network/ChunkPacketParser` | 已完成 |
| T-082 | 关闭/最小化窗口时不再以零尺寸配置 swapchain | `docs/specs/network/play-movement.md` §7.26 | 实测：关窗与定时退出均干净退出（退出码 0，无 panic）| `Framework/Rendering/WindowedRenderer`、`Platform/WindowHost` | 已完成 |
| T-081 | 按 view distance 预分配竞技场容量，消掉倍增尖峰 | `docs/specs/network/play-movement.md` §7.27 | `ArenaSizingTests`（5 项：无提示时倍增、按投影预分配、只预分配一次、样本不足、超大地形不被截断）+ 实测重建 7→2、上传 1278→588 MB | `Framework/Rendering/ArenaSizing`、`ChunkGeometryArena` | 已完成 |
| T-079 | 区块定位由线性扫描改为字典查找 | `docs/specs/network/play-movement.md` §7.24 | `ChunkWorkerPoolTests.MeshingRunsOffTheCallingThreadAndComesBack`（按打包键定位）| `Client/World/ChunkWorkerPool` | 已完成 |
| T-076 | 用常驻缓冲 + `Queue.WriteBuffer` 更新几何，避免每次重建分配 | `docs/specs/network/play-movement.md` §7.23 | 待补 | 未开始 | 进行中 |
| T-073 | 疾跑锁存与冲刺跳跃加成 | `docs/specs/network/play-movement.md` §7.21 | `SprintBehaviourTests`（4 项：锁存、松前进取消、潜行取消、连跳均速 >0.26） | `Common/Entities/LivingEntity`、`PlayerEntity` | 已完成 |
| T-071 | 按 section 细分重网格化并降低单区块耗时 | `docs/specs/network/play-movement.md` §7.19 | 待补 | 未开始 | 进行中 |
| T-067 | 冲刺与潜行的等效（含潜行碰撞盒与眼高） | `docs/specs/network/play-movement.md` §7.16 | `VerticalMovementTests`（潜行 0.0648/tick + 蹲伏盒 0.6×1.5/眼高 1.27、冲刺 0.2807/tick）+ 官方实测潜行 1.296、冲刺 5.623 | `Common/Entities/PlayerEntity`、`EntityDimensions`、`Client/Input` | 已完成 |
| T-066 | 跳跃与下落的等效（含终端速度渐近性） | `docs/specs/network/play-movement.md` §7.15 | `VerticalMovementTests`（4 项：顶点 1.2522、长距离终端 3.90–3.921、短距离 3.25–3.40、快速上升不被压低）+ 官方 1.2492 + 服务端连跳 10s 仅 1 次修正 | `Common/Entities/LivingEntity` | 已完成 |
| T-065 | 处理仅旋转包 `ClientboundPlayerRotationPacket`（id 74） | `docs/specs/network/play-movement.md` §7.14 | `PlayMovementTests`（3 项：交错字段解析、绝对采纳、相对相加）| `Common/Network/PlayMovementPackets`、`PlayerSync` | 已完成 |
| T-063 | 回拉归因与修复（区块流式加载） | `docs/specs/network/play-movement.md` §7.10–7.12 | `EntityWorldAdapterTests`（3 项：未加载读作空气、已加载实体方块、区块到达后地面生效）+ 实测 walk 区块 9→405、修正 190+→2；window 方块 585k→2.0M | `Launcher/LivePlaySession`、`InteractiveSession`、`WalkCommand` | 已完成 |
| T-061 | 移动位移等效验证（对照官方客户端） | `docs/specs/network/play-movement.md` §7.13 | 官方 4.281 格/秒 vs 我们 4.443 格/秒（+3.8%，≤5% 达标），修正 2 次/15 秒 | `Launcher/WalkCommand`、MCP 桥实测 | 已完成 |
| T-059 | 移动同步的真实服务端验证（`walk` 命令） | `docs/specs/network/play-movement.md` §6 | `walk` 实测：登录/传送接受通过，移动包被拒（阻塞） | `Launcher/WalkCommand` | 进行中 |
| T-058 | Play 移动同步（构建/解析/决策/偏差处理/接进 PlaySession） | `docs/specs/network/play-movement.md` | `PlayMovementTests`（12 项：包 id 与字段顺序、服务端位置往返、Relative 位、量化、发送决策、传送吸附、小幅平滑、大幅吸附、相对坐标按偏移） | `Common/Network/PlayMovementPackets`、`PlayerSync`、`PlaySession` | 已完成 |
| T-056 | 固定 20 Hz tick 循环与第一人称相机 | `docs/modules/input.md` §6；`docs/src/sharpcraft-client/{game,camera}.md` | `TickLoopAndCameraTests`（8 项：固定速率、补跑上限、插值因子、视角方向、俯仰夹紧、yaw 归一、相机与移动同向、眼位） | `Client/Game`、`Client/Camera`、`Launcher/InteractiveSession` | 已完成 |
| T-054 | 输入采集与动作映射（GLFW 回调 + FrameInput） | `docs/modules/input.md` §4；`docs/src/sharpcraft-client/input.md` | `InputMapperTests`（7 项：前进/横移、相反抵消、跳跃/冲刺/释放、松键即停、位移只消费一次、失焦清空、重绑定） | `Framework/Platform/WindowHost` + `Client/Input/` | 已完成 |
| T-053 | 实体系统根基（Aabb/尺寸/Entity/LivingEntity/PlayerEntity） | `docs/modules/entities.md` §3–§5；`docs/src/sharpcraft-common/entities.md` | `EntityMovementTests`（12 项：行走 4.317 b/s、冲刺 5.612 b/s、跳跃 1.25 格、加速曲线、冰面滑行、终端速度、贴面静止、墙体阻挡、不穿模、眼高） | `Common/Entities/` | 已完成 |
| T-052 | 玩家物理原型（已被 T-053 取代，原型已删除） | `docs/modules/entities.md`（迁移后以该文为准） | 已并入 `EntityMovementTests`（12 项） | 无（原型删除） | 已完成 |
| T-051 | 着色器包一等接入点（目标/阶段/顺序/uniform 契约 + 校验与回退） | `docs/specs/render/shader-packs.md`；AGENTS 原则 11 | `ShaderPackValidatorTests`（8 项：内置管线可表达为包、按依赖排序、缺最终阶段被拒、未声明目标被拒、循环依赖被拒、未知 uniform 被拒、重名目标被拒、相对尺寸缩放不塌陷） | `src/Sharpcraft.Framework/Rendering/ShaderPacks/` | 已完成 |
| T-050 | 模组宿主与可卸载验证（契约 + collectible ALC + 引用纪律） | `docs/modules/modding.md`；AGENTS 原则 2 | `ModHostUnloadTests`（4 项：宿主只持弱引用、句柄只持弱引用、无入口类型被拒且不残留上下文、缺文件报错清晰）+ 独立探针 `Sharpcraft.Modding.UnloadProbe` | `src/Sharpcraft.Modding/` | 进行中 |
| T-049 | 缺陷最小复现用例库（Repro 约定落地） | `docs/repro/README.md`；AGENTS 原则 12 | `tests/*/Repro/Repro_00*Tests.cs`（5 个用例：顶点布局单一来源、流体 still 回退、UV 内缩、顶点色仅光照、缓存登录登记账号） | `tests/Sharpcraft.{Framework,Client,Launcher}.Tests/Repro/` | 已完成 |
| T-047 | 窗口呈现路径带纹理（共享顶点布局 + 图集绑定） | `docs/modules/render.md` §6.1 | `VertexLayoutTests`（3 项：步长与属性数、偏移首尾相接、格式顺序）；运行时验证 `window <address>` 出帧 | `Framework/Rendering/WindowedRenderer`、`VertexLayout` | 已完成 |
| T-048 | 任何成功登录都刷新账号列表（离线闸门依据） | `docs/modules/accounts.md`；AGENTS 原则 7.1 | `SessionCacheTests`（5 项，含"缓存会话复用也要登记账号"） | `Launcher/AuthenticationFlow` | 已完成 |
| T-046 | 设备码呈现（自动开浏览器 + 预填码） | `docs/modules/auth.md` §设备码的呈现 | `DeviceCodePrompterTests`（5 项：URL 携带码、追加查询串、Windows 指定浏览器、shell 兜底、mac/Linux 命令） | `Launcher/DeviceCodePrompter` | 已完成 |
| T-044 | 会话令牌缓存与续期顺序（免 XSTS 续期） | `docs/modules/auth.md` §5.1 | `SessionCacheTests`（4 项：有效会话零请求、过期直连续期不走 XSTS、被拒回退 Xbox 链、微软令牌在 Xbox 链之前落盘） | `Launcher/AuthenticationFlow`、`Auth/TokenStore`、`Auth/MinecraftServicesClient` | 已完成 |
| T-045 | HTTP 代理支持（裸连 / 代理 / 直连三条路径可选） | `docs/modules/auth.md` §传输层 | `HostTransportProxyTests`（5 项：显式变量优先、`HTTPS_PROXY` 兜底、裸 host:port、坏值回退、无配置=直连） | `Launcher/Transport/HostTransport` | 已完成 |
| T-043 | 文档索引生成与源码模块覆盖门禁 | `docs/modules/doc-indexer.md`；ADR-0008 | `DocIndexTests`（2 项：索引最新、覆盖合规）；真实运行：11 个目录 README 由生成器接管、`--check` / `--check-coverage` 通过、基线 26 个模块 | `tools/Sharpcraft.Docs.Indexer`、`tests/Sharpcraft.Architecture.Tests/DocIndexTests.cs` | 已完成 |
| T-042 | `registry_data` → `BlockStateLayout` 动态装配（服务器增删/重排方块时的 state id → 方块名） | `docs/modules/render.md` §6.1；`docs/analysis/common/registry-order.md` §5 | 待补（先测：注册表条目顺序与基线不同、缺条目、id 越界） | 未开始（当前用 `VanillaBlockRegistry` 基线快照） | 规格已就绪 |
| T-041 | 特殊渲染方块（火把/箱子/活塞/作物等 265 个；流体已由 `_still` 回退覆盖） | 待写：流体与实体渲染规格 | 待补 | 未开始 | 规格不足 |
| T-040 | `blockstates` + `models` 解析（按面取纹理） | `docs/analysis/assets/block-models.md`；`docs/modules/render.md` §6.1 | `BlockModelResolverTests`（7 项：cube_all 链、column 的 end/side、`#变量`链、数组变体取首个、缺 blockstate、坏 JSON、模型优先于名字启发）；真实覆盖率 682 → **1019/1286** | `Client/Assets/BlockModelResolver` | 已完成 |
| T-039 | 基线 `BlockStateLayout` 装配（state id → 方块名） | `docs/modules/render.md` §6.1；`docs/analysis/common/registry-order.md` §5 | `VanillaBlockLayoutTests`（13 项：总数、基准 id 累加、air 首位、redstone_wire=1296、标识符唯一） | `Common/Registries/VanillaBlockRegistry`（嵌入式基线快照） | 已完成 |
| T-038 | 资源层迁至客户端（ADR-0007） | `docs/decisions/0007-resources-live-in-the-client.md`；`docs/modules/asset-resolution.md` §2 | `AssetResolutionTests`（12 项随代码迁入 `Client.Tests`）；`dotnet test` 全绿 | `Client/Assets/`（`AssetStack`、`ResourcePath`、`AssetSources`、`AssetPackManifest`） | 已完成 |
| T-037 | 纹理接入渲染（PNG 解码 → 图集 → UV → 采样） | `docs/modules/render.md` §6.1 | `PngDecoderTests`（12 项：编码器往返、五种扫描线过滤器、灰度、索引+tRNS、4 位索引、16 位高位字节、损坏/隔行/未定义过滤器拒绝）；真实资产解码：stone 16×16、grass_block_top 16×16、water_still 16×512（4 位索引）；`SpriteAtlasTests`（7 项：真实尺寸不重叠、像素落位、UV 覆盖、missing 回落、动画条带取首帧、可复现、包栈覆盖）；真实图集 1024×1024 / 1301 精灵 / 52 ms；`BlockSpriteMapTests`（5 项）+ `TexturedMesherTests`（4 项）+ 顶点格式扩到 12/13 float；2026-10-29 官方 jar 复验：pack 11,533 条目 / 681 ms、图集 1301 精灵、`frame local` 出带纹理帧且石面像素与图集一致 | `Common/Imaging/PngDecoder`、`Client/Assets/`、`Framework/Rendering/` | 已完成 |
| T-036 | 多账号与离线登录闸门（实时判定、离线身份派生、防篡改） | `docs/modules/accounts.md`；ADR-0006 | 评审后按 §9 的 7 组测试实现（闸门真值表、删除即失效、UUID 派生、名字边界、哨兵令牌、篡改检测、原子写） | `Launcher/Accounts/`（`AccountRecord`、`AccountStore`、`AccountsCommand`） | 已完成 |
| T-035 | 启动器 `launch`：校验 → 打包 → 挂载 → 认证 → 装配 → 启动 | `docs/modules/launcher.md` §5.1、§8.1 | `LaunchPlanTests`（8 项：参数齐全、令牌不上命令行、脱敏、会话文件往返、缺失/损坏、客户端路径覆盖、索引可选）+ `ClientArgumentTests`（5 项）+ `AuthTransportRetryTests`（3 项：重试后成功、重试耗尽报尝试次数、调用方取消不重试）；真实安装：pack 复用、索引 34 挂载 5,147 objects、交接退出码 0/2/3、认证端点间歇 RST（同期 curl 可达） | `Launcher/Launch/`、`Client/ClientArguments`、`Common/Lifecycle/SessionFile` | 已完成 |
| T-034 | 资产解析层（有序来源栈 / 元数据兄弟 / 语言合并 / 资源包预留） | `docs/analysis/assets/resource-resolution.md`；`docs/modules/asset-resolution.md` | `AssetResolutionTests`（9 组：栈顺序、覆盖、列举去重、元数据兄弟、语言合并方向、索引 id 校验、缺失策略、资源包叠放、路径推导） | `Client/Assets/`（ADR-0007 迁入；打包与校验留在 `Launcher/Assets/`） | 已完成 |
| T-033 | 资产打包器（白名单 → 单文件 pack） | `docs/modules/assets.md` §5–§6；ADR-0005 | `AssetPackTests`（27 项：白名单 20 组、路径保留、幂等复用、字节确定性、换 jar 重建、仓库内输出拒绝、哈希不符拒绝、损坏包可重建）+ 真实 26.3 jar 实测：11,533 条目 / 5.6 MB / 768 ms | `Launcher/Assets/AssetSelection`、`AssetPackBuilder`、`HostPaths`；`launcher assets` | 已完成 |
| T-032 | 版本清单解析 + jar SHA-1 校验（篡改不可绕过） | `docs/modules/assets.md` §4、§6 | `AssetVerificationTests.cs`（9 项：清单解析/缺字段/无效 JSON/哈希一致/篡改/尺寸/缺失文件/流式哈希）；2026-10-29 用官方 26.3 清单复验：41,483,720 字节 / SHA-1 `e877b6a0…` 一致 | `Launcher/Assets/VersionManifest`、`Launcher/Assets/JarVerifier` | 已完成 |
| T-031 | 贪婪合并网格 | `docs/modules/render.md` §5.2；`docs/specs/performance-baseline.md` §10 | `ChunkMesherQuadTests`（2：轴对齐矩形 + 面积等于逐面计数）、`ChunkMesherBoundsTests`（1）、`ChunkMesherWindingTests`（2：单方块 + 合并四边形）；实测真实世界三角形 110,868→51,402 | `Client/Render/ChunkMesher` | 已完成 |
| T-030 | 光照→顶点亮度与背面剔除 | `docs/modules/render.md` §5.2、§5.3；`docs/specs/performance-baseline.md` §9 | `ChunkLightTests`（6）、`ChunkMesherWindingTests`（1）、`ChunkMesherTests` 光照/朝向明暗（3）；实测真实世界 9/9 区块出图 | `Common/World/ChunkLight`、`Client/Render/ChunkMesher`、`Framework/Rendering/*` | 已完成 |
| T-029 | 几何路径零分配优化（等价性由指纹守护） | `docs/specs/performance-baseline.md` §8；`docs/specs/quality-attributes.md` QA-1.1 | `ChunkMesherGoldenTests`（3 指纹）、`GeometryAllocationTests`（6 项零分配/等价）；实测 P95 132.9→22.2 ms、分配 14.8 MB→0 | `Client/Render/MeshBuffer`、`Client/Render/ChunkMesher`、`Framework/Rendering/ClipSpaceTransform` | 已完成 |
| T-028 | 性能基线套件（区块/网格/变换/PNG/NBT） | `docs/specs/performance-baseline.md`；`docs/specs/quality-attributes.md` QA-1.1 | `benchmarks/Sharpcraft.Benchmarks`（可执行，输出 Markdown）；实测五个测量项 | `benchmarks/Sharpcraft.Benchmarks`、`docs/specs/performance-baseline.md` | 已完成 |
| T-027 | 窗口呈现（交换链）与原生句柄抽象 | `docs/decisions/0004-window-layer.md` | `CameraMathTests.cs`（9 项）；实测窗口呈现 180 帧（BGRA8UnormSrgb）、落盘同一帧 PNG、`960x540 -> 640x360` 自缩后继续呈现 | `Framework/Platform/WindowHost`、`Framework/Rendering/WindowedRenderer`、`Launcher/WindowCommand` | 已完成 |
| T-026 | 网格生成与离屏渲染（PNG 首帧） | `docs/decisions/0003-wgpu-binding.md` | `ChunkMesherTests.cs`（7 项）、`PngEncoderTests.cs`（4 项）；GPU 回读受阻 | `Client/Render/ChunkMesher`、`Common/Imaging/PngEncoder`、`Framework/Rendering/` | 已完成 |
| T-025 | 注册表同步与区块包解析 | `docs/specs/network/protocol-phases.md` §5.1.1、§5.3、§5.3.1；`docs/modules/world.md` §3 | `ChunkPacketTests.cs`（10 项）；实测 9/9 区块、275,141 非空气方块、97 个光照数组 | `Common/Network/`（DynamicRegistries / RegistryDataPackets / ChunkPacketParser / PalettedContainerCodec / LightDataCodec） | 已完成 |
| T-024 | NBT 读写（含上限与两种根） | `docs/modules/nbt.md` §3 | `NbtTests.cs`（13 项：全类型往返、畸形输入、深度/长度上限） | `Common/Nbt/`（NbtTagType / NbtTags / NbtReader / NbtWriter） | 已完成 |
| T-023 | 世界存储层与客户端世界骨架 | `docs/modules/world.md` §3 | `WorldStorageTests.cs`（12 项）、`ClientWorldTests.cs`（9 项） | `Common/World/`（PalettedContainer / ChunkSection / LevelChunk）、`Client/World/ClientWorld` | 已完成 |
| T-022 | play 阶段最小闭环（JoinGame 解析 / keep-alive / ping） | `docs/specs/network/protocol-phases.md` §5.2 | `PlayPacketTests.cs`（6 项）；实测 45 秒存活、回复 2 次 keep-alive | `Common/Network/PlayPackets`、`PlaySession` | 已完成 |
| T-021 | 真实进服：登录 + 配置阶段 + play 阶段入口 | `docs/modules/launcher.md` §8；`docs/specs/network/protocol-phases.md` §5.1 | `play mcf.ddger.top` 实测到达 play（`minecraft:login`）；`TokenStore` 路径守卫由实现保证 | `Common/Network/MinecraftConnection`、`MinecraftLoginClient`；`Launcher/ConfigurationProbe`；`Auth/TokenStore`、`SessionServerClient` | 已完成 |
| T-020 | 真实账号登录验收（设备码端到端） | `docs/modules/auth.md` §3.1、§3.2、§10 | 手工验收 2026-10-28 成功；`IdentityEndpoints` 预设 + 22 项离线测试 | `src/Sharpcraft.Auth/`、`src/Sharpcraft.Launcher/` | 已完成 |
| T-017 | 认证链路：设备码 / XBL / XSTS / Minecraft 会话 | `docs/modules/auth.md` §3–§6 | `DeviceCodeFlowTests.cs`（10 项）、`XboxAndServicesTests.cs`（9 项） | `src/Sharpcraft.Auth/` | 已完成 |
| T-016 | 登录阶段报文（LoginStart / Hello / Key / Disconnect） | `docs/specs/network/encryption.md` §1、§6.1 | `LoginPacketTests.cs`（11 项）、`LoginProbeIntegrationTests`（真实服务器加密验证） | `src/Sharpcraft.Common/Network/LoginPackets.cs` | 已完成 |
| T-015 | 登录加密原语（服务端哈希 / RSA / AES-CFB8） | `docs/specs/network/encryption.md` | `LoginCryptoTests.cs`（13 项，向量来自 JVM 参考实现） | `src/Sharpcraft.Common/Network/`（MinecraftLoginCrypto / AesCfb8） | 已完成 |
| T-014 | 状态查询客户端与 SRV 地址解析 | `docs/specs/network/protocol-phases.md` §4.1、§8；`docs/decisions/0002-dnsclient-dependency.md` | `StatusPingIntegrationTests`（真实服务器端到端）、`ServerAddressResolverTests` | `src/Sharpcraft.Common/Network/`（StatusPingClient / ServerAddressResolver） | 已完成 |
| T-013 | 握手 / 状态阶段与帧缓冲、压缩 | `docs/specs/network/protocol-phases.md` | `ProtocolPacketTests.cs`（13 项：逐字节握手、分片、黏包、压缩边界） | `src/Sharpcraft.Common/Network/`（ProtocolConstants / HandshakePackets / PacketFrameBuffer / PacketCompression） | 已完成 |
| T-012 | 协议编码原语（VarInt / VarLong / 帧前缀 / UTF-8 字符串） | `docs/specs/network/primitives-varint.md`、`primitives-strings.md` | `ProtocolPrimitiveTests.cs`（32 项：往返、边界、截断、畸形） | `src/Sharpcraft.Common/Network/` | 已完成 |
| T-011 | `nextGaussian`（Marsaglia 极坐标） | `docs/analysis/common/random.md` §8 | `PositionalAndWorldgenVectorTests`（gaussianLegacy / gaussianXoro 向量） | `src/Sharpcraft.Common/Random/MarsagliaPolarGaussian.cs` | 已完成 |
| T-008 | 坐标换算与负数取整 | `docs/analysis/common/coordinates.md`；`docs/modules/common-primitives.md` §4.3 | `BlockCoordinatesTests.cs`（20 项，含负数六连） | `src/Sharpcraft.Common/Coordinates/` | 已完成 |

## 使用规则

- 开工前先建行（状态 = 规格已就绪/阻塞于评审），收工时更新状态。
- ID 单调递增，不复用；废弃任务保留并把状态改为"已废弃"。
- 一次提交只推进一到两行，避免矩阵与实现同时漂移。
- 与 `docs/STATE.md` 的分工：STATE 说"现在做什么"，矩阵说"每个任务的规格/测试/代码在哪"。
