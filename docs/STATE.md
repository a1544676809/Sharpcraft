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

- 无。
- 开工前的四项已闭环，证据为 `docs/analysis/common/`：`identifier.md`（等值/排序/长度/空串）与 `random.md`（LCG、xoroshiro128++、种子升级、位置化随机、WorldgenRandom 混合器、RandomSequence）。
- 新发现的前置项（**注册表**开工前必须解决）：`Registry` 的元素顺序由注册顺序还是标识符排序决定，尚未确认。`Identifier` 本身可以直接开工。

## 下一步具体动作

1. 等所有者评审 `docs/modules/common-primitives.md`；通过后置 `[已评审]`。
2. 实现 `Identifier`（`readonly struct`，`Parse`/`TryParse`，校验含空串通过、拒绝 `..`、无长度上限；`CompareTo` 先 path 后 namespace），补单元测试覆盖：非法字符、空路径、冒号位置、排序键顺序、默认命名空间等价。
3. 实现 `LegacyRandom`（48 位 LCG）与 `Xoroshiro128PlusPlus`，先在测试中用固定种子建立"参考向量"基线（需从原版运行取真实输出值，属未验证项，见 `analysis/common/random.md` §10）。
4. 注册表顺序问题解决后再动 `Registry`。

## 未提交改动 / 后台任务

- 本次提交内容见下；无运行中的后台任务。

## 最近提交

- `59ecb3f` feat: project scaffolding with build-time side-boundary guards
- 本次提交：宪章 3.6/4.2 修订、工作区迁移、HotPath 机制、STATE 建立
