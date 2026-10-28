<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**（AGENTS.md 4.2）。设计结论属于 `docs/modules/`，里程碑划分属于 `docs/milestones/roadmap.md`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。**不要在此复述它们。**
> 每次收工必须与代码同一提交更新本文件，并写清"下一步具体动作"。

最后更新：2026-10-28

## 当前里程碑

**M3 —— 资产管线 + 启动器闭环**（出口条件见 `docs/milestones/roadmap.md`）。
M1（基础逻辑）与 M2（世界数据闭环）已达成并留有可执行证据。

## 进行中

- **M3 文档阶段**：`docs/modules/assets.md`（资产模块设计）、`docs/analysis/assets/jar-entry-map.md`（jar 条目清单）、`docs/decisions/0005-asset-pack-and-official-index.md`（资产打包 + 官方索引只读挂载）三份均为 `[待评审]`，等你评审后开始编码。
- ADR-0005 有两个待决项等你裁决：① 索引资产缺失时是否允许启动器从官方端点补全；② pack 内路径是否扁平化（当前倾向扁平化）。

## 已完成（近期，按时间倒序）

1. T-031 贪婪合并：真实世界三角形 110,868 → **51,402**（-54%）；修掉两个只有出图才暴露的 bug（掩码需每切片重置、四边形坐标漏加段基址），绕序改为按 `Cross(U,V)==外法线` 推导的显式表，管线 `FrontFace` 改为 CCW。
2. T-030 光照→顶点亮度 + 背面剔除：nibble 光照存进 `ChunkLight`，面亮度 = 采样格光照 × 朝向系数（上 1.0/侧 0.75/下 0.5，下限 0.12）；管线 `CullMode.Back`。代价 +13%。
3. T-029 几何路径零分配：`MeshBuffer`（ArrayPool）+ 遍历已加载区块 + `ClipSpaceTransform` 的 Span 重载；P95 132.9 → **22.2 ms**、分配 14.8 MB → **0**。
4. T-028 性能基线套件：`benchmarks/MinecraftCSharp.Benchmarks`，读数入 `benchmarks/results/`，口径见 `docs/specs/performance-baseline.md`。
5. T-026 / T-027 渲染与窗口：`Brew.Wgpu` 绑定 + `Silk.NET.Windowing`（ADR-0003 / ADR-0004 已接受）；`launcher frame|window` 可对真实服务器出图与呈现。
6. T-025 区块与光照解析：真实会话 9/9 区块、275,141 非空气方块、97 个光照数组。

## 阻塞项

- 无硬阻塞。
- **待你提供**：26.3 客户端 jar 的本地路径（原则 3：只在你本地、不进仓库）。缺它只影响"真实提取验收"，不影响 T-032/T-033 的编码与合成 jar 测试。

## 下一步具体动作

1. 你评审 `docs/modules/assets.md`；通过后把状态改为 `[已评审]`。
2. T-032：版本清单解析 + jar SHA-1 校验（合成 jar 篡改必须失败，报错含期望/实际哈希）。
3. T-033：资产**打包器**（白名单 → 单文件 zip pack + manifest，原子发布、幂等、`.mcmeta` 成对、zip-slip 防护、输出不得在仓库内）。
4. T-034 / T-035 / T-036 依次：`IAssetSource` 解析层（pack 读取器 + 官方 objects 只读挂载）→ 启动器 `assets`/`launch` 子命令 → 纹理接入渲染。
5. 可选并行：把 `quality-attributes.md` QA-1 的 `[待定]` 阈值定稿（候选值见 `performance-baseline.md` §5）。

## 未提交改动 / 后台任务

- 无后台任务。

## 最近提交

- `6bcfea6` perf(mesh): greedy merge coplanar faces into single quads
- `e70be53` feat(render): light-sampled vertex shading and GPU back-face culling
- `80c4bd3` perf: add a reproducible baseline suite for the world pipeline
