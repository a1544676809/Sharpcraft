<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 原版等效性验证矩阵（全量）

本文件是**验收清单**，不是设计文档。任何功能只有在对应行有"判据 + 自动化检查 + 记录数值"之后，才允许声称"与原版等效"。

## 0. 现状声明（实时联动）

- 本矩阵遵循 `AGENTS.md` 原则 14：**无外部原版真值锚点（Golden Vectors）或同机位参考帧对比的测试，一律只算结构自检，不得标为完全等效**。
- 早期三大结构性损坏（新区块流式加载停滞、半透明层不随流式更新、水与岩浆独立流体通道缺失）均已修复并有实机/单测回归；当前剩余長尾差距集中记录在 [`docs/specs/render/known-gaps.md`](../render/known-gaps.md)，并按 `docs/STATE.md` 的 Phase 0 → Phase 4 大纲推进。

## 1. 矩阵

| # | 特性 | 原版判据 | 检查方式 | 阈值 | 状态 |
|---| --- | --- | --- | --- | --- |
| 1 | 登录/进入世界 | 字段顺序与类型同原版 `ClientboundLoginPacket` / `CommonPlayerSpawnInfo` | `JoinGameFieldTests`、`LoginPacketTests`、`ProtocolPacketTests` | 逐字段相等 | **已验证（字段级 golden）** |
| 2 | KeepAlive/Ping | 收到即回 | `verify-protocol-live.ps1` | 20s 内 ≥1 次 | **已验证（实测 1 次）** |
| 3 | 区块流式加载与卸载 | Release 下客户端网格滞后 ≤2s，视距+2 外自动卸载释放显存 | `verify-mesh-lag.ps1`、`ClientWorld` 卸载单测 | 滞后 ≤2s、无 OOM | **已验证（Release 零积压追平，视距 32 压测 3890 区块无 OOM）** |
| 4 | 区块批处理 ack | 每批 `ChunkBatchFinished` 回 `ChunkBatchReceived` | `verify-batch-ack.ps1` | 区块数 ≥40、failed=0 | **已验证（PASS）** |
| 5 | 玩家位置同步 | 接受传送后发 `player_loaded`(44)，速度对齐原版 4.317 格/秒 | `verify-protocol-live.ps1` | 4.317±0.25、corrections≤1 | **已验证（speed=4.318、corrections=1）** |
| 6 | 方块实体 | 区块内记录与更新包 clientbound 6 解码 | `BlockEntityRecordTests`、`BlockEntityUpdateTests`、`Repro-007` | 逐字段相等 | **部分验证（解码已验证；NBT 保留与 49 条类型注册表已完成 T-657/T-658；NBT 消费待后续）** |
| 7 | 实体模型 | 原版模型 JSON 提取与骨骼/部件渲染 | `EntityPlaceholderMesherTests` | 真实模型与名字牌对齐 | **部分验证（真实部件模型与逐实体动画 provider 已落地 T-355..T-663 系列；名字牌与 held item 尚余）** |
| 8 | 渲染分层与流体 | 三层地形 + 独立水/岩浆流体通道（同种剔除、四角高度、flow UV） | `BlockLayerRulesTests`、`FluidMesherTests`、`FluidSurfaceTests` | 全绿 | **部分验证（静态几何与 flow 已对齐；overlay/图集动画归入 Phase 2 P2.2）** |
| 9 | 生物群系着色与天气 | `ColorMapColorUtil` + 67 群系动态注册表 + 半径 2 混合 + 雨雷衰减 | `BiomeTintSamplingTests`、`BiomeRegistryTests`、`WeatherBlendTests` | 逐点相等 | **已验证（121 点网格 + 真实服务器 67 群系实测草色 #91BD59）** |
| 10 | 图集 UV/padding/mip | 逐精灵归属 mip + 2 纹素 padding，无黑边渗色 | ADR-0015/0016 + `SpriteAtlasTests` | 黑像素=0 | **已验证（细黑线 = 0）** |
| 11 | 面剔除与遮挡形状 | 树叶留内面、冰/玻璃保留内面、精确 VoxelShape 遮挡 | `Repro-005`、`Repro-006` | 面积 256/162 | **部分验证（族规则已对齐；`OcclusionShape` 已完成 T-659，接入 `ChunkMesher` 归入 P1.5）** |
| 12 | 半透明 OIT | 26.3 三阶段小波参考 OIT（`DEPTH_BOUNDS` + 8 系数 `TRANSMITTANCE` + `ACCUMULATE` + `COMPOSITE`） | `OitWaveletTests` + 独立云深度 OIT 实测 | 顺序无关、误差 <0.03 | **已验证（1272 测试通过，高分/大视距实测无条纹）** |
| 13 | 天空/雾/天体/云 | 逐像素 fog 天空盘 + 日月星时间轴 + 逐 Cell 云层 OIT | `CelestialGeometryTests`、`SkyTimelinesTests`、`CloudMeshBuilderTests` + 采样对照 | 天空采样误差 ≤1/255 | **已验证（天空盘 (136,169,255) vs 原版 (137,170,255)；精确雾基色归入 Phase 2 P2.1）** |
| 14 | UI/字体/聊天 | 画布整数缩放、样式段、聊天收发、离屏 UI Pass | UI 单测 + T-104 离屏对比 | 逐像素一致 | **部分验证（内核完成；离屏 UI Pass 与 `chat_type` 归入 Phase 3）** |
| 15 | 性能 | 帧时间 p99 ≤ 16.7 ms | `verify-performance.ps1` | p99 ≤ 16.7 ms | **已验证（1080p 移动 p99 8.03 ms；2560×1334 视距 32 达 162 FPS）** |
| 16 | 调试代码可分离 | 游戏程序集不读环境变量；删除 `Launcher/Diagnostics` 不影响运行 | `check-diagnostics-separation.ps1` + `DiagnosticsSeparationTests` | violations=0 | **已验证（0 处违规）** |
| 17 | 速度读数（宿主显示）| 原版 F3：`Entity.computeSpeed()` 每 tick 采样 3D 位移 | `SpeedOverlay` | 0.281 blocks/tick | **已验证** |


