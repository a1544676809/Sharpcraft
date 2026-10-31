<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 原版等效性验证矩阵（全量）

本文件是**验收清单**，不是设计文档。任何功能只有在对应行有"判据 + 自动化检查 + 记录数值"之后，才允许声称"与原版等效"。

## 0. 现状声明（必须诚实）

- 本会话（2026-10-30 起）共有 **226 个提交**，其中大量直接改变可观测行为（渲染分层、mip、UV、tint、剔除、流式加载）。
- 其中**只有一部分**有逐像素/逐数值证据；其余仅有"看起来对"的截图。按项目规则这不算验证。
- 已知**当前损坏/缺失**：
  1. **新区块不继续加载**：首批之后 `updates 0`，玩家移动时世界不扩展。
  2. **半透明层不随区块流式更新**（水/玻璃在新区块里不出现）。
  3. **流体渲染路径缺失**：原版 `LiquidBlock.getRenderShape = INVISIBLE` + 独立流体渲染器。

## 1. 矩阵

| # | 特性 | 原版判据 | 检查方式 | 阈值 | 状态 |
|---| --- | --- | --- | --- | --- |
| 1 | 登录/进入世界 | JoinGame 字段与顺序一致 | `walk`/`play` 断言 | 完全一致 | 未验证 |
| 2 | KeepAlive/Ping | 收到即回 | `verify-protocol-live.ps1` | 20s 内 ≥1 次 | 已验证：**实测 1 次** ✓ |
| 3 | 区块流式加载 | 移动时区块数持续增长到视距上限 | `tools/verify/verify-streaming.ps1` | ≥expected 90% | **已知缺陷** |
| 4 | 区块批处理 ack | 每批回 `ChunkBatchReceived` | 同上，回包数≥批次数 | 相等 | 未验证 |
| 5 | 玩家位置同步 | 平稳前进、不被反复拉回；速度 4.317 格/秒 | `verify-protocol-live.ps1`（trace 窗口速度）| 4.317±0.25 | **已验证：speed=4.318、corrections=1、20s 走 85.94 格** |
| 6 | 方块实体 | 箱子 14/16、贴地 | `BlockEntityPlaceholderMesherTests` | 像素差 ≤1 | 部分验证 |
| 7 | 实体模型 | 六部件、0.9 比例 | `EntityPlaceholderMesherTests` | 结构断言 | 部分验证（占位）|
| 8 | 分层 | 按材质透明度三层 | `BlockLayerRulesTests`、`Repro-006` | 全绿 | 部分验证 |
| 9 | 生物群系着色 | colormap + tintindex | `BiomeTintTests` + 原版同机位取色 | 均值差 ≤3/255 | 未验证 |
| 10 | 图集 UV/padding/mip | 1:1、无黑边、远处平滑 | ADR-0015/0016 + 黑像素计数 | 黑像素=0 | 部分验证（缺原版输出基准）|
| 11 | 面剔除 | 仅遮挡剔除、树叶留内面 | `Repro-005`、`Repro-006` | 面积 256/162 | 部分验证（仅 CPU 侧，缺画面）|
| 12 | 半透明排序 | 远到近、不写深度 | 像素算术断言 | ≤1 | 部分验证 |
| 13 | 天空/雾/天体 | 渐变 + 日月星 | 原版截图对比 | ΔE 待定 | 未验证（天体未做）|
| 14 | UI/字体/聊天 | 布局字体一致 | UI 截图 + 现有测试 | 待定 | 部分验证 |
| 17 | 速度读数（宿主显示）| 原版 F3：`Entity.computeSpeed()` 每 tick 取 `position-lastKnownPosition` 的 3D 长度，显示 `%.3f blocks/tick` | 目视 + 数值稳定 | 每 tick 一次、不跳动 | **已验证：稳定 0.281 blocks/tick（冲刺 5.61 b/s）** |
| 16 | 调试代码可分离 | 游戏程序集不读环境变量；删除 `Launcher/Diagnostics` 即可移除全部调试 | `check-diagnostics-separation.ps1` + `DiagnosticsSeparationTests` | violations=0 | **已验证（0 处）** |
| 15 | 性能 | p99 ≤16.7ms、剔除 ≤1ms、1 提交/帧 | `benchmarks/` | 见中期目标 | 未验证（当前 150–200ms）|


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
