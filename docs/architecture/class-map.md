<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

状态：[已评审]
基线：Minecraft 26.3
最后更新：2026-11-05

# 实现 ↔ 原版类 对应系统（class-map）

AGENTS.md §3.5 硬约束：**每个模块文档必须写明我们的类型对应原版哪些类**。本系统把它做成"可校验的单一真相源"。

## 组成
- **真相源**：源码上的 `[ReferenceClass("原版全限定类名", "ref://mc-26.3-analysis/....java", Kind=..., Status=..., Note="...")]`（`Sharpcraft.Common.Reference`）。
  - `Kind`：`Mirror`（1:1）/`Merged`（原版多类合并）/`Adapter`（适配层）/`New`（我方独有，需 Note 说明）/`Stub`（占位未实现）。
  - `Status`：`Full`/`Partial`/`NotStarted`（Partial/NotStarted 必须 Note 差距）。
- **工具**：`tools/Sharpcraft.ClassMap`
  - `--write`：扫描 `src/**` 生成 `docs/architecture/class-map.generated.md`。
  - `--check`：生成页是否最新、是否有**新增未注解的 public 类型**（不在 baseline 内）、引用指针是否形如 `ref://`（且当 `MC_ANALYSIS_ROOT` 存在时校验文件存在）。
  - `--update-baseline`：把当前未注解类型写入 `docs/src/.classmap-baseline.txt`（**棘轮，只减不增**）。
- **门禁**：`tools/verify/check-classmap.ps1` + `tests/Sharpcraft.Architecture.Tests/ClassMapTests.cs`（CI 与 `dotnet test` 均执行）。
- **合规**：仓库内**不出现原版源码**，只存类名与 `ref://` 指针；引用解析到本机分析目录 `%MC_ANALYSIS_ROOT%`（默认同级 `../mc-26.3-analysis`）。

## 流程
1. 新建/修改类型时加 `[ReferenceClass(...)]`；
2. `dotnet run --project tools/Sharpcraft.ClassMap -- --write`；
3. 逐步为存量类型补注解，随后 `--update-baseline` 缩减基线；
4. （已实现）Phase 2：对应表**自动注入**各 `docs/src/<project>/<module>.md`（`<!-- BEGIN GENERATED: class-map -->` 块）；**反向覆盖**：`docs/architecture/class-map/<subsystem>.refs.txt` 列出应实现的原版类（每行一个全限定类名，`!` 前缀标 OutOfScope），`--check` 校验均已映射或显式豁免。示例见 `docs/architecture/class-map/client-core.refs.txt`。
