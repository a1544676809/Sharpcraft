<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: 文档索引生成器
summary: 从文档与源码目录生成目录索引，并在 CI 检查索引与模块覆盖
status: 已评审
-->

状态：[已评审]
基线：Minecraft 26.3
证据：ADR-0008；`tools/Sharpcraft.Docs.Indexer`
最后更新：2026-10-29
授权：用户 2026-10-29 选择"先做 Phase 1：生成器 + 索引 + CI 检查"，并要求按**细分模块**（而不是每个文件）组织文档。

# 文档索引生成器（tools/Sharpcraft.Docs.Indexer）

把"目录索引"和"哪些代码有文档"变成可执行检查，取代手工维护的 README 表格。

## 1. 职责范围

负责：

- 扫描 `docs/**/*.md`，为每个目录生成 `README.md` 的索引段（文档表 + 子目录表）。
- 从源码目录推导模块文档路径，维护覆盖基线，提供两个 CI 门禁。
- 提供 `--write` 让开发者一次补齐所有索引。

明确不负责：

- 不生成文档正文，不判断内容质量（质量靠评审与追踪矩阵）。
- 不改写标记之外的手写段落，不删除任何文档。
- 不进入游戏运行时：不属于 `src/` 的任何程序集。

## 2. 侧别归属

- 归属：**外围（工程工具）**。依据 AGENTS 原则 5：它只读仓库文件、只写 `docs/`，不被 `Common` / `Client` / `Server` 引用。
- 与启动器、认证同属外围层，但用途是工程治理，不是产品功能。

## 3. 依赖关系

| 方向 | 对象 | 说明 |
| --- | --- | --- |
| 依赖 | .NET BCL | 只用 `System.IO` / `System.Text`，无第三方包 |
| 被依赖 | `tests/Sharpcraft.Architecture.Tests` | 进程内调用 `Check`，不开子进程 |
| 被依赖 | CI | `--check --check-coverage` |
| 不依赖 | `src/` 的任何程序集 | 工具不引用游戏代码 |

## 4. 对外契约

命令行（退出码：0 通过，1 门禁失败，2 用法错误）：

| 参数 | 行为 |
| --- | --- |
| `--write` | 重写所有目录索引 |
| `--check` | 索引缺失或与生成结果不一致即失败 |
| `--check-coverage` | 源码模块没有文档、或基线过期即失败 |
| `--update-baseline` | 重写 `docs/src/.coverage-baseline.txt` |
| `--root <path>` | 指定仓库根（默认从可执行文件向上找 `Sharpcraft.slnx` + `AGENTS.md`） |

文件契约：

- **生成标记**：`<!-- BEGIN GENERATED: <key> -->` … `<!-- END GENERATED: <key> -->`，`key` 为 `docs` 或 `docs/<相对目录>`。标记之内禁止手改。
- **元数据（可选）**：`<!-- doc` 块，键 `title` / `summary` / `status` / `updated` / `side` / `source`。缺省时从一级标题、`状态：`、`最后更新：` 与首段推导。
- **模块映射**：`src/<Project>/<子目录>/` → `docs/src/<project>/<module>.md`；工程根目录 → `docs/src/<project>.md`。项目名与目录名转小写连字符，文件名不需要手工清单。
- **覆盖基线**：`docs/src/.coverage-baseline.txt`，一行一个尚无文档的源码目录，按序数排序。
- **确定性**：不写时间戳、按序数排序、统一 LF；同一输入逐字节可复现。

## 5. 关键数据结构

| 结构 | 用途 | 生命周期 | 所属线程 |
| --- | --- | --- | --- |
| `DocEntry` | 索引中的一行（路径、标题、摘要、状态、更新时间、侧别、源目录） | 单次运行 | 单线程 |
| 目录树 | 由文档路径推导，决定每个 README 的内容 | 单次运行 | 单线程 |
| 覆盖基线 | 记录尚未写文档的源码模块，只允许缩小 | 提交进仓库 | 单线程 |

## 6. 行为对齐与偏离

本工具不是原版行为对齐的对象。约束是**治理行为**：

- 索引是生成物：任何"手改索引让它看起来对"的提交都会被 `--check` 拒绝。
- 覆盖是棘轮：基线只能通过"写文档 + `--update-baseline`"缩小；新增源码目录必须同时给文档，否则失败。
- 粒度是源码目录：当前 26 个模块。某个目录大到一篇装不下时，**拆源码目录**，而不是给工具加映射例外。
- 有意偏离：不兼容 DocFX / MkDocs 的站点格式；生成的是仓库内可评审的 Markdown。

## 7. 未解决问题与风险

- **文档戏**：基线里的模块可能长期不写文档。缓解：基线只能缩小，且评审新增模块时优先处理；`--check-coverage` 会阻止新目录进入基线。
- **摘要推导误读**：首段未必是摘要。缓解：给该文档补 `<!-- doc ... summary: ... -->`。
- **索引冲突**：两人同时新增文档会改同一个 README。解决方式是重新运行 `--write`；生成结果确定，冲突可机械解决。
- **模板不参与索引**：`docs/_templates/` 被排除，避免模板被当成正式文档。

## 8. 参考资料

- [ADR-0008](../decisions/0008-generated-documentation-index.md)
- [docs/README.md](../README.md)
