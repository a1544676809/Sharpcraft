<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 决策记录（ADR）

AGENTS.md 原则 12：不可逆或跨模块的决策必须在此留档，**只追加、不修改历史**。推翻旧决策时要写新 ADR 并在旧条目上标注 superseded。

## 索引

| 编号 | 标题 | 状态 |
| --- | --- | --- |
| [0001](0001-architecture-rules-as-tests.md) | 架构规则以测试门禁落地，而非文档约定 | 已采纳 |

## 规则

- 文件名：`NNNN-<短横线标题>.md`，四位递增编号，不复用。
- 模板：[`0000-template.md`](0000-template.md)。
- 每份 ADR 必须写清"背景 / 决策 / 影响面 / 替代方案 / 复查条件"。
- 决策涉及许可证或产品策略（原则 7.1、10）时，必须在 ADR 中引用项目所有者的确认。
