<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# 追踪矩阵（Traceability）

AGENTS.md 原则 12：**每个小任务在这里有且仅有一行**，串联"规格章节 ↔ 测试 ↔ 代码目录"。三列任一为空即视为未就绪或未完成。

- 规格列空 → 不得开始实现。
- 测试列空 → 不得声称完成。
- 代码列空 → 任务尚未落地。

## 当前矩阵

| ID | 任务 | 规格（文档章节） | 测试 | 代码目录 | 状态 |
| --- | --- | --- | --- | --- | --- |
| T-001 | 程序集划分与依赖方向 | `AGENTS.md` 原则 5；`docs/architecture/boundaries.md` R1–R6 | `tests/MinecraftCSharp.Architecture.Tests/AssemblyBoundaryTests.cs` | `src/*` | 已完成 |
| T-002 | 构建与警告门禁 | `docs/modules/build-scaffolding.md` §5 | CI `.github/workflows/ci.yml`（`-warnaserror` 由 props 保证） | `Directory.Build.props`、`global.json` | 已完成 |
| T-003 | 热路径零分配机制 | `AGENTS.md` 3.6；`docs/modules/build-scaffolding.md` §6.1 | `tests/MinecraftCSharp.Common.Tests/HotPathAllocationTests.cs` | `src/MinecraftCSharp.Common/Diagnostics/` | 已完成 |
| T-004 | 文档漂移检查 | `AGENTS.md` 原则 12 | `tests/MinecraftCSharp.Architecture.Tests/DocumentationDriftTests.cs` | `docs/` | 已完成 |
| T-005 | 标识符 `Identifier` | `docs/modules/common-primitives.md` §4.1；`docs/analysis/common/identifier.md` | `tests/MinecraftCSharp.Common.Tests/IdentifierTests.cs`（18 项） | `src/MinecraftCSharp.Common/Identifiers/` | 已完成 |
| T-006 | 随机数 `LegacyRandom` / `Xoroshiro128PlusPlus` | `docs/modules/common-primitives.md` §4.4；`docs/analysis/common/random.md`、`random-vectors.md` | `RandomVectorTests.cs`（回放 840 值）、`RandomAllocationTests.cs` | `src/MinecraftCSharp.Common/Random/` | 已完成 |
| T-009 | 位置化随机、世界生成混合器、战利品序列 | `docs/analysis/common/random.md` §5–§7；`random-vectors.md` §4.1 | `PositionalAndWorldgenVectorTests.cs`（105 值） | `src/MinecraftCSharp.Common/Random/`（PositionalSeed / JavaStringHash / *PositionalRandomFactory / WorldgenRandom / RandomSequence） | 已完成 |
| T-007 | 注册表顺序与 `Registry` | `docs/modules/common-primitives.md` §4.2；`docs/analysis/common/registry-order.md` | `RegistryOrderTests.cs`（含 1286 条原版 block 顺序快照） | `src/MinecraftCSharp.Common/Registries/` | 已完成 |
| T-010 | 方块状态全局 id 生成器 | `docs/analysis/common/registry-order.md` §5、§7.1 | `BlockStateIdTests.cs`（含 1442 个原版状态 id 与 35723 总数） | `src/MinecraftCSharp.Common/Registries/`（BlockStateDefinition / BlockStateLayout） | 已完成 |
| T-019 | 全阶段包 id 表导出与交叉校验 | `docs/specs/network/protocol-phases.md` §3 | `PacketIdTableTests.cs`（3 项：id 对照、连续性、唯一性） | `tests/.../TestData/vanilla-packet-ids.txt`（260 条） | 已完成 |
| T-018 | 启动器 CLI 垂直切片（login / probe / help） | `docs/modules/launcher.md` §3–§5 | `LauncherCommandLineTests.cs`（10 项）、手工验收：`probe mcf.ddger.top` 输出 protocol 777 | `src/MinecraftCSharp.Launcher/` | 已完成 |
| T-035 | 启动器 `launch`：校验 → 打包 → 挂载 → 认证 → 装配 → 启动 | `docs/modules/launcher.md` §5.1、§8.1 | `LaunchPlanTests`（8 项：参数齐全、令牌不上命令行、脱敏、会话文件往返、缺失/损坏、客户端路径覆盖、索引可选）+ `ClientArgumentTests`（5 项）+ `AuthTransportRetryTests`（3 项：重试后成功、重试耗尽报尝试次数、调用方取消不重试）；真实安装：pack 复用、索引 34 挂载 5,147 objects、交接退出码 0/2/3、认证端点间歇 RST（同期 curl 可达） | `Launcher/Launch/`、`Client/ClientArguments`、`Common/Lifecycle/SessionFile` | 已完成 |
| T-034 | 资产解析层（有序来源栈 / 元数据兄弟 / 语言合并 / 资源包预留） | `docs/analysis/assets/resource-resolution.md`；`docs/modules/asset-resolution.md` | `AssetResolutionTests`（9 组：栈顺序、覆盖、列举去重、元数据兄弟、语言合并方向、索引 id 校验、缺失策略、资源包叠放、路径推导） | `Launcher/Assets/Resolution/` | 已完成 |
| T-033 | 资产打包器（白名单 → 单文件 pack） | `docs/modules/assets.md` §5–§6；ADR-0005 | `AssetPackTests`（27 项：白名单 20 组、路径保留、幂等复用、字节确定性、换 jar 重建、仓库内输出拒绝、哈希不符拒绝、损坏包可重建）+ 真实 26.3 jar 实测：11,533 条目 / 5.6 MB / 768 ms | `Launcher/Assets/AssetSelection`、`AssetPackBuilder`、`HostPaths`；`launcher assets` | 已完成 |
| T-032 | 版本清单解析 + jar SHA-1 校验（篡改不可绕过） | `AssetVerificationTests.cs`（9 项：清单解析/缺字段/无效 JSON/哈希一致/篡改/尺寸/缺失文件/流式哈希） | `Launcher/Assets/VersionManifest`、`Launcher/Assets/JarVerifier` | `docs/modules/assets.md`（待评审） | 已完成 |
| T-031 | 贪婪合并网格 | `docs/modules/render.md` §5.2；`docs/specs/performance-baseline.md` §10 | `ChunkMesherQuadTests`（2：轴对齐矩形 + 面积等于逐面计数）、`ChunkMesherBoundsTests`（1）、`ChunkMesherWindingTests`（2：单方块 + 合并四边形）；实测真实世界三角形 110,868→51,402 | `Client/Render/ChunkMesher` | 已完成 |
| T-030 | 光照→顶点亮度与背面剔除 | `docs/modules/render.md` §5.2、§5.3；`docs/specs/performance-baseline.md` §9 | `ChunkLightTests`（6）、`ChunkMesherWindingTests`（1）、`ChunkMesherTests` 光照/朝向明暗（3）；实测真实世界 9/9 区块出图 | `Common/World/ChunkLight`、`Client/Render/ChunkMesher`、`Framework/Rendering/*` | 已完成 |
| T-029 | 几何路径零分配优化（等价性由指纹守护） | `docs/specs/performance-baseline.md` §8；`docs/specs/quality-attributes.md` QA-1.1 | `ChunkMesherGoldenTests`（3 指纹）、`GeometryAllocationTests`（6 项零分配/等价）；实测 P95 132.9→22.2 ms、分配 14.8 MB→0 | `Client/Render/MeshBuffer`、`Client/Render/ChunkMesher`、`Framework/Rendering/ClipSpaceTransform` | 已完成 |
| T-028 | 性能基线套件（区块/网格/变换/PNG/NBT） | `docs/specs/performance-baseline.md`；`docs/specs/quality-attributes.md` QA-1.1 | `benchmarks/MinecraftCSharp.Benchmarks`（可执行，输出 Markdown）；实测五个测量项 | `benchmarks/MinecraftCSharp.Benchmarks`、`docs/specs/performance-baseline.md` | 已完成 |
| T-027 | 窗口呈现（交换链）与原生句柄抽象 | `docs/decisions/0004-window-layer.md` | `CameraMathTests.cs`（9 项）；实测窗口呈现 180 帧（BGRA8UnormSrgb）、落盘同一帧 PNG、`960x540 -> 640x360` 自缩后继续呈现 | `Framework/Platform/WindowHost`、`Framework/Rendering/WindowedRenderer`、`Launcher/WindowCommand` | 已完成 |
| T-026 | 网格生成与离屏渲染（PNG 首帧） | `docs/decisions/0003-wgpu-binding.md` | `ChunkMesherTests.cs`（7 项）、`PngEncoderTests.cs`（4 项）；GPU 回读受阻 | `Client/Render/ChunkMesher`、`Common/Imaging/PngEncoder`、`Framework/Rendering/` | 已完成 |
| T-025 | 注册表同步与区块包解析 | `docs/specs/network/protocol-phases.md` §5.1.1、§5.3、§5.3.1；`docs/modules/world.md` §3 | `ChunkPacketTests.cs`（10 项）；实测 9/9 区块、275,141 非空气方块、97 个光照数组 | `Common/Network/`（DynamicRegistries / RegistryDataPackets / ChunkPacketParser / PalettedContainerCodec / LightDataCodec） | 已完成 |
| T-024 | NBT 读写（含上限与两种根） | `docs/modules/nbt.md` §3 | `NbtTests.cs`（13 项：全类型往返、畸形输入、深度/长度上限） | `Common/Nbt/`（NbtTagType / NbtTags / NbtReader / NbtWriter） | 已完成 |
| T-023 | 世界存储层与客户端世界骨架 | `docs/modules/world.md` §3 | `WorldStorageTests.cs`（12 项）、`ClientWorldTests.cs`（9 项） | `Common/World/`（PalettedContainer / ChunkSection / LevelChunk）、`Client/World/ClientWorld` | 已完成 |
| T-022 | play 阶段最小闭环（JoinGame 解析 / keep-alive / ping） | `docs/specs/network/protocol-phases.md` §5.2 | `PlayPacketTests.cs`（6 项）；实测 45 秒存活、回复 2 次 keep-alive | `Common/Network/PlayPackets`、`PlaySession` | 已完成 |
| T-021 | 真实进服：登录 + 配置阶段 + play 阶段入口 | `docs/modules/launcher.md` §8；`docs/specs/network/protocol-phases.md` §5.1 | `play mcf.ddger.top` 实测到达 play（`minecraft:login`）；`TokenStore` 路径守卫由实现保证 | `Common/Network/MinecraftConnection`、`MinecraftLoginClient`；`Launcher/ConfigurationProbe`；`Auth/TokenStore`、`SessionServerClient` | 已完成 |
| T-020 | 真实账号登录验收（设备码端到端） | `docs/modules/auth.md` §3.1、§3.2、§10 | 手工验收 2026-10-28 成功；`IdentityEndpoints` 预设 + 22 项离线测试 | `src/MinecraftCSharp.Auth/`、`src/MinecraftCSharp.Launcher/` | 已完成 |
| T-017 | 认证链路：设备码 / XBL / XSTS / Minecraft 会话 | `docs/modules/auth.md` §3–§6 | `DeviceCodeFlowTests.cs`（10 项）、`XboxAndServicesTests.cs`（9 项） | `src/MinecraftCSharp.Auth/` | 已完成 |
| T-016 | 登录阶段报文（LoginStart / Hello / Key / Disconnect） | `docs/specs/network/encryption.md` §1、§6.1 | `LoginPacketTests.cs`（11 项）、`LoginProbeIntegrationTests`（真实服务器加密验证） | `src/MinecraftCSharp.Common/Network/LoginPackets.cs` | 已完成 |
| T-015 | 登录加密原语（服务端哈希 / RSA / AES-CFB8） | `docs/specs/network/encryption.md` | `LoginCryptoTests.cs`（13 项，向量来自 JVM 参考实现） | `src/MinecraftCSharp.Common/Network/`（MinecraftLoginCrypto / AesCfb8） | 已完成 |
| T-014 | 状态查询客户端与 SRV 地址解析 | `docs/specs/network/protocol-phases.md` §4.1、§8；`docs/decisions/0002-dnsclient-dependency.md` | `StatusPingIntegrationTests`（真实服务器端到端）、`ServerAddressResolverTests` | `src/MinecraftCSharp.Common/Network/`（StatusPingClient / ServerAddressResolver） | 已完成 |
| T-013 | 握手 / 状态阶段与帧缓冲、压缩 | `docs/specs/network/protocol-phases.md` | `ProtocolPacketTests.cs`（13 项：逐字节握手、分片、黏包、压缩边界） | `src/MinecraftCSharp.Common/Network/`（ProtocolConstants / HandshakePackets / PacketFrameBuffer / PacketCompression） | 已完成 |
| T-012 | 协议编码原语（VarInt / VarLong / 帧前缀 / UTF-8 字符串） | `docs/specs/network/primitives-varint.md`、`primitives-strings.md` | `ProtocolPrimitiveTests.cs`（32 项：往返、边界、截断、畸形） | `src/MinecraftCSharp.Common/Network/` | 已完成 |
| T-011 | `nextGaussian`（Marsaglia 极坐标） | `docs/analysis/common/random.md` §8 | `PositionalAndWorldgenVectorTests`（gaussianLegacy / gaussianXoro 向量） | `src/MinecraftCSharp.Common/Random/MarsagliaPolarGaussian.cs` | 已完成 |
| T-008 | 坐标换算与负数取整 | `docs/analysis/common/coordinates.md`；`docs/modules/common-primitives.md` §4.3 | `BlockCoordinatesTests.cs`（20 项，含负数六连） | `src/MinecraftCSharp.Common/Coordinates/` | 已完成 |

## 使用规则

- 开工前先建行（状态 = 规格已就绪/阻塞于评审），收工时更新状态。
- ID 单调递增，不复用；废弃任务保留并把状态改为"已废弃"。
- 一次提交只推进一到两行，避免矩阵与实现同时漂移。
- 与 `docs/STATE.md` 的分工：STATE 说"现在做什么"，矩阵说"每个任务的规格/测试/代码在哪"。
