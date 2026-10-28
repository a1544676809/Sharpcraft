<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 MinecraftCSharp contributors -->

# 资产子系统分析入口

本目录记录"原版 jar 里有什么、如何安全地变成客户端可消费的资源"。

## 清单

| 文件 | 主题 |
| --- | --- |
| [`jar-entry-map.md`](jar-entry-map.md) | jar 条目分类与资产清单（顶层分布、assets 分类、纹理格式、未决问题） |
| [`resource-resolution.md`](resource-resolution.md) | 资源解析语义：路径推导、包栈优先级、`.mcmeta` 兄弟、合并方向、过滤与缺失行为 |

## 与其它文档的关系

- 资产**模块设计**：`docs/modules/assets.md`
- 资产**使用方**（渲染层如何消费纹理）：`docs/modules/render.md`
- 合规红线（不得入库、不得反编译）：`AGENTS.md` 原则 3
- 资产清单的证据指针：`ref://mc-26.3-analysis/entries.csv`

## 写作约定

1. 只写"某类条目在 jar 中的路径模式、数量、用途、提取策略"，不粘贴任何资产内容。
2. 任何无法确认来源的条目，标注为未决问题并进 `docs/risks.md`，不得直接当作结论。
3. 数值必须注明取证来源（本条清单全部来自 `entries.csv` 的统计）。
