<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# STATE —— 跨会话进度锚点

> 本文件只记录**易变状态**（AGENTS.md 4.2）。设计结论属于 `docs/modules/`，索引属于 `docs/README.md`，决策属于 `docs/decisions/`。**不要在此复述它们。**
> 每次收工必须与代码同一提交更新本文件，并写清"下一步具体动作"。

最后更新：2026-10-28

## 当前里程碑

M1 —— **基础逻辑**（Common 地基 + 工程防线）。渲染、网络、客户端表现靠后。

## 已完成（可视为稳定）

- 项目宪章 AGENTS.md（11 条原则 + 3.6 工具链 + 4.2 交接）、LGPL-3.0-or-later 落户。
- 文档体系与颗粒度规范；jar 第一轮架构分析（overview / package-map）。
- 三侧边界规则 R1–R6 + 证据（`docs/architecture/boundaries.md`）。
- 工程骨架：7 个 src 工程 + 2 个测试工程，`MinecraftCSharp.slnx` 作为唯一构建与调试入口（分组模板已就位）。
- 构建防线：`TreatWarningsAsErrors=true`；架构守卫 11 项（含 csproj 声明引用检查，已实测可拦截）；热路径机制 3 项（`[HotPath]` + 分配测试，已实测有区分度）。
- 分析工作区迁移至 `../mc-26.3-analysis`；依赖扫描脚本可 `MC_ANALYSIS_ROOT` 覆盖且输出可复现。
- 治理设施（原则 12）：追踪矩阵 `docs/traceability.md`、风险登记册 `docs/risks.md`、ADR（`docs/decisions/`，含 0001）、可测质量属性 `docs/specs/quality-attributes.md`、缺陷模板 `.github/ISSUE_TEMPLATE/bug_report.md`、复现库约定 `docs/repro/README.md`、CI `.github/workflows/ci.yml`。
- 文档漂移门禁已可执行：SPDX 头、索引完整性、断链、绝对路径、STATE 必备章节、矩阵状态词表，共 7 项断言。

## 进行中

- T-005 已完成：`Identifier` + 18 项测试（`src/MinecraftCSharp.Common/Identifiers/`、`tests/.../IdentifierTests.cs`）。
- T-006 已完成：`LegacyRandomSource`、`Xoroshiro128PlusPlus`、`XoroshiroRandomSource`、`SeedSupport`、`RandomSource` 工厂；向量回放 840 值全绿，三项分配测试通过。

## 阻塞项

- 无。四项开工前置 + 注册表顺序均已闭环：
  - `docs/analysis/common/identifier.md`：等值/排序/长度/空串。
  - `docs/analysis/common/random.md`：LCG、xoroshiro128++、种子升级、位置化随机、WorldgenRandom 混合器、RandomSequence。
  - `docs/analysis/common/registry-order.md`：迭代顺序 = 注册顺序；内置表顺序 = 代码顺序；数据包表排序后注册；方块状态全局 id 的四级链。
- 参考向量已取真值并入库（纯数值），生成器与原始输出留在 `ref://mc-26.3-analysis/vectors/`。

## 下一步具体动作

1. 注册表顺序链（T-007）：先做"顺序快照"数据结构，再实现 `Registry` / `IdMapper`，并配顺序快照测试。
2. `nextGaussian`（Marsaglia 极坐标法 + 缓存第二值）——向量已在 `gaussianLegacy` / `gaussianXoro` 行中，尚未实现。
3. `fork()` / `consumeCount` 的链式派生向量仍未取，等用到时补。
4. T-008 坐标基元（负数取整规则待补规格）。
## 未提交改动 / 后台任务

- 本次提交内容见下；无运行中的后台任务。

## 最近提交

- `cb13137` feat(common): bit-exact LCG and xoroshiro128++ with reference-vector replay
- 本次提交：T-009 位置化随机 / 世界生成混合器 / 战利品序列（含 WorldgenRandom 委托语义修正）
