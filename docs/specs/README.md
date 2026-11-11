<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# 规格（specs）

本目录存放**对外契约**：网络协议、性能基线、可测质量属性。规格先于实现，与实现冲突时先改规格并评审。

本目录的文档索引由 `tools/Sharpcraft.Docs.Indexer` 生成；手写内容放在标记之外。

<!-- BEGIN GENERATED: docs/specs -->

| 文档 | 摘要 | 状态 | 最后更新 |
| --- | --- | --- | --- |
| [`debug-control.md`](debug-control.md) | 让外部代理/工具能驱动一个正在运行的客户端做验收：截图、查询状态、触发场景，而不必注入键盘事件或抓窗口。 | 未标注 | - |
| [`deviations.md`](deviations.md) |  | 未标注 | - |
| [`performance-baseline.md`](performance-baseline.md) | AGENTS.md 原则 9：先有可复现基线，才谈优化；原则 12：性能需求必须"指标 + 阈值 + 测量方法 + 失败判定"。 | 待评审 | 2026-10-28 |
| [`quality-attributes.md`](quality-attributes.md) | 性能、协议一致性、模组热加载、跨平台的指标、阈值与失败判定 | 草稿 | - |

子目录：

| 目录 | 文档数 |
| --- | --- |
| [`network/`](network/README.md) | 6 |
| [`quality/`](quality/README.md) | 1 |
| [`render/`](render/README.md) | 16 |

<!-- END GENERATED: docs/specs -->