## 1.1 纯逻辑等效性（与渲染无关，全部以原版 dump / 向量为依据）

这些项不需要截图，可直接用参考数据逐条断言，属于当前**已完成**的部分：

| 项目 | 原版依据 | 覆盖量 | 落地 |
| --- | --- | --- | --- |
| 方块注册顺序 | `vectors/block-order.txt` | **1286** 个方块，首位 `air`、末位 `firefly_bush` | `RegistryOrderTests` |
| 方块状态计数 | `vectors/block-states.txt` | 合计 **35723** 个全局状态（与 `VanillaBlockRegistry.BlockStateCount` 一致）| `BlockStateIdTests` |
| 状态 id 采样 | `vectors/block-states.txt` 采样 | **1455** 条样本逐条比对 | `BlockStateIdTests` |
| 数据包 ID | `vectors/packet-tables.txt` | 参考 **278** 条；我方常量与之逐条比对 **90** 个，0 不一致 | `PacketIdTableTests` + `check-packet-ids.ps1` |
| Java 随机数 | `vectors/raw-vectors.txt` | **238** 条向量（legacy / xoro / create）| `RandomVectorTests`、`PositionalAndWorldgenVectorTests` |
| 调试代码可分离 | 原则 13 | 游戏程序集环境变量读取 **0** 处 | `check-diagnostics-separation.ps1` + 架构测试 |

上述筛选用到的 24 个测试当前**全部通过**。

**纯逻辑部分仍缺的**是"活体协议行为"断言（不需要截图，只需连服务器并断言计数）：
登录字段、KeepAlive 60s 不断线、区块批处理 ack 次数、玩家位置锁定（≤0.05 格）。这些是下一步的脚本工作。

## 2. 执行顺序

1. 恢复 3、4、5（网络/流式/位置）——世界能否正常看的前提。
2. 恢复 2 并补 12 的流体缺失。
3. 逐行补 1、9、13、15 的自动化检查与原版基准。
4. 每修一项：先写复现用例 → 再改 → 该行转"已验证"并记录数值。

## 3. 脚本约定

- 全部放 `tools/verify/` 与 `tests/`；**禁止**在生产路径加诊断开关，确需遥测复用既有 `MC_DIAG`。
- 每个脚本输出 `PASS/FAIL key=value`，便于汇总回归。
