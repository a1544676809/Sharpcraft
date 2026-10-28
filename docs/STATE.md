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

- 无。`docs/modules/common-primitives.md` 状态仍为 `[草稿]`，代码未开工。

## 阻塞项

`common-primitives` 开工前必须闭环（证据都在 `ref://mc-26.3-analysis/src`）：

1. `Identifier` 的等值 / 哈希构成。
2. `Identifier` 的排序语义（决定注册表遍历顺序与协议一致性）。
3. 路径是否允许空串 / 是否有长度上限。
4. 随机数算法与种子派生（不确认就无法对齐世界生成与战利品）。

## 下一步具体动作

1. 读 `ref://mc-26.3-analysis/src/net/minecraft/resources/Identifier.java` 与其比较/序列化相关类型，产出 `docs/analysis/common/identifier.md`（一文件一主题）。
2. 读随机源实现，产出 `docs/analysis/common/random.md`。
3. 回填 `docs/modules/common-primitives.md` 的 `[待验证]` / `[待分析]`，请所有者评审后置为 `[已评审]`。
4. 实现 `Identifier` + 单元测试（含负号/边界/非法字符）；为标注 `[HotPath]` 的实现补分配测试。

## 未提交改动 / 后台任务

- 无未提交改动；无运行中的后台任务。

## 最近提交

- `59ecb3f` feat: project scaffolding with build-time side-boundary guards
- 本次提交：宪章 3.6/4.2 修订、工作区迁移、HotPath 机制、STATE 建立
