<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

状态：[草稿]
基线：Minecraft 26.3
证据：`docs/architecture/boundaries.md`
最后更新：2026-10-28

# 模块设计文档

每份文档对应一个待实现模块，遵循 AGENTS.md 3.5：**模块文档状态达到 `[已评审]` 之前不写该模块代码**。模板见 [`../_templates/module-doc.md`](../_templates/module-doc.md)。

## 文档清单

| 文档 | 模块 | 侧别 | 状态 |
| --- | --- | --- | --- |
| [`build-scaffolding.md`](build-scaffolding.md) | 构建骨架与程序集划分 | 外围（工程） | 草稿（已实施，待评审） |
| [`common-primitives.md`](common-primitives.md) | Common 基础类型 | Common | 草稿 |
| [`auth.md`](auth.md) | 认证链路（设备码优先） | 已评审 |

## 当前进度

- `build-scaffolding`：7 个工程 + 2 个测试工程已落地，`dotnet build` / `dotnet test` 全绿；架构守卫（B1–B3）已实测可拦截越界引用。
- `common-primitives`：文档就绪，代码未开工。开工前需闭环三处 `[待验证]`（Identifier 的等值/排序/长度）与一处 `[待分析]`（随机数算法）。

## 编写要求

每份模块文档必须回答模板中的 7 个问题；侧别判定必须引用 `docs/architecture/boundaries.md` 的规则编号（R1–R6），不得只写结论。
