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

## 进行中

- `docs/modules/common-primitives.md`：四项阻塞已全部闭环（见下），文档等待所有者评审后置为 `[已评审]`；尚未写代码。

## 阻塞项

- 无。四项开工前置 + 注册表顺序均已闭环：
  - `docs/analysis/common/identifier.md`：等值/排序/长度/空串。
  - `docs/analysis/common/random.md`：LCG、xoroshiro128++、种子升级、位置化随机、WorldgenRandom 混合器、RandomSequence。
  - `docs/analysis/common/registry-order.md`：迭代顺序 = 注册顺序；内置表顺序 = 代码顺序；数据包表排序后注册；方块状态全局 id 的四级链。
- 仍需外部输入：随机数的"参考向量"真值必须由运行原版 jar 产出（所有者已允许运行 jar，产物不得入库）。

## 下一步具体动作

1. 用仓库外的 Java 程序调用 jar 内随机类，导出固定种子的参考向量（LCG 与 xoroshiro 各若干 `nextLong` / `nextInt(bound)`），产物放 `ref://mc-26.3-analysis/vectors/`，仓库内只记录数值与生成方法。
2. 请所有者评审 `docs/modules/common-primitives.md`（含 Registry 章节），通过后置 `[已评审]`。
3. 实现 `Identifier`（`readonly struct`、`Parse`/`TryParse`、`CompareTo` 先 path 后 namespace、空路径合法、无长度上限、拒绝 `..`）+ 单元测试。
4. 实现 `LegacyRandom` 与 `Xoroshiro128PlusPlus`，用第 1 步的向量做回归基线。
5. 注册表顺序链落地时先做"顺序快照"数据结构，再实现 `Registry`。

## 未提交改动 / 后台任务

- 本次提交内容见下；无运行中的后台任务。

## 最近提交

- `11d522b` docs: close the four common-primitives blockers with source evidence
- 本次提交：注册表顺序分析（registry-order.md）与索引同步
