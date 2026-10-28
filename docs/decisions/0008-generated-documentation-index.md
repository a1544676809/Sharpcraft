<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

<!-- doc
title: ADR-0008：文档目录由生成器维护
summary: 目录索引改为工具生成，CI 检查索引与源码模块覆盖
status: 已接受
-->

# 0008：文档目录由生成器维护

- 状态：已接受
- 日期：2026-10-29
- 决策者：项目所有者 + 代理
- 关联：`docs/modules/doc-indexer.md`；`docs/traceability.md` T-043；AGENTS.md 3.5、原则 12

## 背景

文档数量到 52 篇后，目录索引靠人维护已经失效，且现有漂移检查只看形式（SPDX、链接可达、状态词），看不出内容矛盾。实际发生过：

- `docs/decisions/README.md` 只列了 ADR-0001，0002–0007 全部缺失；
- `docs/modules/README.md` 漏了三份模块文档，进度段还写着"代码未开工"；
- `docs/specs/network/README.md` 把已存在的 `protocol-phases.md` 标成"待写"；
- 总索引里 `traceability.md` 与 `risks.md` 重复登记。

同时，模块文档的粒度停在"一个工程一篇"，无法回答"这个目录里的代码归哪份设计"。项目所有者要求按**细分模块**补文档，并明确"逻辑由代码决定"，即不维护手工映射表。

## 决策

1. **目录索引全部生成**：`docs/README.md` 与每个子目录的 `README.md` 由 `tools/Sharpcraft.Docs.Indexer` 生成。手写内容放在 `<!-- BEGIN GENERATED: <dir> -->` / `<!-- END GENERATED: <dir> -->` 标记之外；标记之内禁止手改。
2. **元数据可选**：文档可用 `<!-- doc ... -->` 声明 `title` / `summary` / `status` / `updated`；缺省时从一级标题、`状态：`、`最后更新：` 与首段推导。已有文档不需要一次性改写。
3. **覆盖单位是源码目录，不是文件**：`src/<Project>/<子目录>/` 对应 `docs/src/<project>/<module>.md`；工程根目录对应 `docs/src/<project>.md`。映射是路径的纯变换（小写 + 连字符），没有手工清单。
4. **棘轮式覆盖门禁**：`docs/src/.coverage-baseline.txt` 列出尚未写文档的模块。新模块没有文档即失败；写了文档却仍在基线里也失败，基线只能缩小。`--update-baseline` 重写基线。
5. **两个 CI 门禁**：`--check`（索引与生成结果一致）与 `--check-coverage`（覆盖基线一致）。同一逻辑以 `DocIndexTests` 在测试进程内执行，不依赖 shell。
6. **生成物提交进仓库**：索引的变更随代码一起评审；生成结果不含时间戳、排序稳定，因此可复现。

## 影响面

- 代码：新增 `tools/Sharpcraft.Docs.Indexer`（仅 BCL 依赖）；`Sharpcraft.slnx` 增加 `/tools/` 分组；`Sharpcraft.Architecture.Tests` 引用它并新增两项门禁测试。
- 测试：`DocIndexTests`（索引最新、覆盖合规）；`DocumentationDriftTests.EveryDocumentIsReachableFromTheIndex` 从"在总索引里搜子串"改为"从总索引沿链接遍历"，因为索引现在是树形的。
- 文档：11 个目录 README 改为"手写引言 + 生成段"；补建缺失的 `docs/specs/README.md`；`docs/modules/doc-indexer.md` 记录工具设计。
- 兼容性：不影响运行时、存档、协议或模组 API。

## 替代方案与被否原因

| 方案 | 否决原因 |
| --- | --- |
| 继续手写索引 | 已经出现四处漂移；文档越多越不可控 |
| DocFX / MkDocs / mdBook | 引入第二套工具链；导航自动化不等于覆盖检查；生成的是站点，不是仓库内可评审的索引 |
| 纯约定、无元数据 | 摘要只能靠首段猜测，索引质量随文档增长而下降；本方案保留元数据作为可选增强 |
| 每个 `.cs` 文件一篇 | 106 篇，维护成本过高；项目所有者明确否掉。按源码目录（当前 26 个模块）更接近"细分模块"的实际边界 |

## 复查条件

- `docs/src/` 的模块数超过约 60 个，或某个目录大到一篇文档装不下时：优先拆源码目录，而不是给工具加映射例外。
- 需要对外发布文档站时：另立 ADR 评估静态站点，生成器仍作为唯一索引来源。
- 元数据推导（首段摘要）出现明显误读时：给相关文档补 `<!-- doc -->`，不改推导规则。
