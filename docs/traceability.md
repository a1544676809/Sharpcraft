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
| T-006 | 随机数 `LegacyRandom` / `Xoroshiro128PlusPlus` | `docs/modules/common-primitives.md` §4.4；`docs/analysis/common/random.md`、`random-vectors.md` | 待写：`RandomVectorTests.cs`（回放 `TestData/random-vectors.txt`） | 待写：`src/MinecraftCSharp.Common/Random/` | 阻塞于评审 |
| T-007 | 注册表顺序与 `Registry` | `docs/modules/common-primitives.md` §4.2；`docs/analysis/common/registry-order.md` | 待写：顺序快照测试 | 待写：`src/MinecraftCSharp.Common/Registries/` | 规格已就绪 |
| T-008 | 坐标与几何基元（负数取整规则待补） | `docs/modules/common-primitives.md` §4.3 | 待写 | 待写：`src/MinecraftCSharp.Common/Math/` | 规格不足 |

## 使用规则

- 开工前先建行（状态 = 规格已就绪/阻塞于评审），收工时更新状态。
- ID 单调递增，不复用；废弃任务保留并把状态改为"已废弃"。
- 一次提交只推进一到两行，避免矩阵与实现同时漂移。
- 与 `docs/STATE.md` 的分工：STATE 说"现在做什么"，矩阵说"每个任务的规格/测试/代码在哪"。
